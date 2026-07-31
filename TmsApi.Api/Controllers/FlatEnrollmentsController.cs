using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/enrollments")]
[Tags("FlatEnrollments")]
[Produces("application/json")]
public class FlatEnrollmentsController(TmsDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetEnrollments(CancellationToken ct)
    {
        var enrollments = await dbContext.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .Select(e => new
            {
                id = e.Id.ToString(),
                studentId = e.StudentId,
                studentName = e.Student.Name,
                courseId = e.CourseId.ToString(),
                courseName = e.Course.Title,
                status = e.Status,
                enrolledAt = e.EnrolledAt.ToString("o")
            })
            .ToListAsync(ct);

        return Ok(enrollments);
    }

    [HttpPost]
    public async Task<IActionResult> CreateEnrollment([FromBody] CreateEnrollmentPayload payload, CancellationToken ct)
    {
        var studentIdSearch = payload.StudentId.Trim().ToUpper();
        
        // Find the student by registration number, or Id if they passed a number
        var student = await dbContext.Students.FirstOrDefaultAsync(s => s.RegistrationNumber.ToUpper() == studentIdSearch, ct);
        
        if (student == null && int.TryParse(payload.StudentId.Trim(), out var parsedId))
        {
            student = await dbContext.Students.FindAsync(parsedId, ct);
        }

        if (student == null)
            return BadRequest(new { Message = $"Student '{payload.StudentId}' not found." });

        var courseIdSearch = payload.CourseId.Trim().ToUpper();

        // Find the course by Code (e.g. "CS-101") or Id
        var course = await dbContext.Courses.FirstOrDefaultAsync(c => c.Code.ToUpper() == courseIdSearch, ct);
        
        if (course == null && int.TryParse(payload.CourseId.Trim(), out var parsedCourseId))
        {
            course = await dbContext.Courses.FindAsync(parsedCourseId, ct);
        }

        if (course == null)
            return BadRequest(new { Message = $"Course '{payload.CourseId}' not found." });

        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            CourseId = course.Id,
            Year = 2026, // Defaulting based on term or hardcoded
            Status = "Pending",
            EnrolledAt = DateTime.UtcNow
        };

        dbContext.Enrollments.Add(enrollment);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new
        {
            id = enrollment.Id.ToString(),
            studentId = student.Id,
            studentName = student.Name,
            courseId = course.Id.ToString(),
            courseName = course.Title,
            status = enrollment.Status,
            enrolledAt = enrollment.EnrolledAt.ToString("o")
        });
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveEnrollment(int id, CancellationToken ct)
    {
        var enrollment = await dbContext.Enrollments.FindAsync(id, ct);
        if (enrollment == null)
            return NotFound();

        enrollment.Status = "Approved";
        await dbContext.SaveChangesAsync(ct);

        return Ok();
    }
}

public class CreateEnrollmentPayload
{
    public string StudentId { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public string Term { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<string> BackupCourses { get; set; } = new();
}
