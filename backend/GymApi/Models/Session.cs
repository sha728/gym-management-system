namespace GymApi.Models;

public class Session
{
    public Guid SessionId { get; set; }

    public Guid FitnessProgramId { get; set; }

    public Guid TrainerId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    public FitnessProgram FitnessProgram { get; set; } = null!;

    public Trainer Trainer { get; set; } = null!;

    public ICollection<Booking> Bookings { get; set; }
        = new List<Booking>();
}