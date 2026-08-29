using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TmsApi.Application.Interfaces;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;
[Authorize(Roles = "Instructor,Admin")]
[ApiController]
[Route("api/courses")]
[Tags("Courses")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]

public class CoursesController(ICourseService courseService, LinkGenerator linkGenerator) : ControllerBase
{
    // [HttpGet]
    // public async Task<IActionResult> GetAll(CancellationToken ct)
    // {
    //     var courses = await courseService.GetAllAsync(ct);
    //     return Ok(courses);
    // }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CourseResponseDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List courses with pagination")]
    [EndpointDescription("Returns a paginated, optionally filtered listof TMS courses. PageSize is capped at 50.")]
    public async Task<IActionResult> GetCourses([FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await courseService.GetCoursesAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}", Name = nameof(GetCourseById))]
    [ProducesResponseType(typeof(CourseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get a course by ID")]
    [EndpointDescription("Returns course details with HATEOAS links.Returns 404 if the course does not exist.")]
    public async Task<IActionResult> GetCourseById(int id, CancellationToken ct)
    {
        var course = await courseService.GetByIdAsync(id, ct);
        if (course is null)
            return NotFound();


        var selfHref = linkGenerator.GetPathByName(HttpContext, nameof(GetCourseById), new { id }) ?? $"/api/courses/{id}";
        var enrollmentsHref = linkGenerator.GetPathByName(HttpContext, "ListCourseEnrollments", new { courseId = id }) ?? $"/api/courses/{id}/enrollments";


        var links = new List<LinkDto>
        {
            new LinkDto(Href: selfHref, Rel: "self", Method: "GET"),
            new LinkDto(Href: selfHref, Rel: "update", Method: "PUT"),
            new LinkDto(Href: selfHref, Rel: "delete", Method: "DELETE"),
            new LinkDto(Href: enrollmentsHref, Rel: "enrollments", Method: "GET")
        };


        if (course.EnrollmentCount < course.MaxCapacity)
        {
            links.Add(new LinkDto(Href: enrollmentsHref, Rel: "enroll", Method: "POST"));
        }


        var detailDto = new CourseDetailDto
        {
            Id = course.Id,
            Code = course.Code,
            Title = course.Title,
            MaxCapacity = course.MaxCapacity,
            EnrollmentCount = course.EnrollmentCount,
            Links = links
        };

        return Ok(detailDto);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Create a new course")]
    [EndpointDescription("Creates a course with a unique code. Returns409 if the course code already exists.")]
    public async Task<IActionResult> CreateCourse(CreateCourseRequest request, CancellationToken ct)
    {
        var codeExists = await courseService.CodeExistsAsync(request.Code, ct);
        if (codeExists)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Course code already exists",
                Detail = $"A course with code '{request.Code}' is already registered.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var created = await courseService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetCourseById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EndpointSummary("Update a course")]
    public async Task<IActionResult> UpdateCourse(
        int id,
        [FromBody] UpdateCourseRequest request,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] TmsDbContext context,
        CancellationToken ct)
    {
        var course = await context.Courses.FindAsync(new object[] { id }, ct);
        if (course == null) return NotFound();

        var authResult = await authorizationService.AuthorizeAsync(User, course, "CanEditCourse");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        course.Title = request.Title;
        if (request.MaxCapacity > 0)
        {
            course.MaxCapacity = request.MaxCapacity;
        }

        await context.SaveChangesAsync(ct);
        return NoContent();
    }
}

// public record CreateCourseRequest(string Code, string Title, int MaxCapacity);
// public record UpdateCourseRequest(string Title, int MaxCapacity);
