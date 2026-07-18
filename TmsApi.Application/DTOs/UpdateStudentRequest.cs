namespace TmsApi.Application.DTOs;

public record UpdateStudentRequest(string RegistrationNumber, string Name, decimal GPA, bool IsActive);
