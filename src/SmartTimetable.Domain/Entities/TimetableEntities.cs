using SmartTimetable.Domain.Common;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Domain.Entities;

/// <summary>
/// One generated timetable option. A generation run typically produces several of these,
/// each with a different strategy and quality score.
/// </summary>
public class GeneratedTimetable : Entity
{
    public int AcademicSessionId { get; set; }
    public AcademicSession? AcademicSession { get; set; }

    public string Name { get; set; } = string.Empty;
    public GenerationStrategy Strategy { get; set; }

    /// <summary>Quality score. 10000 is a perfect (penalty-free) timetable.</summary>
    public int Score { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public bool IsSelected { get; set; }

    public ICollection<TimetableEntry> Entries { get; set; } = new List<TimetableEntry>();
}

/// <summary>A single placed lesson: class + subject + teacher + room at a day/period.</summary>
public class TimetableEntry : Entity
{
    public int GeneratedTimetableId { get; set; }
    public GeneratedTimetable? GeneratedTimetable { get; set; }

    public int SchoolClassId { get; set; }
    public SchoolClass? SchoolClass { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public int? RoomId { get; set; }
    public Room? Room { get; set; }

    public Weekday Day { get; set; }

    public int PeriodId { get; set; }
    public Period? Period { get; set; }
}
