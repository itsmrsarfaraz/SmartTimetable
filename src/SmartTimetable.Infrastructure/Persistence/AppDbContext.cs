using Microsoft.EntityFrameworkCore;
using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for the whole application (SQLite).
/// Delete behaviours are configured explicitly to avoid SQLite multiple-cascade-path
/// errors: reference edges use Restrict/SetNull, ownership edges use Cascade.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Campus> Campuses => Set<Campus>();
    public DbSet<AcademicSession> Sessions => Set<AcademicSession>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AcademicProgram> Programs => Set<AcademicProgram>();
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<ClassSubject> ClassSubjects => Set<ClassSubject>();
    public DbSet<ClassSubjectPreferredPeriod> ClassSubjectPreferredPeriods => Set<ClassSubjectPreferredPeriod>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<TeacherAvailability> TeacherAvailabilities => Set<TeacherAvailability>();
    public DbSet<TeacherPreference> TeacherPreferences => Set<TeacherPreference>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Period> Periods => Set<Period>();
    public DbSet<CombinedClassRule> CombinedClasses => Set<CombinedClassRule>();
    public DbSet<CombinedClassMember> CombinedClassMembers => Set<CombinedClassMember>();
    public DbSet<ContradictorySubjectRule> ContradictoryRules => Set<ContradictorySubjectRule>();
    public DbSet<SubjectPairingRule> PairingRules => Set<SubjectPairingRule>();
    public DbSet<GeneratedTimetable> Timetables => Set<GeneratedTimetable>();
    public DbSet<TimetableEntry> TimetableEntries => Set<TimetableEntry>();
    public DbSet<AdminUser> Users => Set<AdminUser>();
    public DbSet<LicenseRecord> Licenses => Set<LicenseRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ----- Academic hierarchy: ownership cascades downward -----
        b.Entity<AcademicSession>()
            .HasOne(x => x.Campus).WithMany(x => x.Sessions)
            .HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Department>()
            .HasOne(x => x.Campus).WithMany(x => x.Departments)
            .HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<AcademicProgram>()
            .HasOne(x => x.Department).WithMany(x => x.Programs)
            .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<SchoolClass>()
            .HasOne(x => x.Program).WithMany(x => x.Classes)
            .HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Cascade);

        // Optional home room: reference edge, so null it out on room delete.
        b.Entity<SchoolClass>()
            .HasOne(x => x.HomeRoom).WithMany()
            .HasForeignKey(x => x.HomeRoomId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<SchoolClass>()
            .HasOne(x => x.BreakPeriod).WithMany()
            .HasForeignKey(x => x.BreakPeriodId).OnDelete(DeleteBehavior.SetNull);

        // ----- Class-subject demand -----
        b.Entity<ClassSubject>()
            .HasOne(x => x.SchoolClass).WithMany(x => x.ClassSubjects)
            .HasForeignKey(x => x.SchoolClassId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<ClassSubject>()
            .HasOne(x => x.Subject).WithMany(x => x.ClassSubjects)
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<ClassSubject>()
            .HasOne(x => x.PreferredTeacher).WithMany()
            .HasForeignKey(x => x.PreferredTeacherId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<ClassSubject>()
            .HasOne(x => x.PinnedPeriod).WithMany()
            .HasForeignKey(x => x.PinnedPeriodId).OnDelete(DeleteBehavior.SetNull);

        // Preferred periods are owned by the demand (cascade), but reference Period (restrict).
        b.Entity<ClassSubjectPreferredPeriod>()
            .HasOne(x => x.ClassSubject).WithMany(x => x.PreferredPeriods)
            .HasForeignKey(x => x.ClassSubjectId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<ClassSubjectPreferredPeriod>()
            .HasOne(x => x.Period).WithMany()
            .HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);

        // ----- Teacher mappings -----
        b.Entity<TeacherSubject>()
            .HasOne(x => x.Teacher).WithMany(x => x.TeacherSubjects)
            .HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<TeacherSubject>()
            .HasOne(x => x.Subject).WithMany(x => x.TeacherSubjects)
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<TeacherAvailability>()
            .HasOne(x => x.Teacher).WithMany(x => x.Availabilities)
            .HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<TeacherPreference>()
            .HasOne(x => x.Teacher).WithMany(x => x.Preferences)
            .HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Cascade);

        // ----- Combined classes -----
        b.Entity<CombinedClassRule>()
            .HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<CombinedClassRule>()
            .HasOne(x => x.SharedRoom).WithMany()
            .HasForeignKey(x => x.SharedRoomId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<CombinedClassRule>()
            .HasOne(x => x.PreferredTeacher).WithMany()
            .HasForeignKey(x => x.PreferredTeacherId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<CombinedClassRule>()
            .HasOne(x => x.PinnedPeriod).WithMany()
            .HasForeignKey(x => x.PinnedPeriodId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<CombinedClassMember>()
            .HasOne(x => x.Rule).WithMany(x => x.Members)
            .HasForeignKey(x => x.CombinedClassRuleId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<CombinedClassMember>()
            .HasOne(x => x.SchoolClass).WithMany()
            .HasForeignKey(x => x.SchoolClassId).OnDelete(DeleteBehavior.Restrict);

        // ----- Contradictory subject rules -----
        b.Entity<ContradictorySubjectRule>()
            .HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<ContradictorySubjectRule>()
            .HasOne(x => x.Program).WithMany()
            .HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Cascade);

        // ----- Subject pairing (two FKs into Subject) -----
        b.Entity<SubjectPairingRule>()
            .HasOne(x => x.SubjectA).WithMany()
            .HasForeignKey(x => x.SubjectAId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<SubjectPairingRule>()
            .HasOne(x => x.SubjectB).WithMany()
            .HasForeignKey(x => x.SubjectBId).OnDelete(DeleteBehavior.Restrict);

        // ----- Timetable results -----
        b.Entity<GeneratedTimetable>()
            .HasOne(x => x.AcademicSession).WithMany()
            .HasForeignKey(x => x.AcademicSessionId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<TimetableEntry>()
            .HasOne(x => x.GeneratedTimetable).WithMany(x => x.Entries)
            .HasForeignKey(x => x.GeneratedTimetableId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<TimetableEntry>()
            .HasOne(x => x.SchoolClass).WithMany()
            .HasForeignKey(x => x.SchoolClassId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<TimetableEntry>()
            .HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<TimetableEntry>()
            .HasOne(x => x.Teacher).WithMany()
            .HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<TimetableEntry>()
            .HasOne(x => x.Room).WithMany()
            .HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<TimetableEntry>()
            .HasOne(x => x.Period).WithMany()
            .HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);

        // ----- Constraints / indexes -----
        b.Entity<AdminUser>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Subject>().HasIndex(x => x.Code);
        b.Entity<TeacherSubject>().HasIndex(x => new { x.TeacherId, x.SubjectId }).IsUnique();
        b.Entity<ClassSubject>().HasIndex(x => new { x.SchoolClassId, x.SubjectId }).IsUnique();
        b.Entity<ClassSubjectPreferredPeriod>().HasIndex(x => new { x.ClassSubjectId, x.PeriodId }).IsUnique();

        base.OnModelCreating(b);
    }
}
