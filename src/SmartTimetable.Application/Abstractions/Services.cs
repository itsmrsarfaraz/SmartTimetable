using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Application.Abstractions;

/// <summary>Salted password hashing (PBKDF2).</summary>
public interface IPasswordHasher
{
    (string Hash, string Salt) Hash(string password);
    bool Verify(string password, string hash, string salt);
}

/// <summary>Single-admin authentication.</summary>
public interface IAuthService
{
    Task<bool> AnyUserExistsAsync(CancellationToken ct = default);
    Task EnsureSeedAdminAsync(string username, string password, string displayName, CancellationToken ct = default);
    Task<AdminUser?> LoginAsync(string username, string password, CancellationToken ct = default);
}

/// <summary>Result of importing/activating a license key inside the app.</summary>
public sealed class LicenseActivationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string LicensedTo { get; init; } = string.Empty;

    public static LicenseActivationResult Fail(string message) => new() { Success = false, Message = message };
    public static LicenseActivationResult Ok(string licensedTo) =>
        new() { Success = true, Message = "License activated.", LicensedTo = licensedTo };
}

/// <summary>App-side licensing: fingerprint, current status, and activation.</summary>
public interface IAppLicenseService
{
    /// <summary>The activation code the customer sends to the vendor.</summary>
    string GetMachineId();

    /// <summary>True when a valid license for THIS machine is already stored.</summary>
    bool IsActivated();

    /// <summary>Institution name from the stored license, or null when not activated.</summary>
    string? LicensedTo();

    /// <summary>Verifies a license key and, if valid, stores it permanently.</summary>
    LicenseActivationResult Activate(string licenseKey);
}
