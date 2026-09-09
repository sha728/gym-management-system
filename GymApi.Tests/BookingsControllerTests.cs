using GymApi.Controllers;
using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymApi.Tests;

public class BookingsControllerTests
{
    private GymDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GymDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GymDbContext(options);
    }

    private void SetUserContext(ControllerBase controller, Guid userId, string role = "Member")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private async Task<(User user, Session session)> SeedUserAndSession(
        GymDbContext db,
        bool isSessionActive = true,
        int capacity = 10,
        int hoursInFuture = 24)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = $"user_{Guid.NewGuid()}@example.com",
            Role = "Member"
        };

        var trainer = new Trainer
        {
            TrainerId = Guid.NewGuid(),
            Name = "Trainer Bob",
            Specialization = "Fitness",
            IsActive = true
        };

        var program = new FitnessProgram
        {
            FitnessProgramId = Guid.NewGuid(),
            Name = "Cardio Blast",
            Description = "High energy cardio session",
            DurationInMinutes = 45,
            IsActive = true
        };

        var startTime = DateTime.UtcNow.AddHours(hoursInFuture);
        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = startTime,
            EndTime = startTime.AddMinutes(45),
            Capacity = capacity,
            IsActive = isSessionActive,
            FitnessProgram = program,
            Trainer = trainer
        };

        db.Users.Add(user);
        db.Trainers.Add(trainer);
        db.FitnessPrograms.Add(program);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        return (user, session);
    }

    [Fact]
    public async Task CreateBooking_ValidRequest_ReturnsCreated()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db);
        var controller = new BookingsController(db);
        SetUserContext(controller, user.UserId);

        var req = new CreateBookingRequest { SessionId = session.SessionId };
        var result = await controller.CreateBooking(req);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<BookingResponse>(created.Value);
        Assert.Equal(session.SessionId, response.SessionId);
        Assert.Equal("Confirmed", response.Status);
        Assert.Equal(1, db.Bookings.Count());
    }

    [Fact]
    public async Task CreateBooking_SessionNotFound_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var (user, _) = await SeedUserAndSession(db);
        var controller = new BookingsController(db);
        SetUserContext(controller, user.UserId);

        var req = new CreateBookingRequest { SessionId = Guid.NewGuid() };
        var result = await controller.CreateBooking(req);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CreateBooking_InactiveSession_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db, isSessionActive: false);
        var controller = new BookingsController(db);
        SetUserContext(controller, user.UserId);

        var req = new CreateBookingRequest { SessionId = session.SessionId };
        var result = await controller.CreateBooking(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateBooking_PastSession_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db, hoursInFuture: -5);
        var controller = new BookingsController(db);
        SetUserContext(controller, user.UserId);

        var req = new CreateBookingRequest { SessionId = session.SessionId };
        var result = await controller.CreateBooking(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateBooking_FullCapacity_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db, capacity: 1);

        // Add 1 confirmed booking from another member to max out capacity
        var otherUser = new User { UserId = Guid.NewGuid(), Email = "other@example.com" };
        db.Users.Add(otherUser);
        db.Bookings.Add(new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = otherUser.UserId,
            SessionId = session.SessionId,
            BookedAt = DateTime.UtcNow,
            Status = "Confirmed"
        });
        await db.SaveChangesAsync();

        var controller = new BookingsController(db);
        SetUserContext(controller, user.UserId);

        var req = new CreateBookingRequest { SessionId = session.SessionId };
        var result = await controller.CreateBooking(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateBooking_DuplicateBooking_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db);

        // Existing booking for the same user and session
        db.Bookings.Add(new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = user.UserId,
            SessionId = session.SessionId,
            BookedAt = DateTime.UtcNow,
            Status = "Confirmed"
        });
        await db.SaveChangesAsync();

        var controller = new BookingsController(db);
        SetUserContext(controller, user.UserId);

        var req = new CreateBookingRequest { SessionId = session.SessionId };
        var result = await controller.CreateBooking(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateBooking_OverlappingConfirmedBooking_ReturnsConflict()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db);
        var overlappingSession = new Session
        {
            SessionId = Guid.NewGuid(),
            FitnessProgramId = session.FitnessProgramId,
            TrainerId = session.TrainerId,
            StartTime = session.StartTime.AddMinutes(15),
            EndTime = session.EndTime.AddMinutes(15),
            Capacity = 10,
            IsActive = true
        };
        db.Sessions.Add(overlappingSession);
        db.Bookings.Add(new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = user.UserId,
            SessionId = session.SessionId,
            BookedAt = DateTime.UtcNow,
            Status = "Confirmed"
        });
        await db.SaveChangesAsync();

        var controller = new BookingsController(db);
        SetUserContext(controller, user.UserId);

        var result = await controller.CreateBooking(new CreateBookingRequest
        {
            SessionId = overlappingSession.SessionId
        });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task CreateBooking_InvalidIdentity_ReturnsUnauthorized()
    {
        using var db = CreateDbContext();
        var (_, session) = await SeedUserAndSession(db);
        var controller = new BookingsController(db);
        SetUserContext(controller, Guid.Empty);
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") }, "TestAuth"));

        var result = await controller.CreateBooking(new CreateBookingRequest
        {
            SessionId = session.SessionId
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void CreateBooking_RequiresMemberRole()
    {
        var authorizeAttribute = typeof(BookingsController)
            .GetMethod(nameof(BookingsController.CreateBooking))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("Member", authorizeAttribute.Roles);
    }

    [Fact]
    public async Task GetMyBookings_ReturnsUserBookings()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db);

        db.Bookings.Add(new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = user.UserId,
            SessionId = session.SessionId,
            BookedAt = DateTime.UtcNow,
            Status = "Confirmed"
        });
        await db.SaveChangesAsync();

        var controller = new BookingsController(db);
        SetUserContext(controller, user.UserId);

        var result = await controller.GetMyBookings();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<BookingResponse>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task GetMyBookings_DoesNotReturnOtherMembersBookings()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db);
        var otherUser = new User
        {
            UserId = Guid.NewGuid(),
            Email = "other-booking-member@example.com",
            Role = "Member"
        };

        db.Users.Add(otherUser);
        db.Bookings.AddRange(
            new Booking
            {
                BookingId = Guid.NewGuid(),
                UserId = user.UserId,
                SessionId = session.SessionId,
                BookedAt = DateTime.UtcNow,
                Status = BookingStatus.Confirmed
            },
            new Booking
            {
                BookingId = Guid.NewGuid(),
                UserId = otherUser.UserId,
                SessionId = session.SessionId,
                BookedAt = DateTime.UtcNow,
                Status = BookingStatus.Confirmed
            });
        await db.SaveChangesAsync();

        var controller = new BookingsController(db);
        SetUserContext(controller, user.UserId);

        var result = await controller.GetMyBookings();

        var ok = Assert.IsType<OkObjectResult>(result);
        var bookings = Assert.IsAssignableFrom<IEnumerable<BookingResponse>>(ok.Value).ToList();
        Assert.Single(bookings);
    }

    [Fact]
    public async Task GetBooking_OtherMember_ReturnsForbid()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db);
        var otherUser = new User
        {
            UserId = Guid.NewGuid(),
            Email = "other-member@example.com",
            Role = "Member"
        };
        db.Users.Add(otherUser);
        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = user.UserId,
            SessionId = session.SessionId,
            BookedAt = DateTime.UtcNow,
            Status = "Confirmed"
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var controller = new BookingsController(db);
        SetUserContext(controller, otherUser.UserId);

        var result = await controller.GetBooking(booking.BookingId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CancelBooking_Success_ReturnsOk()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db, hoursInFuture: 10);

        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = user.UserId,
            SessionId = session.SessionId,
            BookedAt = DateTime.UtcNow,
            Status = "Confirmed"
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var controller = new BookingsController(db);
        SetUserContext(controller, user.UserId);

        var result = await controller.CancelBooking(booking.BookingId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var updatedBooking = await db.Bookings.FindAsync(booking.BookingId);
        Assert.Equal("Cancelled", updatedBooking!.Status);
    }

    [Fact]
    public async Task CancelBooking_PastSession_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db, hoursInFuture: -2);

        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = user.UserId,
            SessionId = session.SessionId,
            BookedAt = DateTime.UtcNow.AddDays(-1),
            Status = "Confirmed"
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var controller = new BookingsController(db);
        SetUserContext(controller, user.UserId);

        var result = await controller.CancelBooking(booking.BookingId);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CancelBooking_OtherMember_ReturnsForbid()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db);
        var otherUser = new User
        {
            UserId = Guid.NewGuid(),
            Email = "other-cancelling-member@example.com",
            Role = "Member"
        };
        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = user.UserId,
            SessionId = session.SessionId,
            BookedAt = DateTime.UtcNow,
            Status = BookingStatus.Confirmed
        };

        db.Users.Add(otherUser);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var controller = new BookingsController(db);
        SetUserContext(controller, otherUser.UserId);

        var result = await controller.CancelBooking(booking.BookingId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public async Task CancelBooking_MakesSeatAvailableForAnotherMember()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db, capacity: 1);
        var otherUser = new User
        {
            UserId = Guid.NewGuid(),
            Email = "replacement-member@example.com",
            Role = "Member"
        };
        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = user.UserId,
            SessionId = session.SessionId,
            BookedAt = DateTime.UtcNow,
            Status = BookingStatus.Confirmed
        };

        db.Users.Add(otherUser);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var cancelController = new BookingsController(db);
        SetUserContext(cancelController, user.UserId);
        await cancelController.CancelBooking(booking.BookingId);

        var bookingController = new BookingsController(db);
        SetUserContext(bookingController, otherUser.UserId);
        var result = await bookingController.CreateBooking(new CreateBookingRequest
        {
            SessionId = session.SessionId
        });

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(2, db.Bookings.Count());
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public async Task CancelBooking_AlreadyCancelled_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var (user, session) = await SeedUserAndSession(db);
        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = user.UserId,
            SessionId = session.SessionId,
            BookedAt = DateTime.UtcNow,
            Status = "Cancelled"
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var controller = new BookingsController(db);
        SetUserContext(controller, user.UserId);

        var result = await controller.CancelBooking(booking.BookingId);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
