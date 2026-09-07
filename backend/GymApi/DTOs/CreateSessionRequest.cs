using System.ComponentModel.DataAnnotations;

namespace GymApi.DTOs;

public class CreateSessionRequest
{
    [Required]
    public Guid FitnessProgramId { get; set; }

    [Required]
    public Guid TrainerId { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [Required]
    [Range(1, 500, ErrorMessage = "Capacity must be between 1 and 500.")]
    public int Capacity { get; set; }
}
