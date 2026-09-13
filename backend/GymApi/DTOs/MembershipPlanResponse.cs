namespace GymApi.DTOs;

public class MembershipPlanResponse
{
    public Guid MembershipPlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationInDays { get; set; }
    public string Benefits { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
