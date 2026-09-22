using System.ComponentModel.DataAnnotations;

namespace GymApi.DTOs;

public class ReviewMembershipRequest
{
    public bool Approve { get; set; }

    [StringLength(500)]
    public string? ReviewNote { get; set; }
}
