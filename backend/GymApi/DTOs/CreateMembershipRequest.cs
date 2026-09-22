using System.ComponentModel.DataAnnotations;

namespace GymApi.DTOs;

public class CreateMembershipRequest
{
    [Required]
    public Guid MembershipPlanId { get; set; }
}
