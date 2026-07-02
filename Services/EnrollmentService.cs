using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;

public interface IEnrollmentService
{
    Task<EnrollmentRecord> EnrollmentAsync(string studentId, int courseCode);
    Task<EnrollmentRecord?> GetByIdAsync(string id);
    Task<IReadOnlyList<EnrollmentRecord>> GetAllAsync();
    Task<bool> DeleteAsync(string id);
}

public class EnrollmentService : IEnrollmentService
{
    private readonly TmsDbContext _db;
    private readonly ILogger<EnrollmentService> _logger;

    public EnrollmentService(TmsDbContext db, ILogger<EnrollmentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<EnrollmentRecord> EnrollmentAsync(string studentId, int courseCode)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.RegistrationNumber == studentId);
        if (student is null)
        {
            throw new InvalidOperationException($"Student '{studentId}' not found.");
        }

        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Code == courseCode);
        if (course is null)
        {
            throw new InvalidOperationException($"Course '{courseCode}' not found.");
        }

        var existing = await _db.Enrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.StudentId == student.Id && e.CourseId == course.Id);

        if (existing is not null)
        {
            var existingRecord = new EnrollmentRecord(existing.Id.ToString(), student.RegistrationNumber, course.Code.ToString(), existing.EnrolledAt);
            _logger.LogWarning("Duplicate enrollment attempt for {StudentId} in {CourseCode} (record {EnrollmentId})", studentId, courseCode, existingRecord.Id);
            return existingRecord;
        }

        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            CourseId = course.Id,
            EnrolledAt = DateTime.UtcNow
        };

        await _db.Enrollments.AddAsync(enrollment);
        await _db.SaveChangesAsync();

        var record = new EnrollmentRecord(enrollment.Id.ToString(), student.RegistrationNumber, course.Code.ToString(), enrollment.EnrolledAt);
        _logger.LogInformation("Enrolled {StudentId} in {CourseCode} record {EnrollmentId}", studentId, courseCode, record.Id);
        return record;
    }

    public async Task<EnrollmentRecord?> GetByIdAsync(string id)
    {
        if (!int.TryParse(id, out var numericId))
        {
            return null;
        }

        var enrollment = await _db.Enrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == numericId);

        if (enrollment is null)
        {
            _logger.LogWarning("Enrollment record {EnrollmentId} not found", id);
            return null;
        }

        return new EnrollmentRecord(enrollment.Id.ToString(), enrollment.Student.RegistrationNumber, enrollment.Course.Code.ToString(), enrollment.EnrolledAt);
    }

    public async Task<IReadOnlyList<EnrollmentRecord>> GetAllAsync()
    {
        var records = await _db.Enrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Include(e => e.Course)
            .Select(e => new EnrollmentRecord(e.Id.ToString(), e.Student.RegistrationNumber, e.Course.Code.ToString(), e.EnrolledAt))
            .ToListAsync();

        _logger.LogInformation("Retrieved {Count} enrollment records", records.Count);
        return records;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (!int.TryParse(id, out var numericId))
        {
            return false;
        }

        var enrollment = await _db.Enrollments.FindAsync(numericId);
        if (enrollment is null)
        {
            _logger.LogWarning("Delete failed enrollment {EnrollmentId} not found", id);
            return false;
        }

        _db.Enrollments.Remove(enrollment);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted enrollment record {EnrollmentId}", id);
        return true;
    }
}

public record EnrollmentRecord(
    string Id,
    string StudentId,
    string CourseCode,
    DateTime EnrolledAt);

public class TmsDatabaseException(string message) : Exception(message);
