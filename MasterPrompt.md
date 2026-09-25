This is an excellent AI project because it solves a real operational problem and does not require a GPU. Timetable generation is primarily a **Constraint Satisfaction Problem (CSP)** and **Optimization Problem**, not a deep-learning problem.

For SmartTimetable, I would strongly recommend:

**Frontend/Desktop**

* C# .NET 10
* WPF (MVVM)
* CommunityToolkit.Mvvm

**Database**

* SQLite (Local)
* SQL Server later

**AI Engine**

* Google OR-Tools CP-SAT Solver (Python)
* Python 3.12

**Communication**

* ASP.NET Core Minimal API
* Python FastAPI

**Why?**

Most people incorrectly think timetable generation requires LLMs.

It doesn't.

The best universities, airlines, railways, hospitals and schools use:

* Constraint Programming
* Genetic Algorithms
* Simulated Annealing
* Tabu Search
* Mixed Integer Programming

Google OR-Tools CP-SAT is one of the strongest timetable solvers available and runs perfectly on CPU.

---

# Project Vision

Project Name:

**SmartTimetable AI**

Tagline:

**AI-Powered Academic Scheduling System for Pakistani Schools and Colleges**

Goal:

Allow administration to:

* Configure academic structure
* Define rules
* Define restrictions
* Define teacher preferences
* Define combined classes
* Generate multiple timetable options
* Edit schedules manually
* Re-generate optimized schedules

---

# Pakistani Education Support

The system should support:

## School

* Grade 1
* Grade 2
* Grade 3
* ...
* Grade 10

## Intermediate College

### FSc Pre-Medical

Subjects:

* Biology
* Chemistry
* Physics
* English
* Urdu
* Islamiat
* Pakistan Studies

### FSc Pre-Engineering

Subjects:

* Mathematics
* Chemistry
* Physics
* English
* Urdu
* Islamiat
* Pakistan Studies

### ICS

Subjects:

* Computer Science
* Mathematics
* Physics
* English
* Urdu
* Islamiat
* Pakistan Studies

---

# High Level Architecture

```text
+----------------------+
| WPF Desktop App      |
| Admin Panel          |
+----------+-----------+
           |
           |
           v
+----------------------+
| ASP.NET Core API     |
| Business Rules       |
+----------+-----------+
           |
           |
           v
+----------------------+
| SQLite Database      |
+----------+-----------+
           |
           |
           v
+----------------------+
| AI Scheduling Engine |
| Python + ORTools     |
+----------------------+
```

---

# Core Modules

## Authentication

Admin Login

Roles:

* Super Admin
* Admin
* Timetable Manager

---

## Academic Structure

### Campus

### Academic Session

Example

```text
2026-2027
```

### Department

Example

```text
School
College
```

### Programs

```text
Pre-Medical
Pre-Engineering
ICS
```

---

# Classes

Examples

```text
Grade 1 A
Grade 1 B

ICS Part 1
ICS Part 2

Pre-Medical Part 1
Pre-Medical Part 2
```

---

# Time Configuration

Admin can define:

```text
School Start Time

School End Time
```

Example:

```text
08:00 AM
02:00 PM
```

---

# Period Builder

Example

```text
Period 1
08:00 - 08:45

Period 2
08:45 - 09:30

Period 3
09:30 - 10:15

Break
10:15 - 10:45

Period 4
10:45 - 11:30
```

---

# Break Rules

Admin can:

### Apply to all

```text
10:15 - 10:45
```

### Exception

```text
Class A
11:00 - 11:30
```

---

# Teacher Module

Teacher Types

```text
Permanent
Visiting
```

Teacher Information

```text
Name
CNIC
Qualification
Phone
Email
```

Availability

```text
Monday
Tuesday
Wednesday
```

Time Availability

```text
8am - 12pm
```

---

# Subject Module

Examples

```text
Math
Biology
Chemistry
Computer Science
Physics
```

---

# Teacher Subject Mapping

Example

```text
Math

Teacher A
Teacher B
Teacher C
```

Many-to-many relationship.

---

# Workload Rules

Permanent

```text
Minimum 6 classes
Maximum 7 classes
```

Visiting

```text
Minimum 1
Maximum configurable
```

---

# Teacher Preferences

Examples

```text
Prefer before break

Avoid last period

Avoid Monday
```

---

# Special Visiting Teacher Rules

Example

```text
Teacher X

Only 1st period
Only 2nd period
```

Priority Weight:

```text
100
```

Solver attempts to satisfy first.

---

# Combined Classes

One of the most important modules.

Example:

```text
Subject: Mathematics

Programs:
ICS
Pre-Engineering
```

Combined Class:

```text
YES
```

Shared Room:

```text
Room 12
```

---

Example:

```text
Chemistry

Programs:
Pre-Medical
Pre-Engineering
```

Combined

```
YES
```

---

# Contradictory Subjects

Admin creates rules.

Example:

```text
Math
Computer Science
```

cannot exist in:

Pre-Medical

````

---

Example:

```text
Biology
````

cannot exist in:

ICS
Pre-Engineering

````

---

# Subject Pairing Rules

Example

```text
Math

can run simultaneously with

Biology
````

---

Example

```text
Computer Science

can run simultaneously with

Chemistry
```

These rules are essential for resource optimization.

---

# Room Management

Classrooms

Labs

Lecture Halls

Capacity

Resources

---

# Lab Rules

Example

```text
Computer Science

requires

Computer Lab
```

---

# Timetable Generator

Admin clicks:

```text
Generate
```

---

# Generation Strategies

Option 1

Teacher Friendly

---

Option 2

Student Friendly

---

Option 3

Room Optimization

---

Option 4

Balanced

---

Option 5

Custom Weights

---

# AI Solver Workflow

```text
Load Data

Load Rules

Load Constraints

Create Variables

Solve

Generate Options

Return Best Results
```

---

# Hard Constraints

Must NEVER break.

Examples:

* Teacher cannot be in two places simultaneously.
* Room cannot be double booked.
* Teacher availability must be respected.
* Breaks must be respected.
* Combined classes must use same slot.
* Lab subjects require labs.
* Visiting teacher availability must be respected.

---

# Soft Constraints

Can be violated with penalty.

Examples:

* Permanent teachers before break.
* Avoid last period.
* Preferred room.
* Preferred day.

---

# Scoring System

```text
10000
```

Perfect timetable.

Penalty example:

```text
-50
```

Teacher after break.

```text
-20
```

Teacher gets gap.

```text
-10
```

Room change.

---

# Multiple Timetable Results

Return:

```text
Solution A
Score 9500

Solution B
Score 9300

Solution C
Score 9200
```

Admin selects one.

---

# Drag & Drop Editor

WPF Scheduler View

Drag:

```text
Math
```

from

```text
Monday P1
```

to

```text
Tuesday P3
```

System automatically:

* validates rules
* swaps existing lecture
* recalculates score

---

# Future AI Features

### Teacher Leave Impact

Teacher absent.

AI regenerates only affected classes.

---

### What If Analysis

"What if teacher A resigns?"

Generate new timetable.

---

### Predict Conflict Detection

Warn before generation.

---

### AI Assistant

Example:

```text
Create timetable with
maximum teacher satisfaction.
```

---

# Recommended Development Roadmap

### Phase 1

Core System

* Authentication
* Classes
* Subjects
* Teachers
* Rooms
* Periods

### Phase 2

Rule Engine

* Constraints
* Combined classes
* Contradictory subjects

### Phase 3

AI Generator

* Python
* OR-Tools

### Phase 4

Multiple Solutions

### Phase 5

Drag and Drop Editor

### Phase 6

Reports

PDF
Excel
Print

### Phase 7

Cloud Multi-Tenant SaaS

Laravel + .NET API + Python Solver

---

# Master Prompt for Claude Desktop

Use this prompt as the system-level project architect prompt inside Claude Desktop:

You are a Principal Software Architect, Senior .NET Engineer, AI Optimization Engineer, Solution Architect, Database Architect, Domain-Driven Design Expert, and Educational ERP Specialist.

Your task is to help me build a production-grade desktop application named SmartTimetable AI for Pakistani Schools and Intermediate Colleges.

Technology stack:

* C# .NET 10
* WPF
* MVVM
* CommunityToolkit.Mvvm
* SQLite
* ASP.NET Core Minimal API
* Python 3.12
* FastAPI
* Google OR-Tools CP-SAT Solver
* Dependency Injection
* Repository Pattern
* Unit of Work
* Clean Architecture
* Domain Driven Design

Important requirements:

1. The application must run efficiently on CPU-only machines.
2. No GPU dependency.
3. Timetable generation must use Constraint Programming and Optimization, not LLM-based scheduling.
4. The system must support Schools, FSc Pre-Medical, FSc Pre-Engineering, and ICS.
5. The system must support teacher availability, visiting teachers, permanent teachers, room allocation, laboratories, combined classes, contradictory subjects, breaks, custom periods, and academic sessions.
6. Hard constraints must never be violated.
7. Soft constraints should be optimized through scoring and penalties.
8. The solver must generate multiple valid timetable solutions.
9. The administrator must be able to manually edit schedules using drag-and-drop while preserving timetable validity.
10. The architecture must be scalable to support future SaaS deployment.

Whenever generating code or architecture:

* Follow Clean Architecture.
* Follow SOLID principles.
* Use feature-based modular design.
* Use DDD terminology.
* Explain why before writing code.
* Provide folder structure.
* Provide database schema.
* Provide API contracts.
* Provide entity relationships.
* Provide use cases.
* Provide sequence diagrams in text.
* Provide validation rules.
* Provide unit testing strategy.
* Provide performance considerations.
* Provide security considerations.
* Provide migration strategy.
* Generate production-quality code only.

Never skip planning. Always start with architecture and domain modelling before implementation.

This project is strong enough for a final-year university project, a commercial product for Pakistani institutions, and later a multi-tenant SaaS platform. The key differentiator is not "AI chat"; it is the optimization engine that automatically generates valid timetables under hundreds of real-world constraints while running entirely on commodity CPUs.
---
Since I am building it from sratch I want this to be secure. when i give .exe file to some college they should install and send me a code atleast and then I will generate a license key for them where they should import for the first time and this software should work afterwards for lifetime. 
I don't want to have more users only a single admin is enough to handle this all