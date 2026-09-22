namespace GymApi.DTOs;

public class AdminBookingResponse
{
    public Guid BookingId { get; set; }
    public Guid UserId { get; set; }
    public string MemberEmail { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public string ProgramTitle { get; set; } = string.Empty;
    public string TrainerName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime BookedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
