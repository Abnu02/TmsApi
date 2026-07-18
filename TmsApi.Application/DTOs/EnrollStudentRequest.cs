using System.ComponentModel.DataAnnotations;
namespace TmsApi.Application.DTOs;

public record EnrollStudentRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "StudentId must be a positive integer.")]
    public required int StudentId { get; init; }
}