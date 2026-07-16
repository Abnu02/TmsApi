namespace TmsApi.Dtos;

public record UpdateAssessmentRequest(
    string Title,
    decimal MaxScore,
    decimal Weight
);