# Completing the Certificate Feature — Step by Step

## Audit: What Exists vs What's Missing

> [!CAUTION]
> Certificates are **much further behind** than Assessments. Almost everything needs to be built.

| Layer | Assessment | Certificate |
|---|---|---|
| Entity | ✅ Done | ✅ Done |
| EF Core Config | ✅ Done | ❌ Missing |
| `DbSet<>` in DbContext | ✅ Done | ✅ Done |
| Migration / DB Table | ✅ Done | ✅ Done |
| Interface (`IXService`) | ✅ Done | ❌ Missing |
| Service implementation | ✅ Done | ❌ Missing |
| DTOs | ✅ Done | ❌ Missing |
| DI Registration | ❌ Missing | ❌ Missing |
| Controller | ❌ Missing | ❌ Missing |

**You need to build 5 things for Certificates.**

---

## Step 1 — EF Core Configuration

### File: `Data/Configurations/CertificateConfiguration.cs` ← **New file**

### Why?
Without this, EF Core uses default conventions (no max lengths, no explicit FK behaviour).
The `AssessmentConfiguration` shows the pattern to follow.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Entities;

namespace TmsApi.Data.Configurations;

public class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable("Certificates");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.SerialNumber)
            .IsRequired()
            .HasMaxLength(100);     // e.g. "CERT-2026-00001"

        builder.Property(c => c.IssuedAt)
            .IsRequired();

        // A certificate belongs to one Student
        builder.HasOne(c => c.Student)
            .WithMany()
            .HasForeignKey(c => c.StudentId)
            .OnDelete(DeleteBehavior.Restrict); // Don't delete certs if student deleted

        // A certificate belongs to one Course
        builder.HasOne(c => c.Course)
            .WithMany()
            .HasForeignKey(c => c.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Enforce uniqueness: one certificate per student per course
        builder.HasIndex(c => new { c.StudentId, c.CourseId })
            .IsUnique();
    }
}
```

> [!NOTE]
> `modelBuilder.ApplyConfigurationsFromAssembly(...)` in `TmsDbContext` automatically picks this up — no manual registration needed.

---

## Step 2 — DTOs

### Why?
DTOs (Data Transfer Objects) are what goes over the wire — not the entity itself.
The entity is for the database; the DTO is for the API consumer.

---

### 2a. `Dtos/CertificateResponseDto.cs` ← **New file**

This is what the API **returns** when a caller reads a certificate.

```csharp
namespace TmsApi.Dtos;

public record CertificateResponseDto(
    int Id,
    string SerialNumber,
    DateTime IssuedAt,
    int StudentId,
    int CourseId
);
```

---

### 2b. `Dtos/IssueCertificateRequest.cs` ← **New file**

This is what the caller sends when **creating** a certificate.
We only need `StudentId` — the `CourseId` comes from the URL route.

```csharp
using System.ComponentModel.DataAnnotations;

namespace TmsApi.Dtos;

public record IssueCertificateRequest(
    [Required] int StudentId
);
```

> [!NOTE]
> We don't include `SerialNumber` or `IssuedAt` in the request — the server generates both automatically.

---

## Step 3 — Interface

### File: `Services/ICertificateService.cs` ← **New file**

### Why?
- Defines the **contract** (what operations exist) without exposing implementation.
- Allows the controller to depend on the abstraction, not the concrete class.
- Enables testability (you can mock the interface in tests).

```csharp
using TmsApi.Dtos;

namespace TmsApi.Services;

public interface ICertificateService
{
    // Issue (create) a certificate for a student in a course
    Task<CertificateResponseDto> IssueAsync(int courseId, IssueCertificateRequest request, CancellationToken ct);

    // Get one certificate by its ID within a course
    Task<CertificateResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct);

    // List all certificates for a course
    Task<IReadOnlyList<CertificateResponseDto>> GetByCourseAsync(int courseId, CancellationToken ct);

    // List all certificates for a student
    Task<IReadOnlyList<CertificateResponseDto>> GetByStudentAsync(int studentId, CancellationToken ct);

    // Revoke (delete) a certificate
    Task<bool> RevokeAsync(int courseId, int id, CancellationToken ct);
}
```

---

## Step 4 — Service Implementation

### File: `Services/CertificateService.cs` ← **New file**

### Why?
This is where the actual business logic lives.
Key decisions made here:
- `SerialNumber` is auto-generated (format: `CERT-{year}-{id:D5}`)
- Only one certificate allowed per student per course (checked before insert)

```csharp
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Dtos;
using TmsApi.Entities;

namespace TmsApi.Services;

public class CertificateService(TmsDbContext db, ILogger<CertificateService> logger)
    : ICertificateService
{
    public async Task<CertificateResponseDto> IssueAsync(
        int courseId,
        IssueCertificateRequest request,
        CancellationToken ct)
    {
        // Business rule: can't issue duplicate certificate for same student + course
        var alreadyExists = await db.Certificates
            .AnyAsync(c => c.StudentId == request.StudentId && c.CourseId == courseId, ct);

        if (alreadyExists)
            throw new InvalidOperationException(
                $"Student {request.StudentId} already has a certificate for course {courseId}.");

        var certificate = new Certificate
        {
            StudentId = request.StudentId,
            CourseId  = courseId,
            IssuedAt  = DateTime.UtcNow,
            SerialNumber = "PENDING" // temporary — updated after save gives us the Id
        };

        db.Certificates.Add(certificate);
        await db.SaveChangesAsync(ct);

        // Now we have the Id — generate a proper serial number
        certificate.SerialNumber = $"CERT-{DateTime.UtcNow.Year}-{certificate.Id:D5}";
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Issued certificate {SerialNumber} for student {StudentId} in course {CourseId}",
            certificate.SerialNumber, request.StudentId, courseId);

        return (await GetByIdAsync(courseId, certificate.Id, ct))!;
    }

    public Task<CertificateResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct) =>
        db.Certificates
            .AsNoTracking()
            .Where(c => c.CourseId == courseId && c.Id == id)
            .Select(c => new CertificateResponseDto(
                c.Id, c.SerialNumber, c.IssuedAt, c.StudentId, c.CourseId))
            .FirstOrDefaultAsync(ct);

    public Task<IReadOnlyList<CertificateResponseDto>> GetByCourseAsync(int courseId, CancellationToken ct) =>
        db.Certificates
            .AsNoTracking()
            .Where(c => c.CourseId == courseId)
            .Select(c => new CertificateResponseDto(
                c.Id, c.SerialNumber, c.IssuedAt, c.StudentId, c.CourseId))
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<CertificateResponseDto>)t.Result, ct);

    public Task<IReadOnlyList<CertificateResponseDto>> GetByStudentAsync(int studentId, CancellationToken ct) =>
        db.Certificates
            .AsNoTracking()
            .Where(c => c.StudentId == studentId)
            .Select(c => new CertificateResponseDto(
                c.Id, c.SerialNumber, c.IssuedAt, c.StudentId, c.CourseId))
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<CertificateResponseDto>)t.Result, ct);

    public async Task<bool> RevokeAsync(int courseId, int id, CancellationToken ct)
    {
        var cert = await db.Certificates
            .FirstOrDefaultAsync(c => c.CourseId == courseId && c.Id == id, ct);

        if (cert is null) return false;

        db.Certificates.Remove(cert);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Revoked certificate {CertId} from course {CourseId}", id, courseId);
        return true;
    }
}
```

---

## Step 5 — Register in DI

### File: [Program.cs](file:///d:/ab/C#/TmsApi/Program.cs) — same block as Step 1 of Assessment guide

Add **one more line**:

```csharp
builder.Services.AddScoped<IAssessmentService, AssessmentService>();   // ← from assessment task
builder.Services.AddScoped<ICertificateService, CertificateService>(); // ← ADD THIS
```

---

## Step 6 — Controller

### File: `Controllers/CertificatesController.cs` ← **New file**

### Route design:
Certificates are issued per course, but a student can also query all their own certificates:
```
GET    /api/courses/{courseId}/certificates       ← all certs for a course
GET    /api/courses/{courseId}/certificates/{id}  ← one cert
POST   /api/courses/{courseId}/certificates       ← issue cert (body has StudentId)
DELETE /api/courses/{courseId}/certificates/{id}  ← revoke cert
```

```csharp
using Microsoft.AspNetCore.Mvc;
using TmsApi.Dtos;
using TmsApi.Services;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/certificates")]
[Tags("Certificates")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class CertificatesController(
    ICertificateService certificateService,
    ICourseService courseService,
    IStudentService studentService) : ControllerBase
{
    // GET /api/courses/{courseId}/certificates
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CertificateResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("List all certificates for a course")]
    public async Task<IActionResult> GetCertificates(int courseId, CancellationToken ct)
    {
        var course = await courseService.GetByIdAsync(courseId, ct);
        if (course is null) return NotFound();

        var certs = await certificateService.GetByCourseAsync(courseId, ct);
        return Ok(certs);
    }

    // GET /api/courses/{courseId}/certificates/{id}
    [HttpGet("{id:int}", Name = nameof(GetCertificateById))]
    [ProducesResponseType(typeof(CertificateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get one certificate by ID")]
    public async Task<IActionResult> GetCertificateById(int courseId, int id, CancellationToken ct)
    {
        var cert = await certificateService.GetByIdAsync(courseId, id, ct);
        return cert is not null ? Ok(cert) : NotFound();
    }

    // POST /api/courses/{courseId}/certificates
    [HttpPost]
    [ProducesResponseType(typeof(CertificateResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Issue a certificate to a student")]
    [EndpointDescription("Issues a certificate. Returns 409 if the student already has one for this course.")]
    public async Task<IActionResult> IssueCertificate(
        int courseId,
        IssueCertificateRequest request,
        CancellationToken ct)
    {
        // Verify course exists
        var course = await courseService.GetByIdAsync(courseId, ct);
        if (course is null) return NotFound();

        // Verify student exists
        var student = await studentService.GetByIdAsync(request.StudentId, ct);
        if (student is null)
            return NotFound(new ProblemDetails
            {
                Title = "Student not found",
                Detail = $"Student {request.StudentId} does not exist.",
                Status = StatusCodes.Status404NotFound
            });

        try
        {
            var cert = await certificateService.IssueAsync(courseId, request, ct);
            return CreatedAtAction(nameof(GetCertificateById), new { courseId, id = cert.Id }, cert);
        }
        catch (InvalidOperationException ex)
        {
            // Service throws this when duplicate certificate detected
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate certificate",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    // DELETE /api/courses/{courseId}/certificates/{id}
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Revoke a certificate")]
    public async Task<IActionResult> RevokeCertificate(int courseId, int id, CancellationToken ct)
    {
        var revoked = await certificateService.RevokeAsync(courseId, id, ct);
        return revoked ? NoContent() : NotFound();
    }
}
```

---

## Full Checklist

- [ ] Step 1 — Create `Data/Configurations/CertificateConfiguration.cs`
- [ ] Step 2a — Create `Dtos/CertificateResponseDto.cs`
- [ ] Step 2b — Create `Dtos/IssueCertificateRequest.cs`
- [ ] Step 3 — Create `Services/ICertificateService.cs`
- [ ] Step 4 — Create `Services/CertificateService.cs`
- [ ] Step 5 — Add DI line in `Program.cs`
- [ ] Step 6 — Create `Controllers/CertificatesController.cs`

---

## How the pieces connect

```
HTTP Request
     │
     ▼
CertificatesController        ← Step 6
     │  (DI — Step 5)
     ▼
ICertificateService           ← Step 3
CertificateService            ← Step 4
     │
     ▼
TmsDbContext → Certificates table   ← already in DB ✅
     │
     └─ uses CertificateConfiguration  ← Step 1
```
