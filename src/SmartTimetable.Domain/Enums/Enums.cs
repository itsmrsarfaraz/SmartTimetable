namespace SmartTimetable.Domain.Enums;

/// <summary>Employment type of a teacher. Drives workload and availability rules.</summary>
public enum TeacherType
{
    Permanent = 0,
    Visiting = 1
}

/// <summary>Physical room category. Lab subjects require a <see cref="Laboratory"/>.</summary>
public enum RoomType
{
    Classroom = 0,
    Laboratory = 1,
    LectureHall = 2
}

/// <summary>High level academic division.</summary>
public enum DepartmentKind
{
    School = 0,
    College = 1
}

/// <summary>Well known Pakistani intermediate program presets.</summary>
public enum ProgramKind
{
    General = 0,
    PreMedical = 1,
    PreEngineering = 2,
    ICS = 3
}

/// <summary>Days of the working week. Sunday kept for completeness but off by default.</summary>
public enum Weekday
{
    Sunday = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6
}

/// <summary>Optimization profile the solver applies when generating a timetable.</summary>
public enum GenerationStrategy
{
    TeacherFriendly = 0,
    StudentFriendly = 1,
    RoomOptimization = 2,
    Balanced = 3,
    Custom = 4
}

/// <summary>Kinds of soft teacher preference the solver tries to honour.</summary>
public enum PreferenceKind
{
    AvoidLastPeriod = 0,
    PreferBeforeBreak = 1,
    AvoidDay = 2,
    PreferMorning = 3,
    OnlyFirstPeriods = 4
}

/// <summary>Class timing preference for a subject demand.</summary>
public enum TimePreferenceKind
{
    Any = 0,
    FirstSpot = 1,
    SecondSpot = 2,
    Morning = 3,
    Afternoon = 4,
    SpecificSlot = 5
}

/// <summary>Administrative roles. The MVP ships a single Super Admin.</summary>
public enum UserRole
{
    SuperAdmin = 0,
    Admin = 1,
    TimetableManager = 2
}
