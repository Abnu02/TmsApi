using Microsoft.AspNetCore.Mvc;
using TmsApi.Entities;
using TmsApi.Services;

[ApiController]
[Route("api/courses")]
public class CoursesController(ICourseService courseService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var courses = await courseService.GetAllAsync(ct);
        return Ok(courses);
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetByCode(int code, CancellationToken ct)
    {
        var course = await courseService.GetByCodeAsync(code, ct);
        return course is not null ? Ok(course) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create(Course course, CancellationToken ct)
    {
        var created = await courseService.CreateAsync(course, ct);
        return CreatedAtAction(nameof(GetByCode), new { code = created.Code }, created);
        throw new NotImplementedException();
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
