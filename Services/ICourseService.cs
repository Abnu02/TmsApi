using TmsApi.Entities;

namespace TmsApi.Services;

public interface ICourseService
{
    Task<Course> CreateAsync(Course course, CancellationToken ct);
    Task<Course?> GetByCodeAsync(int code, CancellationToken ct);
    Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken ct);
    Task<bool> UpdateAsync(int code, Course course, CancellationToken ct);
    Task<bool> DeleteAsync(int code, CancellationToken ct);
}