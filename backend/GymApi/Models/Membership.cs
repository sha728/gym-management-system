namespace GymApi.Models;

public class Membership
{
    public Guid MembershipId { get; set; }

    public Guid UserId { get; set; }

    public Guid MembershipPlanId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string Status { get; set; } = "Active";

    public User User { get; set; } = null!;

    public MembershipPlan MembershipPlan { get; set; } = null!;
}