namespace GymApi.Models;

public class FitnessProgram
{
    public Guid FitnessProgramId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int DurationInMinutes { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Session> Sessions { get; set; }
        = new List<Session>();
}