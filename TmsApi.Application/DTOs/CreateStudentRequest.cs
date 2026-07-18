namespace TmsApi.Application.DTOs;

public record CreateStudentRequest(string RegistrationNumber, string Name, decimal GPA, bool IsActive);
