using System.ComponentModel.DataAnnotations;

namespace GymApi.DTOs;

public class CreateBookingRequest
{
    [Required]
    public Guid SessionId { get; set; }
}
