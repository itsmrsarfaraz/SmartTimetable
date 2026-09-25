using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>
/// Login screen. On first run (no admin exists yet) it switches to a
/// "create administrator" flow, then signs the new admin straight in.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    public event Action<AdminUser>? LoggedIn;

    [ObservableProperty] private bool _isFirstRun;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public LoginViewModel(IAuthService auth)
    {
        _auth = auth;
        _ = LoadAsync();
    }

    public string Title => IsFirstRun ? "Create administrator" : "Welcome back";
    public string Subtitle => IsFirstRun
        ? "Set up the single administrator account for this installation."
        : "Sign in to manage timetables.";
    public string ActionText => IsFirstRun ? "Create & sign in" : "Sign in";

    partial void OnIsFirstRunChanged(bool value)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ActionText));
    }

    private async Task LoadAsync()
    {
        try
        {
            IsFirstRun = !await _auth.AnyUserExistsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Startup error: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        StatusMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "Enter a username and password.";
            return;
        }

        IsBusy = true;
        try
        {
            if (IsFirstRun)
            {
                string display = string.IsNullOrWhiteSpace(DisplayName) ? Username : DisplayName;
                await _auth.EnsureSeedAdminAsync(Username, Password, display);
            }

            var user = await _auth.LoginAsync(Username, Password);
            if (user is null)
            {
                StatusMessage = "Invalid username or password.";
                return;
            }

            LoggedIn?.Invoke(user);
        }
        catch (Exception ex)
        {
            StatusMessage = "Error: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
