namespace SmartTimetable.Domain.Common;

/// <summary>
/// Base class for all persistent domain entities. Every entity has a surrogate
/// integer identity assigned by the database.
/// </summary>
public abstract class Entity
{
    public int Id { get; set; }
}
