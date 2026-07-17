namespace TmsApi.Dtos;

public record UpdateStudentRequest(string RegistrationNumber, string Name, decimal GPA, bool IsActive);
