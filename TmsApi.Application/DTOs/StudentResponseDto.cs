namespace TmsApi.Application.DTOs;

public record StudentResponseDto
(
    int Id,
    string RegistrationNumber,
    string Name,
    double GPA,
    bool IsActive
);