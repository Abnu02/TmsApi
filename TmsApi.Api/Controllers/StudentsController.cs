using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

[ApiController]
[Route("api/students")]
public class StudentsController(IStudentService studentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request, CancellationToken ct)
    {
        var students = await studentService.GetAllAsync(request, ct);
        return Ok(students);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var studentId))
        {
            return BadRequest("Invalid student ID");
        }

        var student = await studentService.GetByIdAsync(studentId, ct);
        return student is not null ? Ok(student) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudentRequest request, CancellationToken ct)
    {
        var created = await studentService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateStudentRequest request, CancellationToken ct)
    {
        if (!int.TryParse(id, out var studentId))
        {
            return BadRequest("Invalid student ID");
        }

        var updated = await studentService.UpdateAsync(studentId, request, ct);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var studentId))
        {
            return BadRequest("Invalid student ID");
        }

        var deleted = await studentService.DeleteAsync(studentId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
