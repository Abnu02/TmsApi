using MediatR;

namespace TmsApi.Application.Enrollments.Queries;

public sealed record GetStudentScheduleQuery(int StudentId) : IRequest<ScheduleDto>;

public sealed record ScheduleDto(int StudentId, List<ScheduleItemDto> Courses);

public sealed record ScheduleItemDto(string CourseCode, string Title, string Schedule);
