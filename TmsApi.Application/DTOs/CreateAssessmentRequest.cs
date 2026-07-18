namespace TmsApi.Application.DTOs;

public record CreateAssessmentRequest(
    string Title,
    decimal MaxScore,
    decimal Weight
);