using TmsApi.Entities;
using TmsApi.Dtos;
namespace TmsApi.Services;



public interface ICourseService
{

    Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct);
    Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct);
    // Task<IReadOnlyList<CourseResponseDto>> GetAllAsync(CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(PagedRequest request, CancellationToken ct);
    Task<bool> UpdateAsync(int id, Course course, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}