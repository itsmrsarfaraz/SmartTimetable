using Microsoft.EntityFrameworkCore;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Application.Solving;
using SmartTimetable.Domain.Entities;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Infrastructure.Solving;

/// <summary>
/// Reads the configured academic data for a session and flattens it into an
/// EF-free <see cref="SolverInput"/> the solver can consume. A fresh unit of work
/// is used per build so this is safe to call from a background thread.
/// </summary>
public sealed class SolverInputBuilder : ISolverInputBuilder
{
    private readonly Func<IUnitOfWork> _uowFactory;

    public SolverInputBuilder(Func<IUnitOfWork> uowFactory) => _uowFactory = uowFactory;

    public async Task<SolverInput> BuildAsync(int academicSessionId, CancellationToken ct = default)
    {
        using var uow = _uowFactory();

        var session = await uow.Sessions.GetByIdAsync(academicSessionId, ct);

        var periods = await uow.Periods.Query().OrderBy(p => p.Order).ToListAsync(ct);
        var classes = await uow.Classes.ListAsync(ct);
        var subjects = await uow.Subjects.ListAsync(ct);
        var teachers = await uow.Teachers.Query().Include(t => t.Availabilities).ToListAsync(ct);
        var teacherSubjects = await uow.TeacherSubjects.ListAsync(ct);
        var preferences = await uow.TeacherPreferences.ListAsync(ct);
        var classSubjects = await uow.ClassSubjects.Query().Include(cs => cs.PreferredPeriods).ToListAsync(ct);
        var combined = await uow.CombinedClasses.Query().Include(c => c.Members).ToListAsync(ct);
        var contradictions = await uow.ContradictoryRules.ListAsync(ct);
        var pairings = await uow.PairingRules.ListAsync(ct);

        var input = new SolverInput
        {
            Days = ParseWorkingDays(session?.WorkingDaysCsv)
        };

        // ----- Periods: teaching slots plus derived IsLast / IsBeforeBreak flags -----
        var breakOrders = periods.Where(p => p.IsBreak).Select(p => p.Order).ToHashSet();
        var allPeriods = periods.OrderBy(p => p.Order).ToList();
        int maxOrder = allPeriods.Count > 0 ? allPeriods.Max(p => p.Order) : 0;

        foreach (var p in allPeriods)
        {
            input.Periods.Add(new SolverPeriod
            {
                Id = p.Id,
                Order = p.Order,
                Name = p.Name,
                IsLast = p.Order == maxOrder,
                IsBeforeBreak = breakOrders.Contains(p.Order + 1),
                IsBreak = p.IsBreak
            });
        }

        // ----- Classes -----
        foreach (var c in classes)
        {
            input.Classes.Add(new SolverClass
            {
                Id = c.Id,
                Name = c.Name,
                HomeRoomId = c.HomeRoomId,
                BreakPeriodId = c.BreakPeriodId,
                StartPeriodOrder = c.StartPeriodOrder,
                EndPeriodOrder = c.EndPeriodOrder
            });
        }

        // ----- Subjects -----
        foreach (var s in subjects)
        {
            input.Subjects.Add(new SolverSubject
            {
                Id = s.Id,
                Name = s.Name,
                IsLab = s.IsLabSubject
            });
        }

        // ----- Rooms (labs flagged from RoomType) -----
        var rooms = await uow.Rooms.ListAsync(ct);
        foreach (var r in rooms)
        {
            input.Rooms.Add(new SolverRoom
            {
                Id = r.Id,
                Name = r.Name,
                IsLab = r.Type == RoomType.Laboratory
            });
        }

        // ----- Teachers + availability restriction flag -----
        foreach (var t in teachers)
        {
            input.Teachers.Add(new SolverTeacher
            {
                Id = t.Id,
                Name = t.FullName,
                IsVisiting = t.Type == TeacherType.Visiting,
                MinPeriods = t.MinWeeklyPeriods,
                MaxPeriods = t.MaxWeeklyPeriods,
                MaxPerDay = t.MaxPeriodsPerDay,
                HasAvailabilityRestriction = t.Availabilities.Count > 0
            });
        }

        // ----- Eligibility -----
        foreach (var ts in teacherSubjects)
        {
            input.Eligibilities.Add(new SolverEligibility
            {
                TeacherId = ts.TeacherId,
                SubjectId = ts.SubjectId
            });
        }

        // ----- Requirements (class studies subject N periods/week) -----
        // Contradictory-subject rules exclude a subject from a whole program, so any
        // demand for a forbidden (subject, program) pair is dropped here and never
        // scheduled — the subject simply isn't offered to that program's classes.
        var classProgram = classes.ToDictionary(c => c.Id, c => c.ProgramId);
        var forbidden = contradictions.Select(r => (r.SubjectId, r.ProgramId)).ToHashSet();

        foreach (var cs in classSubjects)
        {
            if (classProgram.TryGetValue(cs.SchoolClassId, out var programId)
                && forbidden.Contains((cs.SubjectId, programId)))
                continue;

            input.Requirements.Add(new SolverRequirement
            {
                ClassId = cs.SchoolClassId,
                SubjectId = cs.SubjectId,
                PeriodsPerWeek = cs.PeriodsPerWeek,
                PinnedTeacherId = cs.PreferredTeacherId,
                TimePreference = cs.TimePreference,
                PinnedDay = cs.PinnedDay is { } d ? (int)d : null,
                PinnedPeriodId = cs.PinnedPeriodId,
                PreferredPeriodIds = cs.PreferredPeriods.Select(pp => pp.PeriodId).ToList(),
                PreferencePriority = cs.PreferencePriority
            });
        }

        // ----- Allowed slots for availability-restricted teachers -----
        var daySet = input.Days.ToHashSet();
        foreach (var t in teachers.Where(x => x.Availabilities.Count > 0))
        {
            foreach (var window in t.Availabilities)
            {
                int day = (int)window.Day;
                if (!daySet.Contains(day)) continue;

                foreach (var p in allPeriods)
                {
                    if (p.StartTime >= window.AvailableFrom && p.EndTime <= window.AvailableTo)
                    {
                        input.AllowedSlots.Add(new SolverAllowedSlot
                        {
                            TeacherId = t.Id,
                            Day = day,
                            PeriodId = p.Id
                        });
                    }
                }
            }
        }

        // ----- Preferences -----
        foreach (var pref in preferences)
        {
            input.Preferences.Add(new SolverPreference
            {
                TeacherId = pref.TeacherId,
                Kind = pref.Kind,
                Day = pref.Day is { } d ? (int)d : null,
                Weight = pref.Weight
            });
        }

        // ----- Combined classes -----
        foreach (var c in combined)
        {
            input.Combined.Add(new SolverCombined
            {
                SubjectId = c.SubjectId,
                RoomId = c.SharedRoomId,
                TeacherId = c.PreferredTeacherId,
                PinnedDay = c.PinnedDay is { } cd ? (int)cd : null,
                PinnedPeriodId = c.PinnedPeriodId,
                ClassIds = c.Members.Select(m => m.SchoolClassId).ToList()
            });
        }

        // ----- Subject pairing (soft "may run at the same time" hint) -----
        foreach (var pr in pairings)
        {
            if (pr.SubjectAId == pr.SubjectBId) continue;
            input.Pairings.Add(new SolverPairing
            {
                SubjectAId = pr.SubjectAId,
                SubjectBId = pr.SubjectBId
            });
        }

        return input;
    }

    private static List<int> ParseWorkingDays(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return new List<int> { 1, 2, 3, 4, 5, 6 }; // Mon-Sat default

        var days = new List<int>();
        foreach (var token in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(token, out int value) && value is >= 0 and <= 6 && !days.Contains(value))
                days.Add(value);
        }

        days.Sort();
        return days.Count > 0 ? days : new List<int> { 1, 2, 3, 4, 5, 6 };
    }
}
