using MediatR;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Enrollments.Queries;

public sealed class GetStudentScheduleHandler(IEnrollmentService enrollmentService)
    : IRequestHandler<GetStudentScheduleQuery, ScheduleDto>
{
    public async Task<ScheduleDto> Handle(GetStudentScheduleQuery query, CancellationToken ct)
    {
        var enrollments = await enrollmentService.GetByStudentAsync(query.StudentId, ct);

        var courses = enrollments
            .Select(e => new ScheduleItemDto(
                string.Empty,
                string.Empty,
                string.Empty))
            .ToList();

        return new ScheduleDto(query.StudentId, courses);
    }
}
