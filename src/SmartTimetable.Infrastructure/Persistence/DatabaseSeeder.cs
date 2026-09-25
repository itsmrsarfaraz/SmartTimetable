using SmartTimetable.Domain.Entities;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Infrastructure.Persistence;

/// <summary>
/// Seeds a ready-to-run demo dataset modelled on a Pakistani school + intermediate
/// college: subjects, rooms, a Mon-Fri period grid, 14 teachers and 5 classes
/// (Grade 9/10 plus FSc Pre-Medical, Pre-Engineering and ICS). The demand is sized
/// to be comfortably feasible so a first "Generate" produces real timetables.
/// Runs only once (skips if any campus already exists).
/// </summary>
public static class DatabaseSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Campuses.Any())
            return;

        // ---------- Subjects ----------
        var english = Subj("English", "ENG");
        var urdu = Subj("Urdu", "URD");
        var islamiat = Subj("Islamiat", "ISL");
        var pakStudies = Subj("Pakistan Studies", "PST");
        var maths = Subj("Mathematics", "MTH");
        var biology = Subj("Biology", "BIO");
        var chemistry = Subj("Chemistry", "CHE");
        var physics = Subj("Physics", "PHY", isLab: true);
        var computer = Subj("Computer Science", "CSC", isLab: true);

        var subjects = new[] { english, urdu, islamiat, pakStudies, maths, biology, chemistry, physics, computer };

        // ---------- Rooms (5 classrooms + 3 labs) ----------
        var r9 = Room("Room 9", RoomType.Classroom);
        var r10 = Room("Room 10", RoomType.Classroom);
        var rMed = Room("Room 11", RoomType.Classroom);
        var rEng = Room("Room 12", RoomType.Classroom);
        var rIcs = Room("Room 13", RoomType.Classroom);
        var compLab = Room("Computer Lab", RoomType.Laboratory);
        var physLab = Room("Physics Lab", RoomType.Laboratory);
        var sciLab = Room("Science Lab", RoomType.Laboratory);

        var rooms = new[] { r9, r10, rMed, rEng, rIcs, compLab, physLab, sciLab };

        // ---------- Periods: 6 teaching + 1 mid-morning break ----------
        var periods = new[]
        {
            Period(1, "Period 1", 8, 0, 8, 45),
            Period(2, "Period 2", 8, 45, 9, 30),
            Period(3, "Period 3", 9, 30, 10, 15),
            Break(4, "Break", 10, 15, 10, 45),
            Period(5, "Period 4", 10, 45, 11, 30),
            Period(6, "Period 5", 11, 30, 12, 15),
            Period(7, "Period 6", 12, 15, 13, 0),
        };

        // ---------- Teachers (generous availability => feasible demo) ----------
        var tEng1 = Teacher("Ayesha Khan", english);
        var tEng2 = Teacher("Bilal Ahmed", english);
        var tUrdu1 = Teacher("Fatima Noor", urdu);
        var tUrdu2 = Teacher("Hamza Tariq", urdu);
        var tIsl = Teacher("Imran Qadri", islamiat);
        var tPst = Teacher("Sana Malik", pakStudies);
        var tMath1 = Teacher("Usman Raza", maths);
        var tMath2 = Teacher("Nadia Aslam", maths);
        var tBio = Teacher("Rabia Sattar", biology);
        var tChem = Teacher("Kashif Mehmood", chemistry);
        var tPhy1 = Teacher("Zeeshan Ali", physics);
        var tPhy2 = Teacher("Maryam Iqbal", physics);
        var tCsc1 = Teacher("Omar Farooq", computer);
        var tCsc2 = Teacher("Hina Yousuf", computer);

        var teachers = new[]
        {
            tEng1, tEng2, tUrdu1, tUrdu2, tIsl, tPst, tMath1, tMath2,
            tBio, tChem, tPhy1, tPhy2, tCsc1, tCsc2
        };

        // ---------- Academic hierarchy ----------
        var campus = new Campus
        {
            Name = "Main Campus",
            Address = "Lahore, Punjab",
            Phone = "042-000-0000"
        };

        var session = new AcademicSession
        {
            Campus = campus,
            Name = "2026-2027",
            StartDate = new DateOnly(2026, 8, 1),
            EndDate = new DateOnly(2027, 5, 31),
            IsActive = true,
            WorkingDaysCsv = "1,2,3,4,5" // Monday-Friday
        };
        campus.Sessions.Add(session);

        var school = new Department { Campus = campus, Name = "School", Kind = DepartmentKind.School };
        var college = new Department { Campus = campus, Name = "College", Kind = DepartmentKind.College };
        campus.Departments.Add(school);
        campus.Departments.Add(college);

        var pGeneral = Program(school, "Matric (Science)", ProgramKind.General);
        var pMed = Program(college, "FSc Pre-Medical", ProgramKind.PreMedical);
        var pEng = Program(college, "FSc Pre-Engineering", ProgramKind.PreEngineering);
        var pIcs = Program(college, "ICS", ProgramKind.ICS);

        // ---------- Classes + weekly subject demand ----------
        var g9 = Class(pGeneral, "Grade 9", "A", 9, r9);
        Need(g9, english, 5); Need(g9, urdu, 4); Need(g9, islamiat, 3);
        Need(g9, pakStudies, 3); Need(g9, maths, 5); Need(g9, computer, 3);

        var g10 = Class(pGeneral, "Grade 10", "A", 10, r10);
        Need(g10, english, 5); Need(g10, urdu, 4); Need(g10, islamiat, 3);
        Need(g10, pakStudies, 3); Need(g10, maths, 5); Need(g10, computer, 3);

        var med = Class(pMed, "Pre-Medical Part 1", "A", 11, rMed);
        Need(med, english, 4); Need(med, urdu, 3); Need(med, islamiat, 2);
        Need(med, biology, 5); Need(med, chemistry, 5); Need(med, physics, 5);

        var eng = Class(pEng, "Pre-Engineering Part 1", "A", 11, rEng);
        Need(eng, english, 4); Need(eng, urdu, 3); Need(eng, islamiat, 2);
        Need(eng, maths, 5); Need(eng, chemistry, 5); Need(eng, physics, 5);

        var ics = Class(pIcs, "ICS Part 1", "A", 11, rIcs);
        Need(ics, english, 4); Need(ics, urdu, 3); Need(ics, islamiat, 2);
        Need(ics, maths, 5); Need(ics, physics, 5); Need(ics, computer, 5);

        // ---------- Persist ----------
        db.Rooms.AddRange(rooms);
        db.Subjects.AddRange(subjects);
        db.Teachers.AddRange(teachers);
        db.Periods.AddRange(periods);
        db.Campuses.Add(campus); // pulls in sessions, departments, programs, classes, class-subjects

        db.SaveChanges();
    }

    // ---------- local builders ----------

    private static Subject Subj(string name, string code, bool isLab = false) =>
        new() { Name = name, Code = code, IsLabSubject = isLab };

    private static Room Room(string name, RoomType type) =>
        new() { Name = name, Type = type, Capacity = 40, Building = "Main" };

    private static Period Period(int order, string name, int sh, int sm, int eh, int em) =>
        new() { Order = order, Name = name, StartTime = new TimeOnly(sh, sm), EndTime = new TimeOnly(eh, em), IsBreak = false };

    private static Period Break(int order, string name, int sh, int sm, int eh, int em) =>
        new() { Order = order, Name = name, StartTime = new TimeOnly(sh, sm), EndTime = new TimeOnly(eh, em), IsBreak = true };

    private static Teacher Teacher(string name, params Subject[] canTeach)
    {
        var t = new Teacher
        {
            FullName = name,
            Type = TeacherType.Permanent,
            MinWeeklyPeriods = 0,
            MaxWeeklyPeriods = 30
        };
        foreach (var s in canTeach)
            t.TeacherSubjects.Add(new TeacherSubject { Subject = s });
        return t;
    }

    private static AcademicProgram Program(Department dept, string name, ProgramKind kind)
    {
        var p = new AcademicProgram { Department = dept, Name = name, Kind = kind };
        dept.Programs.Add(p);
        return p;
    }

    private static SchoolClass Class(AcademicProgram program, string name, string section, int grade, Room homeRoom)
    {
        var c = new SchoolClass
        {
            Program = program,
            Name = name,
            Section = section,
            GradeLevel = grade,
            StudentCount = 40,
            HomeRoom = homeRoom
        };
        program.Classes.Add(c);
        return c;
    }

    private static void Need(SchoolClass cls, Subject subject, int periodsPerWeek) =>
        cls.ClassSubjects.Add(new ClassSubject { Subject = subject, PeriodsPerWeek = periodsPerWeek });
}
