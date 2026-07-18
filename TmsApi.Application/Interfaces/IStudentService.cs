using TmsApi.Application.DTOs;

namespace TmsApi.Application.Interfaces;

public interface IStudentService
{
    Task<StudentResponseDto> CreateAsync(CreateStudentRequest request, CancellationToken ct);
    Task<StudentResponseDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<PagedResponse<StudentResponseDto>> GetAllAsync(PagedRequest request, CancellationToken ct);
    Task<bool> UpdateAsync(int id, UpdateStudentRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}