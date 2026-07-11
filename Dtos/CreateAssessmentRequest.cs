namespace TmsApi.Dtos;

public record CreateAssessmentRequest(
    string Title,
    decimal MaxScore,
    decimal Weight
);