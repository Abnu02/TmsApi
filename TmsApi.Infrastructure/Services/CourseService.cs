using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;


public class CourseService(TmsDbContext db, ILogger<CourseService> logger) : ICourseService
{
    public async Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct)
    {
        var course = new Course
        {
            Code = request.Code,
            Title = request.Title,
            MaxCapacity = request.MaxCapacity
        };

        db.Courses.Add(course);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created course {CourseId} ({Code})", course.Id, course.Code);
        return (await GetByIdAsync(course.Id, ct))!;
    }
    public Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct) =>
           db.Courses
               .AsNoTracking()
               .Where(c => c.Id == id)
               .Select(c => new CourseResponseDto(
                   c.Id,
                   c.Code,
                   c.Title,
                   c.MaxCapacity,
                   c.Enrollments.Count))
               .FirstOrDefaultAsync(ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) => db.Courses.AsNoTracking().AnyAsync(c => c.Code == code, ct);

    public async Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(PagedRequest request, CancellationToken ct)
    {
        // TODO 1: Start with a no-tracking IQueryable<Course>:
        IQueryable<Course> query = db.Courses.AsNoTracking();

        // TODO 2: If request.Search has a value, append a Where clause:
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(c => EF.Functions.ILike(c.Title, $"%{request.Search}%")
                                     || EF.Functions.ILike(c.Code, $"%{request.Search}%"));
        }

        // TODO 3: Count BEFORE paging:
        var totalCount = await query.CountAsync(ct);

        // TODO 4: Apply OrderBy, then Skip/Take, then Select projection.
        IOrderedQueryable<Course> sortedQuery = request.OrderBy switch
        {
            "Code" => request.Descending ? query.OrderByDescending(c => c.Code) : query.OrderBy(c => c.Code),
            "MaxCapacity" => request.Descending ? query.OrderByDescending(c => c.MaxCapacity) : query.OrderBy(c => c.MaxCapacity),
            _ => request.Descending ? query.OrderByDescending(c => c.Title) : query.OrderBy(c => c.Title)
        };

        // TODO 5: Materialise:
        var items = await sortedQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count))
            .ToListAsync(ct);

        // TODO 6: Return new PagedResponse<CourseResponseDto>
        return new PagedResponse<CourseResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
    // public async Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    // {
    //     var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
    //     if (course is null)
    //     {
    //         logger.LogWarning("Course with id {CourseId} not found", id);
    //         return null;
    //     }

    //     return MapToDto(course);
    // }

    // public async Task<CourseResponseDto?> GetByIdAsync(string code, CancellationToken ct)
    // {
    //     var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code, ct);
    //     if (course is null)
    //     {
    //         logger.LogWarning("Course with code {CourseCode} not found", code);
    //         return null;
    //     }
    //     return course;
    // }

    // public async Task<IReadOnlyList<CourseResponseDto>> GetAllAsync(CancellationToken ct)
    // {
    //     var all = await db.Courses.AsNoTracking().ToListAsync(ct);
    //     logger.LogInformation("Retrieved {Count} course records", all.Count);
    //     return all.Select(MapToDto).ToList();
    // }


    public async Task<bool> UpdateAsync(int id, Course course, CancellationToken ct)
    {
        var existing = await db.Courses.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (existing is null)
        {
            logger.LogWarning("Course with id {CourseId} not found for update", id);
            return false;
        }

        existing.Title = course.Title;
        existing.MaxCapacity = course.MaxCapacity;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Updated course with id {CourseId}", id);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await db.Courses.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (existing is null)
        {
            logger.LogWarning("Delete failed: course with id {CourseId} not found", id);
            return false;
        }

        db.Courses.Remove(existing);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Deleted course with id {CourseId}", id);
        return true;
    }

    public async Task<Course?> GetByCodeAsync(string code, CancellationToken ct)
    {
        return await db.Courses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Code == code, ct);
    }
    //     private static CourseResponseDto MapToDto(Course course) => new(
    //         course.Id,
    //         course.Code,
    //         course.Title,
    //         course.MaxCapacity,
    //         course.Enrollments.Count);
}
// }