using System.ComponentModel.DataAnnotations;

namespace GymApi.DTOs;

public class UpdateFitnessProgramRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Range(1, 300)]
    public int DurationInMinutes { get; set; }

    public bool IsActive { get; set; }
}