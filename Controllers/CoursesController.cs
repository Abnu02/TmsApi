using Microsoft.AspNetCore.Mvc;
using TmsApi.Entities;
using Microsoft.AspNetCore.Routing;
using TmsApi.Services;
using TmsApi.Dtos;

namespace TmsApi.Controllers;

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
    public async Task<IActionResult> GetCourses([FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await courseService.GetCoursesAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}", Name = nameof(GetCourseById))]
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



    // [HttpPut("{code}")]
    // public async Task<IActionResult> Update(int code, [FromBody] UpdateCourseRequest request, CancellationToken ct)
    // {
    //     var course = new Course
    //     {
    //         Code = code,
    //         Title = request.Title,
    //         MaxCapacity = request.MaxCapacity
    //     };

    //     var updated = await courseService.UpdateAsync(code, course);
    //     return updated ? NoContent() : NotFound();
    // }

    // [HttpDelete("{code}")]
    // public async Task<IActionResult> Delete(string code)
    // {
    //     var deleted = await courseService.DeleteAsync(code);
    //     return deleted ? NoContent() : NotFound();
    // }
}

// public record CreateCourseRequest(string Code, string Title, int MaxCapacity);
// public record UpdateCourseRequest(string Title, int MaxCapacity);
