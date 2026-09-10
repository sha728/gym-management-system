namespace GymApi.Models;

public class Membership
{
    public Guid MembershipId { get; set; }

    public Guid UserId { get; set; }

    public Guid MembershipPlanId { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string Status { get; set; } = MembershipStatus.Pending;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public User User { get; set; } = null!;

    public MembershipPlan MembershipPlan { get; set; } = null!;
}
