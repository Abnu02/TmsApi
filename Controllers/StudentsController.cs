using Microsoft.AspNetCore.Mvc;
using TmsApi.Entities;

[ApiController]
[Route("api/students")]
public class StudentsController(IStudentService studentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var students = await studentService.GetAllAsync();
        return Ok(students);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        if (!int.TryParse(id, out var studentId))
        {
            return BadRequest("Invalid student ID");
        }
        var student = await studentService.GetByIdAsync(studentId);
        return student is not null ? Ok(student) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudentRequest request)
    {
        var student = new Student
        {
            RegistrationNumber = request.RegistrationNumber,
            Name = request.Name,
            GPA = request.GPA,
            IsActive = request.IsActive
        };
        
        var created = await studentService.CreateAsync(student);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateStudentRequest request)
    {
        if (!int.TryParse(id, out var studentId))
        {
            return BadRequest("Invalid student ID");
        }
        var student = new Student
        {
            Id = studentId,
            RegistrationNumber = request.RegistrationNumber,
            Name = request.Name,
            GPA = request.GPA,
            IsActive = request.IsActive
        };
        
        var updated = await studentService.UpdateAsync(studentId, student);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!int.TryParse(id, out var studentId))
        {
            return BadRequest("Invalid student ID");
        }
        var deleted = await studentService.DeleteAsync(studentId);
        return deleted ? NoContent() : NotFound();
    }
}

public record CreateStudentRequest(string RegistrationNumber, string Name, decimal GPA, bool IsActive);
public record UpdateStudentRequest(string RegistrationNumber, string Name, decimal GPA, bool IsActive);
