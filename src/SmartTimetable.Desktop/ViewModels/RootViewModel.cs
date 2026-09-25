using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>
/// Top-level view model that swaps the visible screen between the licensing gate,
/// the login screen, and the main application shell.
/// </summary>
public partial class RootViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly IAppLicenseService _license;

    [ObservableProperty]
    private ObservableObject? _current;

    public RootViewModel(IServiceProvider services, IAppLicenseService license)
    {
        _services = services;
        _license = license;
    }

    public void Start()
    {
        if (_license.IsActivated())
            ShowLogin();
        else
            ShowLicense();
    }

    private void ShowLicense()
    {
        var vm = _services.GetRequiredService<LicenseViewModel>();
        vm.Activated += ShowLogin;
        Current = vm;
    }

    private void ShowLogin()
    {
        var vm = _services.GetRequiredService<LoginViewModel>();
        vm.LoggedIn += OnLoggedIn;
        Current = vm;
    }

    private void OnLoggedIn(AdminUser user)
    {
        var shell = _services.GetRequiredService<ShellViewModel>();
        shell.Initialize(user);
        shell.LogoutRequested += ShowLogin;
        Current = shell;
    }
}
