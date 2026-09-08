using System.ComponentModel.DataAnnotations;

namespace GymApi.DTOs;

public class UpdateTrainerAvailabilityRequest
{
    [Required]
    [Range(0, 6, ErrorMessage = "DayOfWeek must be 0 (Sunday) through 6 (Saturday).")]
    public int DayOfWeek { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }
}
