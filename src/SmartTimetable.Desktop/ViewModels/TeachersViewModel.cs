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
    public override string Description => "Teaching staff, employment type and daily/weekly workload limits.";

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
    [ObservableProperty] private int _maxPeriodsPerDay = 6;
    [ObservableProperty] private int _minWeeklyPeriods;
    [ObservableProperty] private int _maxWeeklyPeriods = 42;
    [ObservableProperty] private string _status = string.Empty;

    // While loading a teacher into the form we assign Type directly; that must NOT
    // overwrite the loaded caps with the type's suggested defaults.
    private bool _suppressTypeDefaults;

    public TeachersViewModel(Func<IUnitOfWork> uow) => _uow = uow;

    public string FormTitle => EditingId == 0 ? "Add teacher" : "Edit teacher";

    partial void OnEditingIdChanged(int value) => OnPropertyChanged(nameof(FormTitle));

    /// <summary>
    /// When the admin picks an employment type, pre-fill the caps the way a college
    /// runs them: a permanent teacher ~6 periods/day (≈42/week), a visiting teacher
    /// 1/day (≈6/week). These are only starting points — the admin can still edit them.
    /// </summary>
    partial void OnTypeChanged(TeacherType value)
    {
        if (_suppressTypeDefaults) return;
        if (value == TeacherType.Visiting)
        {
            MaxPeriodsPerDay = 1;
            MaxWeeklyPeriods = 6;
        }
        else
        {
            MaxPeriodsPerDay = 6;
            MaxWeeklyPeriods = 42;
        }
    }

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
        _suppressTypeDefaults = true;
        EditingId = value.Id;
        FullName = value.FullName;
        Cnic = value.Cnic;
        Qualification = value.Qualification;
        Phone = value.Phone;
        Email = value.Email;
        Type = value.Type;
        MaxPeriodsPerDay = value.MaxPeriodsPerDay;
        MinWeeklyPeriods = value.MinWeeklyPeriods;
        MaxWeeklyPeriods = value.MaxWeeklyPeriods;
        Status = string.Empty;
        _suppressTypeDefaults = false;
    }

    private void ResetForm()
    {
        _suppressTypeDefaults = true;
        Selected = null;
        EditingId = 0;
        FullName = string.Empty;
        Cnic = string.Empty;
        Qualification = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        Type = TeacherType.Permanent;
        MaxPeriodsPerDay = 6;
        MinWeeklyPeriods = 0;
        MaxWeeklyPeriods = 42;
        _suppressTypeDefaults = false;
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
        if (MaxPeriodsPerDay <= 0)
        {
            Status = "Maximum periods per day must be greater than zero.";
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
                    MaxPeriodsPerDay = MaxPeriodsPerDay,
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
                e.MaxPeriodsPerDay = MaxPeriodsPerDay;
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
