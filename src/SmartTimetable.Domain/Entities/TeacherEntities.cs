using SmartTimetable.Domain.Common;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Domain.Entities;

/// <summary>A member of teaching staff.</summary>
public class Teacher : Entity
{
    public string FullName { get; set; } = string.Empty;
    public string Cnic { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public TeacherType Type { get; set; } = TeacherType.Permanent;

    /// <summary>Minimum weekly teaching periods (workload floor).</summary>
    public int MinWeeklyPeriods { get; set; }

    /// <summary>Maximum weekly teaching periods (workload ceiling).</summary>
    public int MaxWeeklyPeriods { get; set; } = 30;

    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    public ICollection<TeacherAvailability> Availabilities { get; set; } = new List<TeacherAvailability>();
    public ICollection<TeacherPreference> Preferences { get; set; } = new List<TeacherPreference>();
}

/// <summary>Many-to-many mapping: which subjects a teacher is qualified to teach.</summary>
public class TeacherSubject : Entity
{
    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
}

/// <summary>
/// A window during which a teacher is available on a given day.
/// If a teacher has NO availability rows they are treated as available on every working slot.
/// </summary>
public class TeacherAvailability : Entity
{
    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public Weekday Day { get; set; }
    public TimeOnly AvailableFrom { get; set; } = new(8, 0);
    public TimeOnly AvailableTo { get; set; } = new(14, 0);
}

/// <summary>A soft scheduling preference for a teacher.</summary>
public class TeacherPreference : Entity
{
    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public PreferenceKind Kind { get; set; }

    /// <summary>Target day for <see cref="PreferenceKind.AvoidDay"/>; ignored otherwise.</summary>
    public Weekday? Day { get; set; }

    /// <summary>Penalty applied by the solver when this preference is violated.</summary>
    public int Weight { get; set; } = 10;
}
