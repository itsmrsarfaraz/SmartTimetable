using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Application.Solving;

// ----- Input DTOs (flattened, EF-free, so the solver stays pure) -----

public sealed class SolverClass
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? HomeRoomId { get; set; }
    public int? BreakPeriodId { get; set; }
    public int? StartPeriodOrder { get; set; }
    public int? EndPeriodOrder { get; set; }
}

public sealed class SolverSubject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsLab { get; set; }
}

public sealed class SolverTeacher
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsVisiting { get; set; }
    public int MinPeriods { get; set; }
    public int MaxPeriods { get; set; } = 30;

    /// <summary>When true, only slots listed in <see cref="SolverInput.AllowedSlots"/> may be used.</summary>
    public bool HasAvailabilityRestriction { get; set; }
}

public sealed class SolverRoom
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsLab { get; set; }
}

public sealed class SolverPeriod
{
    public int Id { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsLast { get; set; }
    public bool IsBeforeBreak { get; set; }
    public bool IsBreak { get; set; }
}

/// <summary>A class needs a subject a fixed number of periods per week.</summary>
public sealed class SolverRequirement
{
    public int ClassId { get; set; }
    public int SubjectId { get; set; }
    public int PeriodsPerWeek { get; set; }
    public int? PinnedTeacherId { get; set; }
    public TimePreferenceKind TimePreference { get; set; } = TimePreferenceKind.Any;
    public int? PinnedDay { get; set; }
    public int? PinnedPeriodId { get; set; }

    /// <summary>Periods the assigned teacher prefers for this lecture (soft). Empty = no preference.</summary>
    public List<int> PreferredPeriodIds { get; set; } = new();

    /// <summary>Strength of the preferred-period pull for this demand. 0 = ignore.</summary>
    public int PreferencePriority { get; set; }
}

/// <summary>Teacher is qualified to teach subject.</summary>
public sealed class SolverEligibility
{
    public int TeacherId { get; set; }
    public int SubjectId { get; set; }
}

/// <summary>A single (teacher, day, period) slot the teacher IS allowed to work.</summary>
public sealed class SolverAllowedSlot
{
    public int TeacherId { get; set; }
    public int Day { get; set; }
    public int PeriodId { get; set; }
}

public sealed class SolverPreference
{
    public int TeacherId { get; set; }
    public PreferenceKind Kind { get; set; }
    public int? Day { get; set; }
    public int Weight { get; set; } = 10;
}

/// <summary>Combined class: all listed classes take the subject together in the same slot.</summary>
public sealed class SolverCombined
{
    public int SubjectId { get; set; }
    public List<int> ClassIds { get; set; } = new();
    public int? RoomId { get; set; }
    public int? TeacherId { get; set; }
    public int? PinnedDay { get; set; }
    public int? PinnedPeriodId { get; set; }
}

/// <summary>
/// Two subjects that may run at the same time. The solver softly rewards placing a
/// lesson of each in the same slot, aligning parallel electives across programs and
/// letting them share teachers/rooms. Never a hard constraint, so it can't cause
/// infeasibility.
/// </summary>
public sealed class SolverPairing
{
    public int SubjectAId { get; set; }
    public int SubjectBId { get; set; }
}

public sealed class SolverInput
{
    public List<int> Days { get; set; } = new();
    public List<SolverClass> Classes { get; set; } = new();
    public List<SolverSubject> Subjects { get; set; } = new();
    public List<SolverTeacher> Teachers { get; set; } = new();
    public List<SolverRoom> Rooms { get; set; } = new();
    public List<SolverPeriod> Periods { get; set; } = new();
    public List<SolverRequirement> Requirements { get; set; } = new();
    public List<SolverEligibility> Eligibilities { get; set; } = new();
    public List<SolverAllowedSlot> AllowedSlots { get; set; } = new();
    public List<SolverPreference> Preferences { get; set; } = new();
    public List<SolverCombined> Combined { get; set; } = new();
    public List<SolverPairing> Pairings { get; set; } = new();
}

// ----- Options -----

/// <summary>
/// User-tunable penalty weights for the Custom generation strategy. Each value is a
/// small multiplier (0 disables that objective). Mirrors the built-in strategy
/// profiles so the admin can dial in their own balance.
/// </summary>
public sealed class SolverCustomWeights
{
    public int TeacherPref { get; set; } = 2;
    public int LastPeriod { get; set; } = 2;
    public int Afternoon { get; set; } = 1;
    public int UnderMin { get; set; } = 1;
    public int LabOffPeak { get; set; } = 1;
    public int PeriodPref { get; set; } = 2;
    public int Pairing { get; set; } = 2;
}

public sealed class SolverOptions
{
    /// <summary>One solution is produced per strategy listed.</summary>
    public List<GenerationStrategy> Strategies { get; set; } = new() { GenerationStrategy.Balanced };
    public double MaxSecondsPerStrategy { get; set; } = 15;
    public int RandomSeed { get; set; } = 1;

    /// <summary>
    /// Weights applied when <see cref="GenerationStrategy.Custom"/> is generated.
    /// Null falls back to the balanced defaults.
    /// </summary>
    public SolverCustomWeights? CustomWeights { get; set; }
}

// ----- Output DTOs -----

public sealed class SolverAssignment
{
    public int ClassId { get; set; }
    public int SubjectId { get; set; }
    public int TeacherId { get; set; }
    public int? RoomId { get; set; }
    public int Day { get; set; }
    public int PeriodId { get; set; }
}

public sealed class SolverSolution
{
    public GenerationStrategy Strategy { get; set; }
    public bool IsFeasible { get; set; }
    public int Score { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public List<SolverAssignment> Assignments { get; set; } = new();
}

/// <summary>Constraint-programming timetable solver contract.</summary>
public interface ITimetableSolver
{
    /// <summary>Solves the model once per requested strategy and returns the solutions.</summary>
    IReadOnlyList<SolverSolution> Solve(SolverInput input, SolverOptions options, CancellationToken ct = default);
}
