using TmsApi.Dtos;
public interface IAssessmentService
{
    Task<AssessmentResponseDto> CreateAsync(int courseId, CreateAssessmentRequest request, CancellationToken ct);
    Task<AssessmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct);
    Task<PagedResponse<AssessmentResponseDto>> GetByCourseAsync(int courseId, PagedRequest request, CancellationToken ct);
    Task<bool> UpdateAsync(int courseId, int id, UpdateAssessmentRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(int courseId, int id, CancellationToken ct);
}