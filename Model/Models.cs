
public interface IGradable
{
    string Title { get; }
    decimal CalculateGrade();
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