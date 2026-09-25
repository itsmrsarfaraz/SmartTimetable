using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>
/// CRUD screen for the daily period template. Times are entered as 24-hour
/// "HH:mm" text so the grid stays keyboard-friendly and culture-independent.
/// </summary>
public partial class PeriodsViewModel : PageViewModel
{
    private readonly Func<IUnitOfWork> _uow;

    public override string Title => "Periods";
    public override string Description => "The daily timetable grid: numbered teaching periods and breaks.";

    public ObservableCollection<Period> Items { get; } = new();

    [ObservableProperty] private Period? _selected;
    [ObservableProperty] private int _editingId;
    [ObservableProperty] private int _order = 1;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _startText = "08:00";
    [ObservableProperty] private string _endText = "08:45";
    [ObservableProperty] private bool _isBreak;
    [ObservableProperty] private string _status = string.Empty;

    public PeriodsViewModel(Func<IUnitOfWork> uow) => _uow = uow;

    public string FormTitle => EditingId == 0 ? "Add period" : "Edit period";

    partial void OnEditingIdChanged(int value) => OnPropertyChanged(nameof(FormTitle));

    public override async Task LoadAsync()
    {
        Items.Clear();
        using var uow = _uow();
        var list = await uow.Periods.ListAsync();
        foreach (var p in list.OrderBy(p => p.Order))
            Items.Add(p);
        ResetForm();
    }

    partial void OnSelectedChanged(Period? value)
    {
        if (value is null) return;
        EditingId = value.Id;
        Order = value.Order;
        Name = value.Name;
        StartText = value.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        EndText = value.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        IsBreak = value.IsBreak;
        Status = string.Empty;
    }

    private void ResetForm()
    {
        Selected = null;
        EditingId = 0;
        Order = Items.Count + 1;
        Name = string.Empty;
        StartText = "08:00";
        EndText = "08:45";
        IsBreak = false;
    }

    private static bool TryParseTime(string text, out TimeOnly value)
    {
        text = (text ?? string.Empty).Trim();
        return TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
            || TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
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
        if (string.IsNullOrWhiteSpace(Name))
        {
            Status = "Period name is required (e.g. \"Period 1\" or \"Break\").";
            return;
        }
        if (!TryParseTime(StartText, out var start))
        {
            Status = "Start time must be in 24-hour HH:mm format, e.g. 08:00.";
            return;
        }
        if (!TryParseTime(EndText, out var end))
        {
            Status = "End time must be in 24-hour HH:mm format, e.g. 08:45.";
            return;
        }
        if (end <= start)
        {
            Status = "End time must be after start time.";
            return;
        }

        using var uow = _uow();
        if (EditingId == 0)
        {
            await uow.Periods.AddAsync(new Period
            {
                Order = Order,
                Name = Name.Trim(),
                StartTime = start,
                EndTime = end,
                IsBreak = IsBreak
            });
        }
        else
        {
            var e = await uow.Periods.GetByIdAsync(EditingId);
            if (e is null) { Status = "This period no longer exists."; return; }
            e.Order = Order;
            e.Name = Name.Trim();
            e.StartTime = start;
            e.EndTime = end;
            e.IsBreak = IsBreak;
            uow.Periods.Update(e);
        }

        await uow.SaveChangesAsync();
        await LoadAsync();
        Status = "Saved.";
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (EditingId == 0) { Status = "Select a period to delete."; return; }

        using var uow = _uow();
        var e = await uow.Periods.GetByIdAsync(EditingId);
        if (e is not null)
        {
            uow.Periods.Remove(e);
            try
            {
                await uow.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Status = "Cannot delete: the period is still referenced by a timetable. " + ex.Message;
                return;
            }
        }

        await LoadAsync();
        Status = "Deleted.";
    }
}
