using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/enrollments")]
public class EnrollmentsController(IEnrollmentService enrollmentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var enrollments = await enrollmentService.GetAllAsync();
        return Ok(enrollments);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var enrollment = await enrollmentService.GetByIdAsync(id);
        return enrollment is not null ? Ok(enrollment) : NotFound();
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEnrollmentRequest request)
    {
        var record = await enrollmentService.EnrollmentAsync(request.StudentId, request.CourseCode);
        return CreatedAtAction(nameof(GetById), new { id = record.Id }, record);
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await enrollmentService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}

public record CreateEnrollmentRequest(string StudentId, string CourseCode);