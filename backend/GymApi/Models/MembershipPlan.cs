namespace GymApi.Models;

public class MembershipPlan
{
    public Guid MembershipPlanId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int DurationInDays { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Membership> Memberships { get; set; }
        = new List<Membership>();
}