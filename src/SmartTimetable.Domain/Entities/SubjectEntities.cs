using SmartTimetable.Domain.Common;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Domain.Entities;

/// <summary>A teachable subject such as Mathematics or Chemistry.</summary>
public class Subject : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    /// <summary>True when the subject must be scheduled in a laboratory.</summary>
    public bool IsLabSubject { get; set; }

    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    public ICollection<ClassSubject> ClassSubjects { get; set; } = new List<ClassSubject>();
}

/// <summary>
/// The requirement that a class studies a subject a fixed number of periods per week.
/// This is the primary demand the solver must satisfy.
/// </summary>
public class ClassSubject : Entity
{
    public int SchoolClassId { get; set; }
    public SchoolClass? SchoolClass { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public int PeriodsPerWeek { get; set; } = 1;

    /// <summary>Optional teacher pinned by the admin. When null the solver picks an eligible teacher.</summary>
    public int? PreferredTeacherId { get; set; }
    public Teacher? PreferredTeacher { get; set; }

    /// <summary>Timing preference for this subject in the class timetable.</summary>
    public TimePreferenceKind TimePreference { get; set; } = TimePreferenceKind.Any;

    /// <summary>Optional specific day pinned for this lecture demand.</summary>
    public Weekday? PinnedDay { get; set; }

    /// <summary>
    /// Weekdays this subject is allowed to be scheduled on, as a comma-separated list of
    /// <see cref="Weekday"/> ints (1=Mon .. 6=Sat), mirroring
    /// <c>AcademicSession.WorkingDaysCsv</c>. Empty means "no restriction" — the subject may
    /// use any of the session's working days. Use this to split a class's subjects across the
    /// week, e.g. Course A on Mon–Wed and Course B on Thu–Sat.
    /// </summary>
    public string AllowedDaysCsv { get; set; } = string.Empty;

    /// <summary>Optional specific period pinned for this lecture demand.</summary>
    public int? PinnedPeriodId { get; set; }
    public Period? PinnedPeriod { get; set; }

    /// <summary>
    /// How strongly the assigned teacher wants this class taught in one of its
    /// <see cref="PreferredPeriods"/>. 0 = no preference (periods ignored); higher = stronger
    /// soft pull. Because the demand belongs to a single (class, subject) pair, each of a
    /// teacher's classes can carry its own priority and its own set of preferred periods.
    /// </summary>
    public int PreferencePriority { get; set; }

    /// <summary>
    /// The specific periods the assigned teacher would like this lecture placed in.
    /// A soft preference: the solver is penalised (weighted by <see cref="PreferencePriority"/>)
    /// for every placement outside this set, but may still violate it to stay feasible.
    /// </summary>
    public ICollection<ClassSubjectPreferredPeriod> PreferredPeriods { get; set; } = new List<ClassSubjectPreferredPeriod>();
}

/// <summary>
/// A period that the teacher assigned to a <see cref="ClassSubject"/> demand prefers to
/// teach that lecture in. One demand may list several preferred periods.
/// </summary>
public class ClassSubjectPreferredPeriod : Entity
{
    public int ClassSubjectId { get; set; }
    public ClassSubject? ClassSubject { get; set; }

    public int PeriodId { get; set; }
    public Period? Period { get; set; }
}

/// <summary>Rule stating a subject may NOT be offered inside a given program.</summary>
public class ContradictorySubjectRule : Entity
{
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public int ProgramId { get; set; }
    public AcademicProgram? Program { get; set; }

    public string Reason { get; set; } = string.Empty;
}

/// <summary>Rule stating two subjects may be scheduled in the same slot (resource sharing hint).</summary>
public class SubjectPairingRule : Entity
{
    public int SubjectAId { get; set; }
    public Subject? SubjectA { get; set; }

    public int SubjectBId { get; set; }
    public Subject? SubjectB { get; set; }
}

/// <summary>
/// A combined class: several sections study one subject together in a shared room and slot.
/// </summary>
public class CombinedClassRule : Entity
{
    public string Name { get; set; } = string.Empty;

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public int? SharedRoomId { get; set; }
    public Room? SharedRoom { get; set; }

    /// <summary>Optional teacher pinned for the combined class lecture.</summary>
    public int? PreferredTeacherId { get; set; }
    public Teacher? PreferredTeacher { get; set; }

    /// <summary>Optional specific day pinned for this combined lecture.</summary>
    public Weekday? PinnedDay { get; set; }

    /// <summary>Optional specific period pinned for this combined lecture.</summary>
    public int? PinnedPeriodId { get; set; }
    public Period? PinnedPeriod { get; set; }

    public ICollection<CombinedClassMember> Members { get; set; } = new List<CombinedClassMember>();
}

/// <summary>Membership row linking a class into a <see cref="CombinedClassRule"/>.</summary>
public class CombinedClassMember : Entity
{
    public int CombinedClassRuleId { get; set; }
    public CombinedClassRule? Rule { get; set; }

    public int SchoolClassId { get; set; }
    public SchoolClass? SchoolClass { get; set; }
}
