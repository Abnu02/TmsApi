public interface IEnrollmentService
{
    Task<EnrollmentRecord> EnrollmentAsync(string userId, string courseId);
    Task<EnrollmentRecord?> GetByidAsync(string id);
    Task<IReadOnlyList<EnrollmentRecord>> GetAllAsync();



}

public class EnrollmentService : IEnrollmentService
{
    private readonly Dictionary<string, EnrollmentRecord> _store = new();
    private readonly ILogger<EnrollmentService> _logger;
    public EnrollmentService(ILogger<EnrollmentService> logger)
    {
        _logger = logger;
    }
    public Task<EnrollmentRecord> EnrollmentAsync(string StudentId, string courseCode)
    {
        var existing = _store.Values.FirstOrDefault(e => e.StudentId == StudentId && e.CourseCode == courseCode);
        if (existing is not null)
        {
            _logger.LogWarning("Duplicate enrollment attempt {StudentId} already in {CourseCode} (record {EnrollmentI d})", StudentId, courseCode, existing.Id);
            return Task.FromResult(existing);
        }
        ;
        var id = Guid.NewGuid().ToString("N")[..8];
        var record = new EnrollmentRecord(id, StudentId, courseCode, DateTime.UtcNow);
        _store[id] = record;
        _logger.LogInformation("Enrolled {StudentId} in {CourseCode} record {EnrollmentId}", StudentId, courseCode, id);
        return Task.FromResult(record);
    }
    public Task<EnrollmentRecord?> GetByIdAsync(string id)
    {
        _store.TryGetValue(id, out var record);

        if (record is null)
        {
            _logger.LogWarning("Enrollment record {EnrollmentId} not found", id);
        }
        return Task.FromResult(record);
    }
    public Task<IReadOnlyList<EnrollmentRecord>> GetAllAsync()
    {
        IReadOnlyList<EnrollmentRecord> all = _store.Values.ToList();
        return Task.FromResult(all);
    }
    public Task<bool> DeleteAsync(string id)
    {
        var removed = _store.Remove(id);
        if (removed)
        {
            _logger.LogInformation("Deleted enrollment record {EnrollmentId}", id);
        }
        else
        {
            _logger.LogWarning("Delete failed enrollment {EnrollmentId} not found", id);
        }
        return Task.FromResult(removed);
    }

    public Task<EnrollmentRecord?> GetByidAsync(string id)
    {
        throw new NotImplementedException();
    }
}
public record EnrollmentRecord(
string Id,
string StudentId,
string CourseCode,
DateTime EnrolledAt);