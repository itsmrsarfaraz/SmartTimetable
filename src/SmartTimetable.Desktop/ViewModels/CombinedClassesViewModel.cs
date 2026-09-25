using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>
/// Manages combined-class rules: several sections that study one subject together in
/// the same slot (parallel electives, shared-hall lectures). The solver co-schedules
/// the member sections' demands for the rule's subject, so each member class must also
/// list that subject in its weekly subjects (Classes screen) with the same periods/week.
/// </summary>
public partial class CombinedClassesViewModel : PageViewModel
{
    private readonly Func<IUnitOfWork> _uow;

    public override string Title => "Combined Classes";
    public override string Description =>
        "Group sections that attend one subject together in the same room and time slot.";

    private static readonly Room NoRoom = new() { Id = 0, Name = "(no shared room)" };
    private static readonly Teacher AnyTeacher = new() { Id = 0, FullName = "(any eligible teacher)" };

    public ObservableCollection<CombinedClassRule> Rules { get; } = new();
    public ObservableCollection<Subject> Subjects { get; } = new();
    public ObservableCollection<Room> Rooms { get; } = new();
    public ObservableCollection<Teacher> Teachers { get; } = new();
    public ObservableCollection<SchoolClass> Classes { get; } = new();     // add-member source
    public ObservableCollection<SchoolClass> Members { get; } = new();     // sections in the rule

    private Dictionary<int, Subject> _subjectById = new();
    private Dictionary<int, Room> _roomById = new();
    private Dictionary<int, Teacher> _teacherById = new();
    private Dictionary<int, SchoolClass> _classById = new();

    [ObservableProperty] private CombinedClassRule? _selected;
    [ObservableProperty] private int _editingId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private Subject? _selectedSubject;
    [ObservableProperty] private Room? _selectedRoom;
    [ObservableProperty] private Teacher? _selectedTeacher;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _statusIsError;

    // Member editor.
    [ObservableProperty] private SchoolClass? _memberToAdd;
    [ObservableProperty] private SchoolClass? _selectedMember;
    [ObservableProperty] private string _memberStatus = string.Empty;
    [ObservableProperty] private bool _memberStatusIsError;

    public CombinedClassesViewModel(Func<IUnitOfWork> uow) => _uow = uow;

    public string FormTitle => EditingId == 0 ? "Add combined class" : "Edit combined class";

    /// <summary>Members can only be attached once the rule row exists.</summary>
    public bool CanEditMembers => EditingId != 0;

    partial void OnEditingIdChanged(int value)
    {
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(CanEditMembers));
    }

    private void Ok(string m) { StatusIsError = false; Status = m; }
    private void Fail(string m) { StatusIsError = true; Status = m; }
    private void MemberOk(string m) { MemberStatusIsError = false; MemberStatus = m; }
    private void MemberFail(string m) { MemberStatusIsError = true; MemberStatus = m; }

    public override async Task LoadAsync()
    {
        using var uow = _uow();
        var subjects = await uow.Subjects.ListAsync();
        var rooms = await uow.Rooms.ListAsync();
        var teachers = await uow.Teachers.ListAsync();
        var classes = await uow.Classes.ListAsync();
        var rules = await uow.CombinedClasses.Query().Include(r => r.Members).ToListAsync();

        _subjectById = subjects.ToDictionary(s => s.Id);
        _roomById = rooms.ToDictionary(r => r.Id);
        _teacherById = teachers.ToDictionary(t => t.Id);
        _classById = classes.ToDictionary(c => c.Id);

        Subjects.Clear();
        foreach (var s in subjects) Subjects.Add(s);

        Rooms.Clear();
        Rooms.Add(NoRoom);
        foreach (var r in rooms) Rooms.Add(r);

        Teachers.Clear();
        Teachers.Add(AnyTeacher);
        foreach (var t in teachers) Teachers.Add(t);

        Classes.Clear();
        foreach (var c in classes) Classes.Add(c);

        Rules.Clear();
        foreach (var rule in rules)
        {
            if (_subjectById.TryGetValue(rule.SubjectId, out var s)) rule.Subject = s;
            Rules.Add(rule);
        }

        ResetForm();
    }

    partial void OnSelectedChanged(CombinedClassRule? value)
    {
        if (value is null) return;
        EditingId = value.Id;
        Name = value.Name;
        SelectedSubject = _subjectById.TryGetValue(value.SubjectId, out var s) ? s : Subjects.FirstOrDefault();
        SelectedRoom = value.SharedRoomId is int rid && _roomById.TryGetValue(rid, out var r) ? r : NoRoom;
        SelectedTeacher = value.PreferredTeacherId is int tid && _teacherById.TryGetValue(tid, out var t) ? t : AnyTeacher;
        Status = string.Empty;
        MemberStatus = string.Empty;
        _ = LoadMembersAsync(value.Id);
    }

    private async Task LoadMembersAsync(int ruleId)
    {
        Members.Clear();
        if (ruleId == 0) return;

        using var uow = _uow();
        var links = await uow.CombinedClassMembers.ListAsync(m => m.CombinedClassRuleId == ruleId);
        foreach (var link in links)
            if (_classById.TryGetValue(link.SchoolClassId, out var c))
                Members.Add(c);
    }

    private void ResetForm()
    {
        Selected = null;
        EditingId = 0;
        Name = string.Empty;
        SelectedSubject = Subjects.FirstOrDefault();
        SelectedRoom = NoRoom;
        SelectedTeacher = AnyTeacher;
        Members.Clear();
        MemberToAdd = Classes.FirstOrDefault();
    }

    [RelayCommand]
    private void New()
    {
        ResetForm();
        Status = string.Empty;
        MemberStatus = string.Empty;
    }

    [RelayCommand]
    private async Task SaveRuleAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { Fail("Give the combined class a name."); return; }
        if (SelectedSubject is null) { Fail("Select the subject taught to the combined group."); return; }

        int? roomId = SelectedRoom is null || SelectedRoom.Id == 0 ? null : SelectedRoom.Id;
        int? teacherId = SelectedTeacher is null || SelectedTeacher.Id == 0 ? null : SelectedTeacher.Id;
        int subjectId = SelectedSubject.Id;
        int keepId;

        using (var uow = _uow())
        {
            try
            {
                if (EditingId == 0)
                {
                    var rule = new CombinedClassRule
                    {
                        Name = Name.Trim(), SubjectId = subjectId,
                        SharedRoomId = roomId, PreferredTeacherId = teacherId
                    };
                    await uow.CombinedClasses.AddAsync(rule);
                    await uow.SaveChangesAsync();
                    keepId = rule.Id;
                }
                else
                {
                    var e = await uow.CombinedClasses.GetByIdAsync(EditingId);
                    if (e is null) { Fail("This combined class no longer exists."); return; }
                    e.Name = Name.Trim();
                    e.SubjectId = subjectId;
                    e.SharedRoomId = roomId;
                    e.PreferredTeacherId = teacherId;
                    uow.CombinedClasses.Update(e);
                    await uow.SaveChangesAsync();
                    keepId = e.Id;
                }
            }
            catch (Exception ex) { Fail("Could not save: " + (ex.InnerException?.Message ?? ex.Message)); return; }
        }

        await LoadAsync();
        Selected = Rules.FirstOrDefault(r => r.Id == keepId);
        Ok("Combined class saved. Now add the sections that attend together.");
    }

    [RelayCommand]
    private async Task DeleteRuleAsync()
    {
        if (EditingId == 0) { Fail("Select a combined class to delete."); return; }
        using (var uow = _uow())
        {
            var e = await uow.CombinedClasses.GetByIdAsync(EditingId);
            if (e is not null)
            {
                uow.CombinedClasses.Remove(e); // members cascade-delete
                try { await uow.SaveChangesAsync(); }
                catch (Exception ex) { Fail("Cannot delete: " + (ex.InnerException?.Message ?? ex.Message)); return; }
            }
        }
        await LoadAsync();
        Ok("Combined class deleted.");
    }

    [RelayCommand]
    private async Task AddMemberAsync()
    {
        if (EditingId == 0) { MemberFail("Save the combined class first."); return; }
        var cls = MemberToAdd;
        if (cls is null) { MemberFail("Pick a section to add."); return; }
        if (Members.Any(m => m.Id == cls.Id)) { MemberFail("That section is already in this group."); return; }

        using (var uow = _uow())
        {
            try
            {
                await uow.CombinedClassMembers.AddAsync(new CombinedClassMember
                {
                    CombinedClassRuleId = EditingId,
                    SchoolClassId = cls.Id
                });
                await uow.SaveChangesAsync();
            }
            catch (Exception ex) { MemberFail("Could not add the section: " + (ex.InnerException?.Message ?? ex.Message)); return; }
        }

        await LoadMembersAsync(EditingId);
        MemberOk($"Added “{cls.Name}”. Make sure it also lists this subject in its weekly subjects.");
    }

    [RelayCommand]
    private async Task RemoveMemberAsync()
    {
        var row = SelectedMember;
        if (row is null) { MemberFail("Select a section to remove."); return; }

        using (var uow = _uow())
        {
            try
            {
                var link = await uow.CombinedClassMembers
                    .FirstOrDefaultAsync(m => m.CombinedClassRuleId == EditingId && m.SchoolClassId == row.Id);
                if (link is not null)
                {
                    uow.CombinedClassMembers.Remove(link);
                    await uow.SaveChangesAsync();
                }
            }
            catch (Exception ex) { MemberFail("Could not remove the section: " + (ex.InnerException?.Message ?? ex.Message)); return; }
        }

        await LoadMembersAsync(EditingId);
        MemberOk("Section removed.");
    }
}
