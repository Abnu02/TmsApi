using TmsApi.Entities;

public interface ICourseService
{
    Task<Course> CreateAsync(Course course);
    Task<Course?> GetByCodeAsync(string code);
    Task<IReadOnlyList<Course>> GetAllAsync();
    Task<bool> UpdateAsync(string code, Course course);
    Task<bool> DeleteAsync(string code);
}

public class CourseService : ICourseService
{
    private readonly Dictionary<string, Course> _store = new();
    private readonly ILogger<CourseService> _logger;

    public CourseService(ILogger<CourseService> logger)
    {
        _logger = logger;
    }

    public Task<Course> CreateAsync(Course course)
    {
        if (_store.ContainsKey(course.Code))
        {
            _logger.LogWarning("Course with code {CourseCode} already exists", course.Code);
            throw new InvalidOperationException($"Course with code {course.Code} already exists");
        }

        _store[course.Code] = course;
        _logger.LogInformation("Created course {CourseCode} with title {CourseTitle}", course.Code, course.Title);
        return Task.FromResult(course);
    }

    public Task<Course?> GetByCodeAsync(string code)
    {
        _store.TryGetValue(code, out var course);

        if (course is null)
        {
            _logger.LogWarning("Course {CourseCode} not found", code);
        }
        return Task.FromResult(course);
    }

    public Task<IReadOnlyList<Course>> GetAllAsync()
    {
        IReadOnlyList<Course> all = _store.Values.ToList();
        _logger.LogInformation("Retrieved {Count} course records", all.Count);
        return Task.FromResult(all);
    }

    public Task<bool> UpdateAsync(string code, Course course)
    {
        if (!_store.ContainsKey(code))
        {
            _logger.LogWarning("Course {CourseCode} not found for update", code);
            return Task.FromResult(false);
        }

        _store[code] = course;
        _logger.LogInformation("Updated course {CourseCode}", code);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string code)
    {
        var removed = _store.Remove(code);
        if (removed)
        {
            _logger.LogInformation("Deleted course {CourseCode}", code);
        }
        else
        {
            _logger.LogWarning("Delete failed: course {CourseCode} not found", code);
        }
        return Task.FromResult(removed);
    }
}
