using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class EnrollmentService(TmsDbContext db, ILogger<EnrollmentService> logger) : IEnrollmentService
{
    public Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct) =>
        db.Enrollments
            .AsNoTracking()
            .Where(e => e.Id == id && e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(e.Id, e.CourseId, e.StudentId, e.EnrolledAt))
            .FirstOrDefaultAsync(ct);

    public async Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct)
    {
        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = request.StudentId,
            EnrolledAt = DateTime.UtcNow
        };

        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Created enrollment {EnrollmentId} for course {CourseId} and student {StudentId}",
            enrollment.Id,
            courseId,
            request.StudentId);

        return await GetByIdAsync(courseId, enrollment.Id, ct)
            ?? throw new InvalidOperationException($"Enrollment {enrollment.Id} could not be loaded after creation.");
    }

    public async Task<IReadOnlyList<EnrollmentResponseDto>> GetByCourseAsync(int courseId, CancellationToken ct)
    {
        var enrollments = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .OrderBy(e => e.EnrolledAt)
            .Select(e => new EnrollmentResponseDto(e.Id, e.CourseId, e.StudentId, e.EnrolledAt))
            .ToListAsync(ct);

        return enrollments;
    }

    public async Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct)
    {
        return await db.Enrollments
            .AnyAsync(e => e.StudentId == studentId && e.Course.Code == courseCode, ct);
    }

    public async Task AddAsync(Enrollment enrollment, CancellationToken ct)
    {
        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Enrollment>> GetByStudentIdAsync(int studentId, CancellationToken ct)
    {
        return await db.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == studentId)
            .ToListAsync(ct);
    }
}