namespace TmsApi.Dtos;

public record CreateStudentRequest(string RegistrationNumber, string Name, decimal GPA, bool IsActive);
