using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Entities;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>
/// CRUD screen for teaching staff. Captures identity, employment type and the
/// weekly workload floor/ceiling the solver honours. Subject qualifications are
/// assigned on the Teacher–Subject screen; per-class period preferences on the
/// Teacher Periods screen.
/// </summary>
public partial class TeachersViewModel : PageViewModel
{
    private readonly Func<IUnitOfWork> _uow;

    public override string Title => "Teachers";
    public override string Description => "Teaching staff, employment type and weekly workload limits.";

    public ObservableCollection<Teacher> Items { get; } = new();

    public Array TeacherTypes { get; } = Enum.GetValues(typeof(TeacherType));

    [ObservableProperty] private Teacher? _selected;
    [ObservableProperty] private int _editingId;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _cnic = string.Empty;
    [ObservableProperty] private string _qualification = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private TeacherType _type = TeacherType.Permanent;
    [ObservableProperty] private int _minWeeklyPeriods;
    [ObservableProperty] private int _maxWeeklyPeriods = 30;
    [ObservableProperty] private string _status = string.Empty;

    public TeachersViewModel(Func<IUnitOfWork> uow) => _uow = uow;

    public string FormTitle => EditingId == 0 ? "Add teacher" : "Edit teacher";

    partial void OnEditingIdChanged(int value) => OnPropertyChanged(nameof(FormTitle));

    public override async Task LoadAsync()
    {
        Items.Clear();
        using var uow = _uow();
        foreach (var t in await uow.Teachers.ListAsync())
            Items.Add(t);
        ResetForm();
    }

    partial void OnSelectedChanged(Teacher? value)
    {
        if (value is null) return;
        EditingId = value.Id;
        FullName = value.FullName;
        Cnic = value.Cnic;
        Qualification = value.Qualification;
        Phone = value.Phone;
        Email = value.Email;
        Type = value.Type;
        MinWeeklyPeriods = value.MinWeeklyPeriods;
        MaxWeeklyPeriods = value.MaxWeeklyPeriods;
        Status = string.Empty;
    }

    private void ResetForm()
    {
        Selected = null;
        EditingId = 0;
        FullName = string.Empty;
        Cnic = string.Empty;
        Qualification = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        Type = TeacherType.Permanent;
        MinWeeklyPeriods = 0;
        MaxWeeklyPeriods = 30;
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
        if (string.IsNullOrWhiteSpace(FullName))
        {
            Status = "Teacher name is required.";
            return;
        }
        if (MaxWeeklyPeriods <= 0)
        {
            Status = "Maximum weekly periods must be greater than zero.";
            return;
        }
        if (MinWeeklyPeriods < 0 || MinWeeklyPeriods > MaxWeeklyPeriods)
        {
            Status = "Minimum weekly periods must be between 0 and the maximum.";
            return;
        }

        using var uow = _uow();
        try
        {
            if (EditingId == 0)
            {
                await uow.Teachers.AddAsync(new Teacher
                {
                    FullName = FullName.Trim(),
                    Cnic = Cnic.Trim(),
                    Qualification = Qualification.Trim(),
                    Phone = Phone.Trim(),
                    Email = Email.Trim(),
                    Type = Type,
                    MinWeeklyPeriods = MinWeeklyPeriods,
                    MaxWeeklyPeriods = MaxWeeklyPeriods
                });
            }
            else
            {
                var e = await uow.Teachers.GetByIdAsync(EditingId);
                if (e is null) { Status = "This teacher no longer exists."; return; }
                e.FullName = FullName.Trim();
                e.Cnic = Cnic.Trim();
                e.Qualification = Qualification.Trim();
                e.Phone = Phone.Trim();
                e.Email = Email.Trim();
                e.Type = Type;
                e.MinWeeklyPeriods = MinWeeklyPeriods;
                e.MaxWeeklyPeriods = MaxWeeklyPeriods;
                uow.Teachers.Update(e);
            }

            await uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Surface the real database error (e.g. a stale schema from EnsureCreated)
            // instead of crashing, so it can be acted on.
            Status = "Could not save: " + (ex.InnerException?.Message ?? ex.Message);
            return;
        }

        await LoadAsync();
        Status = "Saved.";
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (EditingId == 0) { Status = "Select a teacher to delete."; return; }

        using var uow = _uow();
        var e = await uow.Teachers.GetByIdAsync(EditingId);
        if (e is not null)
        {
            uow.Teachers.Remove(e);
            try
            {
                await uow.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Status = "Cannot delete: the teacher is still referenced. " + ex.Message;
                return;
            }
        }

        await LoadAsync();
        Status = "Deleted.";
    }
}
