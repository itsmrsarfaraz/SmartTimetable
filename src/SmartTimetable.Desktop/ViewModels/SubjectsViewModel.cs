using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>CRUD screen for teachable subjects.</summary>
public partial class SubjectsViewModel : PageViewModel
{
    private readonly Func<IUnitOfWork> _uow;

    public override string Title => "Subjects";
    public override string Description => "Define the subjects taught across your institution.";

    public ObservableCollection<Subject> Items { get; } = new();

    [ObservableProperty] private Subject? _selected;
    [ObservableProperty] private int _editingId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private bool _isLabSubject;
    [ObservableProperty] private string _status = string.Empty;

    public SubjectsViewModel(Func<IUnitOfWork> uow) => _uow = uow;

    public string FormTitle => EditingId == 0 ? "Add subject" : "Edit subject";

    partial void OnEditingIdChanged(int value) => OnPropertyChanged(nameof(FormTitle));

    public override async Task LoadAsync()
    {
        Items.Clear();
        using var uow = _uow();
        foreach (var s in await uow.Subjects.ListAsync())
            Items.Add(s);
        ResetForm();
    }

    partial void OnSelectedChanged(Subject? value)
    {
        if (value is null) return;
        EditingId = value.Id;
        Name = value.Name;
        Code = value.Code;
        IsLabSubject = value.IsLabSubject;
        Status = string.Empty;
    }

    private void ResetForm()
    {
        Selected = null;
        EditingId = 0;
        Name = string.Empty;
        Code = string.Empty;
        IsLabSubject = false;
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
            Status = "Subject name is required.";
            return;
        }

        using var uow = _uow();
        if (EditingId == 0)
        {
            await uow.Subjects.AddAsync(new Subject
            {
                Name = Name.Trim(),
                Code = Code.Trim(),
                IsLabSubject = IsLabSubject
            });
        }
        else
        {
            var e = await uow.Subjects.GetByIdAsync(EditingId);
            if (e is null) { Status = "This subject no longer exists."; return; }
            e.Name = Name.Trim();
            e.Code = Code.Trim();
            e.IsLabSubject = IsLabSubject;
            uow.Subjects.Update(e);
        }

        await uow.SaveChangesAsync();
        await LoadAsync();
        Status = "Saved.";
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (EditingId == 0) { Status = "Select a subject to delete."; return; }

        using var uow = _uow();
        var e = await uow.Subjects.GetByIdAsync(EditingId);
        if (e is not null)
        {
            uow.Subjects.Remove(e);
            try
            {
                await uow.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Status = "Cannot delete: the subject is still in use. " + ex.Message;
                return;
            }
        }

        await LoadAsync();
        Status = "Deleted.";
    }
}
