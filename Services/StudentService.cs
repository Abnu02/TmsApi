using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;

public interface IStudentService
{
    Task<Student> CreateAsync(Student student);
    Task<Student?> GetByIdAsync(int id);
    Task<IReadOnlyList<Student>> GetAllAsync();
    Task<bool> UpdateAsync(int id, Student student);
    Task<bool> DeleteAsync(int id);
}

public class StudentService : IStudentService
{
    private readonly TmsDbContext _db;
    private readonly ILogger<StudentService> _logger;

    public StudentService(TmsDbContext db, ILogger<StudentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Student> CreateAsync(Student student)
    {
        await _db.Students.AddAsync(student);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created student {StudentId} with name {StudentName}", student.Id, student.Name);
        return student;
    }

    public async Task<Student?> GetByIdAsync(int id)
    {
        var student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (student is null)
        {
            _logger.LogWarning("Student {StudentId} not found", id);
        }
        return student;
    }

    public async Task<IReadOnlyList<Student>> GetAllAsync()
    {
        var all = await _db.Students.AsNoTracking().ToListAsync();
        _logger.LogInformation("Retrieved {Count} student records", all.Count);
        return all;
    }

    public async Task<bool> UpdateAsync(int id, Student student)
    {
        var existing = await _db.Students.FindAsync(id);
        if (existing is null)
        {
            _logger.LogWarning("Student {StudentId} not found for update", id);
            return false;
        }

        existing.RegistrationNumber = student.RegistrationNumber;
        existing.Name = student.Name;
        existing.GPA = student.GPA;
        existing.IsActive = student.IsActive;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated student {StudentId}", id);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var existing = await _db.Students.FindAsync(id);
        if (existing is null)
        {
            _logger.LogWarning("Delete failed: student {StudentId} not found", id);
            return false;
        }

        _db.Students.Remove(existing);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted student {StudentId}", id);
        return true;
    }
}
