namespace GymApi.Models;

public class TrainerAvailability
{
    public Guid TrainerAvailabilityId { get; set; }

    public Guid TrainerId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public Trainer Trainer { get; set; } = null!;
}