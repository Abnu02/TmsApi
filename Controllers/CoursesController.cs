using Microsoft.AspNetCore.Mvc;
using TmsApi.Entities;
using TmsApi.Services;
using TmsApi.Dtos;

[ApiController]
[Route("api/courses")]
public class CoursesController(ICourseService courseService) : ControllerBase
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
        return course is not null ? Ok(course) : NotFound();
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

public record CreateCourseRequest(string Code, string Title, int MaxCapacity);
public record UpdateCourseRequest(string Title, int MaxCapacity);
