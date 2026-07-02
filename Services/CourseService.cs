using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Data;
using TmsApi.Entities;
using TmsApi.Services;

public class CourseService(TmsDbContext db, ILogger<CourseService> logger) : ICourseService
{
    public async Task<Course> CreateAsync(Course course, CancellationToken ct)
    {
        var existing = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Code == course.Code, ct);
        if (existing is not null)
        {
            logger.LogWarning("Course with code {CourseCode} already exists", course.Code);
            throw new InvalidOperationException($"Course with code {course.Code} already exists");
        }

        await db.Courses.AddAsync(course, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created course {CourseCode} with title {CourseTitle}", course.Code, course.Title);
        return course;
    }

    public async Task<Course?> GetByCodeAsync(int code, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code, ct);
        if (course is null)
        {
            logger.LogWarning("Course {CourseCode} not found", code);
        }
        return course;
    }

    public async Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken ct)
    {
        var all = await db.Courses.AsNoTracking().ToListAsync(ct);
        logger.LogInformation("Retrieved {Count} course records", all.Count);
        return all;
    }

    public async Task<bool> UpdateAsync(int code, Course course, CancellationToken ct)
    {
        var existing = await db.Courses.FirstOrDefaultAsync(c => c.Code == code, ct);
        if (existing is null)
        {
            logger.LogWarning("Course {CourseCode} not found for update", code);
            return false;
        }

        existing.Title = course.Title;
        existing.MaxCapacity = course.MaxCapacity;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Updated course {CourseCode}", code);
        return true;
    }

    public async Task<bool> DeleteAsync(int code, CancellationToken ct)
    {
        var existing = await db.Courses.FirstOrDefaultAsync(c => c.Code == code, ct);
        if (existing is null)
        {
            logger.LogWarning("Delete failed: course {CourseCode} not found", code);
            return false;
        }

        db.Courses.Remove(existing);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Deleted course {CourseCode}", code);
        return true;
    }
}
