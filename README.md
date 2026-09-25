# SmartTimetable AI

An offline, AI-assisted academic timetable generator for Pakistani schools and
intermediate colleges (Grades 1–10 and FSc **Pre-Medical**, **Pre-Engineering**,
and **ICS** programs). It replaces the hand-drawn, endlessly-patched wall chart
with a constraint solver that produces conflict-free timetables in seconds, and
ships as a **single Windows `.exe`** with **machine-locked, lifetime, offline
licensing** — one purchase, one computer, no internet required.

> Status: MVP under active development. The Domain, Application, Licensing,
> Infrastructure, Solver, and Avalonia Desktop layers are implemented. Build and
> publish on Windows with the scripts below.

---

## What it does

- **Data setup** — academic sessions, programs, classes/sections, subjects,
  teachers (with the subjects they can teach), rooms/labs, and the weekly period
  grid, including a break period.
- **Weekly demand** — per class, "this subject runs N periods a week, preferably
  with this teacher."
- **AI generation** — a Google OR-Tools **CP-SAT** solver places every lesson so
  that no teacher, class, or room is ever double-booked, weekly counts are exact,
  labs land in lab rooms, and teacher availability is respected. Multiple
  **strategies** (teacher-friendly, student-friendly, room-optimised, balanced)
  each yield a scored candidate timetable so the admin can compare and keep one.
- **Review & export** — view any candidate by class, keep the chosen one, and
  export to CSV.
- **Single admin** — one password-protected administrator account (PBKDF2), no
  multi-user complexity.

---

## Tech stack

| Concern | Choice |
|---|---|
| Language / runtime | C# on **.NET 10** (`net10.0`) |
| Desktop UI | **Avalonia UI 11.2** + Fluent theme, MVVM via CommunityToolkit.Mvvm |
| Solver | **Google OR-Tools (CP-SAT)**, in-process (`Google.OrTools` NuGet) |
| Data | **SQLite** via EF Core 9 (`EnsureCreated` + seeded demo data) |
| Composition | Microsoft.Extensions.DependencyInjection |
| Licensing | **ECDSA P-256** signed, machine-locked, offline license tokens |
| Architecture | Clean Architecture + DDD + Repository / Unit of Work |

Everything is in-process C#: no Python, no bundled web server, no listening port.
That keeps the deliverable to one self-contained binary and a small attack surface.

---

## Architecture

The dependency rule points inward — outer layers depend on inner ones, never the
reverse.

```
Domain          entities, value objects, rules.            (no dependencies)
Application  →  Domain. Use-cases, DTOs, service interfaces.
Licensing       machine fingerprint, request code, signed token, validator. (no dependencies)
Infrastructure → Application, Domain, Licensing. EF Core (SQLite), repositories, UnitOfWork.
Solver       →  Application, Domain. CP-SAT model, strategy profiles, solution extraction.
Desktop      →  Application, Infrastructure, Solver, Licensing. Views, ViewModels, DI root.
tools/LicenseGenerator → Licensing. Vendor-only CLI (never shipped to customers).
tests        →  Domain, Application, Licensing, Infrastructure, Solver.
```

The concrete OR-Tools solver is registered by the Desktop composition root, not by
Infrastructure, so the OR-Tools native dependency stays out of the data layer.
See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the full domain model and
solver design (note: that document is the original planning baseline and predates
the switch from WPF to Avalonia).

---

## Prerequisites

- Windows 10/11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) — verify with `dotnet --version`

No other tooling is required to build. The published `.exe` is self-contained, so
**colleges do not need .NET installed** to run it.

---

## Build & run (developer machine)

From the repository root:

```bat
build.bat
```

This restores packages, builds the whole solution in Release, and runs the tests.
To run the app during development:

```bat
dotnet run --project src\SmartTimetable.Desktop\SmartTimetable.Desktop.csproj -c Release
```

On first launch the app creates and seeds its SQLite database under
`%LocalAppData%\SmartTimetable`.

---

## Publish the single `.exe`

```bat
publish.bat
```

This produces one self-contained file at `publish\SmartTimetable.exe` (win-x64).
Notes baked into the script:

- **Self-contained** — the .NET runtime is bundled inside the exe.
- **`IncludeNativeLibrariesForSelfExtract`** is required because OR-Tools ships
  native code that is extracted on first launch.
- **Trimming is intentionally disabled** — it breaks Avalonia's XAML reflection and
  OR-Tools' native bindings.

Hand `SmartTimetable.exe` to the college. That single file is the installer.

---

## Licensing (vendor workflow)

The goal: sell to a college, one purchase = one computer, lifetime, fully offline.
The app embeds only the **public** key; the **private** key never leaves your
machine and is never committed. Full detail lives in
[`docs/LICENSING.md`](docs/LICENSING.md); the essentials:

**One-time setup (your machine only)**

1. Generate a key pair:
   ```bat
   SmartTimetable.LicenseGenerator keygen --out D:\vault\stt-keys
   ```
   This writes `vendor_private.pem` (passphrase-encrypted) and `vendor_public.txt`.
2. Back up `vendor_private.pem` **and** its passphrase offline. If you lose them you
   can never issue licenses for any build carrying the matching public key.
3. Paste the public-key line into
   `src/SmartTimetable.Licensing/VendorPublicKey.cs` → `SpkiBase64`, then rebuild
   and publish.

**Per customer**

```
College PC                              You (vendor)
----------                              ------------
Open app → "Activate" tab
App shows a REQUEST CODE   ─────────▶   LicenseGenerator issue --request <code> --customer "College"
(e.g. AEPUU-JRBC4-35THZ-…)               → SmartTimetable-<College>-<id>.lic  (+ a one-line key)
Paste the key / open .lic  ◀─────────   send by email or WhatsApp
App verifies offline, stores it
Works forever — on that PC only
```

Handy commands: `decode --request <code>` (inspect what the customer sent),
`issue … --expires 2027-08-31` (time-limited/trial), `verify --license file.lic`.
Every issued license is appended to `issued_licenses.csv` — your customer register.

> **Security note — configure the key before shipping.** While
> `VendorPublicKey.SpkiBase64` is empty, the app runs **unlocked in development
> mode** (no activation required). That is convenient for building and testing, but
> a build shipped with an empty key is unlocked for everyone. Always paste your
> public key and rebuild before giving the `.exe` to a college.

The request code is derived from up to five hardware identifiers (Windows
MachineGuid, SMBIOS UUID, BIOS serial, motherboard serial, first non-USB disk
serial). A license stores those hashes with a minimum-match count, so a Windows
reinstall or a new disk keeps the license valid, but copying it to another PC does
not. This is a realistic anti-copy scheme, not DRM against a determined reverse
engineer — see `docs/LICENSING.md` for the honest limits and hardening steps
(obfuscation, code-signing, multiple check points).

---

## Project structure

```
SmartTimetable/
├─ SmartTimetable.slnx           solution
├─ build.bat                     restore + build + test
├─ publish.bat                   single-file self-contained .exe
├─ docs/
│  ├─ ARCHITECTURE.md            domain model + solver design (planning baseline)
│  └─ LICENSING.md               full licensing design & operations
├─ src/
│  ├─ SmartTimetable.Domain/         entities, value objects, rules
│  ├─ SmartTimetable.Application/    use-cases, DTOs, service interfaces
│  ├─ SmartTimetable.Licensing/      fingerprint, request code, validator
│  ├─ SmartTimetable.Infrastructure/ EF Core (SQLite), repositories, licensing adapter
│  ├─ SmartTimetable.Solver/         CP-SAT model + strategies
│  └─ SmartTimetable.Desktop/        Avalonia UI, ViewModels, DI composition root
├─ tools/
│  └─ SmartTimetable.LicenseGenerator/  vendor-only CLI (keep private)
└─ tests/
   └─ SmartTimetable.Tests/         unit tests
```

---

## Roadmap

| Phase | Scope | Status |
|---|---|---|
| 0 | Solution scaffold, licensing, license generator, tests | done |
| 1 | Domain + EF Core schema + admin setup + CRUD screens | done |
| 2 | Rule engine (availability, combined classes, exclusions) | in progress |
| 3 | CP-SAT model + feasibility pre-check | in progress |
| 4 | Multiple scored solutions + comparison UI | in progress |
| 5 | Drag-and-drop editor with live validation | planned |
| 6 | Reports: PDF / Excel / print per class, teacher, room | planned |
| 7 | SaaS: API host, multi-tenant, online activation | future |

---

## Repository

<https://github.com/itsmrsarfaraz/SmartTimetable>

The vendor private key, the license generator's output, and any `*private*.pem` are
git-ignored and must never be committed or shipped.
