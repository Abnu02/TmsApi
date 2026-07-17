using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Data;
using TmsApi.Dtos;
using TmsApi.Entities;
using TmsApi.Services;

public class StudentService : IStudentService
{
    private readonly TmsDbContext _db;
    private readonly ILogger<StudentService> _logger;

    public StudentService(TmsDbContext db, ILogger<StudentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<StudentResponseDto> CreateAsync(CreateStudentRequest request, CancellationToken ct)
    {
        var student = new Student
        {
            RegistrationNumber = request.RegistrationNumber,
            Name = request.Name,
            GPA = request.GPA,
            IsActive = request.IsActive
        };

        _db.Students.Add(student);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created student {StudentId} with name {StudentName}", student.Id, student.Name);
        return (await GetByIdAsync(student.Id, ct))!;
    }

    public Task<StudentResponseDto?> GetByIdAsync(int id, CancellationToken ct) =>
        _db.Students
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new StudentResponseDto(
                s.Id,
                s.RegistrationNumber,
                s.Name,
                (double)s.GPA,
                s.IsActive))
            .FirstOrDefaultAsync(ct);

    public async Task<PagedResponse<StudentResponseDto>> GetAllAsync(PagedRequest request, CancellationToken ct)
    {
        var query = _db.Students
            .AsNoTracking()
            .Select(s => new StudentResponseDto(
                s.Id,
                s.RegistrationNumber,
                s.Name,
                (double)s.GPA,
                s.IsActive));

        var totalCount = await query.CountAsync(ct);
        var students = await query.Skip((request.Page - 1) * request.PageSize)
                                  .Take(request.PageSize)
                                  .ToListAsync(ct);

        _logger.LogInformation("Retrieved {Count} student records", students.Count);
        return new PagedResponse<StudentResponseDto>
        {
            Items = students,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<bool> UpdateAsync(int id, UpdateStudentRequest request, CancellationToken ct)
    {
        var existing = await _db.Students.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (existing is null)
        {
            _logger.LogWarning("Student {StudentId} not found for update", id);
            return false;
        }

        existing.RegistrationNumber = request.RegistrationNumber;
        existing.Name = request.Name;
        existing.GPA = request.GPA;
        existing.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated student {StudentId}", id);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await _db.Students.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (existing is null)
        {
            _logger.LogWarning("Delete failed: student {StudentId} not found", id);
            return false;
        }

        _db.Students.Remove(existing);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Deleted student {StudentId}", id);
        return true;
    }
}
