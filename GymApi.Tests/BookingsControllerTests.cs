using System.Security.Claims;
using GymApi.Controllers;
using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymApi.Tests;

public class BookingsControllerTests
{
    // Each test gets a separate in-memory database so that test data does not affect other tests.
    private GymDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GymDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GymDbContext(options);
    }

    private static void SetUser(ControllerBase controller, Guid userId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
    }

    private static async Task<(FitnessProgram program, Trainer trainer, Session session)> SeedSessionAsync(
        GymDbContext db,
        int capacity = 1,
        DateTime? startTime = null,
        bool isActive = true)
    {
        var program = new FitnessProgram
        {
            FitnessProgramId = Guid.NewGuid(),
            Name = "Yoga",
            Description = "Relaxing yoga class",
            DurationInMinutes = 60,
            IsActive = true
        };

        var trainer = new Trainer
        {
            TrainerId = Guid.NewGuid(),
            Name = "Alex Trainer",
            Specialization = "Yoga",
            IsActive = true
        };

        var start = startTime ?? DateTime.UtcNow.AddDays(1);

        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start,
            EndTime = start.AddHours(1),
            Capacity = capacity,
            IsActive = isActive
        };

        db.FitnessPrograms.Add(program);
        db.Trainers.Add(trainer);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        return (program, trainer, session);
    }

    private static async Task<User> SeedMemberAsync(GymDbContext db, string email = "member@test.com")
    {
        var member = new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            Role = "Member",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(member);
        await db.SaveChangesAsync();

        return member;
    }

    [Fact]
    public async Task CreateBooking_ForOpenFutureSession_ReturnsCreatedBooking()
    {
        using var dbContext = CreateDbContext();
        var (_, _, session) = await SeedSessionAsync(dbContext);
        var member = await SeedMemberAsync(dbContext);

        var controller = new BookingsController(dbContext);
        SetUser(controller, member.UserId, "Member");

        var result = await controller.CreateBooking(new CreateBookingRequest { SessionId = session.SessionId });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<BookingResponse>(created.Value);

        Assert.Equal(session.SessionId, response.SessionId);
        Assert.Equal(BookingStatus.Confirmed, response.Status);
    }

    [Fact]
    public async Task CreateBooking_ForInactiveSession_ReturnsBadRequest()
    {
        using var dbContext = CreateDbContext();
        var (_, _, session) = await SeedSessionAsync(dbContext, isActive: false);
        var member = await SeedMemberAsync(dbContext);

        var controller = new BookingsController(dbContext);
        SetUser(controller, member.UserId, "Member");

        var result = await controller.CreateBooking(new CreateBookingRequest { SessionId = session.SessionId });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateBooking_ForSessionThatAlreadyStarted_ReturnsBadRequest()
    {
        using var dbContext = CreateDbContext();
        var (_, _, session) = await SeedSessionAsync(dbContext, startTime: DateTime.UtcNow.AddMinutes(-30));
        var member = await SeedMemberAsync(dbContext);

        var controller = new BookingsController(dbContext);
        SetUser(controller, member.UserId, "Member");

        var result = await controller.CreateBooking(new CreateBookingRequest { SessionId = session.SessionId });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateBooking_WhenSessionIsFull_ReturnsBadRequest()
    {
        using var dbContext = CreateDbContext();
        var (_, _, session) = await SeedSessionAsync(dbContext, capacity: 1);
        var existingMember = await SeedMemberAsync(dbContext, "first@test.com");
        var newMember = await SeedMemberAsync(dbContext, "second@test.com");

        dbContext.Bookings.Add(new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = existingMember.UserId,
            SessionId = session.SessionId,
            Status = BookingStatus.Confirmed
        });
        await dbContext.SaveChangesAsync();

        var controller = new BookingsController(dbContext);
        SetUser(controller, newMember.UserId, "Member");

        var result = await controller.CreateBooking(new CreateBookingRequest { SessionId = session.SessionId });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateBooking_WhenAlreadyBookedBySameMember_ReturnsBadRequest()
    {
        using var dbContext = CreateDbContext();
        var (_, _, session) = await SeedSessionAsync(dbContext, capacity: 5);
        var member = await SeedMemberAsync(dbContext);

        dbContext.Bookings.Add(new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = member.UserId,
            SessionId = session.SessionId,
            Status = BookingStatus.Confirmed
        });
        await dbContext.SaveChangesAsync();

        var controller = new BookingsController(dbContext);
        SetUser(controller, member.UserId, "Member");

        var result = await controller.CreateBooking(new CreateBookingRequest { SessionId = session.SessionId });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateBooking_WithOverlappingConfirmedSession_ReturnsConflict()
    {
        using var dbContext = CreateDbContext();
        var start = DateTime.UtcNow.AddDays(1);

        var (_, _, firstSession) = await SeedSessionAsync(dbContext, startTime: start);
        var (_, _, secondSession) = await SeedSessionAsync(dbContext, startTime: start.AddMinutes(30));
        var member = await SeedMemberAsync(dbContext);

        dbContext.Bookings.Add(new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = member.UserId,
            SessionId = firstSession.SessionId,
            Status = BookingStatus.Confirmed
        });
        await dbContext.SaveChangesAsync();

        var controller = new BookingsController(dbContext);
        SetUser(controller, member.UserId, "Member");

        var result = await controller.CreateBooking(new CreateBookingRequest { SessionId = secondSession.SessionId });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task CancelBooking_ByOwner_MarksBookingCancelled()
    {
        using var dbContext = CreateDbContext();
        var (_, _, session) = await SeedSessionAsync(dbContext);
        var member = await SeedMemberAsync(dbContext);

        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = member.UserId,
            SessionId = session.SessionId,
            Status = BookingStatus.Confirmed
        };
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        var controller = new BookingsController(dbContext);
        SetUser(controller, member.UserId, "Member");

        var result = await controller.CancelBooking(booking.BookingId);

        Assert.IsType<OkObjectResult>(result);

        var updated = await dbContext.Bookings.FindAsync(booking.BookingId);
        Assert.Equal(BookingStatus.Cancelled, updated!.Status);
    }

    [Fact]
    public async Task CancelBooking_ByAnotherMember_ReturnsNotFound()
    {
        using var dbContext = CreateDbContext();
        var (_, _, session) = await SeedSessionAsync(dbContext);
        var owner = await SeedMemberAsync(dbContext, "owner@test.com");
        var otherMember = await SeedMemberAsync(dbContext, "other@test.com");

        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = owner.UserId,
            SessionId = session.SessionId,
            Status = BookingStatus.Confirmed
        };
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        var controller = new BookingsController(dbContext);
        SetUser(controller, otherMember.UserId, "Member");

        var result = await controller.CancelBooking(booking.BookingId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CancelBooking_ForSessionThatAlreadyStarted_ReturnsBadRequest()
    {
        using var dbContext = CreateDbContext();
        var (_, _, session) = await SeedSessionAsync(dbContext, startTime: DateTime.UtcNow.AddMinutes(-15));
        var member = await SeedMemberAsync(dbContext);

        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = member.UserId,
            SessionId = session.SessionId,
            Status = BookingStatus.Confirmed
        };
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        var controller = new BookingsController(dbContext);
        SetUser(controller, member.UserId, "Member");

        var result = await controller.CancelBooking(booking.BookingId);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAllBookings_AsAdmin_ReturnsAllBookingsPaged()
    {
        using var dbContext = CreateDbContext();
        var (_, _, session) = await SeedSessionAsync(dbContext, capacity: 5);
        var memberOne = await SeedMemberAsync(dbContext, "one@test.com");
        var memberTwo = await SeedMemberAsync(dbContext, "two@test.com");

        dbContext.Bookings.AddRange(
            new Booking { BookingId = Guid.NewGuid(), UserId = memberOne.UserId, SessionId = session.SessionId, Status = BookingStatus.Confirmed },
            new Booking { BookingId = Guid.NewGuid(), UserId = memberTwo.UserId, SessionId = session.SessionId, Status = BookingStatus.Cancelled }
        );
        await dbContext.SaveChangesAsync();

        var controller = new BookingsController(dbContext);
        SetUser(controller, Guid.NewGuid(), "Admin");

        var result = await controller.GetAllBookings();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, GetPropertyValue<int>(ok.Value!, "totalCount"));
    }

    [Fact]
    public async Task GetAllBookings_FilteredByStatus_ReturnsOnlyMatchingBookings()
    {
        using var dbContext = CreateDbContext();
        var (_, _, session) = await SeedSessionAsync(dbContext, capacity: 5);
        var memberOne = await SeedMemberAsync(dbContext, "one@test.com");
        var memberTwo = await SeedMemberAsync(dbContext, "two@test.com");

        dbContext.Bookings.AddRange(
            new Booking { BookingId = Guid.NewGuid(), UserId = memberOne.UserId, SessionId = session.SessionId, Status = BookingStatus.Confirmed },
            new Booking { BookingId = Guid.NewGuid(), UserId = memberTwo.UserId, SessionId = session.SessionId, Status = BookingStatus.Cancelled }
        );
        await dbContext.SaveChangesAsync();

        var controller = new BookingsController(dbContext);
        SetUser(controller, Guid.NewGuid(), "Admin");

        var result = await controller.GetAllBookings(status: BookingStatus.Cancelled);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, GetPropertyValue<int>(ok.Value!, "totalCount"));
    }

    // GetAllBookings returns an anonymous type, so reflection is used to read its fields from outside the assembly.
    private static T GetPropertyValue<T>(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Property '{propertyName}' not found on {source.GetType()}.");

        return (T)property.GetValue(source)!;
    }
}
