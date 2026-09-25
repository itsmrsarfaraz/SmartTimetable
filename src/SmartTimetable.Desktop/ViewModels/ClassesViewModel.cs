using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>
/// Manages class sections and, for the selected class, the weekly subject demand
/// (subject + periods-per-week + optional pinned teacher) that the solver must satisfy.
/// </summary>
public partial class ClassesViewModel : PageViewModel
{
    private readonly Func<IUnitOfWork> _uow;

    public override string Title => "Classes";
    public override string Description => "Class sections and the weekly subject demand the solver must satisfy.";

    // "None"/"Any" sentinels for the optional combo selections.
    private static readonly Room NoRoom = new() { Id = 0, Name = "(no home room)" };
    private static readonly Teacher AnyTeacher = new() { Id = 0, FullName = "(any eligible teacher)" };

    public ObservableCollection<SchoolClass> Items { get; } = new();
    public ObservableCollection<AcademicProgram> Programs { get; } = new();
    public ObservableCollection<Room> Rooms { get; } = new();
    public ObservableCollection<Subject> Subjects { get; } = new();
    public ObservableCollection<Teacher> Teachers { get; } = new();
    public ObservableCollection<ClassSubject> Demands { get; } = new();

    private Dictionary<int, Subject> _subjectById = new();
    private Dictionary<int, Teacher> _teacherById = new();
    private Dictionary<int, Room> _roomById = new();
    private Dictionary<int, AcademicProgram> _programById = new();

    [ObservableProperty] private SchoolClass? _selected;
    [ObservableProperty] private int _editingId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _section = string.Empty;
    [ObservableProperty] private int _gradeLevel;
    [ObservableProperty] private int _studentCount = 30;
    [ObservableProperty] private AcademicProgram? _selectedProgram;
    [ObservableProperty] private Room? _selectedHomeRoom;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _statusIsError;

    // Demand editor.
    [ObservableProperty] private Subject? _demandSubject;
    [ObservableProperty] private int _demandPeriods = 5;
    [ObservableProperty] private Teacher? _demandTeacher;
    [ObservableProperty] private ClassSubject? _selectedDemand;
    [ObservableProperty] private string _demandStatus = string.Empty;
    [ObservableProperty] private bool _demandStatusIsError;

    public ClassesViewModel(Func<IUnitOfWork> uow) => _uow = uow;

    public string FormTitle => EditingId == 0 ? "Add class" : "Edit class";

    /// <summary>The weekly-subjects editor only makes sense once a class row exists.</summary>
    public bool CanEditDemands => EditingId != 0;

    partial void OnEditingIdChanged(int value)
    {
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(CanEditDemands));
    }

    /// <summary>Show a green success banner.</summary>
    private void Ok(string message) { StatusIsError = false; Status = message; }

    /// <summary>Show a red error/validation banner.</summary>
    private void Fail(string message) { StatusIsError = true; Status = message; }

    private void DemandOk(string message) { DemandStatusIsError = false; DemandStatus = message; }

    private void DemandFail(string message) { DemandStatusIsError = true; DemandStatus = message; }

    public override async Task LoadAsync()
    {
        using var uow = _uow();
        var programs = await uow.Programs.ListAsync();
        var rooms = await uow.Rooms.ListAsync();
        var subjects = await uow.Subjects.ListAsync();
        var teachers = await uow.Teachers.ListAsync();
        var classes = await uow.Classes.ListAsync();

        _programById = programs.ToDictionary(p => p.Id);
        _roomById = rooms.ToDictionary(r => r.Id);
        _subjectById = subjects.ToDictionary(s => s.Id);
        _teacherById = teachers.ToDictionary(t => t.Id);

        Programs.Clear();
        foreach (var p in programs) Programs.Add(p);

        Rooms.Clear();
        Rooms.Add(NoRoom);
        foreach (var r in rooms) Rooms.Add(r);

        Subjects.Clear();
        foreach (var s in subjects) Subjects.Add(s);

        Teachers.Clear();
        Teachers.Add(AnyTeacher);
        foreach (var t in teachers) Teachers.Add(t);

        Items.Clear();
        foreach (var c in classes)
        {
            if (_programById.TryGetValue(c.ProgramId, out var prog)) c.Program = prog;
            Items.Add(c);
        }

        ResetForm();
    }

    partial void OnSelectedChanged(SchoolClass? value)
    {
        if (value is null) return;
        EditingId = value.Id;
        Name = value.Name;
        Section = value.Section;
        GradeLevel = value.GradeLevel;
        StudentCount = value.StudentCount;
        SelectedProgram = _programById.TryGetValue(value.ProgramId, out var p) ? p : Programs.FirstOrDefault();
        SelectedHomeRoom = value.HomeRoomId is int rid && _roomById.TryGetValue(rid, out var r) ? r : NoRoom;
        Status = string.Empty;
        DemandStatus = string.Empty;
        _ = LoadDemandsAsync(value.Id);
    }

    private async Task LoadDemandsAsync(int classId)
    {
        Demands.Clear();
        if (classId == 0) return;

        using var uow = _uow();
        var list = await uow.ClassSubjects.ListAsync(cs => cs.SchoolClassId == classId);
        foreach (var cs in list)
        {
            if (_subjectById.TryGetValue(cs.SubjectId, out var s)) cs.Subject = s;
            if (cs.PreferredTeacherId is int tid && _teacherById.TryGetValue(tid, out var t)) cs.PreferredTeacher = t;
            Demands.Add(cs);
        }
    }

    private void ResetForm()
    {
        Selected = null;
        EditingId = 0;
        Name = string.Empty;
        Section = string.Empty;
        GradeLevel = 0;
        StudentCount = 30;
        SelectedProgram = Programs.FirstOrDefault();
        SelectedHomeRoom = NoRoom;
        Demands.Clear();
        DemandSubject = Subjects.FirstOrDefault();
        DemandTeacher = AnyTeacher;
        DemandPeriods = 5;
    }

    [RelayCommand]
    private void New()
    {
        ResetForm();
        Status = string.Empty;
        DemandStatus = string.Empty;
    }

    [RelayCommand]
    private async Task SaveClassAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Fail("Class name is required.");
            return;
        }
        if (SelectedProgram is null)
        {
            Fail("Select a program first. If the list is empty, create one on the Academic Structure screen.");
            return;
        }
        if (StudentCount <= 0)
        {
            Fail("Student count must be greater than zero.");
            return;
        }

        int? homeRoomId = SelectedHomeRoom is null || SelectedHomeRoom.Id == 0 ? null : SelectedHomeRoom.Id;
        int keepId;

        using (var uow = _uow())
        {
            try
            {
                if (EditingId == 0)
                {
                    var c = new SchoolClass
                    {
                        Name = Name.Trim(),
                        Section = Section.Trim(),
                        GradeLevel = GradeLevel,
                        StudentCount = StudentCount,
                        ProgramId = SelectedProgram.Id,
                        HomeRoomId = homeRoomId
                    };
                    await uow.Classes.AddAsync(c);
                    await uow.SaveChangesAsync();
                    EditingId = c.Id;
                }
                else
                {
                    var e = await uow.Classes.GetByIdAsync(EditingId);
                    if (e is null) { Fail("This class no longer exists."); return; }
                    e.Name = Name.Trim();
                    e.Section = Section.Trim();
                    e.GradeLevel = GradeLevel;
                    e.StudentCount = StudentCount;
                    e.ProgramId = SelectedProgram.Id;
                    e.HomeRoomId = homeRoomId;
                    uow.Classes.Update(e);
                    await uow.SaveChangesAsync();
                }
                keepId = EditingId;
            }
            catch (Exception ex)
            {
                // Surface the real database error (e.g. a stale schema from EnsureCreated
                // after an update) instead of failing silently.
                Fail("Could not save the class: " + (ex.InnerException?.Message ?? ex.Message));
                return;
            }
        }

        await LoadAsync();
        var again = Items.FirstOrDefault(x => x.Id == keepId);
        if (again is not null) Selected = again;
        Ok("Class saved. You can now add its weekly subjects below.");
    }

    [RelayCommand]
    private async Task DeleteClassAsync()
    {
        if (EditingId == 0) { Fail("Select a class to delete."); return; }

        using var uow = _uow();
        var e = await uow.Classes.GetByIdAsync(EditingId);
        if (e is not null)
        {
            var demands = await uow.ClassSubjects.ListAsync(cs => cs.SchoolClassId == EditingId);
            uow.ClassSubjects.RemoveRange(demands);
            uow.Classes.Remove(e);
            try
            {
                await uow.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Fail("Cannot delete: the class is referenced by a timetable. " + (ex.InnerException?.Message ?? ex.Message));
                return;
            }
        }

        await LoadAsync();
        Ok("Class deleted.");
    }

    [RelayCommand]
    private async Task AddDemandAsync()
    {
        if (EditingId == 0) { DemandFail("Save the class first, then add its subjects."); return; }
        var subject = DemandSubject;
        if (subject is null) { DemandFail("Select a subject."); return; }
        if (DemandPeriods <= 0) { DemandFail("Periods per week must be greater than zero."); return; }

        int? teacherId = DemandTeacher is null || DemandTeacher.Id == 0 ? null : DemandTeacher.Id;

        using (var uow = _uow())
        {
            try
            {
                var existing = await uow.ClassSubjects.ListAsync(cs => cs.SchoolClassId == EditingId && cs.SubjectId == subject.Id);
                if (existing.Count > 0)
                {
                    var e = existing[0];
                    e.PeriodsPerWeek = DemandPeriods;
                    e.PreferredTeacherId = teacherId;
                    uow.ClassSubjects.Update(e);
                }
                else
                {
                    await uow.ClassSubjects.AddAsync(new ClassSubject
                    {
                        SchoolClassId = EditingId,
                        SubjectId = subject.Id,
                        PeriodsPerWeek = DemandPeriods,
                        PreferredTeacherId = teacherId
                    });
                }
                await uow.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                DemandFail("Could not save the subject: " + (ex.InnerException?.Message ?? ex.Message));
                return;
            }
        }

        await LoadDemandsAsync(EditingId);
        DemandOk($"Saved “{subject.Name}” — {DemandPeriods} period(s)/week.");
    }

    [RelayCommand]
    private async Task RemoveDemandAsync()
    {
        var row = SelectedDemand;
        if (row is null) { DemandFail("Select a subject row to remove."); return; }

        using (var uow = _uow())
        {
            try
            {
                var e = await uow.ClassSubjects.GetByIdAsync(row.Id);
                if (e is not null)
                {
                    uow.ClassSubjects.Remove(e);
                    await uow.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                DemandFail("Could not remove the subject: " + (ex.InnerException?.Message ?? ex.Message));
                return;
            }
        }

        await LoadDemandsAsync(EditingId);
        DemandOk("Subject removed.");
    }
}
