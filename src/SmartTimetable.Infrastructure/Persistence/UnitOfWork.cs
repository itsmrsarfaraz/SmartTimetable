using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Common;
using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Infrastructure.Persistence;

/// <summary>
/// Owns a single <see cref="AppDbContext"/> for one logical transaction and exposes
/// a repository per aggregate. A fresh instance is created per operation (see the
/// <c>Func&lt;IUnitOfWork&gt;</c> registration), which keeps the background solver's
/// DB access isolated from UI queries.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    private readonly Dictionary<Type, object> _repos = new();

    public UnitOfWork(AppDbContext db) => _db = db;

    private IRepository<T> Repo<T>() where T : Entity
    {
        if (_repos.TryGetValue(typeof(T), out var existing))
            return (IRepository<T>)existing;

        var repo = new EfRepository<T>(_db);
        _repos[typeof(T)] = repo;
        return repo;
    }

    public IRepository<Campus> Campuses => Repo<Campus>();
    public IRepository<AcademicSession> Sessions => Repo<AcademicSession>();
    public IRepository<Department> Departments => Repo<Department>();
    public IRepository<AcademicProgram> Programs => Repo<AcademicProgram>();
    public IRepository<SchoolClass> Classes => Repo<SchoolClass>();
    public IRepository<Subject> Subjects => Repo<Subject>();
    public IRepository<ClassSubject> ClassSubjects => Repo<ClassSubject>();
    public IRepository<Teacher> Teachers => Repo<Teacher>();
    public IRepository<TeacherSubject> TeacherSubjects => Repo<TeacherSubject>();
    public IRepository<TeacherAvailability> TeacherAvailabilities => Repo<TeacherAvailability>();
    public IRepository<TeacherPreference> TeacherPreferences => Repo<TeacherPreference>();
    public IRepository<Room> Rooms => Repo<Room>();
    public IRepository<Period> Periods => Repo<Period>();
    public IRepository<CombinedClassRule> CombinedClasses => Repo<CombinedClassRule>();
    public IRepository<CombinedClassMember> CombinedClassMembers => Repo<CombinedClassMember>();
    public IRepository<ContradictorySubjectRule> ContradictoryRules => Repo<ContradictorySubjectRule>();
    public IRepository<SubjectPairingRule> PairingRules => Repo<SubjectPairingRule>();
    public IRepository<GeneratedTimetable> Timetables => Repo<GeneratedTimetable>();
    public IRepository<TimetableEntry> TimetableEntries => Repo<TimetableEntry>();
    public IRepository<AdminUser> Users => Repo<AdminUser>();
    public IRepository<LicenseRecord> Licenses => Repo<LicenseRecord>();

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public void Dispose() => _db.Dispose();
}
