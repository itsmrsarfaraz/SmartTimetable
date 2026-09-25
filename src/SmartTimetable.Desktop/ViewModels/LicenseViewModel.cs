using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartTimetable.Application.Abstractions;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>
/// First-run licensing gate. Shows the machine's activation code and accepts a
/// signed license key. On success the app is unlocked permanently on this machine.
/// </summary>
public partial class LicenseViewModel : ObservableObject
{
    private readonly IAppLicenseService _license;

    public event Action? Activated;

    [ObservableProperty] private string _machineId = string.Empty;
    [ObservableProperty] private string _licenseKey = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isError;

    public LicenseViewModel(IAppLicenseService license)
    {
        _license = license;
        MachineId = _license.GetMachineId();
    }

    [RelayCommand]
    private void Activate()
    {
        var result = _license.Activate(LicenseKey);
        IsError = !result.Success;
        StatusMessage = result.Success
            ? $"Activated for {result.LicensedTo}. Loading…"
            : result.Message;

        if (result.Success)
            Activated?.Invoke();
    }
}
