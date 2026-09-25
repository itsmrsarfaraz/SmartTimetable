# SmartTimetable AI - Architecture (v0.1, planning baseline)

## 1. Decisions that differ from MasterPrompt.md (and why)

| MasterPrompt | This plan | Reason |
|---|---|---|
| OR-Tools in Python + FastAPI | **OR-Tools via the `Google.OrTools` NuGet package, in-process** (already in `SmartTimetable.Solver`) | You are shipping a single `.exe` to colleges. A Python runtime + a second service means a bigger installer, ports, antivirus prompts, and Python source that anyone can read/copy. In-process C# gives one binary and the same CP-SAT engine. |
| ASP.NET Core Minimal API between UI and logic | **No HTTP layer for the desktop product**; WPF calls Application use-cases directly | One machine, one admin. An API adds a listening port and attack surface for zero benefit. Application-layer interfaces stay clean, so an API host can be added for the SaaS phase. |
| Roles: Super Admin / Admin / Timetable Manager | **Exactly one admin account** | Your requirement. Stored as a single row with a PBKDF2 password hash; created on first run. |

Everything else in MasterPrompt (WPF + MVVM + CommunityToolkit.Mvvm, SQLite, DDD, Clean Architecture, hard vs soft constraints, scoring, multiple solutions, drag-and-drop) stands.

## 2. Solution layout and dependency rule

```
Domain          no dependencies. Entities, value objects, rule definitions, domain services.
Application     -> Domain. Use-cases, DTOs, interfaces (repositories, ISolver, IClock, ILicenseGate).
Licensing       no dependencies. Machine fingerprint, request code, signed license, validator.  [DONE + tested]
Infrastructure  -> Application, Domain, Licensing. EF Core (SQLite), repositories, UnitOfWork, migrations, PDF/Excel export.
Solver          -> Application, Domain. CP-SAT model builder, strategy profiles, solution extractor.
Desktop (WPF)   -> Application, Infrastructure, Solver, Licensing. Views, ViewModels, DI composition root.
tools/LicenseGenerator  -> Licensing. Vendor-only CLI.                                          [DONE + tested]
tests           -> everything except Desktop.
```
Feature folders inside each layer (`Teachers/`, `Rooms/`, `Timetabling/`, `Rules/`), not technical folders.
Desktop targets `net10.0-windows`; every other project stays `net10.0` so tests and CI run anywhere.

## 3. Domain model (bounded contexts)

**Academic structure** - `AcademicSession(Name, Start, End, IsActive)`, `Department` (School/College),
`Program` (Pre-Medical, Pre-Engineering, ICS, Grade-level), `SchoolClass(Name, ProgramId, Section, StudentCount, HomeRoomId?)`.

**Curriculum** - `Subject(Code, Name, RequiresLab, LabKind?)`, `ClassSubject(ClassId, SubjectId, PeriodsPerWeek, DoublePeriod, PreferredTeacherId?)`.

**Time** - `PeriodDefinition(Order, Start, End, IsBreak)` per active session, `BreakException(ClassId, Start, End)`,
working days (Mon-Fri or Mon-Sat).

**People** - `Teacher(Name, Cnic, Qualification, Phone, Email, Type: Permanent|Visiting, MinLoad, MaxLoad)`,
`TeacherSubject` (many-to-many), `TeacherAvailability(Day, FromPeriod, ToPeriod)`,
`TeacherPreference(Kind: PreferBeforeBreak|AvoidLastPeriod|AvoidDay, Weight)`,
`TeacherPeriodRestriction(AllowedPeriods, Weight=100)` for visiting-teacher rules.

**Space** - `Room(Name, Kind: Classroom|Lab|LectureHall, LabKind?, Capacity)`.

**Rules** - `CombinedGroup(SubjectId, ClassIds[], RoomId?)` (one lesson, one slot, several classes),
`SubjectExclusion(ProgramId, SubjectId)` ("Biology cannot exist in ICS"), `SubjectPairing(SubjectA, SubjectB)`
("Math may run simultaneously with Biology").

**Output** - `TimetableRun(SessionId, Strategy, CreatedAt)` -> `TimetableSolution(Label A/B/C, Score, IsSelected)`
-> `TimetableEntry(ClassId, SubjectId, TeacherId, RoomId, Day, Period, CombinedGroupId?)`.

**Admin/Settings** - `AdminAccount` (single row), `AppSetting`.

Invariants live in the domain (e.g. a Permanent teacher's `MinLoad <= MaxLoad`; a `SubjectExclusion` makes the
subject un-assignable to that program at `ClassSubject` creation time), not only in the UI.

## 4. Solver design (CP-SAT)

A *lesson* = one weekly period of one `ClassSubject` (or one `CombinedGroup` occurrence).
Slots = working days x non-break periods.

* Variables: `x[lesson, slot]` (bool), `t[lesson, teacher]` (only eligible teachers), `r[lesson, room]` (only fitting rooms).
* **Hard** (model constraints): each lesson exactly one slot; a class has at most one lesson per slot; a teacher at most one
  lesson per slot; a room at most one lesson per slot; teacher availability and visiting-teacher restrictions;
  breaks; lab subjects only in matching lab rooms; combined lessons share one slot and one room; exclusions;
  min/max weekly load.
* **Soft** (penalty terms in the objective): prefer-before-break, avoid-last-period, avoid-day, teacher gaps,
  room changes, same subject twice in a day, heavy-subject spread. Score = 10000 - weighted penalties.
* Strategies (Teacher-friendly, Student-friendly, Room-optimised, Balanced, Custom) = weight profiles over the same model.
* "Solution A/B/C": solve once per profile and/or with different random seeds, then keep the top N *distinct* solutions.
* Pre-check before solving: cheap feasibility diagnostics (total demand vs capacity per teacher/room/class) so the admin gets
  "Teacher X needs 34 periods but only 30 slots are available" instead of a silent INFEASIBLE.
* Runs on a background thread with a time limit and cancellation; never on the WPF UI thread.

**To confirm with you:** "SubjectPairing" - I read it as *permission* for two subjects of different classes to occupy the same
slot (needed for shared teachers/rooms across programs). If you meant it as an obligation ("when Math runs, Biology must run
for the other program"), it becomes a hard synchronisation constraint instead.

## 5. Security baseline

* Single admin: PBKDF2-SHA256 (>= 600k iterations) + per-user salt; first-run setup screen; lockout delay after failed attempts.
* Licensing: see `docs/LICENSING.md` (ECDSA P-256 signed, machine-locked, offline, lifetime).
* SQLite file lives in `%LocalAppData%\SmartTimetable`; parameterised EF Core queries only. The DB is not encrypted at rest
  (would need SQLCipher) - decide later whether a college's teacher CNIC/phone data justifies it.
* Release build: obfuscate, code-sign, no debug symbols shipped.

## 6. Roadmap and status

| Phase | Scope | Status |
|---|---|---|
| 0 | Solution scaffold, versions, licensing, license generator, tests | **Licensing + generator done** (this drop). Desktop project needs fixing. |
| 1 | Domain + EF Core schema + first-run admin setup + CRUD screens (sessions, classes, subjects, teachers, rooms, periods) | next |
| 2 | Rule engine (availability, combined, exclusion, pairing) + validation | |
| 3 | CP-SAT model + feasibility pre-check | |
| 4 | Multiple solutions + scoring UI | |
| 5 | Drag-and-drop editor with live validation/score | |
| 6 | Reports: PDF/Excel/print per class, teacher, room | |
| 7 | SaaS: API host, multi-tenant, online activation | |

Testing strategy: xUnit; domain invariants unit-tested; solver tested on small hand-built instances with known
optimum + a realistic college fixture (FSc Pre-Med/Pre-Eng/ICS with combined Math/Chemistry/English); property test that
*every* returned solution passes the same validator the drag-and-drop editor uses.
