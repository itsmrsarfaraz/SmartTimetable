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
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>
/// Per-teacher availability windows. A teacher with NO windows is treated as available
/// in every teaching slot. Adding windows restricts the teacher: the solver may only use
/// periods that fall entirely inside a window on a listed day — days with no window become
/// unavailable. Used as a hard constraint by the CP-SAT solver.
/// </summary>
public partial class TeacherAvailabilityViewModel : PageViewModel
{
    private readonly Func<IUnitOfWork> _uow;

    public override string Title => "Teacher Availability";
    public override string Description =>
        "Optional working-time windows per teacher. Leave a teacher with no windows to keep them fully available.";

    public Array Weekdays { get; } = Enum.GetValues(typeof(Weekday));

    public ObservableCollection<Teacher> Teachers { get; } = new();
    public ObservableCollection<TeacherAvailability> Windows { get; } = new();

    [ObservableProperty] private Teacher? _selectedTeacher;
    [ObservableProperty] private TeacherAvailability? _selectedWindow;

    [ObservableProperty] private int _editingWindowId;
    [ObservableProperty] private Weekday _day = Weekday.Monday;
    [ObservableProperty] private TimeSpan? _fromTime = new(8, 0, 0);
    [ObservableProperty] private TimeSpan? _toTime = new(14, 0, 0);
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _statusIsError;

    public TeacherAvailabilityViewModel(Func<IUnitOfWork> uow) => _uow = uow;

    public string FormTitle => EditingWindowId == 0 ? "Add window" : "Edit window";
    partial void OnEditingWindowIdChanged(int value) => OnPropertyChanged(nameof(FormTitle));

    /// <summary>The window editor only makes sense once a teacher is selected.</summary>
    public bool CanEditWindows => SelectedTeacher is not null;

    private void Ok(string m) { StatusIsError = false; Status = m; }
    private void Fail(string m) { StatusIsError = true; Status = m; }

    public override async Task LoadAsync()
    {
        using var uow = _uow();
        var teachers = await uow.Teachers.Query().Include(t => t.Availabilities).ToListAsync();

        Teachers.Clear();
        foreach (var t in teachers.OrderBy(t => t.FullName))
            Teachers.Add(t);

        SelectedTeacher = Teachers.FirstOrDefault();
        if (SelectedTeacher is null)
            Windows.Clear();
    }

    partial void OnSelectedTeacherChanged(Teacher? value)
    {
        OnPropertyChanged(nameof(CanEditWindows));
        NewWindow();
        _ = LoadWindowsAsync(value?.Id ?? 0);
    }

    private async Task LoadWindowsAsync(int teacherId)
    {
        Windows.Clear();
        if (teacherId == 0) return;
        using var uow = _uow();
        var rows = await uow.TeacherAvailabilities.ListAsync(a => a.TeacherId == teacherId);
        foreach (var w in rows.OrderBy(w => (int)w.Day).ThenBy(w => w.AvailableFrom))
            Windows.Add(w);
    }

    partial void OnSelectedWindowChanged(TeacherAvailability? value)
    {
        if (value is null) return;
        EditingWindowId = value.Id;
        Day = value.Day;
        FromTime = value.AvailableFrom.ToTimeSpan();
        ToTime = value.AvailableTo.ToTimeSpan();
        Status = string.Empty;
    }

    [RelayCommand]
    private void NewWindow()
    {
        SelectedWindow = null;
        EditingWindowId = 0;
        Day = Weekday.Monday;
        FromTime = new TimeSpan(8, 0, 0);
        ToTime = new TimeSpan(14, 0, 0);
        Status = string.Empty;
    }

    [RelayCommand]
    private async Task SaveWindowAsync()
    {
        if (SelectedTeacher is null) { Fail("Select a teacher first."); return; }
        if (FromTime is null || ToTime is null) { Fail("Enter both a start and an end time."); return; }
        if (ToTime <= FromTime) { Fail("The end time must be after the start time."); return; }

        var from = TimeOnly.FromTimeSpan(FromTime.Value);
        var to = TimeOnly.FromTimeSpan(ToTime.Value);
        int teacherId = SelectedTeacher.Id;

        using (var uow = _uow())
        {
            try
            {
                if (EditingWindowId == 0)
                {
                    await uow.TeacherAvailabilities.AddAsync(new TeacherAvailability
                    {
                        TeacherId = teacherId, Day = Day, AvailableFrom = from, AvailableTo = to
                    });
                }
                else
                {
                    var e = await uow.TeacherAvailabilities.GetByIdAsync(EditingWindowId);
                    if (e is null) { Fail("This window no longer exists."); return; }
                    e.Day = Day;
                    e.AvailableFrom = from;
                    e.AvailableTo = to;
                    uow.TeacherAvailabilities.Update(e);
                }
                await uow.SaveChangesAsync();
            }
            catch (Exception ex) { Fail("Could not save: " + (ex.InnerException?.Message ?? ex.Message)); return; }
        }

        await ReloadKeepingTeacherAsync(teacherId);
        Ok($"Saved {Day} {from:HH:mm}–{to:HH:mm}.");
    }

    [RelayCommand]
    private async Task DeleteWindowAsync()
    {
        if (EditingWindowId == 0) { Fail("Select a window to delete."); return; }
        int teacherId = SelectedTeacher?.Id ?? 0;

        using (var uow = _uow())
        {
            var e = await uow.TeacherAvailabilities.GetByIdAsync(EditingWindowId);
            if (e is not null)
            {
                uow.TeacherAvailabilities.Remove(e);
                try { await uow.SaveChangesAsync(); }
                catch (Exception ex) { Fail("Could not delete: " + (ex.InnerException?.Message ?? ex.Message)); return; }
            }
        }

        await ReloadKeepingTeacherAsync(teacherId);
        Ok("Window removed.");
    }

    [RelayCommand]
    private async Task ClearAllForTeacherAsync()
    {
        if (SelectedTeacher is null) { Fail("Select a teacher first."); return; }
        int teacherId = SelectedTeacher.Id;

        using (var uow = _uow())
        {
            var rows = await uow.TeacherAvailabilities.ListAsync(a => a.TeacherId == teacherId);
            if (rows.Count == 0) { Ok("This teacher is already fully available."); return; }
            uow.TeacherAvailabilities.RemoveRange(rows);
            try { await uow.SaveChangesAsync(); }
            catch (Exception ex) { Fail("Could not clear: " + (ex.InnerException?.Message ?? ex.Message)); return; }
        }

        await ReloadKeepingTeacherAsync(teacherId);
        Ok("All windows cleared — teacher is now fully available.");
    }

    /// <summary>Reload the teacher list (to refresh window counts) and reselect the same teacher.</summary>
    private async Task ReloadKeepingTeacherAsync(int teacherId)
    {
        await LoadAsync();
        var again = Teachers.FirstOrDefault(t => t.Id == teacherId);
        if (again is not null) SelectedTeacher = again;
    }
}
