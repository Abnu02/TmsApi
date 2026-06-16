
public interface IGradable
{
    string Title { get; }
    decimal CalculateGrade();
}

public class Course
{
    public string? Id { get; set; }

    public required string Code { get; set; }
    public required string Title
    {
        get; set => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Title is can't be empty or whitespace.", nameof(Title));
    }
    public int Capacity { get; set => field = value > 0 ? value : throw new ArgumentException("Capacity must be a positive integer.", nameof(Capacity)); }
    public int EnrolledCount { get; set; }

}

public class Student
{
    public required string Id { get; set; }
    public required string Name { get; set => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Name is can't be empty or whitespace.", nameof(value)); }
    public int Age { get; set => field = value is >= 16 and <= 100 ? value : throw new ArgumentOutOfRangeException("Age must be between 16 and 100.", nameof(value)); }
    public decimal GPA
    {
        get;
        set => field = value is >= 0.0m and <= 4.0m
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), "GPA must be between 0.0and 4.0.");
    }
}

public class Quiz : IGradable
{
    public required string Title { get; set; }
    public required int CorrectAnswers { get; set; }
    public required int TotalQuestions { get; set; }

    public decimal CalculateGrade()
    {
        if (TotalQuestions == 0) return 0m;
        return (decimal)CorrectAnswers / TotalQuestions * 100m;
    }
}
public class LabAssignment : IGradable
{
    public required string Title { get; set; }
    public required decimal FunctionalityScore { get; set; }
    public required decimal CodeQualityScore { get; set; }
    public decimal CalculateGrade()
    {
        return (FunctionalityScore * 0.7m) + (CodeQualityScore * 0.3m);
    }
}