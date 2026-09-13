namespace GymApi.Models;

public class Membership
{
    public Guid MembershipId { get; set; }

    public Guid UserId { get; set; }

    public Guid MembershipPlanId { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public MembershipStatus Status { get; set; } = MembershipStatus.Pending;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    // Approved memberships keep the terms accepted on that day, even if the plan changes later.
    public string? PlanName { get; set; }

    public decimal? PlanPrice { get; set; }

    public int? PlanDurationInDays { get; set; }

    public string? PlanBenefits { get; set; }

    public User User { get; set; } = null!;

    public MembershipPlan MembershipPlan { get; set; } = null!;
}
