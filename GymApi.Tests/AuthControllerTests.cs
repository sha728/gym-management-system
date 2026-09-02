using GymApi.Controllers;
using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GymApi.Tests;

public class AuthControllerTests
{
    // Each test gets a separate in-memory database so that test data does not affect other tests.
    private GymDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GymDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GymDbContext(options);
    }

    // Generate a temporary JWT key for tests instead of storing a credential-like value in source code.
    private string CreateTestJwtKey()
    {
        return Guid.NewGuid().ToString() + Guid.NewGuid().ToString();
    }

    // Generate temporary passwords so test credentials are not hardcoded in the repository.
    private string CreateTestPassword()
    {
        return Guid.NewGuid().ToString();
    }

    private IConfiguration CreateConfiguration()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = CreateTestJwtKey()
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    [Fact]
    public async Task Register_WithNewEmail_ReturnsSuccess()
    {
        using var dbContext = CreateDbContext();
        var configuration = CreateConfiguration();

        var controller = new AuthController(dbContext, configuration);

        var password = CreateTestPassword();

        var request = new RegisterRequest
        {
            Email = "newmember@test.com",
            Password = password
        };

        var result = await controller.Register(request);

        var okResult = Assert.IsType<OkObjectResult>(result);

        Assert.Equal(
            "Registration successful.",
            okResult.Value
        );

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        Assert.NotNull(user);
        Assert.Equal("Member", user.Role);

        // Make sure the password was hashed instead of being stored as plain text.
        Assert.NotEqual(request.Password, user.PasswordHash);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsBadRequest()
    {
        using var dbContext = CreateDbContext();
        var configuration = CreateConfiguration();

        var existingUser = new User
        {
            UserId = Guid.NewGuid(),
            Email = "existing@test.com",
            Role = "Member",
            PasswordHash = "existing-hash",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(existingUser);
        await dbContext.SaveChangesAsync();

        var controller = new AuthController(dbContext, configuration);

        var request = new RegisterRequest
        {
            Email = "existing@test.com",
            Password = CreateTestPassword()
        };

        var result = await controller.Register(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            "A user with this email already exists.",
            badRequest.Value
        );
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsToken()
    {
        using var dbContext = CreateDbContext();
        var configuration = CreateConfiguration();

        var password = CreateTestPassword();

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "login@test.com",
            Role = "Member",
            CreatedAt = DateTime.UtcNow
        };

        var passwordHasher = new PasswordHasher<User>();

        user.PasswordHash = passwordHasher.HashPassword(
            user,
            password
        );

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var controller = new AuthController(dbContext, configuration);

        var request = new LoginRequest
        {
            Email = "login@test.com",
            Password = password
        };

        var result = await controller.Login(request);

        var okResult = Assert.IsType<OkObjectResult>(result);

        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Login_WithIncorrectPassword_ReturnsUnauthorized()
    {
        using var dbContext = CreateDbContext();
        var configuration = CreateConfiguration();

        var correctPassword = CreateTestPassword();
        var wrongPassword = CreateTestPassword();

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "wrongpassword@test.com",
            Role = "Member",
            CreatedAt = DateTime.UtcNow
        };

        var passwordHasher = new PasswordHasher<User>();

        user.PasswordHash = passwordHasher.HashPassword(
            user,
            correctPassword
        );

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var controller = new AuthController(dbContext, configuration);

        var request = new LoginRequest
        {
            Email = "wrongpassword@test.com",
            Password = wrongPassword
        };

        var result = await controller.Login(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);

        Assert.Equal(
            "Invalid email or password.",
            unauthorized.Value
        );
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        using var dbContext = CreateDbContext();
        var configuration = CreateConfiguration();

        var controller = new AuthController(dbContext, configuration);

        var request = new LoginRequest
        {
            Email = "doesnotexist@test.com",
            Password = CreateTestPassword()
        };

        var result = await controller.Login(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);

        Assert.Equal(
            "Invalid email or password.",
            unauthorized.Value
        );
    }
}