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
/// Manages subject-pairing rules: two subjects that may run at the same time. The
/// generator softly aligns them into the same period so parallel electives across
/// programs line up and can share teachers or rooms. Pairing is symmetric.
/// </summary>
public partial class SubjectPairingViewModel : PageViewModel
{
    private readonly Func<IUnitOfWork> _uow;

    public override string Title => "Parallel Electives";
    public override string Description =>
        "Let two subjects run at the same time (parallel electives). The generator tries to place them in the same period so electives across programs line up and can share a slot.";

    public ObservableCollection<SubjectPairingRule> Rules { get; } = new();
    public ObservableCollection<Subject> Subjects { get; } = new();

    private Dictionary<int, Subject> _subjectById = new();

    [ObservableProperty] private SubjectPairingRule? _selected;
    [ObservableProperty] private int _editingId;
    [ObservableProperty] private Subject? _selectedSubjectA;
    [ObservableProperty] private Subject? _selectedSubjectB;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _statusIsError;

    public SubjectPairingViewModel(Func<IUnitOfWork> uow) => _uow = uow;

    public string FormTitle => EditingId == 0 ? "Add elective pair" : "Edit elective pair";
    partial void OnEditingIdChanged(int value) => OnPropertyChanged(nameof(FormTitle));

    private void Ok(string m) { StatusIsError = false; Status = m; }
    private void Fail(string m) { StatusIsError = true; Status = m; }

    public override async Task LoadAsync()
    {
        using var uow = _uow();
        var subjects = await uow.Subjects.ListAsync();
        var rules = await uow.PairingRules.ListAsync();

        _subjectById = subjects.ToDictionary(s => s.Id);

        Subjects.Clear();
        foreach (var s in subjects) Subjects.Add(s);

        Rules.Clear();
        foreach (var r in rules)
        {
            if (_subjectById.TryGetValue(r.SubjectAId, out var a)) r.SubjectA = a;
            if (_subjectById.TryGetValue(r.SubjectBId, out var b)) r.SubjectB = b;
            Rules.Add(r);
        }

        ResetForm();
    }

    partial void OnSelectedChanged(SubjectPairingRule? value)
    {
        if (value is null) return;
        EditingId = value.Id;
        SelectedSubjectA = _subjectById.TryGetValue(value.SubjectAId, out var a) ? a : Subjects.FirstOrDefault();
        SelectedSubjectB = _subjectById.TryGetValue(value.SubjectBId, out var b) ? b : Subjects.FirstOrDefault();
        Status = string.Empty;
    }

    private void ResetForm()
    {
        Selected = null;
        EditingId = 0;
        SelectedSubjectA = Subjects.FirstOrDefault();
        SelectedSubjectB = Subjects.Skip(1).FirstOrDefault() ?? Subjects.FirstOrDefault();
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
        if (SelectedSubjectA is null || SelectedSubjectB is null) { Fail("Pick two subjects."); return; }
        if (SelectedSubjectA.Id == SelectedSubjectB.Id) { Fail("Pick two different subjects."); return; }

        int aId = SelectedSubjectA.Id;
        int bId = SelectedSubjectB.Id;
        int keepId;

        using (var uow = _uow())
        {
            try
            {
                // Pairing is symmetric, so block (A,B) and (B,A) as duplicates.
                var dupe = await uow.PairingRules.FirstOrDefaultAsync(r =>
                    r.Id != EditingId &&
                    ((r.SubjectAId == aId && r.SubjectBId == bId) ||
                     (r.SubjectAId == bId && r.SubjectBId == aId)));
                if (dupe is not null) { Fail("Those two subjects are already set to run in parallel."); return; }

                if (EditingId == 0)
                {
                    var rule = new SubjectPairingRule { SubjectAId = aId, SubjectBId = bId };
                    await uow.PairingRules.AddAsync(rule);
                    await uow.SaveChangesAsync();
                    keepId = rule.Id;
                }
                else
                {
                    var e = await uow.PairingRules.GetByIdAsync(EditingId);
                    if (e is null) { Fail("This rule no longer exists."); return; }
                    e.SubjectAId = aId;
                    e.SubjectBId = bId;
                    uow.PairingRules.Update(e);
                    await uow.SaveChangesAsync();
                    keepId = e.Id;
                }
            }
            catch (Exception ex) { Fail("Could not save: " + (ex.InnerException?.Message ?? ex.Message)); return; }
        }

        await LoadAsync();
        Selected = Rules.FirstOrDefault(r => r.Id == keepId);
        Ok("Saved. It takes effect the next time you generate a timetable.");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (EditingId == 0) { Fail("Select a rule to delete."); return; }
        using (var uow = _uow())
        {
            var e = await uow.PairingRules.GetByIdAsync(EditingId);
            if (e is not null)
            {
                uow.PairingRules.Remove(e);
                try { await uow.SaveChangesAsync(); }
                catch (Exception ex) { Fail("Could not delete: " + (ex.InnerException?.Message ?? ex.Message)); return; }
            }
        }
        await LoadAsync();
        Ok("Deleted.");
    }
}
