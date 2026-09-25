using SmartTimetable.Domain.Common;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Domain.Entities;

/// <summary>A physical room: classroom, laboratory or lecture hall.</summary>
public class Room : Entity
{
    public string Name { get; set; } = string.Empty;
    public RoomType Type { get; set; } = RoomType.Classroom;
    public int Capacity { get; set; } = 40;
    public string Building { get; set; } = string.Empty;
}

/// <summary>
/// A time slot template shared across the campus: a numbered teaching period or a break.
/// Break periods are excluded from scheduling but preserved for display.
/// </summary>
public class Period : Entity
{
    /// <summary>1-based ordering across the day (breaks included in ordering).</summary>
    public int Order { get; set; }

    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    /// <summary>True for breaks/recess; no lessons may be placed here.</summary>
    public bool IsBreak { get; set; }
}
