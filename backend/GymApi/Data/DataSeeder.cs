using GymApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GymApi.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<GymDbContext>();
        var passwordHasher = new PasswordHasher<User>();
        // Create the initial admin account if one does not already exist.
        var adminExists = await dbContext.Users
            .AnyAsync(u => u.Role == "Admin");

        if (adminExists)
        {
            return;
        }

        var admin = new User
        {
            UserId = Guid.NewGuid(),
            Email = "admin@gym.com",
            Role = "Admin",
            CreatedAt = DateTime.UtcNow
        };
        // Hash the development password before storing it in the database.
        admin.PasswordHash = passwordHasher.HashPassword(admin, "Admin@123");

        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync();
    }
}