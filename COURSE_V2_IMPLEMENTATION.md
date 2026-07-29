# Course v2 Implementation Guide

This document describes how to standardize the `Course` entity on the v2 CQRS/MediatR pattern used elsewhere in the project. It gives step-by-step instructions, rationale, and ready-to-copy code snippets.

## Goal

- Move create/update/delete business logic into MediatR command handlers.
- Keep controllers thin (HTTP ↔ commands/queries).
- Use validators (FluentValidation) in the MediatR pipeline.
- Invalidate hybrid cache (`ICachedCourseService`) on writes.
- Return typed results with `Result<T, TError>` so controllers map to HTTP status codes easily.

---

## Files to add (paths relative to repo root)

- `TmsApi.Application/Common/CourseError.cs`
- `TmsApi.Application/Courses/Commands/CreateCourseCommand.cs`
- `TmsApi.Application/Courses/Commands/CreateCourseHandler.cs`
- `TmsApi.Application/Courses/Commands/CreateCourseValidator.cs`
- `TmsApi.Application/Courses/Commands/UpdateCourseCommand.cs`
- `TmsApi.Application/Courses/Commands/UpdateCourseHandler.cs`
- `TmsApi.Application/Courses/Commands/UpdateCourseValidator.cs`
- `TmsApi.Application/Courses/Commands/DeleteCourseCommand.cs`
- `TmsApi.Application/Courses/Commands/DeleteCourseHandler.cs`
- `TmsApi.Application/Courses/Queries/GetCoursesQuery.cs`
- `TmsApi.Application/Courses/Queries/GetCoursesHandler.cs`
- `TmsApi.Application/Courses/Queries/GetCourseByIdQuery.cs`
- `TmsApi.Application/Courses/Queries/GetCourseByIdHandler.cs`

---

## 1) `CourseError` (typed failures)

Create `TmsApi.Application/Common/CourseError.cs`:

```csharp
namespace TmsApi.Application.Common;

public sealed record CourseError(string Code, string Message)
{
    public static CourseError NotFound(int id) => new("not_found", $"Course with id {id} not found.");
    public static CourseError DuplicateCode(string code) => new("duplicate_code", $"Course with code '{code}' already exists.");
    public static CourseError Invalid(string message) => new("invalid", message);
}
```

Why: typed errors make mapping to HTTP responses predictable and unit-testable.

---

## 2) Commands, Handlers and Validators (write side)

Create a `Commands` folder under `TmsApi.Application/Courses` and add the following examples.

Example: `CreateCourseCommand.cs`:

```csharp
using MediatR;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;

namespace TmsApi.Application.Courses.Commands;

public sealed record CreateCourseCommand(CreateCourseRequest Request) : IRequest<Result<CourseCreated, CourseError>>;

public sealed record CourseCreated(int Id, string Code, string Title);
```

Example: `CreateCourseHandler.cs` (orchestrates create + cache invalidation):

```csharp
using MediatR;
using TmsApi.Application.Common;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Courses.Commands;

public class CreateCourseHandler(
    ICourseService courseService,
    ICachedCourseService cachedService)
    : IRequestHandler<CreateCourseCommand, Result<CourseCreated, CourseError>>
{
    public async Task<Result<CourseCreated, CourseError>> Handle(CreateCourseCommand request, CancellationToken ct)
    {
        var dto = request.Request;
        if (await courseService.CodeExistsAsync(dto.Code, ct))
            return Result<CourseCreated, CourseError>.Failure(CourseError.DuplicateCode(dto.Code));

        var created = await courseService.CreateAsync(dto, ct);
        await cachedService.InvalidateCourseCacheAsync(ct);
        return Result<CourseCreated, CourseError>.Success(new CourseCreated(created.Id, created.Code, created.Title));
    }
}
```

Validator example: `CreateCourseValidator.cs`:

```csharp
using FluentValidation;

namespace TmsApi.Application.Courses.Commands;

public class CreateCourseValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseValidator()
    {
        RuleFor(x => x.Request.Code).NotEmpty().Matches("^[A-Z]{3}-\\d{3}$");
        RuleFor(x => x.Request.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.MaxCapacity).InclusiveBetween(1, 200);
    }
}
```

Repeat the pattern for `UpdateCourseCommand` / `UpdateCourseHandler` (call `ICourseService.UpdateAsync`) and `DeleteCourseCommand` / `DeleteCourseHandler` (call `ICourseService.DeleteAsync`). Always invalidate `ICachedCourseService` on success.

---

## 3) Queries (read side)

Keep reads simple – they can use `ICachedCourseService` or call `ICourseService`.

Example: `GetCourseByIdQuery.cs` + handler:

```csharp
using MediatR;
using TmsApi.Application.DTOs;

public sealed record GetCourseByIdQuery(int Id) : IRequest<CourseResponseDto?>;

public class GetCourseByIdHandler(ICourseService service) : IRequestHandler<GetCourseByIdQuery, CourseResponseDto?>
{
    public async Task<CourseResponseDto?> Handle(GetCourseByIdQuery request, CancellationToken ct)
        => await service.GetByIdAsync(request.Id, ct);
}
```

For list queries prefer `ICachedCourseService.GetAllCoursesAsync(ct)` for stampede protection.

---

## 4) Controller changes

Update `TmsApi.Api/Controllers/V2/CoursesController.cs` to use `IMediator` for write operations and either `ICachedCourseService` or MediatR queries for reads.

Example POST and PUT handlers:

```csharp
[HttpPost]
public async Task<IActionResult> CreateCourse([FromBody] CreateCourseRequest request, [FromServices] IMediator mediator, CancellationToken ct)
{
    var result = await mediator.Send(new CreateCourseCommand(request), ct);
    return result.Match<IActionResult>(
        onSuccess: created => CreatedAtAction(nameof(GetCourseById), new { id = created.Id }, created),
        onFailure: err => err.Code == "duplicate_code" ? Conflict(err.Message) : Problem(detail: err.Message));
}

[HttpPut("{id}")]
public async Task<IActionResult> UpdateCourse(int id, [FromBody] CreateCourseRequest request, [FromServices] IMediator mediator, CancellationToken ct)
{
    var result = await mediator.Send(new UpdateCourseCommand(id, request.Title, request.MaxCapacity), ct);
    return result.Match<IActionResult>(
        onSuccess: _ => NoContent(),
        onFailure: err => err.Code == "not_found" ? NotFound() : Problem(detail: err.Message));
}
```

Why: controllers stay focused on HTTP mapping; business logic, validation, cache invalidation live inside handlers.

---

## 5) DI and registration

Ensure these are present in `Program.cs` (they likely already are):

```csharp
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SomeHandlerInApplication).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(SomeValidatorInApplication).Assembly);
builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();
```

If you added new handler types in `TmsApi.Application`, `AddMediatR` should pick them up when registering that assembly.

---

## 6) Build & run (local)

Commands:

```powershell
dotnet build
dotnet run --project TmsApi.Api/TmsApi.Api.csproj
```

Test with `curl` or Postman (examples):

```bash
curl -X POST https://localhost:5001/api/v2/courses \
  -H "Content-Type: application/json" \
  -d '{"code":"CSE-101","title":"Intro to CS","maxCapacity":50}'

curl https://localhost:5001/api/v2/courses
```

---

## 7) Testing recommendations

- Unit test handlers: mock `ICourseService` and `ICachedCourseService`. Assert on success and failure flows.
- Integration test: run the web app with a test DB (or in-memory) and call the real HTTP endpoints.

---

## Checklist (copy into your tracker)

- [ ] Add `CourseError` type
- [ ] Add `CreateCourse` command/handler/validator
- [ ] Add `UpdateCourse` command/handler/validator
- [ ] Add `DeleteCourse` command/handler
- [ ] Add `GetCourseById` and `GetCourses` queries/handlers
- [ ] Update `V2/CoursesController` to use `IMediator`
- [ ] Build and run; fix compile errors
- [ ] Write unit tests for handlers

---

If you want, I can now generate these files for you automatically, or I can walk you through adding the first command and handler step-by-step. Tell me which you prefer.
