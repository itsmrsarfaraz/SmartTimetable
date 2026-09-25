using SmartTimetable.Domain.Enums;
using SmartTimetable.Application.Solving;

namespace SmartTimetable.Solver;

/// <summary>
/// Per-strategy penalty weights. Changing these multipliers is what makes each
/// generation strategy produce a meaningfully different timetable from the same data.
/// </summary>
internal readonly record struct StrategyProfile(
    int TeacherPref,   // multiplier applied to every teacher-preference penalty
    int LastPeriod,    // penalty for each lesson placed in the final period of a day
    int Afternoon,     // penalty for each lesson placed after the mid-day break
    int UnderMin,      // penalty per period a teacher falls below their minimum load
    int LabOffPeak,    // penalty for each lab lesson placed in the last period
    int PeriodPref,    // multiplier applied to per-demand preferred-period penalties
    int Pairing)       // reward per slot where two paired subjects run together
{
    public static StrategyProfile For(GenerationStrategy strategy) => strategy switch
    {
        // Honour teacher availability/preferences first; students come second.
        GenerationStrategy.TeacherFriendly => new StrategyProfile(TeacherPref: 3, LastPeriod: 1, Afternoon: 0, UnderMin: 2, LabOffPeak: 0, PeriodPref: 3, Pairing: 1),

        // Front-load the day and keep the last period clear for students.
        GenerationStrategy.StudentFriendly => new StrategyProfile(TeacherPref: 1, LastPeriod: 4, Afternoon: 2, UnderMin: 1, LabOffPeak: 1, PeriodPref: 1, Pairing: 1),

        // Keep labs in prime slots and spread resource usage.
        GenerationStrategy.RoomOptimization => new StrategyProfile(TeacherPref: 1, LastPeriod: 1, Afternoon: 1, UnderMin: 1, LabOffPeak: 3, PeriodPref: 1, Pairing: 3),

        // A sensible middle ground.
        GenerationStrategy.Balanced => new StrategyProfile(TeacherPref: 2, LastPeriod: 2, Afternoon: 1, UnderMin: 1, LabOffPeak: 1, PeriodPref: 2, Pairing: 2),

        _ => new StrategyProfile(TeacherPref: 2, LastPeriod: 2, Afternoon: 1, UnderMin: 1, LabOffPeak: 1, PeriodPref: 2, Pairing: 2),
    };

    /// <summary>Builds a profile from admin-supplied Custom-strategy weights (clamped to sane bounds).</summary>
    public static StrategyProfile FromWeights(SolverCustomWeights w) => new(
        TeacherPref: Clamp(w.TeacherPref),
        LastPeriod: Clamp(w.LastPeriod),
        Afternoon: Clamp(w.Afternoon),
        UnderMin: Clamp(w.UnderMin),
        LabOffPeak: Clamp(w.LabOffPeak),
        PeriodPref: Clamp(w.PeriodPref),
        Pairing: Clamp(w.Pairing));

    private static int Clamp(int v) => v < 0 ? 0 : (v > 100 ? 100 : v);
}
