using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Entities;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>CRUD screen for rooms (classrooms, laboratories, lecture halls).</summary>
public partial class RoomsViewModel : PageViewModel
{
    private readonly Func<IUnitOfWork> _uow;

    public override string Title => "Rooms";
    public override string Description => "Classrooms, laboratories and lecture halls available for scheduling.";

    public ObservableCollection<Room> Items { get; } = new();

    /// <summary>All room-type values for the type selector.</summary>
    public Array RoomTypes { get; } = Enum.GetValues(typeof(RoomType));

    [ObservableProperty] private Room? _selected;
    [ObservableProperty] private int _editingId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private RoomType _type = RoomType.Classroom;
    [ObservableProperty] private int _capacity = 40;
    [ObservableProperty] private string _building = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    public RoomsViewModel(Func<IUnitOfWork> uow) => _uow = uow;

    public string FormTitle => EditingId == 0 ? "Add room" : "Edit room";

    partial void OnEditingIdChanged(int value) => OnPropertyChanged(nameof(FormTitle));

    public override async Task LoadAsync()
    {
        Items.Clear();
        using var uow = _uow();
        foreach (var r in await uow.Rooms.ListAsync())
            Items.Add(r);
        ResetForm();
    }

    partial void OnSelectedChanged(Room? value)
    {
        if (value is null) return;
        EditingId = value.Id;
        Name = value.Name;
        Type = value.Type;
        Capacity = value.Capacity;
        Building = value.Building;
        Status = string.Empty;
    }

    private void ResetForm()
    {
        Selected = null;
        EditingId = 0;
        Name = string.Empty;
        Type = RoomType.Classroom;
        Capacity = 40;
        Building = string.Empty;
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
            Status = "Room name is required.";
            return;
        }
        if (Capacity <= 0)
        {
            Status = "Capacity must be greater than zero.";
            return;
        }

        using var uow = _uow();
        if (EditingId == 0)
        {
            await uow.Rooms.AddAsync(new Room
            {
                Name = Name.Trim(),
                Type = Type,
                Capacity = Capacity,
                Building = Building.Trim()
            });
        }
        else
        {
            var e = await uow.Rooms.GetByIdAsync(EditingId);
            if (e is null) { Status = "This room no longer exists."; return; }
            e.Name = Name.Trim();
            e.Type = Type;
            e.Capacity = Capacity;
            e.Building = Building.Trim();
            uow.Rooms.Update(e);
        }

        await uow.SaveChangesAsync();
        await LoadAsync();
        Status = "Saved.";
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (EditingId == 0) { Status = "Select a room to delete."; return; }

        using var uow = _uow();
        var e = await uow.Rooms.GetByIdAsync(EditingId);
        if (e is not null)
        {
            uow.Rooms.Remove(e);
            try
            {
                await uow.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Status = "Cannot delete: the room is still referenced. " + ex.Message;
                return;
            }
        }

        await LoadAsync();
        Status = "Deleted.";
    }
}
