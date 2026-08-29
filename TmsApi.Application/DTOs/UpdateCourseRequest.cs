using System.ComponentModel.DataAnnotations;
namespace TmsApi.Application.DTOs;

public class UpdateCourseRequest
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }
    [Range(1, 200)]
    public int MaxCapacity { get; init; }
}
