using SmartTimetable.Domain.Common;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Domain.Entities;

/// <summary>A physical campus of an institution. Root of the academic hierarchy.</summary>
public class Campus : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public ICollection<AcademicSession> Sessions { get; set; } = new List<AcademicSession>();
    public ICollection<Department> Departments { get; set; } = new List<Department>();
}

/// <summary>An academic year such as "2026-2027".</summary>
public class AcademicSession : Entity
{
    public int CampusId { get; set; }
    public Campus? Campus { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Working days enabled for this session, stored as a comma separated list of <see cref="Weekday"/> values.</summary>
    public string WorkingDaysCsv { get; set; } = "1,2,3,4,5,6";
}

/// <summary>School or College division of a campus.</summary>
public class Department : Entity
{
    public int CampusId { get; set; }
    public Campus? Campus { get; set; }

    public string Name { get; set; } = string.Empty;
    public DepartmentKind Kind { get; set; }

    public ICollection<AcademicProgram> Programs { get; set; } = new List<AcademicProgram>();
}

/// <summary>A program of study (e.g. Pre-Medical, ICS) within a department.</summary>
public class AcademicProgram : Entity
{
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    public string Name { get; set; } = string.Empty;
    public ProgramKind Kind { get; set; }

    public ICollection<SchoolClass> Classes { get; set; } = new List<SchoolClass>();
}

/// <summary>A concrete teachable class/section such as "Grade 1 A" or "ICS Part 1".</summary>
public class SchoolClass : Entity
{
    public int ProgramId { get; set; }
    public AcademicProgram? Program { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public int GradeLevel { get; set; }
    public int StudentCount { get; set; }

    /// <summary>Optional default classroom used for non-lab lessons.</summary>
    public int? HomeRoomId { get; set; }
    public Room? HomeRoom { get; set; }

    /// <summary>Optional section-specific break period. When null, campus default break is used.</summary>
    public int? BreakPeriodId { get; set; }
    public Period? BreakPeriod { get; set; }

    /// <summary>Optional earliest teaching period order for this section (e.g. 1).</summary>
    public int? StartPeriodOrder { get; set; }

    /// <summary>Optional latest teaching period order for this section (e.g. 6).</summary>
    public int? EndPeriodOrder { get; set; }

    public ICollection<ClassSubject> ClassSubjects { get; set; } = new List<ClassSubject>();
}
