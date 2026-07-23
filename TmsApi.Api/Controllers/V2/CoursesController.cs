using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Application.Interfaces;
namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v{version:apiVersion}/courses")]
[ApiVersion("2.0")]
public class CoursesController(TmsDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCourses(
        [FromServices] ICachedCourseService cachedService,
        CancellationToken ct = default)
    {
        // Using cached service for stampede protection as instructed
        var courses = await cachedService.GetAllCoursesAsync(ct);
        
        return Ok(new
        {
            data = courses
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCourse(
        int id, 
        [FromBody] TmsApi.Application.DTOs.CreateCourseRequest request,
        [FromServices] ICourseService service,
        [FromServices] ICachedCourseService cachedService,
        CancellationToken ct)
    {
        var course = await context.Courses.FindAsync(new object[] { id }, ct);
        if (course == null) return NotFound();
        
        course.Title = request.Title;
       
        await context.SaveChangesAsync(ct);
        
        await cachedService.InvalidateCourseCacheAsync(ct);
        return Ok(course);
    }
}
