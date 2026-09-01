namespace GymApi.Models;

public class Trainer
{
    public Guid TrainerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Specialization { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<TrainerAvailability> Availabilities { get; set; }
        = new List<TrainerAvailability>();
}