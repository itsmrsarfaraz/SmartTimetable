# SmartTimetable AI — User Guide

*AI-Powered Academic Scheduling for Pakistani Schools and Colleges*

This guide explains what the application does, how it thinks, and exactly what to
click to go from an empty screen to a finished, conflict-free timetable. It is
written for the administrator who runs the software day to day. Keep it next to the
app — the last section maps what is built today against the wider project roadmap so
you always know where you are.

---

## 1. What this application is (in one minute)

Building a timetable by hand is slow and error-prone: a teacher ends up in two rooms
at once, a lab subject lands in a normal classroom, a class has a free period in the
middle of the day. SmartTimetable treats this the way airlines and universities do —
as a **constraint problem**. You describe your college once (its subjects, rooms,
periods, teachers and what each class must study), press **Generate**, and an
optimization engine (Google OR-Tools CP-SAT) searches millions of possibilities to
find timetables that break none of your hard rules and honour as many preferences as
possible. It runs entirely on a normal CPU — no internet, no GPU, no cloud.

There is a single administrator account. The app is licensed per machine (see
section 9), and everything you enter is stored locally in a small database file on
that computer.

---

## 2. The mental model — how SmartTimetable thinks

Everything in the app falls into one of three buckets. If you keep these three in
mind, the whole system makes sense:

```
   RESOURCES                 REQUIREMENTS                 RESULT
   (what you have)           (what must happen)           (what the AI builds)

   Subjects                  Each class studies           A weekly grid:
   Rooms          +          certain subjects,      -->   every class, every
   Periods                   so many periods per          period filled with
   Teachers                  week, taught by a            a subject + teacher
                             qualified teacher            + room, with no clashes
```

- **Resources** are your building blocks: the subjects offered, the rooms and labs,
  the daily period grid, and the teaching staff.
- **Requirements** connect them: for each class you list its weekly subjects and how
  many periods each needs, and you say who may teach them.
- **Result**: the solver places every required lecture into the period grid so that
  no teacher, class or room is ever double-booked, labs get lab rooms, and breaks
  stay clear.

You spend 95% of your time entering **resources** and **requirements**. The AI does
the hard part in seconds.

---

## 3. The data model in plain words

A few terms appear throughout the app. Here is what each means and how they connect.

- **Program** — an academic track, e.g. *Matric (Science)*, *FSc Pre-Medical*,
  *FSc Pre-Engineering*, *ICS*. Every class belongs to one program. *(A sample set is
  pre-loaded, and you can add or edit your own on the **Academic Structure** screen.)*
- **Campus / Session / Department** — the wider structure a program sits inside: the
  physical campus, the academic year (one is marked *active*), and the department that
  owns the program. You manage all of these on the **Academic Structure** screen.
- **Class** — a specific section that gets a timetable, e.g. *Grade 9 A* or
  *ICS Part 1*. A class belongs to a program and has a student count and an optional
  home room.
- **Subject** — e.g. *Mathematics*, *Physics*, *Computer Science*. A subject can be
  flagged as a **lab subject**, which forces the solver to schedule it in a
  laboratory room.
- **Room** — a classroom or a laboratory, with a capacity.
- **Period** — a named time slot in the day (Period 1, Period 2, …), plus **breaks**.
  Breaks are never used for teaching.
- **Teacher** — a staff member, marked *Permanent* or *Visiting*, with a weekly
  workload floor (minimum) and ceiling (maximum) the solver respects.
- **Teacher–Subject qualification** — which subjects each teacher is *allowed* to
  teach. This is a many-to-many list you manage on the *Teacher Subjects* screen.
- **Weekly subject demand** — the heart of it: for one class, "this subject, this
  many periods per week, taught by this teacher (or any qualified teacher)." You
  create these on the *Classes* screen. Each demand is one row.
- **Period preference** — optionally, for one class's subject, the periods a teacher
  would *prefer* to teach it in, with a priority. This is a *soft* wish (an optional
  step — see section 5), managed on the *Teacher Periods* screen.
- **Teacher availability window** — optionally, the days and periods a teacher *can*
  work. If you give a teacher any windows, the solver will never schedule them outside
  those windows (a *hard* rule). Leave a teacher with no windows to keep them fully
  available. Managed on the *Teacher Availability* screen.
- **Combined class** — two or more sections that attend a subject *together* in one
  room at one time (e.g. a shared elective). The solver books them into the same slot
  with one teacher and, optionally, one shared room. Managed on *Combined Classes*.
- **Parallel electives** — two subjects you want to run *at the same time* across a class
  so parallel electives line up. This is a *soft* reward: the solver tries to place
  them in the same period but will not fail if it cannot. Managed on *Parallel Electives*.
- **Contradictory subject** — a subject *blocked* from a whole program. Classes in that
  program are never scheduled for it (a *hard* rule). Managed on *Contradictory
  Subjects*.

The chain the solver ultimately reads is:

```
Class  ──has──▶  Weekly subject demands  ──each names──▶  a Subject
                          │                                    │
                          ├──optionally pins──▶ a Teacher       └─ may need a Lab room
                          └──optionally prefers──▶ certain Periods (soft)

Teacher ──is qualified for──▶ Subjects        Rooms + Periods define the grid
```

---

## 4. The screens, in the order you will use them

The left-hand navigation lists every screen. In normal use you work roughly
top-to-bottom:

| Screen | What it is for |
|---|---|
| **Dashboard** | A quick overview of your data and the latest timetable. |
| **Academic Structure** | Add/edit campuses, academic sessions (one is *active*), departments and programs — the framework every class hangs off. |
| **Subjects** | Add every subject. Flag labs (e.g. Physics, Computer Science). |
| **Rooms** | Add classrooms and laboratories with capacities. |
| **Periods** | Define the daily slots and mark which ones are breaks. |
| **Teachers** | Add staff: name, CNIC, qualification, phone, email, type, min/max weekly periods. |
| **Classes** | Create each class **and** list its weekly subject demands (this is where you assign teachers to a class). |
| **Combined Classes** | Group sections that attend one subject together in the same room and time slot. |
| **Contradictory Subjects** | Block a subject from a program, so its classes are never scheduled for it. |
| **Parallel Electives** | Ask the solver to run two subjects in the same period so parallel electives line up (soft). |
| **Teacher Subjects** | Tick which subjects each teacher is qualified to teach. |
| **Teacher Periods** | Optional: for a teacher's class, choose preferred periods and how strongly to honour them. |
| **Teacher Availability** | Optional: set the days/periods a teacher can work; the solver never schedules them outside those windows. |
| **Generate** | Pick strategies and run the solver to produce timetable options. |
| **Timetable** | View as a grid or list, filter by class, manually edit lessons, and print or export (PDF, Excel, CSV). |

> **Note on order.** The navigation lists *Teacher Subjects* and *Teacher
> Availability* below *Classes*, but when setting up you should fill in *Teacher
> Subjects* **before** you create classes — a teacher can only be picked for a class
> once they are qualified for that subject. The step-by-step in section 5 uses the
> recommended order.

> **The app ships with a full sample college already loaded** — an academic structure
> (campus, an active 2026–2027 session, departments and programs), 9 subjects, 8 rooms,
> a Mon–Fri period grid with a mid-morning break, 14 teachers, and 5 classes with their
> weekly demands. You can press **Generate** immediately to see it work, then edit or
> replace the sample data with your own.

---

## 5. Step-by-step: setting up your own college from scratch

Do these in order. Each step assumes the one before it is done.

1. **Academic Structure.** Go to *Academic Structure* → confirm there is a campus, an
   **active** academic session, and the departments and programs you need. Add or edit
   any that are missing. Every class you create later must point at a program from here.
2. **Subjects.** Go to *Subjects* → for each subject, click **New**, type a name and
   code, tick **lab subject** if it must run in a laboratory, then **Save**.
3. **Rooms.** Go to *Rooms* → add each classroom and lab with its capacity. Mark labs
   as type *Laboratory* so lab subjects can be placed there.
4. **Periods.** Go to *Periods* → create Period 1, Period 2, … in order, and add your
   **Break** slot(s). Times are for display and ordering; the solver cares about the
   order and which slots are breaks.
5. **Teachers.** Go to *Teachers* → for each teacher click **New**, fill in the
   details, choose *Permanent* or *Visiting*, set **min** and **max weekly periods**,
   then **Save**.
6. **Teacher Subjects.** Go to *Teacher Subjects* → for each teacher, tick every
   subject they are qualified to teach. *A teacher who is not qualified for a subject
   can never be assigned to teach it, so don't skip this.*
7. **Classes + their weekly subjects.** Go to *Classes* → this is the big one, and
   the exact steps are in section 6 below.
8. **Optional rules.** Tighten the result with any of these, in any order:
   - *Teacher Periods* — fine-tune when specific teachers teach specific classes.
   - *Teacher Availability* — block the days/periods a teacher cannot work.
   - *Combined Classes* — make sections share one lecture (parallel electives).
   - *Parallel Electives* — ask two subjects to run in the same period.
   - *Contradictory Subjects* — block a subject from a whole program.
9. **Generate.** Go to *Generate* → pick one or more strategies and run it.
10. **Timetable.** Open a result to view, edit, print or export it.

---

## 6. Create a class and assign teachers — the exact steps

This is the screen most people get stuck on, so here it is precisely. The Classes
screen has **two stages**: first you save the class, then you add its weekly
subjects. You cannot do the second before the first.

**Stage 1 — create the class**

1. Click **New** first. *(This clears the form. If you skip this while a class is
   highlighted in the list, you will edit that class instead of creating a new one.)*
2. Type a **Class name** (e.g. `Grade 9`). This is required.
3. Choose a **Program** from the dropdown. *(Programs come from the Academic Structure
   screen — add one there first if the list is empty.)*
4. Set **Section**, **Grade level**, **Student count** and optionally a **Home room**.
5. Click **Save class**. You should see the class appear in the list on the left and
   the message *"Class saved. You can now add its weekly subjects below."*

**Stage 2 — add its weekly subjects and assign teachers**

6. With the class now saved (and selected), scroll to **Weekly subjects**.
7. Choose a **Subject**, set **Periods / week**, and pick a **Preferred teacher** —
   this is how you assign a teacher to the class. Leave it on *"(any eligible
   teacher)"* to let the solver choose from qualified staff.
8. Click **Add / update subject**. The row appears in the subjects table.
9. Repeat step 7–8 for every subject the class studies.

That's it — the class now has its full weekly demand, with teachers assigned.

**A second way to assign teachers.** The *Teacher Periods* screen approaches it from
the teacher's side: pick a teacher, use **Assign another class** to pin them to a
class that needs one of their subjects, and then tick the periods they prefer for it.
Use *Classes* when you think "this class needs these subjects"; use *Teacher Periods*
when you think "this teacher should take these classes, at these times."

---

## 7. Troubleshooting: "I click Save / Add and nothing happens"

The screen is almost always refusing the action for a reason shown as a small grey
line of text near the buttons. Check these, top to bottom:

**When creating a class (Save class does nothing):**

- **No class name.** The name is required. Type one and save again.
- **You meant to create a new class but a class is selected.** Click **New** first,
  then fill the form and save. Otherwise you are editing the selected class, and if
  you changed nothing visible, it looks like nothing happened.
- **Student count is zero.** Set it to a positive number.

**When adding a subject / assigning a teacher (Add / update subject does nothing):**

- **The class isn't saved yet.** This is the most common one. You must complete
  Stage 1 (Save class) before Stage 2. If the message reads *"Save the class first,
  then add its subjects,"* that's this. Save the class, then add subjects.
- **No subject chosen**, or **Periods / week is zero.** Pick a subject and set a
  positive number.

**The Preferred-teacher dropdown is missing a teacher.** A teacher only appears as
"eligible" once they are qualified for that subject on the *Teacher Subjects* screen.
Add the qualification there, reopen the class, and they'll be selectable.

**Generate says "No active academic session was found."** Open *Academic Structure*
and mark one session as **active** (or add one and activate it). Alternatively, restart
the app so the sample data — which includes the active 2026–2027 session — is created.
See section 8.

If you have checked all of the above and a button still does nothing, note exactly
which button and what (if any) grey text appears, and report it — that pins down the
cause immediately.

---

## 8. Resetting data and updating the app

The app stores everything in one local database file:

```
%APPDATA%\SmartTimetable\smarttimetable.db
```

(On this machine that is `C:\Users\<you>\AppData\Roaming\SmartTimetable\smarttimetable.db`.)

- **To wipe everything and get the fresh sample college back:** close the app, delete
  that file, and start the app again. It rebuilds the database and re-seeds the
  sample data on launch.
- **After installing a new build that changed the data structure:** you must delete
  that same file once, then relaunch. The app creates missing tables on a *new*
  database but does not alter an *existing* one, so an old database against a new
  build can cause saves to fail. Deleting it forces a clean, correct rebuild.

> Deleting the database removes all classes, teachers and timetables you entered, so
> only do this when you intend to start over or right after an update.

---

## 9. Licensing (for shipping to a college)

The app is designed to be handed to a college and locked to their machine:

1. The college installs and opens the app. On first run it shows an **Activation
   Code** — a fingerprint of that specific computer.
2. They send you that code. On your own machine you run the license generator tool to
   produce a signed **license key** for that code.
3. They import the key once. The app unlocks on that machine for its lifetime,
   offline.

The signing (private) key never ships inside the app — only the public key it uses to
verify a license does. That means a key issued for one college's machine will not work
on another, and no one can mint their own key. There is a single administrator; there
is no multi-user management to configure.

*(If the public key has not been embedded yet during development, the app runs
unlocked in a developer mode. Before shipping to a real college, the vendor key must
be embedded and the app rebuilt.)*

**Installing and removing on the college's PC.** You can hand the college either the
bare `SmartTimetable.exe` (produced by `publish.bat`) or, for a tidier experience, the
**installer** `SmartTimetable-Setup.exe` (produced by `build-installer.bat`, which uses
Inno Setup). The installer creates Start-Menu shortcuts, marks the publisher as
*Crafting Colons*, and registers a proper Windows uninstaller. When someone later goes
to remove the app from *Apps & features* / *Programs and Features*, the uninstall entry
shows the **TimetableDelete** icon. Uninstalling leaves the college's timetable database
and license in place, so a reinstall keeps their data and activation.

---

## 10. What is built today vs. the full roadmap

This build now delivers the full MasterPrompt vision for a single-college, offline
desktop product — the academic structure, all data setup, the AI solver with five
strategies, combined classes and subject rules, teacher availability, manual editing,
and reports. Only a few forward-looking items remain. Knowing the boundary saves
confusion:

**Working now**

- Single-admin login and offline, machine-locked licensing.
- **Academic Structure** — manage campuses, academic sessions (one active), departments
  and programs; every class attaches to a program you control here.
- Full data management: Subjects, Rooms, Periods, Teachers, Teacher–Subject
  qualifications, Classes with weekly subject demands.
- Per-class teacher **period preferences** with a priority (soft constraint).
- Teacher **availability windows** — the days/periods a teacher can work (hard
  constraint), on top of the min/max weekly workload limits.
- Teacher **workload limits** (min/max weekly periods) and **lab subjects require lab
  rooms**, enforced by the solver.
- **Combined classes** — sections that attend one subject together in the same room and
  time slot (parallel electives).
- **Parallel electives** — two subjects nudged into the same period so parallel electives
  line up (soft reward).
- **Contradictory subjects** — a subject blocked from a whole program (hard rule).
- **Generate** with **five strategies** — *Balanced*, *Teacher Friendly*, *Student
  Friendly*, *Room Optimization*, and *Custom* (you set the weights yourself) — each
  producing a scored timetable option, with a time limit and random seed you can set.
- **Timetable** viewer with a day × period **grid** or a flat **list**, a filter by
  class, **manual editing** (move a lesson, reassign its teacher/room, or delete it —
  with clash validation), and **reports**: **print** and export to **PDF, Excel and
  CSV**.

**On the roadmap (not in this build yet)**

- **Break exceptions** per class (per-class variations to the shared break grid).
- **Drag-and-drop** editing on the grid (today editing is done through the edit panel
  in the list view).
- Cloud, multi-tenant SaaS with multiple user roles (today it is single-admin, offline,
  and licensed per machine — by design).

---

## Quick reference — the shortest path to a timetable

```
1. Academic Structure  →  2. Subjects  →  3. Rooms  →  4. Periods  →  5. Teachers  →
6. Teacher Subjects  →  7. Classes (save class, then add weekly subjects + teachers)  →
8. optional rules (Teacher Periods / Availability, Combined, Parallel Electives, Contradictory)  →
9. Generate  →  10. open a result in Timetable (view, edit, print/export)
```

Or simply press **Generate** on the sample data to watch the whole thing work first.

---

*SmartTimetable AI — a product by Crafting Colons.*
