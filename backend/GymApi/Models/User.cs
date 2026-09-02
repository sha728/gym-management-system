namespace GymApi.Models;

public class User
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "Member";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

}