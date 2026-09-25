using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>A subject row with a checkbox indicating whether the teacher can teach it.</summary>
public partial class SubjectToggle : ObservableObject
{
    public int SubjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    [ObservableProperty] private bool _isAssigned;
}

/// <summary>
/// Maps the many-to-many "which subjects can this teacher teach" relationship.
/// Pick a teacher, tick the subjects they are qualified for, then Save.
/// </summary>
public partial class TeacherSubjectViewModel : PageViewModel
{
    private readonly Func<IUnitOfWork> _uow;

    public override string Title => "Teacher–Subject Mapping";
    public override string Description => "Choose which subjects each teacher is qualified to teach. The solver only assigns qualified teachers.";

    public ObservableCollection<Teacher> Teachers { get; } = new();
    public ObservableCollection<SubjectToggle> SubjectToggles { get; } = new();

    [ObservableProperty] private Teacher? _selectedTeacher;
    [ObservableProperty] private string _status = string.Empty;

    private List<Subject> _allSubjects = new();

    public TeacherSubjectViewModel(Func<IUnitOfWork> uow) => _uow = uow;

    public override async Task LoadAsync()
    {
        using var uow = _uow();
        var teachers = await uow.Teachers.ListAsync();
        _allSubjects = await uow.Subjects.ListAsync();

        Teachers.Clear();
        foreach (var t in teachers) Teachers.Add(t);

        SubjectToggles.Clear();
        SelectedTeacher = Teachers.Count > 0 ? Teachers[0] : null;
    }

    partial void OnSelectedTeacherChanged(Teacher? value)
    {
        _ = LoadTogglesAsync(value);
    }

    private async Task LoadTogglesAsync(Teacher? teacher)
    {
        SubjectToggles.Clear();
        Status = string.Empty;
        if (teacher is null) return;

        HashSet<int> assigned = new();
        using (var uow = _uow())
        {
            var maps = await uow.TeacherSubjects.ListAsync(ts => ts.TeacherId == teacher.Id);
            foreach (var m in maps) assigned.Add(m.SubjectId);
        }

        foreach (var s in _allSubjects)
        {
            SubjectToggles.Add(new SubjectToggle
            {
                SubjectId = s.Id,
                Name = string.IsNullOrWhiteSpace(s.Code) ? s.Name : $"{s.Name} ({s.Code})",
                IsAssigned = assigned.Contains(s.Id)
            });
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedTeacher is null) { Status = "Select a teacher first."; return; }

        int teacherId = SelectedTeacher.Id;
        using var uow = _uow();
        var current = await uow.TeacherSubjects.ListAsync(ts => ts.TeacherId == teacherId);
        var currentBySubject = new Dictionary<int, TeacherSubject>();
        foreach (var m in current) currentBySubject[m.SubjectId] = m;

        int added = 0, removed = 0;
        foreach (var toggle in SubjectToggles)
        {
            bool has = currentBySubject.ContainsKey(toggle.SubjectId);
            if (toggle.IsAssigned && !has)
            {
                await uow.TeacherSubjects.AddAsync(new TeacherSubject
                {
                    TeacherId = teacherId,
                    SubjectId = toggle.SubjectId
                });
                added++;
            }
            else if (!toggle.IsAssigned && has)
            {
                uow.TeacherSubjects.Remove(currentBySubject[toggle.SubjectId]);
                removed++;
            }
        }

        if (added == 0 && removed == 0)
        {
            Status = "No changes to save.";
            return;
        }

        await uow.SaveChangesAsync();
        Status = $"Saved. {added} added, {removed} removed.";
    }
}
