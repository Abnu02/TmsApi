using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/reporting")]
public class ReportingController : ControllerBase
{
    private readonly TmsDbContext _context;

    public ReportingController(TmsDbContext context)
    {
        _context = context;
    }

    [HttpGet("active-students-gpa-count")]
    public async Task<IActionResult> GetActiveStudentsWithGpaAtLeastThree()
    {
        Console.WriteLine(">>> Running active students GPA >= 3.0 count query...");

        var count = await _context.Students
            .Where(s => s.IsActive && s.GPA >= 3.0m)
            .CountAsync();
        var list = await _context.Courses.Select(c=>new
        {
            c.Title,         
            EnrollmentCount = _context.Enrollments.Count(e => e.CourseId == c.Id)
        }).OrderByDescending(x => x.EnrollmentCount).ToListAsync();
        return Ok(new { Count = count, List = list });
    }
}
