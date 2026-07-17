namespace TmsApi.Dtos;

public record StudentResponseDto
(
    int Id,
    string RegistrationNumber,
    string Name,
    double GPA,
    bool IsActive
);