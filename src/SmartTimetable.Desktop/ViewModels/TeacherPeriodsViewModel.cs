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

/// <summary>One of a teacher's pinned class-subject demands, shown as a selectable row.</summary>
public partial class TeacherAssignmentRow : ObservableObject
{
    public int ClassSubjectId { get; init; }
    public string ClassName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public int PeriodsPerWeek { get; init; }

    /// <summary>Preference strength saved for this demand (0 = no period preference).</summary>
    public int Priority { get; set; }

    /// <summary>Ids of the periods currently preferred for this demand.</summary>
    public HashSet<int> PreferredPeriodIds { get; } = new();

    /// <summary>Human-readable summary shown under the row (refreshed after each save).</summary>
    [ObservableProperty] private string _preferredText = string.Empty;

    public string Header => $"{ClassName} — {SubjectName}";
}

/// <summary>A teaching period the teacher may tick as preferred for the selected demand.</summary>
public partial class PeriodPick : ObservableObject
{
    public int PeriodId { get; init; }
    public string Name { get; init; } = string.Empty;
    [ObservableProperty] private bool _isSelected;
}

/// <summary>An unpinned class-subject demand this teacher is qualified to take.</summary>
public sealed class AssignableDemand
{
    public int ClassSubjectId { get; init; }
    public string Label { get; init; } = string.Empty;
}

/// <summary>
/// Lets the admin record, per (teacher, class, subject) assignment, which periods the
/// teacher would like that lecture placed in and how strongly. Preferences are soft: the
/// solver is penalised (weighted by the priority) for placing the lecture outside the
/// chosen periods, but may still break the preference to keep the timetable feasible.
/// Because each demand is a single (class, subject) pair, every class a teacher takes can
/// carry its own set of preferred periods and its own priority — so the same teacher can
/// ask for Period 1 in one class and Period 5 or 7 in another.
/// </summary>
public partial class TeacherPeriodsViewModel : PageViewModel
{
    private readonly Func<IUnitOfWork> _uow;

    public override string Title => "Teacher Periods";
    public override string Description =>
        "For each class a teacher is assigned to, choose the periods they prefer and how strongly to honour them.";

    public ObservableCollection<Teacher> Teachers { get; } = new();
    public ObservableCollection<TeacherAssignmentRow> Assignments { get; } = new();
    public ObservableCollection<PeriodPick> Periods { get; } = new();
    public ObservableCollection<AssignableDemand> AssignableDemands { get; } = new();

    [ObservableProperty] private Teacher? _selectedTeacher;
    [ObservableProperty] private TeacherAssignmentRow? _selectedAssignment;
    [ObservableProperty] private AssignableDemand? _demandToAssign;
    [ObservableProperty] private int _priority = 1;
    [ObservableProperty] private string _status = string.Empty;

    // Non-break teaching periods, loaded once per screen visit.
    private List<Period> _teachingPeriods = new();

    public TeacherPeriodsViewModel(Func<IUnitOfWork> uow) => _uow = uow;

    /// <summary>True while an assignment is selected, so the period editor is shown.</summary>
    public bool HasSelectedAssignment => SelectedAssignment is not null;

    public override async Task LoadAsync()
    {
        using var uow = _uow();
        var teachers = await uow.Teachers.ListAsync();
        _teachingPeriods = await uow.Periods.Query()
            .Where(p => !p.IsBreak)
            .OrderBy(p => p.Order)
            .ToListAsync();

        Assignments.Clear();
        AssignableDemands.Clear();
        Periods.Clear();
        SelectedAssignment = null;

        Teachers.Clear();
        foreach (var t in teachers) Teachers.Add(t);

        // Setting the teacher triggers OnSelectedTeacherChanged, which loads assignments.
        SelectedTeacher = Teachers.Count > 0 ? Teachers[0] : null;
    }

    partial void OnSelectedTeacherChanged(Teacher? value) => _ = LoadAssignmentsAsync(value);

    partial void OnSelectedAssignmentChanged(TeacherAssignmentRow? value)
    {
        OnPropertyChanged(nameof(HasSelectedAssignment));
        Priority = value?.Priority ?? 1;
        BuildPeriodPicks(value);
        Status = string.Empty;
    }

    private async Task LoadAssignmentsAsync(Teacher? teacher)
    {
        Assignments.Clear();
        AssignableDemands.Clear();
        Periods.Clear();
        SelectedAssignment = null;
        Status = string.Empty;
        if (teacher is null) return;

        using var uow = _uow();

        // The demands this teacher is pinned to, with class, subject and preferred periods.
        var pinned = await uow.ClassSubjects.Query()
            .Include(cs => cs.SchoolClass)
            .Include(cs => cs.Subject)
            .Include(cs => cs.PreferredPeriods)
            .Where(cs => cs.PreferredTeacherId == teacher.Id)
            .ToListAsync();

        foreach (var cs in pinned
                     .OrderBy(c => c.SchoolClass!.Name)
                     .ThenBy(c => c.Subject!.Name))
        {
            var row = new TeacherAssignmentRow
            {
                ClassSubjectId = cs.Id,
                ClassName = cs.SchoolClass?.Name ?? $"Class {cs.SchoolClassId}",
                SubjectName = cs.Subject?.Name ?? $"Subject {cs.SubjectId}",
                PeriodsPerWeek = cs.PeriodsPerWeek,
                Priority = cs.PreferencePriority
            };
            foreach (var pp in cs.PreferredPeriods) row.PreferredPeriodIds.Add(pp.PeriodId);
            row.PreferredText = DescribePreferred(row);
            Assignments.Add(row);
        }

        // Unpinned demands this teacher is qualified for, offered for assignment here.
        var eligibleSubjectIds = (await uow.TeacherSubjects.ListAsync(ts => ts.TeacherId == teacher.Id))
            .Select(ts => ts.SubjectId)
            .ToHashSet();

        if (eligibleSubjectIds.Count > 0)
        {
            var open = await uow.ClassSubjects.Query()
                .Include(cs => cs.SchoolClass)
                .Include(cs => cs.Subject)
                .Where(cs => cs.PreferredTeacherId == null && eligibleSubjectIds.Contains(cs.SubjectId))
                .ToListAsync();

            foreach (var cs in open
                         .OrderBy(c => c.SchoolClass!.Name)
                         .ThenBy(c => c.Subject!.Name))
            {
                AssignableDemands.Add(new AssignableDemand
                {
                    ClassSubjectId = cs.Id,
                    Label = $"{cs.SchoolClass?.Name} — {cs.Subject?.Name} ({cs.PeriodsPerWeek}/wk)"
                });
            }
        }

        SelectedAssignment = Assignments.Count > 0 ? Assignments[0] : null;
    }

    private void BuildPeriodPicks(TeacherAssignmentRow? row)
    {
        Periods.Clear();
        if (row is null) return;
        foreach (var p in _teachingPeriods)
        {
            Periods.Add(new PeriodPick
            {
                PeriodId = p.Id,
                Name = p.Name,
                IsSelected = row.PreferredPeriodIds.Contains(p.Id)
            });
        }
    }

    private string DescribePreferred(TeacherAssignmentRow row)
    {
        string load = $"{row.PeriodsPerWeek}/wk";
        if (row.Priority <= 0 || row.PreferredPeriodIds.Count == 0)
            return $"{load} · no period preference";

        var names = _teachingPeriods
            .Where(p => row.PreferredPeriodIds.Contains(p.Id))
            .Select(p => p.Name);
        return $"{load} · priority {row.Priority}: {string.Join(", ", names)}";
    }

    [RelayCommand]
    private async Task SavePreferenceAsync()
    {
        // Capture the selection once: it is a bindable property that could change under us,
        // and a local also lets the null-flow analysis see it as non-null after the guard.
        var row = SelectedAssignment;
        if (row is null) { Status = "Select an assigned class first."; return; }

        var chosen = Periods.Where(p => p.IsSelected).Select(p => p.PeriodId).ToHashSet();
        int priority = Priority < 0 ? 0 : Priority;

        if (priority > 0 && chosen.Count == 0)
        {
            Status = "Tick at least one preferred period, or set priority to 0 for no preference.";
            return;
        }

        // Priority 0 means "no preference", so clear any ticked periods (they would be ignored).
        if (priority == 0) chosen.Clear();

        using var uow = _uow();
        var cs = await uow.ClassSubjects.Query()
            .Include(x => x.PreferredPeriods)
            .FirstOrDefaultAsync(x => x.Id == row.ClassSubjectId);
        if (cs is null) { Status = "This assignment no longer exists."; return; }

        cs.PreferencePriority = priority;

        // Reconcile the child rows: drop unticked, add newly ticked. Removed children are
        // orphan-deleted on save because the relationship is required and cascades.
        foreach (var pp in cs.PreferredPeriods.ToList())
            if (!chosen.Contains(pp.PeriodId))
                cs.PreferredPeriods.Remove(pp);

        var have = cs.PreferredPeriods.Select(pp => pp.PeriodId).ToHashSet();
        foreach (var pid in chosen)
            if (!have.Contains(pid))
                cs.PreferredPeriods.Add(new ClassSubjectPreferredPeriod { PeriodId = pid });

        uow.ClassSubjects.Update(cs);
        try
        {
            await uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Status = "Could not save: " + (ex.InnerException?.Message ?? ex.Message);
            return;
        }

        // Reflect the saved state back into the selected row + its list summary.
        row.Priority = priority;
        row.PreferredPeriodIds.Clear();
        foreach (var pid in chosen) row.PreferredPeriodIds.Add(pid);
        row.PreferredText = DescribePreferred(row);

        Status = priority > 0
            ? "Preference saved."
            : "Cleared — this class now has no period preference.";
    }

    [RelayCommand]
    private async Task AssignDemandAsync()
    {
        var teacher = SelectedTeacher;
        var demand = DemandToAssign;
        if (teacher is null) { Status = "Select a teacher first."; return; }
        if (demand is null) { Status = "Choose a class to assign to this teacher."; return; }

        using var uow = _uow();
        var cs = await uow.ClassSubjects.GetByIdAsync(demand.ClassSubjectId);
        if (cs is null) { Status = "That class demand no longer exists."; return; }

        cs.PreferredTeacherId = teacher.Id;
        uow.ClassSubjects.Update(cs);
        try
        {
            await uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Status = "Could not assign: " + (ex.InnerException?.Message ?? ex.Message);
            return;
        }

        await LoadAssignmentsAsync(teacher);
        Status = "Class assigned. Select it above to set its preferred periods.";
    }
}
