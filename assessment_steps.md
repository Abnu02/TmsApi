# Completing the Assessment Feature — Step by Step

## Overview

You have already built everything: the entity, the database migration, the service, and the DTOs.
You are only missing **two things** to make assessments usable via HTTP.

---

## Step 1 — Register `IAssessmentService` in DI

### File: [Program.cs](file:///d:/ab/C#/TmsApi/Program.cs)

### Why?
ASP.NET Core uses **Dependency Injection (DI)**. When the controller asks for an
`IAssessmentService`, the DI container must know which class to hand it.
Without this line, the app **crashes at startup** (because `ValidateOnBuild = true`).

### What to add:
Find the block where the other services are registered (around line 32):

```csharp
// BEFORE (current state)
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
```

Add one line:

```csharp
// AFTER
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IAssessmentService, AssessmentService>(); // ← ADD THIS
```

### What `AddScoped` means:
- A **new instance** of `AssessmentService` is created per HTTP request.
- It is disposed when the request ends.
- This is correct for services that use `DbContext` (which is also scoped).

---

## Step 2 — Create `AssessmentsController`

### File: `Controllers/AssessmentsController.cs` ← **New file**

### Why?
The service has all the business logic but it is not exposed over HTTP yet.
The controller is the layer that:
- Maps HTTP routes → service method calls
- Returns the correct HTTP status codes (200, 201, 404, etc.)

### The route design:
Assessments belong to a course, so the URL is **nested**:
```
GET    /api/courses/{courseId}/assessments          ← list all assessments for a course
GET    /api/courses/{courseId}/assessments/{id}     ← get one assessment
POST   /api/courses/{courseId}/assessments          ← create assessment
PUT    /api/courses/{courseId}/assessments/{id}     ← update assessment
DELETE /api/courses/{courseId}/assessments/{id}     ← delete assessment
```

### The full controller code:

```csharp
using Microsoft.AspNetCore.Mvc;
using TmsApi.Dtos;
using TmsApi.Services;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/assessments")]  // ← nested under courses
[Tags("Assessments")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class AssessmentsController(
    IAssessmentService assessmentService,      // ← injected by DI (Step 1 enables this)
    ICourseService courseService) : ControllerBase
{
    // GET /api/courses/{courseId}/assessments
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AssessmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("List assessments for a course")]
    public async Task<IActionResult> GetAssessments(
        int courseId,
        [FromQuery] PagedRequest request,
        CancellationToken ct)
    {
        // First verify the course exists, return 404 if not
        var courseExists = await courseService.GetByIdAsync(courseId, ct);
        if (courseExists is null)
            return NotFound();

        var result = await assessmentService.GetByCourseAsync(courseId, request, ct);
        return Ok(result);
    }

    // GET /api/courses/{courseId}/assessments/{id}
    [HttpGet("{id:int}", Name = nameof(GetAssessmentById))]
    [ProducesResponseType(typeof(AssessmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get one assessment by ID")]
    public async Task<IActionResult> GetAssessmentById(int courseId, int id, CancellationToken ct)
    {
        var assessment = await assessmentService.GetByIdAsync(courseId, id, ct);
        return assessment is not null ? Ok(assessment) : NotFound();
    }

    // POST /api/courses/{courseId}/assessments
    [HttpPost]
    [ProducesResponseType(typeof(AssessmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Create an assessment for a course")]
    public async Task<IActionResult> CreateAssessment(
        int courseId,
        CreateAssessmentRequest request,
        CancellationToken ct)
    {
        // Guard: course must exist before creating an assessment in it
        var courseExists = await courseService.GetByIdAsync(courseId, ct);
        if (courseExists is null)
            return NotFound();

        var created = await assessmentService.CreateAsync(courseId, request, ct);

        // 201 Created with a Location header pointing to the new resource
        return CreatedAtAction(nameof(GetAssessmentById), new { courseId, id = created.Id }, created);
    }

    // PUT /api/courses/{courseId}/assessments/{id}
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Update an assessment")]
    public async Task<IActionResult> UpdateAssessment(
        int courseId,
        int id,
        UpdateAssessmentRequest request,
        CancellationToken ct)
    {
        var updated = await assessmentService.UpdateAsync(courseId, id, request, ct);
        return updated ? NoContent() : NotFound();   // 204 = success, 404 = not found
    }

    // DELETE /api/courses/{courseId}/assessments/{id}
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Delete an assessment")]
    public async Task<IActionResult> DeleteAssessment(int courseId, int id, CancellationToken ct)
    {
        var deleted = await assessmentService.DeleteAsync(courseId, id, ct);
        return deleted ? NoContent() : NotFound();   // 204 = success, 404 = not found
    }
}
```

---

## Summary of Status Codes Used

| Method | Success | Failure |
|--------|---------|---------|
| GET (list) | `200 OK` with paged list | `404` if course not found |
| GET (single) | `200 OK` with item | `404` if assessment not found |
| POST | `201 Created` + Location header | `404` if course not found |
| PUT | `204 No Content` | `404` if assessment not found |
| DELETE | `204 No Content` | `404` if assessment not found |

---

## How the pieces connect

```
HTTP Request
     │
     ▼
AssessmentsController   ← you are adding this (Step 2)
     │  (injected by DI — Step 1 enables this)
     ▼
IAssessmentService / AssessmentService  ← already done ✅
     │
     ▼
TmsDbContext → PostgreSQL (Assessments table)  ← already done ✅
```

---

## After you add both — build to verify

```bash
dotnet build
```

If no errors, run the app and open Scalar UI at `/scalar/v1` to see the new endpoints.
