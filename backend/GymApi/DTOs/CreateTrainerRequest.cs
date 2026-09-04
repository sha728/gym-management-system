using System.ComponentModel.DataAnnotations;

namespace GymApi.DTOs;

public class CreateTrainerRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Specialization { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}