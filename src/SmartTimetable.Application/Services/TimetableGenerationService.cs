using SmartTimetable.Application.Abstractions;
using SmartTimetable.Application.Solving;
using SmartTimetable.Domain.Entities;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Application.Services;

/// <summary>
/// End-to-end timetable generation use case:
/// build model -> solve per strategy -> assign rooms -> persist options.
/// </summary>
public sealed class TimetableGenerationService : ITimetableGenerationService
{
    private readonly ISolverInputBuilder _builder;
    private readonly ITimetableSolver _solver;
    private readonly Func<IUnitOfWork> _uowFactory;

    public TimetableGenerationService(ISolverInputBuilder builder, ITimetableSolver solver, Func<IUnitOfWork> uowFactory)
    {
        _builder = builder;
        _solver = solver;
        _uowFactory = uowFactory;
    }

    public async Task<GenerationReport> GenerateAsync(int academicSessionId, SolverOptions options, CancellationToken ct = default)
    {
        var report = new GenerationReport();

        var input = await _builder.BuildAsync(academicSessionId, ct);

        // Guard rails so the solver never runs on obviously empty/invalid data.
        if (input.Classes.Count == 0) { report.Message = "No classes are configured."; return report; }
        if (input.Periods.Count == 0) { report.Message = "No teaching periods are configured."; return report; }
        if (input.Days.Count == 0) { report.Message = "No working days are configured for this session."; return report; }
        if (input.Requirements.Count == 0) { report.Message = "No class-subject requirements are configured."; return report; }
        if (input.Teachers.Count == 0) { report.Message = "No teachers are configured."; return report; }

        // Only teaching periods can hold a lesson — the daily break is never scheduled,
        // so it must not count towards a class's weekly capacity.
        int teachingPeriodsPerDay = input.Periods.Count(p => !p.IsBreak);
        int totalCapacity = input.Days.Count * teachingPeriodsPerDay;
        foreach (var cls in input.Classes)
        {
            int demand = input.Requirements.Where(r => r.ClassId == cls.Id).Sum(r => r.PeriodsPerWeek);
            if (demand > totalCapacity)
            {
                report.Message = $"Class '{cls.Name}' needs {demand} periods per week but only {totalCapacity} teaching slots exist " +
                                 $"({input.Days.Count} days × {teachingPeriodsPerDay} periods). " +
                                 "Reduce the periods-per-week on its subjects, or add teaching periods/working days.";
                return report;
            }
        }

        IReadOnlyList<SolverSolution> solutions = _solver.Solve(input, options, ct);

        using var uow = _uowFactory();
        int index = 1;
        foreach (var solution in solutions)
        {
            if (!solution.IsFeasible)
            {
                report.Options.Add(new TimetableOptionSummary
                {
                    TimetableId = 0,
                    Name = $"{Humanize(solution.Strategy)} (no solution)",
                    Strategy = solution.Strategy.ToString(),
                    Score = 0,
                    IsFeasible = false,
                    PlacedLessons = 0,
                    Status = solution.StatusText
                });
                continue;
            }

            RoomAssigner.Assign(solution.Assignments, input);

            var timetable = new GeneratedTimetable
            {
                AcademicSessionId = academicSessionId,
                Name = $"Option {index}: {Humanize(solution.Strategy)}",
                Strategy = solution.Strategy,
                Score = solution.Score,
                CreatedUtc = DateTime.UtcNow
            };

            foreach (var a in solution.Assignments)
            {
                timetable.Entries.Add(new TimetableEntry
                {
                    SchoolClassId = a.ClassId,
                    SubjectId = a.SubjectId,
                    TeacherId = a.TeacherId,
                    RoomId = a.RoomId,
                    Day = (Weekday)a.Day,
                    PeriodId = a.PeriodId
                });
            }

            await uow.Timetables.AddAsync(timetable, ct);
            await uow.SaveChangesAsync(ct); // save per option so we obtain the generated Id

            report.AnyFeasible = true;
            report.Options.Add(new TimetableOptionSummary
            {
                TimetableId = timetable.Id,
                Name = timetable.Name,
                Strategy = solution.Strategy.ToString(),
                Score = solution.Score,
                IsFeasible = true,
                PlacedLessons = solution.Assignments.Count,
                Status = solution.StatusText
            });
            index++;
        }

        report.Message = report.AnyFeasible
            ? $"Generated {report.Options.Count(o => o.IsFeasible)} timetable option(s)."
            : "The solver could not fit every lesson into a conflict-free timetable. " +
              "Usual fixes: raise a teacher's daily/weekly period cap, map more teachers to busy subjects, " +
              "widen teacher availability windows, add a lab room, or lower some periods-per-week.";

        return report;
    }

    private static string Humanize(GenerationStrategy strategy) => strategy switch
    {
        GenerationStrategy.TeacherFriendly => "Teacher Friendly",
        GenerationStrategy.StudentFriendly => "Student Friendly",
        GenerationStrategy.RoomOptimization => "Room Optimization",
        GenerationStrategy.Balanced => "Balanced",
        _ => "Custom"
    };
}

/// <summary>
/// Assigns a concrete room to each placed lesson AFTER solving.
/// Lab lessons receive distinct lab rooms per slot; ordinary lessons use the
/// class home room; combined lessons use their configured shared room.
/// </summary>
internal static class RoomAssigner
{
    public static void Assign(List<SolverAssignment> assignments, SolverInput input)
    {
        var labRooms = input.Rooms.Where(r => r.IsLab).Select(r => r.Id).ToList();
        var subjectIsLab = input.Subjects.ToDictionary(s => s.Id, s => s.IsLab);
        var classHome = input.Classes.ToDictionary(c => c.Id, c => c.HomeRoomId);

        // (classId, subjectId) -> shared room for combined classes that pinned a room
        var combinedRoom = new Dictionary<(int ClassId, int SubjectId), int>();
        foreach (var c in input.Combined)
        {
            if (c.RoomId is int rid)
            {
                foreach (var cid in c.ClassIds)
                    combinedRoom[(cid, c.SubjectId)] = rid;
            }
        }

        foreach (var slot in assignments.GroupBy(a => (a.Day, a.PeriodId)))
        {
            int labCursor = 0;
            foreach (var a in slot)
            {
                if (combinedRoom.TryGetValue((a.ClassId, a.SubjectId), out int shared))
                {
                    a.RoomId = shared;
                }
                else if (subjectIsLab.TryGetValue(a.SubjectId, out bool isLab) && isLab && labRooms.Count > 0)
                {
                    a.RoomId = labRooms[labCursor % labRooms.Count];
                    labCursor++;
                }
                else
                {
                    a.RoomId = classHome.TryGetValue(a.ClassId, out int? home) ? home : null;
                }
            }
        }
    }
}
