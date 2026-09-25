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
/// Manages contradictory-subject rules: a subject that must NOT be offered inside a
/// given program. During generation, any weekly demand for a forbidden (subject,
/// program) pair is dropped, so the subject never appears on that program's timetable.
/// </summary>
public partial class ContradictorySubjectsViewModel : PageViewModel
{
    private readonly Func<IUnitOfWork> _uow;

    public override string Title => "Contradictory Subjects";
    public override string Description =>
        "Block a subject from a program. Classes in that program will never be scheduled for the subject.";

    public ObservableCollection<ContradictorySubjectRule> Rules { get; } = new();
    public ObservableCollection<Subject> Subjects { get; } = new();
    public ObservableCollection<AcademicProgram> Programs { get; } = new();

    private Dictionary<int, Subject> _subjectById = new();
    private Dictionary<int, AcademicProgram> _programById = new();

    [ObservableProperty] private ContradictorySubjectRule? _selected;
    [ObservableProperty] private int _editingId;
    [ObservableProperty] private Subject? _selectedSubject;
    [ObservableProperty] private AcademicProgram? _selectedProgram;
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _statusIsError;

    public ContradictorySubjectsViewModel(Func<IUnitOfWork> uow) => _uow = uow;

    public string FormTitle => EditingId == 0 ? "Add rule" : "Edit rule";
    partial void OnEditingIdChanged(int value) => OnPropertyChanged(nameof(FormTitle));

    private void Ok(string m) { StatusIsError = false; Status = m; }
    private void Fail(string m) { StatusIsError = true; Status = m; }

    public override async Task LoadAsync()
    {
        using var uow = _uow();
        var subjects = await uow.Subjects.ListAsync();
        var programs = await uow.Programs.ListAsync();
        var rules = await uow.ContradictoryRules.ListAsync();

        _subjectById = subjects.ToDictionary(s => s.Id);
        _programById = programs.ToDictionary(p => p.Id);

        Subjects.Clear();
        foreach (var s in subjects) Subjects.Add(s);

        Programs.Clear();
        foreach (var p in programs) Programs.Add(p);

        Rules.Clear();
        foreach (var r in rules)
        {
            if (_subjectById.TryGetValue(r.SubjectId, out var s)) r.Subject = s;
            if (_programById.TryGetValue(r.ProgramId, out var p)) r.Program = p;
            Rules.Add(r);
        }

        ResetForm();
    }

    partial void OnSelectedChanged(ContradictorySubjectRule? value)
    {
        if (value is null) return;
        EditingId = value.Id;
        SelectedSubject = _subjectById.TryGetValue(value.SubjectId, out var s) ? s : Subjects.FirstOrDefault();
        SelectedProgram = _programById.TryGetValue(value.ProgramId, out var p) ? p : Programs.FirstOrDefault();
        Reason = value.Reason;
        Status = string.Empty;
    }

    private void ResetForm()
    {
        Selected = null;
        EditingId = 0;
        SelectedSubject = Subjects.FirstOrDefault();
        SelectedProgram = Programs.FirstOrDefault();
        Reason = string.Empty;
    }

    [RelayCommand]
    private void New()
    {
        ResetForm();
        Status = string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedSubject is null) { Fail("Select a subject."); return; }
        if (SelectedProgram is null) { Fail("Select a program. If the list is empty, create one on the Academic Structure screen."); return; }

        int subjectId = SelectedSubject.Id;
        int programId = SelectedProgram.Id;
        int keepId;

        using (var uow = _uow())
        {
            try
            {
                // Prevent duplicate (subject, program) pairs.
                var dupe = await uow.ContradictoryRules
                    .FirstOrDefaultAsync(r => r.SubjectId == subjectId && r.ProgramId == programId && r.Id != EditingId);
                if (dupe is not null) { Fail("That subject is already blocked for that program."); return; }

                if (EditingId == 0)
                {
                    var rule = new ContradictorySubjectRule { SubjectId = subjectId, ProgramId = programId, Reason = Reason.Trim() };
                    await uow.ContradictoryRules.AddAsync(rule);
                    await uow.SaveChangesAsync();
                    keepId = rule.Id;
                }
                else
                {
                    var e = await uow.ContradictoryRules.GetByIdAsync(EditingId);
                    if (e is null) { Fail("This rule no longer exists."); return; }
                    e.SubjectId = subjectId;
                    e.ProgramId = programId;
                    e.Reason = Reason.Trim();
                    uow.ContradictoryRules.Update(e);
                    await uow.SaveChangesAsync();
                    keepId = e.Id;
                }
            }
            catch (Exception ex) { Fail("Could not save: " + (ex.InnerException?.Message ?? ex.Message)); return; }
        }

        await LoadAsync();
        Selected = Rules.FirstOrDefault(r => r.Id == keepId);
        Ok("Rule saved. It takes effect the next time you generate a timetable.");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (EditingId == 0) { Fail("Select a rule to delete."); return; }
        using (var uow = _uow())
        {
            var e = await uow.ContradictoryRules.GetByIdAsync(EditingId);
            if (e is not null)
            {
                uow.ContradictoryRules.Remove(e);
                try { await uow.SaveChangesAsync(); }
                catch (Exception ex) { Fail("Could not delete: " + (ex.InnerException?.Message ?? ex.Message)); return; }
            }
        }
        await LoadAsync();
        Ok("Rule deleted.");
    }
}
