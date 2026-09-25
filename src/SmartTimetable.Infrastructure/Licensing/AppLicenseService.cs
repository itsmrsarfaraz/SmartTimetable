using SmartTimetable.Application.Abstractions;
using SmartTimetable.Licensing;

namespace SmartTimetable.Infrastructure.Licensing;

/// <summary>
/// App-side licensing adapter for the desktop application.
/// Uses ECDSA P-256 hardware-locked offline licenses.
/// </summary>
public sealed class AppLicenseService : IAppLicenseService
{
    private readonly LicenseService _licenseService;
    private LicensePayload? _cachedPayload;
    private bool _statusChecked;
    private bool _isActivated;

    public AppLicenseService(LicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    public string GetMachineId() => _licenseService.GetRequestCode();

    public bool IsActivated()
    {
        EnsureStatus();
        return _isActivated;
    }

    public string? LicensedTo()
    {
        EnsureStatus();
        return _isActivated ? (_cachedPayload?.Customer ?? "Development Mode") : null;
    }

    public LicenseActivationResult Activate(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return LicenseActivationResult.Fail("Please paste the license key or file path you received.");

        var result = _licenseService.Import(licenseKey.Trim());
        if (!result.IsValid || result.Payload is null)
        {
            return LicenseActivationResult.Fail(result.Message);
        }

        _cachedPayload = result.Payload;
        _isActivated = true;
        _statusChecked = true;

        return LicenseActivationResult.Ok(result.Payload.Customer);
    }

    private void EnsureStatus()
    {
        if (_statusChecked) return;
        _statusChecked = true;

        // If vendor public key is not configured, allow running in dev mode
        if (!VendorPublicKey.IsConfigured)
        {
            _isActivated = true;
            return;
        }

        var result = _licenseService.CheckInstalledLicense();
        if (result.IsValid && result.Payload is not null)
        {
            _cachedPayload = result.Payload;
            _isActivated = true;
        }
        else
        {
            _isActivated = false;
            _cachedPayload = null;
        }
    }
}
