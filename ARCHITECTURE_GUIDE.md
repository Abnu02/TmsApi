# Unified Architecture Implementation Guide

## Pattern: MediatR CQRS + Hybrid Caching (v2)

All entities follow the same structure:

### 1️⃣ **Cache Keys** (shared file)
```csharp
// Infrastructure/Caching/CacheKeys.cs - ADD THESE LINES
public static string Student(int id) => $"{SchemaVersion}:student:{id}";
public static string StudentsAll => $"{SchemaVersion}:students:all";
public const string StudentsTag = "students";

public static string Assessment(int id) => $"{SchemaVersion}:assessment:{id}";
public static string AssessmentsAll => $"{SchemaVersion}:assessments:all";
public const string AssessmentsTag = "assessments";

public static string Certificate(int id) => $"{SchemaVersion}:certificate:{id}";
public static string CertificatesAll => $"{SchemaVersion}:certificates:all";
public const string CertificatesTag = "certificates";
```

---

### 2️⃣ **Commands Structure** (per entity)

#### Example: Student Commands
**File:** `Application/Students/Commands/CreateStudentCommand.cs`
```csharp
using MediatR;
using TmsApi.Application.Common;

namespace TmsApi.Application.Students.Commands;

public record CreateStudentCommand(
    string RegistrationNumber,
    string Name,
    decimal GPA) 
    : IRequest<Result<StudentCreated, StudentError>>;

public record StudentCreated(int Id, string RegistrationNumber, string Name);
```

**File:** `Application/Students/Commands/CreateStudentHandler.cs`
```csharp
using MediatR;
using TmsApi.Application.Common;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Students.Commands;

public class CreateStudentHandler(
    IStudentService studentService) 
    : IRequestHandler<CreateStudentCommand, Result<StudentCreated, StudentError>>
{
    public async Task<Result<StudentCreated, StudentError>> Handle(
        CreateStudentCommand command, CancellationToken ct)
    {
        var existing = await studentService.GetByRegistrationAsync(command.RegistrationNumber, ct);
        if (existing is not null)
            return Result<StudentCreated, StudentError>.Failure(
                StudentError.DuplicateRegistration(command.RegistrationNumber));

        var student = new Student
        {
            RegistrationNumber = command.RegistrationNumber,
            Name = command.Name,
            GPA = command.GPA,
            IsActive = true
        };

        var created = await studentService.CreateAsync(student, ct);
        return Result<StudentCreated, StudentError>.Success(
            new StudentCreated(created.Id, created.RegistrationNumber, created.Name));
    }
}
```

**File:** `Application/Students/Commands/CreateStudentValidator.cs`
```csharp
using FluentValidation;

namespace TmsApi.Application.Students.Commands;

public class CreateStudentValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentValidator()
    {
        RuleFor(x => x.RegistrationNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GPA).InclusiveBetween(0m, 4m);
    }
}
```

---

#### Similarly, add:
- `DeleteStudentCommand.cs` → `DeleteStudentHandler.cs` + validator
- `UpdateStudentCommand.cs` → `UpdateStudentHandler.cs` + validator

---

### 3️⃣ **Queries Structure** (per entity)

**File:** `Application/Students/Queries/GetAllStudentsQuery.cs`
```csharp
using MediatR;
using TmsApi.Application.DTOs;

namespace TmsApi.Application.Students.Queries;

public record GetAllStudentsQuery(int PageNumber = 1, int PageSize = 20)
    : IRequest<PagedResponse<StudentResponseDto>>;
```

**File:** `Application/Students/Queries/GetAllStudentsHandler.cs`
```csharp
using MediatR;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Students.Queries;

public class GetAllStudentsHandler(
    IStudentService studentService)
    : IRequestHandler<GetAllStudentsQuery, PagedResponse<StudentResponseDto>>
{
    public async Task<PagedResponse<StudentResponseDto>> Handle(
        GetAllStudentsQuery query, CancellationToken ct)
    {
        var pagedRequest = new PagedRequest { PageNumber = query.PageNumber, PageSize = query.PageSize };
        return await studentService.GetAllAsync(pagedRequest, ct);
    }
}
```

#### Similarly, add:
- `GetStudentByIdQuery.cs` → `GetStudentByIdHandler.cs`

---

### 4️⃣ **Caching Service** (per entity)

**File:** `Infrastructure/Services/CachedStudentService.cs`
```csharp
using Microsoft.Extensions.Caching.Hybrid;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Caching;

namespace TmsApi.Infrastructure.Services;

public class CachedStudentService(
    IStudentService inner,
    HybridCache cache)
    : ICachedStudentService
{
    public async Task<StudentResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var cacheKey = CacheKeys.Student(id);
        return await cache.GetOrCreateAsync(
            cacheKey,
            async cancel => await inner.GetByIdAsync(id, cancel),
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            },
            tags: [CacheKeys.StudentsTag],
            cancellationToken: ct);
    }

    public async Task InvalidateStudentCacheAsync(CancellationToken ct)
    {
        await cache.RemoveByTagAsync(CacheKeys.StudentsTag, ct);
    }
}
```

**File:** `Application/Interfaces/ICachedStudentService.cs`
```csharp
using TmsApi.Application.DTOs;

namespace TmsApi.Application.Interfaces;

public interface ICachedStudentService
{
    Task<StudentResponseDto?> GetByIdAsync(int id, CancellationToken ct);
    Task InvalidateStudentCacheAsync(CancellationToken ct);
}
```

---

### 5️⃣ **v2 Controller** (per entity)

**File:** `Api/Controllers/V2/StudentsController.cs`
```csharp
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs;
using TmsApi.Application.Students.Commands;
using TmsApi.Application.Students.Queries;

namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v{version:apiVersion}/students")]
[ApiVersion("2.0")]
public class StudentsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(StudentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStudent(
        CreateStudentCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Match<IActionResult>(
            onSuccess: created => CreatedAtAction(
                nameof(GetStudentById),
                new { id = created.Id },
                created),
            onFailure: error => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Student creation failed",
                detail: error.Message));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(StudentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentById(int id, CancellationToken ct)
    {
        var student = await mediator.Send(new GetStudentByIdQuery(id), ct);
        return student is not null ? Ok(student) : NotFound();
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<StudentResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllStudents(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetAllStudentsQuery(pageNumber, pageSize), ct);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStudent(
        int id,
        UpdateStudentCommand command,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            command with { Id = id }, ct);
        return result.Match<IActionResult>(
            onSuccess: _ => NoContent(),
            onFailure: error => error.Code == "not_found"
                ? NotFound()
                : Problem(detail: error.Message));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStudent(int id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteStudentCommand(id), ct);
        return result.Match<IActionResult>(
            onSuccess: _ => NoContent(),
            onFailure: error => error.Code == "not_found"
                ? NotFound()
                : Problem(detail: error.Message));
    }
}
```

---

### 6️⃣ **Register in DI** (Program.cs)

```csharp
// After existing service registrations:

// Students v2 (MediatR)
builder.Services.AddScoped<ICachedStudentService, CachedStudentService>();
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateStudentCommand).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(CreateStudentValidator).Assembly);

// Assessments v2
builder.Services.AddScoped<ICachedAssessmentService, CachedAssessmentService>();

// Certificates v2
builder.Services.AddScoped<ICachedCertificateService, CachedCertificateService>();
```

---

## 📋 **Checklist for Each Entity**

For **Students**, **Assessments**, **Certificates**, create:

### Commands
- [ ] `Create[Entity]Command.cs` + `Handler` + `Validator`
- [ ] `Update[Entity]Command.cs` + `Handler` + `Validator`
- [ ] `Delete[Entity]Command.cs` + `Handler`

### Queries
- [ ] `GetAll[Entity]sQuery.cs` + `Handler`
- [ ] `GetById[Entity]Query.cs` + `Handler`

### Caching
- [ ] `Cached[Entity]Service.cs`
- [ ] `ICached[Entity]Service.cs` interface
- [ ] Add cache keys to `CacheKeys.cs`

### API
- [ ] `V2/[Entity]sController.cs`

### DI
- [ ] Register in `Program.cs`

---

## 🎯 **Priority Order**

1. **Students** (heavily used, simple logic)
2. **Assessments** (new, moderate complexity)
3. **Certificates** (complex, can be async)
4. **Courses** (already partially done, finalize)

