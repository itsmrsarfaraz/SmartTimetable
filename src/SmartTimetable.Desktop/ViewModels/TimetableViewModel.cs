using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Desktop.Export;
using SmartTimetable.Domain.Entities;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>A single rendered lesson row in the timetable grid.</summary>
public sealed class TimetableCell
{
    public int EntryId { get; init; }
    public string Day { get; init; } = string.Empty;
    public int DayOrder { get; init; }
    public string Period { get; init; } = string.Empty;
    public int PeriodOrder { get; init; }
    public string Time { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Teacher { get; init; } = string.Empty;
    public string Room { get; init; } = string.Empty;
}

/// <summary>A class option in the timetable filter combo.</summary>
public sealed class ClassFilter
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public override string ToString() => Name;
}

/// <summary>One cell (a single day at a single period) in the day × period matrix.</summary>
public sealed class TimetableGridCell
{
    public string Text { get; init; } = string.Empty;
    public bool HasContent { get; init; }
}

/// <summary>One period row of the matrix, holding one cell per shown day (in column order).</summary>
public sealed class TimetableGridRow
{
    public string PeriodLabel { get; init; } = string.Empty;
    public string TimeLabel { get; init; } = string.Empty;
    public List<TimetableGridCell> Cells { get; init; } = new();
}

/// <summary>A teaching period option in the manual-edit combo.</summary>
public sealed class PeriodOption
{
    public int Id { get; init; }
    public int Order { get; init; }
    public string Label { get; init; } = string.Empty;
    public override string ToString() => Label;
}

/// <summary>A teacher option in the manual-edit combo.</summary>
public sealed class TeacherOption
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public override string ToString() => Name;
}

/// <summary>A room option (Id null = no room) in the manual-edit combo.</summary>
public sealed class RoomOption
{
    public int? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public override string ToString() => Name;
}

/// <summary>
/// Displays a generated timetable. The class filter narrows the grid to one class,
/// the timetable can be marked as the kept option, and the current view exports to CSV.
/// </summary>
public partial class TimetableViewModel : PageViewModel
{
    private readonly Func<IUnitOfWork> _uow;
    private readonly AppState _state;

    public override string Title => "Timetable";
    public override string Description => "Review a generated timetable, filter by class, keep the best one, or export it.";

    private int _requestedId;
    private int _loadedId;

    private List<TimetableEntry> _entries = new();
    private Dictionary<int, string> _classNames = new();
    private Dictionary<int, string> _subjectNames = new();
    private Dictionary<int, string> _teacherNames = new();
    private Dictionary<int, string> _roomNames = new();
    private Dictionary<int, (string Name, int Order, string Time)> _periodInfo = new();

    // Caches used by the manual-edit panel.
    private List<Period> _periods = new();
    private List<Room> _rooms = new();
    private List<Teacher> _teachers = new();
    private Dictionary<int, List<int>> _teacherIdsBySubject = new();
    private int _editingEntryId;
    private int _editingSubjectId;
    private int _editingClassId;
    private int _editingTeacherId;

    public ObservableCollection<ClassFilter> Classes { get; } = new();
    public ObservableCollection<TimetableCell> Cells { get; } = new();

    /// <summary>Column headers (day names) for the matrix view, in day order.</summary>
    public ObservableCollection<string> GridDays { get; } = new();

    /// <summary>Period rows for the matrix view; each row's Cells align to <see cref="GridDays"/>.</summary>
    public ObservableCollection<TimetableGridRow> GridRows { get; } = new();

    [ObservableProperty] private string _headerName = string.Empty;
    [ObservableProperty] private string _strategy = string.Empty;
    [ObservableProperty] private int _score;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private ClassFilter? _selectedClassFilter;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowToolbarStatus))]
    private string _status = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowToolbarStatus))]
    private bool _hasTimetable;

    /// <summary>Show the status line in the toolbar only when a timetable is loaded and there is a message.</summary>
    public bool ShowToolbarStatus => HasTimetable && !string.IsNullOrWhiteSpace(Status);

    /// <summary>True = day × period matrix; false = flat list. The matrix is the default.</summary>
    [ObservableProperty] private bool _showGrid = true;

    // ----- Manual-edit panel -----

    /// <summary>Weekday choices for moving a lesson (only days the timetable actually uses).</summary>
    public ObservableCollection<Weekday> EditableDays { get; } = new();

    /// <summary>Teaching-period choices for moving a lesson (breaks excluded).</summary>
    public ObservableCollection<PeriodOption> EditablePeriods { get; } = new();

    /// <summary>Teachers qualified for the selected lesson's subject.</summary>
    public ObservableCollection<TeacherOption> EditableTeachers { get; } = new();

    /// <summary>Room choices (including a "no room" option).</summary>
    public ObservableCollection<RoomOption> EditableRooms { get; } = new();

    /// <summary>The lesson picked in the list view; drives the edit panel. Null = panel hidden.</summary>
    [ObservableProperty] private TimetableCell? _selectedCell;

    [ObservableProperty] private string _editLessonHeader = string.Empty;
    [ObservableProperty] private Weekday _editDay;
    [ObservableProperty] private PeriodOption? _editPeriod;
    [ObservableProperty] private TeacherOption? _editTeacher;
    [ObservableProperty] private RoomOption? _editRoom;
    [ObservableProperty] private string _editStatus = string.Empty;
    [ObservableProperty] private bool _editStatusIsError;

    public TimetableViewModel(Func<IUnitOfWork> uow, AppState state)
    {
        _uow = uow;
        _state = state;
    }

    /// <summary>Sets which timetable to load on the next <see cref="LoadAsync"/>. 0 = most recent/kept.</summary>
    public void SetTimetable(int id) => _requestedId = id;

    public override async Task LoadAsync()
    {
        Cells.Clear();
        Classes.Clear();
        _entries.Clear();
        HasTimetable = false;
        Status = string.Empty;

        using var uow = _uow();

        GeneratedTimetable? tt = _requestedId != 0
            ? await uow.Timetables.GetByIdAsync(_requestedId)
            : null;

        if (tt is null)
        {
            var forSession = await uow.Timetables.ListAsync(t => t.AcademicSessionId == _state.ActiveSessionId);
            tt = forSession.FirstOrDefault(t => t.IsSelected)
                 ?? forSession.OrderByDescending(t => t.Id).FirstOrDefault();
        }

        if (tt is null)
        {
            Status = "No timetable yet. Open the Generate screen to create one.";
            return;
        }

        _loadedId = tt.Id;
        HeaderName = tt.Name;
        Strategy = tt.Strategy.ToString();
        Score = tt.Score;
        IsSelected = tt.IsSelected;

        var classes = await uow.Classes.ListAsync();
        var subjects = await uow.Subjects.ListAsync();
        var teachers = await uow.Teachers.ListAsync();
        var rooms = await uow.Rooms.ListAsync();
        var periods = await uow.Periods.ListAsync();
        var teacherSubjects = await uow.TeacherSubjects.ListAsync();

        _classNames = classes.ToDictionary(c => c.Id, Describe);
        _subjectNames = subjects.ToDictionary(s => s.Id, s => s.Name);
        _teacherNames = teachers.ToDictionary(t => t.Id, t => t.FullName);
        _roomNames = rooms.ToDictionary(r => r.Id, r => r.Name);
        _periodInfo = periods.ToDictionary(
            p => p.Id,
            p => (p.Name, p.Order, $"{p.StartTime:HH\\:mm}-{p.EndTime:HH\\:mm}"));

        // Caches for the manual-edit panel.
        _periods = periods;
        _rooms = rooms;
        _teachers = teachers;
        _teacherIdsBySubject = teacherSubjects
            .GroupBy(ts => ts.SubjectId)
            .ToDictionary(g => g.Key, g => g.Select(ts => ts.TeacherId).Distinct().ToList());

        _entries = await uow.TimetableEntries.ListAsync(e => e.GeneratedTimetableId == tt.Id);

        Classes.Add(new ClassFilter { Id = 0, Name = "All classes" });
        foreach (var cid in _entries.Select(e => e.SchoolClassId).Distinct())
        {
            Classes.Add(new ClassFilter
            {
                Id = cid,
                Name = _classNames.TryGetValue(cid, out var n) ? n : $"Class {cid}"
            });
        }

        HasTimetable = true;
        SelectedClassFilter = Classes.FirstOrDefault();
        BuildEditSources();
        SelectedCell = null;
        Rebuild();
    }

    /// <summary>Fills the day/period/room combos used by the manual-edit panel.</summary>
    private void BuildEditSources()
    {
        EditableDays.Clear();
        foreach (var o in _entries.Select(e => (int)e.Day).Distinct().OrderBy(x => x))
            EditableDays.Add((Weekday)o);

        EditablePeriods.Clear();
        foreach (var p in _periods.Where(p => !p.IsBreak).OrderBy(p => p.Order))
            EditablePeriods.Add(new PeriodOption
            {
                Id = p.Id,
                Order = p.Order,
                Label = $"{p.Name} ({p.StartTime:HH\\:mm}-{p.EndTime:HH\\:mm})"
            });

        EditableRooms.Clear();
        EditableRooms.Add(new RoomOption { Id = null, Name = "— No room —" });
        foreach (var r in _rooms.OrderBy(r => r.Name))
            EditableRooms.Add(new RoomOption { Id = r.Id, Name = r.Name });
    }

    private static string Describe(SchoolClass c) =>
        string.IsNullOrWhiteSpace(c.Section) ? c.Name : $"{c.Name} {c.Section}";

    partial void OnSelectedClassFilterChanged(ClassFilter? value) => Rebuild();

    /// <summary>When a lesson is picked in the list, load it into the edit panel.</summary>
    partial void OnSelectedCellChanged(TimetableCell? value)
    {
        EditStatus = string.Empty;
        EditStatusIsError = false;

        if (value is null) return;

        var entry = _entries.FirstOrDefault(e => e.Id == value.EntryId);
        if (entry is null) { SelectedCell = null; return; }

        _editingEntryId = entry.Id;
        _editingSubjectId = entry.SubjectId;
        _editingClassId = entry.SchoolClassId;
        _editingTeacherId = entry.TeacherId;

        string cls = _classNames.TryGetValue(entry.SchoolClassId, out var cn) ? cn : $"Class {entry.SchoolClassId}";
        string sub = _subjectNames.TryGetValue(entry.SubjectId, out var sn) ? sn : $"Subject {entry.SubjectId}";
        EditLessonHeader = $"{sub} · {cls}";

        // Teachers qualified for this subject; always include the current teacher.
        EditableTeachers.Clear();
        var eligible = _teacherIdsBySubject.TryGetValue(entry.SubjectId, out var ids)
            ? new List<int>(ids)
            : new List<int>();
        if (!eligible.Contains(entry.TeacherId)) eligible.Add(entry.TeacherId);
        foreach (var tid in eligible.OrderBy(id => _teacherNames.TryGetValue(id, out var n) ? n : string.Empty))
            EditableTeachers.Add(new TeacherOption
            {
                Id = tid,
                Name = _teacherNames.TryGetValue(tid, out var n) ? n : $"Teacher {tid}"
            });

        EditDay = entry.Day;
        EditPeriod = EditablePeriods.FirstOrDefault(p => p.Id == entry.PeriodId);
        EditTeacher = EditableTeachers.FirstOrDefault(t => t.Id == entry.TeacherId);
        EditRoom = EditableRooms.FirstOrDefault(r => r.Id == entry.RoomId) ?? EditableRooms.FirstOrDefault();
    }

    [RelayCommand]
    private void CancelEdit() => SelectedCell = null;

    /// <summary>Applies the edited day/period/teacher/room after checking for clashes.</summary>
    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (SelectedCell is null || _editingEntryId == 0) return;
        if (EditPeriod is null) { EditFail("Choose a period."); return; }
        if (EditTeacher is null) { EditFail("Choose a teacher."); return; }

        int targetDay = (int)EditDay;
        int targetPeriod = EditPeriod.Id;
        int targetTeacher = EditTeacher.Id;
        int? targetRoom = EditRoom?.Id;

        // Guard against moving onto a break period (should never appear in the combo).
        var period = _periods.FirstOrDefault(p => p.Id == targetPeriod);
        if (period is null || period.IsBreak) { EditFail("That period can't hold a lesson."); return; }

        // Teacher must be qualified for the subject (or be the lesson's current teacher).
        bool qualified = targetTeacher == _editingTeacherId
            || (_teacherIdsBySubject.TryGetValue(_editingSubjectId, out var quals) && quals.Contains(targetTeacher));
        if (!qualified) { EditFail($"{EditTeacher.Name} isn't mapped to teach this subject."); return; }

        try
        {
            using var uow = _uow();

            // Re-read siblings from the database so clash checks use current data.
            var siblings = await uow.TimetableEntries.ListAsync(
                e => e.GeneratedTimetableId == _loadedId && e.Id != _editingEntryId);

            var clash = siblings.FirstOrDefault(e =>
                (int)e.Day == targetDay && e.PeriodId == targetPeriod && e.SchoolClassId == _editingClassId);
            if (clash is not null) { EditFail("This class already has a lesson in that slot."); return; }

            clash = siblings.FirstOrDefault(e =>
                (int)e.Day == targetDay && e.PeriodId == targetPeriod && e.TeacherId == targetTeacher);
            if (clash is not null)
            {
                string who = _teacherNames.TryGetValue(targetTeacher, out var n) ? n : "That teacher";
                EditFail($"{who} is already teaching another class in that slot.");
                return;
            }

            if (targetRoom is int rid)
            {
                clash = siblings.FirstOrDefault(e =>
                    (int)e.Day == targetDay && e.PeriodId == targetPeriod && e.RoomId == rid);
                if (clash is not null)
                {
                    string rn = _roomNames.TryGetValue(rid, out var n) ? n : "That room";
                    EditFail($"{rn} is already in use in that slot.");
                    return;
                }
            }

            var entry = await uow.TimetableEntries.GetByIdAsync(_editingEntryId);
            if (entry is null) { EditFail("This lesson no longer exists. Reload the timetable."); return; }

            entry.Day = EditDay;
            entry.PeriodId = targetPeriod;
            entry.TeacherId = targetTeacher;
            entry.RoomId = targetRoom;
            uow.TimetableEntries.Update(entry);
            await uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            EditFail("Save failed: " + ex.Message);
            return;
        }

        await ReloadAsync();
        Status = "Lesson moved. The timetable was updated.";
    }

    /// <summary>Removes the selected lesson from the timetable.</summary>
    [RelayCommand]
    private async Task DeleteEntryAsync()
    {
        if (SelectedCell is null || _editingEntryId == 0) return;

        try
        {
            using var uow = _uow();
            var entry = await uow.TimetableEntries.GetByIdAsync(_editingEntryId);
            if (entry is not null)
            {
                uow.TimetableEntries.Remove(entry);
                await uow.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            EditFail("Delete failed: " + ex.Message);
            return;
        }

        await ReloadAsync();
        Status = "Lesson removed from the timetable.";
    }

    private void EditFail(string message)
    {
        EditStatus = message;
        EditStatusIsError = true;
    }

    /// <summary>Reloads the currently displayed timetable after an edit.</summary>
    private async Task ReloadAsync()
    {
        _requestedId = _loadedId;
        SelectedCell = null;
        await LoadAsync();
    }

    private void Rebuild()
    {
        Cells.Clear();
        if (!HasTimetable) return;

        int filter = SelectedClassFilter?.Id ?? 0;

        var rows = new List<TimetableCell>();
        foreach (var e in _entries.Where(e => filter == 0 || e.SchoolClassId == filter))
        {
            var pinfo = _periodInfo.TryGetValue(e.PeriodId, out var pi)
                ? pi
                : (Name: $"Period {e.PeriodId}", Order: e.PeriodId, Time: string.Empty);

            rows.Add(new TimetableCell
            {
                EntryId = e.Id,
                Day = e.Day.ToString(),
                DayOrder = (int)e.Day,
                Period = pinfo.Name,
                PeriodOrder = pinfo.Order,
                Time = pinfo.Time,
                ClassName = _classNames.TryGetValue(e.SchoolClassId, out var cn) ? cn : string.Empty,
                Subject = _subjectNames.TryGetValue(e.SubjectId, out var sn) ? sn : string.Empty,
                Teacher = _teacherNames.TryGetValue(e.TeacherId, out var tn) ? tn : string.Empty,
                Room = e.RoomId is int rid && _roomNames.TryGetValue(rid, out var rn) ? rn : string.Empty
            });
        }

        foreach (var c in rows
                     .OrderBy(r => r.DayOrder)
                     .ThenBy(r => r.PeriodOrder)
                     .ThenBy(r => r.ClassName))
        {
            Cells.Add(c);
        }

        RebuildMatrix(rows);
    }

    /// <summary>Pivots the current (already filtered) lessons into a day × period grid.</summary>
    private void RebuildMatrix(List<TimetableCell> rows)
    {
        GridDays.Clear();
        GridRows.Clear();
        if (!HasTimetable) return;

        // Day columns come from ALL entries (not just the filtered rows) so the columns
        // stay stable when the user switches the class filter.
        var dayOrders = _entries.Select(e => (int)e.Day).Distinct().OrderBy(x => x).ToList();
        if (dayOrders.Count == 0) return;
        foreach (var o in dayOrders) GridDays.Add(((Weekday)o).ToString());

        // Period rows: every defined period, in order (break rows show as empty rows).
        var periodsOrdered = _periodInfo
            .Select(kv => (kv.Value.Name, kv.Value.Order, kv.Value.Time))
            .OrderBy(p => p.Order)
            .ToList();

        // Index the filtered lessons by (day order, period order).
        var byCell = new Dictionary<(int Day, int Period), List<TimetableCell>>();
        foreach (var r in rows)
        {
            var key = (r.DayOrder, r.PeriodOrder);
            if (!byCell.TryGetValue(key, out var list)) byCell[key] = list = new List<TimetableCell>();
            list.Add(r);
        }

        bool singleClass = (SelectedClassFilter?.Id ?? 0) != 0;

        foreach (var p in periodsOrdered)
        {
            var row = new TimetableGridRow { PeriodLabel = p.Name, TimeLabel = p.Time };
            foreach (var o in dayOrders)
            {
                if (byCell.TryGetValue((o, p.Order), out var lessons) && lessons.Count > 0)
                {
                    string text = string.Join("\n\n",
                        lessons.OrderBy(l => l.ClassName).Select(l => FormatCell(l, singleClass)));
                    row.Cells.Add(new TimetableGridCell { Text = text, HasContent = true });
                }
                else
                {
                    row.Cells.Add(new TimetableGridCell { Text = string.Empty, HasContent = false });
                }
            }
            GridRows.Add(row);
        }
    }

    private static string FormatCell(TimetableCell c, bool singleClass)
    {
        var lines = new List<string>();
        if (!singleClass && !string.IsNullOrWhiteSpace(c.ClassName)) lines.Add(c.ClassName);
        if (!string.IsNullOrWhiteSpace(c.Subject)) lines.Add(c.Subject);

        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.Teacher)) meta.Add(c.Teacher);
        if (!string.IsNullOrWhiteSpace(c.Room)) meta.Add(c.Room);
        if (meta.Count > 0) lines.Add(string.Join(" · ", meta));

        return string.Join("\n", lines);
    }

    [RelayCommand]
    private async Task KeepAsync()
    {
        if (_loadedId == 0) { Status = "Nothing to keep yet."; return; }

        using var uow = _uow();
        var forSession = await uow.Timetables.ListAsync(t => t.AcademicSessionId == _state.ActiveSessionId);
        foreach (var t in forSession)
        {
            bool shouldSelect = t.Id == _loadedId;
            if (t.IsSelected != shouldSelect)
            {
                t.IsSelected = shouldSelect;
                uow.Timetables.Update(t);
            }
        }

        await uow.SaveChangesAsync();
        IsSelected = true;
        Status = "This timetable is now marked as the kept option for the session.";
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (Cells.Count == 0) { Status = "Nothing to export."; return; }

        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SmartTimetable", "exports");
            Directory.CreateDirectory(dir);

            string file = Path.Combine(dir, $"timetable_{_loadedId}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("Day,Period,Time,Class,Subject,Teacher,Room");
            foreach (var c in Cells)
            {
                sb.Append(Csv(c.Day)).Append(',')
                  .Append(Csv(c.Period)).Append(',')
                  .Append(Csv(c.Time)).Append(',')
                  .Append(Csv(c.ClassName)).Append(',')
                  .Append(Csv(c.Subject)).Append(',')
                  .Append(Csv(c.Teacher)).Append(',')
                  .Append(Csv(c.Room)).AppendLine();
            }

            File.WriteAllText(file, sb.ToString());
            Status = "Exported to " + file;
        }
        catch (Exception ex)
        {
            Status = "Export failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void ExportPdf()
    {
        if (GridRows.Count == 0) { Status = "Nothing to export."; return; }
        try
        {
            string file = ExportFilePath("pdf");
            TimetableExporter.ExportPdf(BuildTitle(), BuildSubtitle(),
                GridDays.ToList(), GridRows.ToList(), file);
            Status = "Exported PDF to " + file;
        }
        catch (Exception ex)
        {
            Status = "PDF export failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void ExportExcel()
    {
        if (GridRows.Count == 0) { Status = "Nothing to export."; return; }
        try
        {
            string file = ExportFilePath("xlsx");
            TimetableExporter.ExportExcel(BuildTitle(), BuildSubtitle(),
                GridDays.ToList(), GridRows.ToList(), file);
            Status = "Exported Excel to " + file;
        }
        catch (Exception ex)
        {
            Status = "Excel export failed: " + ex.Message;
        }
    }

    /// <summary>Builds a PDF and hands it to the OS print handler (falls back to just opening it).</summary>
    [RelayCommand]
    private void Print()
    {
        if (GridRows.Count == 0) { Status = "Nothing to print."; return; }
        try
        {
            string file = ExportFilePath("pdf");
            TimetableExporter.ExportPdf(BuildTitle(), BuildSubtitle(),
                GridDays.ToList(), GridRows.ToList(), file);

            try
            {
                // Ask the default PDF handler to print directly.
                Process.Start(new ProcessStartInfo(file) { UseShellExecute = true, Verb = "print" });
                Status = "Sent to your PDF printer. The saved copy is at " + file;
            }
            catch
            {
                // No "print" verb registered — just open it so the admin can print with Ctrl+P.
                Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                Status = "Opened the timetable for printing. The saved copy is at " + file;
            }
        }
        catch (Exception ex)
        {
            Status = "Print failed: " + ex.Message;
        }
    }

    private static string ExportsDir()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SmartTimetable", "exports");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string ExportFilePath(string extension) =>
        Path.Combine(ExportsDir(), $"timetable_{_loadedId}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}");

    private string BuildTitle()
    {
        string name = string.IsNullOrWhiteSpace(HeaderName) ? "Timetable" : HeaderName;
        string scope = SelectedClassFilter?.Name ?? "All classes";
        return $"{name} — {scope}";
    }

    private string BuildSubtitle()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Strategy)) parts.Add(Strategy);
        parts.Add($"Score: {Score}");
        return string.Join("  ·  ", parts);
    }

    private static string Csv(string s) =>
        s.Contains(',') || s.Contains('"')
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;
}
