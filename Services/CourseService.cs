using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;

public interface ICourseService
{
    Task<Course> CreateAsync(Course course);
    Task<Course?> GetByCodeAsync(string code);
    Task<IReadOnlyList<Course>> GetAllAsync();
    Task<bool> UpdateAsync(string code, Course course);
    Task<bool> DeleteAsync(string code);
}

public class CourseService : ICourseService
{
    private readonly TmsDbContext _db;
    private readonly ILogger<CourseService> _logger;

    public CourseService(TmsDbContext db, ILogger<CourseService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Course> CreateAsync(Course course)
    {
        var existing = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Code == course.Code);
        if (existing is not null)
        {
            _logger.LogWarning("Course with code {CourseCode} already exists", course.Code);
            throw new InvalidOperationException($"Course with code {course.Code} already exists");
        }

        await _db.Courses.AddAsync(course);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created course {CourseCode} with title {CourseTitle}", course.Code, course.Title);
        return course;
    }

    public async Task<Course?> GetByCodeAsync(string code)
    {
        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code);
        if (course is null)
        {
            _logger.LogWarning("Course {CourseCode} not found", code);
        }
        return course;
    }

    public async Task<IReadOnlyList<Course>> GetAllAsync()
    {
        var all = await _db.Courses.AsNoTracking().ToListAsync();
        _logger.LogInformation("Retrieved {Count} course records", all.Count);
        return all;
    }

    public async Task<bool> UpdateAsync(string code, Course course)
    {
        var existing = await _db.Courses.FirstOrDefaultAsync(c => c.Code == code);
        if (existing is null)
        {
            _logger.LogWarning("Course {CourseCode} not found for update", code);
            return false;
        }

        existing.Title = course.Title;
        existing.Capacity = course.Capacity;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated course {CourseCode}", code);
        return true;
    }

    public async Task<bool> DeleteAsync(string code)
    {
        var existing = await _db.Courses.FirstOrDefaultAsync(c => c.Code == code);
        if (existing is null)
        {
            _logger.LogWarning("Delete failed: course {CourseCode} not found", code);
            return false;
        }

        _db.Courses.Remove(existing);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted course {CourseCode}", code);
        return true;
    }
}
