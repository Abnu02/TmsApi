using TmsApi.Entities;

public interface IStudentService
{
    Task<Student> CreateAsync(Student student);
    Task<Student?> GetByIdAsync(int id);
    Task<IReadOnlyList<Student>> GetAllAsync();
    Task<bool> UpdateAsync(int id, Student student);
    Task<bool> DeleteAsync(int id);
}

public class StudentService : IStudentService
{
    private readonly Dictionary<int, Student> _store = new();
    private readonly ILogger<StudentService> _logger;

    public StudentService(ILogger<StudentService> logger)
    {
        _logger = logger;
    }

    public Task<Student> CreateAsync(Student student)
    {
        if (_store.ContainsKey(student.Id))
        {
            _logger.LogWarning("Student with Id {StudentId} already exists", student.Id);
            throw new InvalidOperationException($"Student with Id {student.Id} already exists");
        }

        _store[student.Id] = student;
        _logger.LogInformation("Created student {StudentId} with name {StudentName}", student.Id, student.Name);
        return Task.FromResult(student);
    }

    public Task<Student?> GetByIdAsync(int id)
    {
        _store.TryGetValue(id, out var student);

        if (student is null)
        {
            _logger.LogWarning("Student {StudentId} not found", id);
        }
        return Task.FromResult(student);
    }

    public Task<IReadOnlyList<Student>> GetAllAsync()
    {
        IReadOnlyList<Student> all = _store.Values.ToList();
        _logger.LogInformation("Retrieved {Count} student records", all.Count);
        return Task.FromResult(all);
    }

    public Task<bool> UpdateAsync(int id, Student student)
    {
        if (!_store.ContainsKey(id))
        {
            _logger.LogWarning("Student {StudentId} not found for update", id);
            return Task.FromResult(false);
        }

        _store[id] = student;
        _logger.LogInformation("Updated student {StudentId}", id);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int id)
    {
        var removed = _store.Remove(id);
        if (removed)
        {
            _logger.LogInformation("Deleted student {StudentId}", id);
        }
        else
        {
            _logger.LogWarning("Delete failed: student {StudentId} not found", id);
        }
        return Task.FromResult(removed);
    }
}
