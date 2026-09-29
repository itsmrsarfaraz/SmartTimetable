using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SmartTimetable.Desktop.Diagnostics;

/// <summary>
/// Best-effort crash logging for startup and unhandled exceptions.
///
/// A managed exception thrown before the main window appears kills the process with the
/// generic Windows exit code <c>0xe0434352</c> ("a .NET exception") and no visible message,
/// which is impossible to diagnose from the exit code alone. This logger records the full
/// exception — type, message and stack, including every inner exception — to
/// <c>%APPDATA%\SmartTimetable\logs\crash.log</c> and to standard error (so running the app
/// from a console also prints it). It never throws: logging must not become a second crash.
/// </summary>
internal static class CrashLogger
{
    private static readonly object Gate = new();

    /// <summary>Full path to the crash log. Shown to the user in the startup-error window.</summary>
    public static string LogPath { get; } = BuildLogPath();

    /// <summary>
    /// Subscribes to the process-wide unhandled-exception hooks. Call once, first thing in
    /// <c>Main</c>, so even a failure during Avalonia bootstrap is captured.
    /// </summary>
    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write("AppDomain.UnhandledException", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>
    /// Appends an exception (with a short context label) to the crash log and stderr.
    /// Best-effort: any I/O failure here is swallowed.
    /// </summary>
    public static void Write(string context, Exception? ex)
    {
        try
        {
            string text = Format(context, ex);

            lock (Gate)
            {
                try
                {
                    var dir = Path.GetDirectoryName(LogPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    File.AppendAllText(LogPath, text);
                }
                catch
                {
                    // Disk not writable — still try stderr below.
                }
            }

            try { Console.Error.Write(text); } catch { /* no console */ }
        }
        catch
        {
            // Never let the crash handler crash.
        }
    }

    /// <summary>Renders an exception chain into a readable, timestamped block.</summary>
    public static string Format(string context, Exception? ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine("========================================================");
        sb.AppendLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {context}");

        if (ex is null)
        {
            sb.AppendLine("(no exception object was provided)");
        }
        else
        {
            int depth = 0;
            for (Exception? e = ex; e is not null; e = e.InnerException, depth++)
            {
                if (depth > 0) sb.AppendLine("--- caused by ---");
                sb.AppendLine($"{e.GetType().FullName}: {e.Message}");
                if (!string.IsNullOrWhiteSpace(e.StackTrace))
                    sb.AppendLine(e.StackTrace);
            }
        }

        sb.AppendLine();
        return sb.ToString();
    }

    private static string BuildLogPath()
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SmartTimetable", "logs");
            return Path.Combine(dir, "crash.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "SmartTimetable-crash.log");
        }
    }
}
