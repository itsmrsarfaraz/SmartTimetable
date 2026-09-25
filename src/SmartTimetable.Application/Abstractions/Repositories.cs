using System.Linq.Expressions;
using SmartTimetable.Domain.Common;
using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Application.Abstractions;

/// <summary>Generic persistence-ignorant repository over an aggregate/entity type.</summary>
public interface IRepository<T> where T : Entity
{
    /// <summary>Composable query root (tracked). Use for filtering and includes.</summary>
    IQueryable<T> Query();

    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<List<T>> ListAsync(CancellationToken ct = default);
    Task<List<T>> ListAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);
}

/// <summary>
/// Unit of Work exposing every repository and a single transactional save.
/// One instance represents one logical business transaction.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IRepository<Campus> Campuses { get; }
    IRepository<AcademicSession> Sessions { get; }
    IRepository<Department> Departments { get; }
    IRepository<AcademicProgram> Programs { get; }
    IRepository<SchoolClass> Classes { get; }
    IRepository<Subject> Subjects { get; }
    IRepository<ClassSubject> ClassSubjects { get; }
    IRepository<Teacher> Teachers { get; }
    IRepository<TeacherSubject> TeacherSubjects { get; }
    IRepository<TeacherAvailability> TeacherAvailabilities { get; }
    IRepository<TeacherPreference> TeacherPreferences { get; }
    IRepository<Room> Rooms { get; }
    IRepository<Period> Periods { get; }
    IRepository<CombinedClassRule> CombinedClasses { get; }
    IRepository<CombinedClassMember> CombinedClassMembers { get; }
    IRepository<ContradictorySubjectRule> ContradictoryRules { get; }
    IRepository<SubjectPairingRule> PairingRules { get; }
    IRepository<GeneratedTimetable> Timetables { get; }
    IRepository<TimetableEntry> TimetableEntries { get; }
    IRepository<AdminUser> Users { get; }
    IRepository<LicenseRecord> Licenses { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
