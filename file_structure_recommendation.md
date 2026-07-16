# TmsApi – Recommended File Structure

## Current State (Quick Audit)

| Issue | Location | Severity |
|---|---|---|
| `WeatherForecast.cs` – scaffold leftover | Root | 🔴 Delete |
| `PaymentOptions.cs` – loose in root | Root | 🟡 Move to `Options/` |
| `TrainingAuthHandler.cs` – loose in root | Root | 🟡 Move to `Auth/` |
| `Model/Models.cs` – single file, vague name | `Model/` | 🟡 Merge or rename |
| `Services/changed.cs` – temp debug file | `Services/` | 🔴 Delete |
| `assessment_steps.md` / `certificate_steps.md` – docs in project root | Root | 🟡 Move to `docs/` |
| Interfaces (`IXxxService.cs`) mixed with implementations | `Services/` | 🟡 Split into sub-folders |
| `IAssessmentService` not registered in DI | `Program.cs` | 🟡 Bug fix |
| Seed data inlined in `Program.cs` | `Program.cs` | 🟡 Already have `DataSeeder.cs` – use it exclusively |

---

## Recommended Structure

```
TmsApi/
│
├── Auth/                          # ← NEW (move from root)
│   └── TrainingAuthHandler.cs
│
├── Controllers/                   # ✅ Keep as-is
│   ├── CoursesController.cs
│   ├── EnrollmentsController.cs
│   ├── ReportingController.cs
│   ├── StudentsController.cs
│   └── TestController.cs
│
├── Data/                          # ✅ Keep, minor clean-up
│   ├── Configurations/
│   │   └── (EF entity configs)
│   ├── DataSeeder.cs
│   └── TmsDbContext.cs
│
├── Dtos/                          # ✅ Keep – consider sub-folders once it grows
│   ├── Requests/                  # ← NEW sub-folder split
│   │   ├── CreateAssessmentRequest.cs
│   │   ├── CreateCourseRequest.cs
│   │   ├── CreateStudentRequest.cs
│   │   ├── EnrollStudentRequest.cs
│   │   ├── UpdateAssessmentRequest.cs
│   │   └── UpdateStudentRequest.cs
│   ├── Responses/                 # ← NEW sub-folder split
│   │   ├── AssessmentResponseDto.cs
│   │   ├── CourseDetailDto.cs
│   │   ├── CourseResponseDto.cs
│   │   ├── EnrollmentResponseDto.cs
│   │   └── StudentResponseDto.cs
│   ├── LinkDto.cs                 # shared shape
│   ├── PagedRequest.cs
│   └── PagedResponse.cs
│
├── Entities/                      # ✅ Keep as-is
│   ├── Assessment.cs
│   ├── Certificate.cs
│   ├── Course.cs
│   ├── Enrollment.cs
│   └── Student.cs
│
├── Filters/                       # ✅ Keep as-is
│   └── AuditLogFilter.cs
│
├── Middleware/                    # ✅ Keep as-is
│   └── RequestLoggingMiddleware.cs
│
├── Migrations/                    # ✅ Auto-generated – do not touch manually
│
├── Options/                       # ← NEW (move from root)
│   └── PaymentOptions.cs
│
├── Services/                      # 🟡 Split interfaces from implementations
│   ├── Interfaces/                # ← NEW sub-folder
│   │   ├── IAssessmentService.cs
│   │   ├── ICourseService.cs
│   │   ├── IEnrollmentService.cs
│   │   └── IStudentService.cs
│   ├── AssessmentService.cs
│   ├── CourseService.cs
│   ├── EnrollmentService.cs
│   ├── EnrollmentWorker.cs
│   └── StudentService.cs
│
├── docs/                          # ← NEW
│   ├── assessment_steps.md
│   └── certificate_steps.md
│
├── Properties/
│   └── launchSettings.json
│
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── TmsApi.csproj
└── .gitignore
```

---

## Reasoning

### 1. Keep the Flat Feature Layout (No "Feature Folders" Yet)

Your domain has **5 entities** (Student, Course, Enrollment, Assessment, Certificate). A feature-folder layout (one folder per feature containing its controller, service, DTOs) only pays off when features number in the teens or when teams own different slices independently. At this size, **layer folders** (Controllers / Services / Entities / Dtos) keep the project navigable with zero ceremony.

> **Rule of thumb**: Switch to feature folders when any single layer folder exceeds ~15 files.

---

### 2. Auth/ Folder

`TrainingAuthHandler.cs` is the seed of an **authentication** layer. Keeping it loose in the root next to `Program.cs` makes it easy to overlook. Moving it to `Auth/` signals "here is where auth lives" and leaves room for future handlers, requirements, and policies.

---

### 3. Options/ Folder

`PaymentOptions.cs` is an **Options pattern** class. It is not a controller, service, entity, or DTO. A dedicated `Options/` folder is conventional in .NET projects and scales naturally (e.g., `DatabaseOptions.cs`, `EmailOptions.cs`).

---

### 4. Dtos/Requests + Dtos/Responses

You already have 14 DTO files. Splitting them into `Requests/` and `Responses/` sub-folders provides:

- **Instant context** – you know what a file is for before opening it.
- **Reduced noise** – when working on a controller endpoint you only browse `Requests/`, not 14 mixed files.
- Namespaces stay `TmsApi.Dtos` (or use `TmsApi.Dtos.Requests`) – your choice.

---

### 5. Services/Interfaces/ Sub-folder

Mixing `IXxxService.cs` and `XxxService.cs` in the same folder means every file-open dialog shows paired names. Putting interfaces in a sub-folder:

- Makes it explicit that the interfaces are the **contract** and the implementations are the **detail**.
- Mirrors the popular convention used in large .NET repos (e.g., ASP.NET Core itself).

Alternatively, some teams co-locate them (same file or `IFoo.cs` right next to `Foo.cs`) – that's acceptable too. The key is **consistency**.

---

### 6. docs/ Folder for Markdown Planning Files

`assessment_steps.md` and `certificate_steps.md` are developer planning notes. They don't belong in the project root (they confuse the root with both C# entry points and documentation). A `docs/` folder is the universal convention.

---

### 7. Clean Up Root-Level Noise

| File | Action | Why |
|---|---|---|
| `WeatherForecast.cs` | **Delete** | Scaffold leftover, unused |
| `Services/changed.cs` | **Delete** | Temp/debug file |
| `TmsApi.http` | Keep | Useful for manual testing |

---

### 8. Program.cs – Move Seed Data Out

The inline seed block (lines 109–141 in `Program.cs`) duplicates the role of `DataSeeder.cs`. Consolidate all seeding into `DataSeeder.cs` to keep `Program.cs` as a **composition root only** (register services, build pipeline, run app). This makes `Program.cs` under 80 lines and keeps seeding logic testable independently.

---

## What NOT to Do Yet

- ❌ **Don't add a Repository layer** unless you need to swap data sources or unit-test without a real DB. You have EF Core + service layer, which is already a sufficient abstraction.
- ❌ **Don't split into multiple projects** (`.Core`, `.Infrastructure`, `.Api`). That's appropriate for large teams with strict dependency rules; at this scale it adds build overhead with no real benefit.
- ❌ **Don't use feature folders** until the flat layer folders genuinely become hard to navigate.
