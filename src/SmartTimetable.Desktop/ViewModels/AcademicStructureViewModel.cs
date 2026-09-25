using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Entities;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>A single working-day toggle (Mon..Sun) used by the session editor.</summary>
public partial class DayToggle : ObservableObject
{
    public int Value { get; init; }
    public string Label { get; init; } = string.Empty;
    [ObservableProperty] private bool _isOn;
}

/// <summary>
/// Manages the academic hierarchy the rest of the app hangs off:
/// Campus → (Academic Session, Department) and Department → Program.
/// Classes attach to a Program, so without this screen the admin was limited to
/// the four seeded programs. Each section is an independent list + editor; the
/// session/department lists follow the selected campus, and programs follow the
/// selected department.
/// </summary>
public partial class AcademicStructureViewModel : PageViewModel
{
    private readonly Func<IUnitOfWork> _uow;
    private readonly AppState _state;

    public override string Title => "Academic Structure";
    public override string Description =>
        "Define your campuses, academic sessions, departments and programs. Every class attaches to a program.";

    public AcademicStructureViewModel(Func<IUnitOfWork> uow, AppState state)
    {
        _uow = uow;
        _state = state;
    }

    public Array DepartmentKinds { get; } = Enum.GetValues(typeof(DepartmentKind));
    public Array ProgramKinds { get; } = Enum.GetValues(typeof(ProgramKind));

    // ===================== Campuses =====================
    public ObservableCollection<Campus> Campuses { get; } = new();
    [ObservableProperty] private Campus? _selectedCampus;
    [ObservableProperty] private int _campusEditingId;
    [ObservableProperty] private string _campusName = string.Empty;
    [ObservableProperty] private string _campusAddress = string.Empty;
    [ObservableProperty] private string _campusPhone = string.Empty;
    [ObservableProperty] private string _campusStatus = string.Empty;
    [ObservableProperty] private bool _campusStatusIsError;

    public string CampusFormTitle => CampusEditingId == 0 ? "Add campus" : "Edit campus";
    partial void OnCampusEditingIdChanged(int value) => OnPropertyChanged(nameof(CampusFormTitle));

    // ===================== Sessions =====================
    public ObservableCollection<AcademicSession> Sessions { get; } = new();
    public ObservableCollection<DayToggle> WorkingDays { get; } = new();
    [ObservableProperty] private AcademicSession? _selectedSession;
    [ObservableProperty] private int _sessionEditingId;
    [ObservableProperty] private string _sessionName = string.Empty;
    [ObservableProperty] private DateTimeOffset? _sessionStart = DateTimeOffset.Now;
    [ObservableProperty] private DateTimeOffset? _sessionEnd = DateTimeOffset.Now.AddMonths(10);
    [ObservableProperty] private bool _sessionIsActive;
    [ObservableProperty] private string _sessionStatus = string.Empty;
    [ObservableProperty] private bool _sessionStatusIsError;

    public string SessionFormTitle => SessionEditingId == 0 ? "Add session" : "Edit session";
    partial void OnSessionEditingIdChanged(int value) => OnPropertyChanged(nameof(SessionFormTitle));

    // ===================== Departments =====================
    public ObservableCollection<Department> Departments { get; } = new();
    [ObservableProperty] private Department? _selectedDepartment;
    [ObservableProperty] private int _departmentEditingId;
    [ObservableProperty] private string _departmentName = string.Empty;
    [ObservableProperty] private DepartmentKind _departmentKind = DepartmentKind.College;
    [ObservableProperty] private string _departmentStatus = string.Empty;
    [ObservableProperty] private bool _departmentStatusIsError;

    public string DepartmentFormTitle => DepartmentEditingId == 0 ? "Add department" : "Edit department";
    partial void OnDepartmentEditingIdChanged(int value) => OnPropertyChanged(nameof(DepartmentFormTitle));

    // ===================== Programs =====================
    public ObservableCollection<AcademicProgram> Programs { get; } = new();
    [ObservableProperty] private AcademicProgram? _selectedProgram;
    [ObservableProperty] private int _programEditingId;
    [ObservableProperty] private string _programName = string.Empty;
    [ObservableProperty] private ProgramKind _programKind = ProgramKind.General;
    [ObservableProperty] private string _programStatus = string.Empty;
    [ObservableProperty] private bool _programStatusIsError;

    public string ProgramFormTitle => ProgramEditingId == 0 ? "Add program" : "Edit program";
    partial void OnProgramEditingIdChanged(int value) => OnPropertyChanged(nameof(ProgramFormTitle));

    // ===================== Load =====================
    public override async Task LoadAsync()
    {
        Campuses.Clear();
        using (var uow = _uow())
        {
            foreach (var c in (await uow.Campuses.ListAsync()).OrderBy(c => c.Name))
                Campuses.Add(c);
        }

        BuildDayToggles(null);
        // Selecting the first campus cascades to its sessions + departments via
        // OnSelectedCampusChanged. Only when there is no campus do we blank the editor.
        SelectedCampus = Campuses.FirstOrDefault();
        if (SelectedCampus is null)
        {
            Sessions.Clear();
            Departments.Clear();
            Programs.Clear();
            NewCampus();
        }
    }

    // ===================== Campus commands =====================
    partial void OnSelectedCampusChanged(Campus? value)
    {
        if (value is not null)
        {
            CampusEditingId = value.Id;
            CampusName = value.Name;
            CampusAddress = value.Address;
            CampusPhone = value.Phone;
            CampusStatus = string.Empty;
        }
        _ = ReloadForCampusAsync(value?.Id ?? 0);
    }

    private async Task ReloadForCampusAsync(int campusId)
    {
        Sessions.Clear();
        Departments.Clear();
        Programs.Clear();
        SelectedDepartment = null;
        if (campusId == 0) { NewSession(); NewDepartment(); NewProgram(); return; }

        using var uow = _uow();
        foreach (var s in (await uow.Sessions.ListAsync(x => x.CampusId == campusId)).OrderByDescending(s => s.Id))
            Sessions.Add(s);
        foreach (var d in (await uow.Departments.ListAsync(x => x.CampusId == campusId)).OrderBy(d => d.Name))
            Departments.Add(d);

        NewSession();
        NewDepartment();
        NewProgram();
    }

    [RelayCommand]
    private void NewCampus()
    {
        SelectedCampus = null;
        CampusEditingId = 0;
        CampusName = string.Empty;
        CampusAddress = string.Empty;
        CampusPhone = string.Empty;
        CampusStatus = string.Empty;
    }

    [RelayCommand]
    private async Task SaveCampusAsync()
    {
        if (string.IsNullOrWhiteSpace(CampusName)) { CampusFail("Campus name is required."); return; }

        int keepId;
        using (var uow = _uow())
        {
            try
            {
                if (CampusEditingId == 0)
                {
                    var c = new Campus { Name = CampusName.Trim(), Address = CampusAddress.Trim(), Phone = CampusPhone.Trim() };
                    await uow.Campuses.AddAsync(c);
                    await uow.SaveChangesAsync();
                    keepId = c.Id;
                }
                else
                {
                    var e = await uow.Campuses.GetByIdAsync(CampusEditingId);
                    if (e is null) { CampusFail("This campus no longer exists."); return; }
                    e.Name = CampusName.Trim();
                    e.Address = CampusAddress.Trim();
                    e.Phone = CampusPhone.Trim();
                    uow.Campuses.Update(e);
                    await uow.SaveChangesAsync();
                    keepId = e.Id;
                }
            }
            catch (Exception ex) { CampusFail("Could not save: " + (ex.InnerException?.Message ?? ex.Message)); return; }
        }

        await LoadAsync();
        SelectedCampus = Campuses.FirstOrDefault(c => c.Id == keepId);
        CampusOk("Campus saved.");
    }

    [RelayCommand]
    private async Task DeleteCampusAsync()
    {
        if (CampusEditingId == 0) { CampusFail("Select a campus to delete."); return; }
        using (var uow = _uow())
        {
            var e = await uow.Campuses.GetByIdAsync(CampusEditingId);
            if (e is not null)
            {
                uow.Campuses.Remove(e);
                try { await uow.SaveChangesAsync(); }
                catch (Exception ex) { CampusFail("Cannot delete: " + (ex.InnerException?.Message ?? ex.Message)); return; }
            }
        }
        await LoadAsync();
        CampusOk("Campus deleted.");
    }

    // ===================== Session commands =====================
    partial void OnSelectedSessionChanged(AcademicSession? value)
    {
        if (value is null) return;
        SessionEditingId = value.Id;
        SessionName = value.Name;
        SessionStart = value.StartDate.ToDateTime(TimeOnly.MinValue);
        SessionEnd = value.EndDate.ToDateTime(TimeOnly.MinValue);
        SessionIsActive = value.IsActive;
        BuildDayToggles(value.WorkingDaysCsv);
        SessionStatus = string.Empty;
    }

    [RelayCommand]
    private void NewSession()
    {
        SelectedSession = null;
        SessionEditingId = 0;
        SessionName = string.Empty;
        SessionStart = DateTimeOffset.Now;
        SessionEnd = DateTimeOffset.Now.AddMonths(10);
        SessionIsActive = Sessions.Count == 0; // first session for a campus defaults to active
        BuildDayToggles(null);
        SessionStatus = string.Empty;
    }

    [RelayCommand]
    private async Task SaveSessionAsync()
    {
        if (SelectedCampus is null) { SessionFail("Select or create a campus first."); return; }
        if (string.IsNullOrWhiteSpace(SessionName)) { SessionFail("Session name is required."); return; }
        if (SessionStart is null || SessionEnd is null) { SessionFail("Start and end dates are required."); return; }
        if (SessionEnd < SessionStart) { SessionFail("End date must be on or after the start date."); return; }

        string csv = string.Join(",", WorkingDays.Where(d => d.IsOn).Select(d => d.Value));
        if (string.IsNullOrEmpty(csv)) { SessionFail("Pick at least one working day."); return; }

        var start = DateOnly.FromDateTime(SessionStart.Value.DateTime);
        var end = DateOnly.FromDateTime(SessionEnd.Value.DateTime);
        int campusId = SelectedCampus.Id;
        int keepId;

        using (var uow = _uow())
        {
            try
            {
                if (SessionIsActive)
                {
                    // Only one active session app-wide: clear the flag everywhere else.
                    foreach (var other in await uow.Sessions.ListAsync(s => s.IsActive))
                    {
                        other.IsActive = false;
                        uow.Sessions.Update(other);
                    }
                }

                if (SessionEditingId == 0)
                {
                    var s = new AcademicSession
                    {
                        CampusId = campusId, Name = SessionName.Trim(),
                        StartDate = start, EndDate = end, IsActive = SessionIsActive, WorkingDaysCsv = csv
                    };
                    await uow.Sessions.AddAsync(s);
                    await uow.SaveChangesAsync();
                    keepId = s.Id;
                }
                else
                {
                    var e = await uow.Sessions.GetByIdAsync(SessionEditingId);
                    if (e is null) { SessionFail("This session no longer exists."); return; }
                    e.Name = SessionName.Trim();
                    e.StartDate = start;
                    e.EndDate = end;
                    e.IsActive = SessionIsActive;
                    e.WorkingDaysCsv = csv;
                    uow.Sessions.Update(e);
                    await uow.SaveChangesAsync();
                    keepId = e.Id;
                }
            }
            catch (Exception ex) { SessionFail("Could not save: " + (ex.InnerException?.Message ?? ex.Message)); return; }
        }

        if (SessionIsActive) _state.ActiveSessionId = keepId;

        await ReloadForCampusAsync(campusId);
        SelectedSession = Sessions.FirstOrDefault(s => s.Id == keepId);
        SessionOk(SessionIsActive ? "Session saved and set active." : "Session saved.");
    }

    [RelayCommand]
    private async Task DeleteSessionAsync()
    {
        if (SessionEditingId == 0) { SessionFail("Select a session to delete."); return; }
        int campusId = SelectedCampus?.Id ?? 0;
        using (var uow = _uow())
        {
            var e = await uow.Sessions.GetByIdAsync(SessionEditingId);
            if (e is not null)
            {
                uow.Sessions.Remove(e);
                try { await uow.SaveChangesAsync(); }
                catch (Exception ex) { SessionFail("Cannot delete: it may have generated timetables. " + (ex.InnerException?.Message ?? ex.Message)); return; }
            }
        }
        await ReloadForCampusAsync(campusId);
        SessionOk("Session deleted.");
    }

    // ===================== Department commands =====================
    partial void OnSelectedDepartmentChanged(Department? value)
    {
        if (value is not null)
        {
            DepartmentEditingId = value.Id;
            DepartmentName = value.Name;
            DepartmentKind = value.Kind;
            DepartmentStatus = string.Empty;
        }
        _ = ReloadProgramsAsync(value?.Id ?? 0);
    }

    private async Task ReloadProgramsAsync(int departmentId)
    {
        Programs.Clear();
        if (departmentId == 0) { NewProgram(); return; }
        using var uow = _uow();
        foreach (var p in (await uow.Programs.ListAsync(x => x.DepartmentId == departmentId)).OrderBy(p => p.Name))
            Programs.Add(p);
        NewProgram();
    }

    [RelayCommand]
    private void NewDepartment()
    {
        SelectedDepartment = null;
        DepartmentEditingId = 0;
        DepartmentName = string.Empty;
        DepartmentKind = DepartmentKind.College;
        DepartmentStatus = string.Empty;
    }

    [RelayCommand]
    private async Task SaveDepartmentAsync()
    {
        if (SelectedCampus is null) { DepartmentFail("Select or create a campus first."); return; }
        if (string.IsNullOrWhiteSpace(DepartmentName)) { DepartmentFail("Department name is required."); return; }

        int campusId = SelectedCampus.Id;
        int keepId;
        using (var uow = _uow())
        {
            try
            {
                if (DepartmentEditingId == 0)
                {
                    var d = new Department { CampusId = campusId, Name = DepartmentName.Trim(), Kind = DepartmentKind };
                    await uow.Departments.AddAsync(d);
                    await uow.SaveChangesAsync();
                    keepId = d.Id;
                }
                else
                {
                    var e = await uow.Departments.GetByIdAsync(DepartmentEditingId);
                    if (e is null) { DepartmentFail("This department no longer exists."); return; }
                    e.Name = DepartmentName.Trim();
                    e.Kind = DepartmentKind;
                    uow.Departments.Update(e);
                    await uow.SaveChangesAsync();
                    keepId = e.Id;
                }
            }
            catch (Exception ex) { DepartmentFail("Could not save: " + (ex.InnerException?.Message ?? ex.Message)); return; }
        }

        await ReloadForCampusAsync(campusId);
        SelectedDepartment = Departments.FirstOrDefault(d => d.Id == keepId);
        DepartmentOk("Department saved.");
    }

    [RelayCommand]
    private async Task DeleteDepartmentAsync()
    {
        if (DepartmentEditingId == 0) { DepartmentFail("Select a department to delete."); return; }
        int campusId = SelectedCampus?.Id ?? 0;
        using (var uow = _uow())
        {
            var e = await uow.Departments.GetByIdAsync(DepartmentEditingId);
            if (e is not null)
            {
                uow.Departments.Remove(e);
                try { await uow.SaveChangesAsync(); }
                catch (Exception ex) { DepartmentFail("Cannot delete: " + (ex.InnerException?.Message ?? ex.Message)); return; }
            }
        }
        await ReloadForCampusAsync(campusId);
        DepartmentOk("Department deleted.");
    }

    // ===================== Program commands =====================
    partial void OnSelectedProgramChanged(AcademicProgram? value)
    {
        if (value is null) return;
        ProgramEditingId = value.Id;
        ProgramName = value.Name;
        ProgramKind = value.Kind;
        ProgramStatus = string.Empty;
    }

    [RelayCommand]
    private void NewProgram()
    {
        SelectedProgram = null;
        ProgramEditingId = 0;
        ProgramName = string.Empty;
        ProgramKind = ProgramKind.General;
        ProgramStatus = string.Empty;
    }

    [RelayCommand]
    private async Task SaveProgramAsync()
    {
        if (SelectedDepartment is null) { ProgramFail("Select a department first (in the Departments section)."); return; }
        if (string.IsNullOrWhiteSpace(ProgramName)) { ProgramFail("Program name is required."); return; }

        int deptId = SelectedDepartment.Id;
        int keepId;
        using (var uow = _uow())
        {
            try
            {
                if (ProgramEditingId == 0)
                {
                    var p = new AcademicProgram { DepartmentId = deptId, Name = ProgramName.Trim(), Kind = ProgramKind };
                    await uow.Programs.AddAsync(p);
                    await uow.SaveChangesAsync();
                    keepId = p.Id;
                }
                else
                {
                    var e = await uow.Programs.GetByIdAsync(ProgramEditingId);
                    if (e is null) { ProgramFail("This program no longer exists."); return; }
                    e.Name = ProgramName.Trim();
                    e.Kind = ProgramKind;
                    uow.Programs.Update(e);
                    await uow.SaveChangesAsync();
                    keepId = e.Id;
                }
            }
            catch (Exception ex) { ProgramFail("Could not save: " + (ex.InnerException?.Message ?? ex.Message)); return; }
        }

        await ReloadProgramsAsync(deptId);
        SelectedProgram = Programs.FirstOrDefault(p => p.Id == keepId);
        ProgramOk("Program saved.");
    }

    [RelayCommand]
    private async Task DeleteProgramAsync()
    {
        if (ProgramEditingId == 0) { ProgramFail("Select a program to delete."); return; }
        int deptId = SelectedDepartment?.Id ?? 0;
        using (var uow = _uow())
        {
            var e = await uow.Programs.GetByIdAsync(ProgramEditingId);
            if (e is not null)
            {
                uow.Programs.Remove(e);
                try { await uow.SaveChangesAsync(); }
                catch (Exception ex) { ProgramFail("Cannot delete: the program still has classes. " + (ex.InnerException?.Message ?? ex.Message)); return; }
            }
        }
        await ReloadProgramsAsync(deptId);
        ProgramOk("Program deleted.");
    }

    // ===================== Helpers =====================
    private void BuildDayToggles(string? csv)
    {
        // Weekday enum: Sunday=0..Saturday=6. Present Mon..Sat then Sun.
        var on = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(csv))
        {
            on = new HashSet<int> { 1, 2, 3, 4, 5 }; // Mon-Fri default
        }
        else
        {
            foreach (var tok in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (int.TryParse(tok, out int v)) on.Add(v);
        }

        WorkingDays.Clear();
        void Add(int val, string label) => WorkingDays.Add(new DayToggle { Value = val, Label = label, IsOn = on.Contains(val) });
        Add(1, "Mon"); Add(2, "Tue"); Add(3, "Wed"); Add(4, "Thu"); Add(5, "Fri"); Add(6, "Sat"); Add(0, "Sun");
    }

    private void CampusOk(string m) { CampusStatusIsError = false; CampusStatus = m; }
    private void CampusFail(string m) { CampusStatusIsError = true; CampusStatus = m; }
    private void SessionOk(string m) { SessionStatusIsError = false; SessionStatus = m; }
    private void SessionFail(string m) { SessionStatusIsError = true; SessionStatus = m; }
    private void DepartmentOk(string m) { DepartmentStatusIsError = false; DepartmentStatus = m; }
    private void DepartmentFail(string m) { DepartmentStatusIsError = true; DepartmentStatus = m; }
    private void ProgramOk(string m) { ProgramStatusIsError = false; ProgramStatus = m; }
    private void ProgramFail(string m) { ProgramStatusIsError = true; ProgramStatus = m; }
}
