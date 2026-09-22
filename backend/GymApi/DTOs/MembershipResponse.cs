namespace GymApi.DTOs;

public class MembershipResponse
{
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string MemberEmail { get; set; } = string.Empty;
    public Guid MembershipPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationInDays { get; set; }
    public string Benefits { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ReviewNote { get; set; }
}
