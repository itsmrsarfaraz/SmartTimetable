using SmartTimetable.Application.Solving;
using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Application.Abstractions;

/// <summary>
/// Builds a flattened <see cref="SolverInput"/> for a session by reading the
/// database. Implemented in Infrastructure (needs EF includes); kept behind an
/// interface so the Application orchestration stays persistence-ignorant.
/// </summary>
public interface ISolverInputBuilder
{
    Task<SolverInput> BuildAsync(int academicSessionId, CancellationToken ct = default);
}

/// <summary>Summary of one produced timetable option.</summary>
public sealed class TimetableOptionSummary
{
    public int TimetableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public int Score { get; set; }
    public bool IsFeasible { get; set; }
    public int PlacedLessons { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Outcome of a generation run.</summary>
public sealed class GenerationReport
{
    public bool AnyFeasible { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<TimetableOptionSummary> Options { get; set; } = new();
}

/// <summary>Orchestrates the end-to-end timetable generation use case.</summary>
public interface ITimetableGenerationService
{
    Task<GenerationReport> GenerateAsync(int academicSessionId, SolverOptions options, CancellationToken ct = default);
}
