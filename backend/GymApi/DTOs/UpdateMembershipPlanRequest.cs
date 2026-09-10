using System.ComponentModel.DataAnnotations;

namespace GymApi.DTOs;

public class UpdateMembershipPlanRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999.99")]
    public decimal Price { get; set; }

    [Range(1, 3650)]
    public int DurationInDays { get; set; }

    [Required]
    [StringLength(1000)]
    public string Benefits { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
