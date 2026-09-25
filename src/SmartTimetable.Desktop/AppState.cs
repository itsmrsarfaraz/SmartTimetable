using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Desktop;

/// <summary>Process-wide UI state shared across view models.</summary>
public sealed class AppState
{
    public AdminUser? CurrentUser { get; set; }
    public int ActiveSessionId { get; set; }
    public string? LicensedTo { get; set; }
}
