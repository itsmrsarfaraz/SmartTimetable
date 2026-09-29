using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace SmartTimetable.Infrastructure.Persistence;

/// <summary>
/// Lightweight forward-only schema patcher for the SQLite database.
///
/// The app creates its schema with <c>Database.EnsureCreated()</c>, which is simple but
/// has one sharp edge: it only ever creates tables that do not yet exist. When we add a
/// new property to an entity, an <b>existing</b> database keeps its old shape and is now
/// missing that column, so the next query throws "no such column …" — which shows up to
/// the user as a screen that will not save or a timetable that will not generate.
///
/// Rather than force a database delete (which would wipe the college's data and their
/// activation), this guard runs on startup and adds any columns introduced after the
/// first release with <c>ALTER TABLE … ADD COLUMN</c>. It is idempotent: a column that
/// already exists is skipped, so it is safe to run on every launch.
///
/// When you add a new mapped property to an entity, add a matching row to
/// <see cref="Columns"/> so upgrading users pick it up without losing data.
/// </summary>
internal static class SqliteSchemaGuard
{
    // (table, column, column-definition) for columns added after the initial schema.
    private static readonly (string Table, string Column, string Ddl)[] Columns =
    {
        // Added with the daily teacher-cap feature. Default 6 ≈ a permanent teacher's day.
        ("Teachers", "MaxPeriodsPerDay", "INTEGER NOT NULL DEFAULT 6"),
    };

    public static void Apply(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        bool openedHere = conn.State != ConnectionState.Open;
        if (openedHere) conn.Open();
        try
        {
            foreach (var (table, column, ddl) in Columns)
            {
                if (!TableExists(conn, table)) continue;   // table not created yet — EnsureCreated will make it fresh
                if (ColumnExists(conn, table, column)) continue;

                using var alter = conn.CreateCommand();
                alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {ddl};";
                alter.ExecuteNonQuery();
            }
        }
        finally
        {
            if (openedHere) conn.Close();
        }
    }

    private static bool TableExists(DbConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
        AddParam(cmd, "$name", table);
        return cmd.ExecuteScalar() is not null;
    }

    private static bool ColumnExists(DbConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        // PRAGMA does not take parameters; the table name here comes from our own constant list.
        cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            // table_info columns: 0=cid, 1=name, 2=type, 3=notnull, 4=dflt_value, 5=pk
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
