using SmartTimetable.Domain.Common;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Domain.Entities;

/// <summary>An administrative user. The MVP provisions a single Super Admin.</summary>
public class AdminUser : Entity
{
    public string Username { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash (Base64).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Per-user random salt (Base64).</summary>
    public string PasswordSalt { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.SuperAdmin;

    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// The activated license stored locally after a valid key is imported.
/// A single row means the installation is unlocked for life on this machine.
/// </summary>
public class LicenseRecord : Entity
{
    public string LicenseKey { get; set; } = string.Empty;
    public string LicensedTo { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public DateTime ActivatedUtc { get; set; } = DateTime.UtcNow;
}
