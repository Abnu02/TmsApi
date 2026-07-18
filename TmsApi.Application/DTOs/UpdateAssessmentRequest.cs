namespace TmsApi.Application.DTOs;

public record UpdateAssessmentRequest(
    string Title,
    decimal MaxScore,
    decimal Weight
);