namespace GymApi.Models;

public class Booking
{
    public Guid BookingId { get; set; }

    public Guid UserId { get; set; }

    public Guid SessionId { get; set; }

    public DateTime BookedAt { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = BookingStatus.Confirmed;

    public User User { get; set; } = null!;

    public Session Session { get; set; } = null!;
}