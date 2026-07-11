using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Data;
using TmsApi.Dtos;
using TmsApi.Entities;

namespace TmsApi.Services;

public class AssessmentService(TmsDbContext db, ILogger<AssessmentService> logger) : IAssessmentService
{
    public async Task<AssessmentResponseDto> CreateAsync(int courseId, CreateAssessmentRequest request, CancellationToken ct)
    {
        var assessment = new Assessment
        {
            Title = request.Title,
            MaxScore = request.MaxScore,
            Weight = request.Weight,
            CourseId = courseId

        };
        db.Assessments.Add(assessment);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created assessment {AssessmentId} ({Title}) for course {CourseId}", assessment.Id, assessment.Title, courseId);
        return (await GetByIdAsync(courseId, assessment.Id, ct))!;
    }
    public Task<AssessmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct) =>
        db.Assessments
            .AsNoTracking()
            .Where(a => a.CourseId == courseId && a.Id == id)
            .Select(a => new AssessmentResponseDto(
                a.Id,
                a.Title,
                a.MaxScore,
                a.Weight,
                a.CourseId))
            .FirstOrDefaultAsync(ct);

    public Task<bool> AssessmentExistsAsync(int courseId, int id, CancellationToken ct) =>
        db.Assessments.AsNoTracking().AnyAsync(a => a.CourseId == courseId && a.Id == id, ct);

    public async Task<PagedResponse<AssessmentResponseDto>> GetByCourseAsync(int courseId, PagedRequest request, CancellationToken ct)
    {
        IQueryable<Assessment> query = db.Assessments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(a => EF.Functions.ILike(a.Title, $"%{request.Search}%")
                                     || EF.Functions.ILike(a.Title, $"%{request.Search}%"));
        }
        var totalCount = await query.CountAsync(ct);
        IOrderedQueryable<Assessment> sortedQuery = request.OrderBy switch
        {
            "Title" => request.Descending ? query.OrderByDescending(a => a.Title) : query.OrderBy(a => a.Title),
            "MaxScore" => request.Descending ? query.OrderByDescending(a => a.MaxScore) : query.OrderBy(a => a.MaxScore),
            _ => request.Descending ? query.OrderByDescending(a => a.Weight) : query.OrderBy(a => a.Weight)
        };
        var items = await sortedQuery.Skip((request.Page - 1) * request.PageSize)
                                  .Take(request.PageSize)
                                  .Select(a => new AssessmentResponseDto(
                                      a.Id,
                                      a.Title,
                                      a.MaxScore,
                                      a.Weight,
                                      a.CourseId))
                                  .ToListAsync(ct);
        return new PagedResponse<AssessmentResponseDto>
        {
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            Items = items
        };
    }

    public async Task<bool> UpdateAsync(
int courseId,
int id,
UpdateAssessmentRequest request,
CancellationToken ct)
    {
        var assessment = await db.Assessments
            .FirstOrDefaultAsync(a => a.CourseId == courseId && a.Id == id, ct);

        if (assessment is null)
        {
            return false;
        }

        assessment.Title = request.Title;
        assessment.MaxScore = request.MaxScore;
        assessment.Weight = request.Weight;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int courseId, int id, CancellationToken ct)
    {
        var assessment = await db.Assessments
            .FirstOrDefaultAsync(a => a.CourseId == courseId && a.Id == id, ct);

        if (assessment is null)
        {
            return false;
        }

        db.Assessments.Remove(assessment);
        await db.SaveChangesAsync(ct);
        return true;
    }
}