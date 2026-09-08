namespace GymApi.DTOs;

public class BookingResponse
{
    public Guid BookingId { get; set; }
    public Guid SessionId { get; set; }
    public string ProgramTitle { get; set; } = string.Empty;
    public string TrainerName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime BookedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
