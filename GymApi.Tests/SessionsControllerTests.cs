using GymApi.Controllers;
using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Security.Claims;

namespace GymApi.Tests;

public class SessionsControllerTests
{
    private GymDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GymDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GymDbContext(options);
    }

    private void SetUserContext(ControllerBase controller, string role = "Admin")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private async Task<(Trainer trainer, FitnessProgram program)> SeedTrainerAndProgram(
        GymDbContext db,
        DayOfWeek day,
        int availStartHour = 8,
        int availEndHour = 20)
    {
        var trainer = new Trainer
        {
            TrainerId = Guid.NewGuid(),
            Name = "Test Trainer",
            Specialization = "HIIT",
            IsActive = true
        };

        var program = new FitnessProgram
        {
            FitnessProgramId = Guid.NewGuid(),
            Name = "Test Program",
            Description = "Desc",
            DurationInMinutes = 60,
            IsActive = true
        };

        var availability = new TrainerAvailability
        {
            TrainerAvailabilityId = Guid.NewGuid(),
            TrainerId = trainer.TrainerId,
            DayOfWeek = day,
            StartTime = new TimeSpan(availStartHour, 0, 0),
            EndTime = new TimeSpan(availEndHour, 0, 0)
        };

        db.Trainers.Add(trainer);
        db.FitnessPrograms.Add(program);
        db.TrainerAvailabilities.Add(availability);
        await db.SaveChangesAsync();

        return (trainer, program);
    }

    private DateTime NextDay(DayOfWeek day, int startHour)
    {
        var today = DateTime.UtcNow.Date;
        int daysUntil = ((int)day - (int)today.DayOfWeek + 7) % 7;
        if (daysUntil == 0) daysUntil = 7;
        return DateTime.SpecifyKind(today.AddDays(daysUntil).AddHours(startHour), DateTimeKind.Utc);
    }

    [Fact]
    public async Task CreateSession_ValidRequest_ReturnsCreated()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);
        var controller = new SessionsController(db);
        SetUserContext(controller, "Admin");

        var start = NextDay(DayOfWeek.Monday, 9);

        var req = new CreateSessionRequest
        {
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start,
            EndTime = start.AddHours(1),
            Capacity = 10
        };

        var result = await controller.CreateSession(req);

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(1, db.Sessions.Count());
    }

    [Fact]
    public async Task CreateSession_SpansMidnight_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);
        var controller = new SessionsController(db);
        SetUserContext(controller, "Admin");

        // 11 PM to 1 AM next day
        var start = NextDay(DayOfWeek.Monday, 23);
        var req = new CreateSessionRequest
        {
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start,
            EndTime = start.AddHours(2),
            Capacity = 10
        };

        var result = await controller.CreateSession(req);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateSession_UnspecifiedDateTimeKind_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);
        var controller = new SessionsController(db);
        SetUserContext(controller, "Admin");

        var unspecifiedStart = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(1), DateTimeKind.Unspecified);

        var req = new CreateSessionRequest
        {
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = unspecifiedStart,
            EndTime = unspecifiedStart.AddHours(1),
            Capacity = 10
        };

        var result = await controller.CreateSession(req);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateSession_PartialOverlapConflict_ReturnsConflict()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday, 8, 20);
        var start = NextDay(DayOfWeek.Monday, 10);

        // Existing session 10:00 to 11:00
        db.Sessions.Add(new Session
        {
            SessionId = Guid.NewGuid(),
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start,
            EndTime = start.AddHours(1),
            Capacity = 10,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        SetUserContext(controller, "Admin");

        // Attempting partial overlap: 10:30 to 11:30
        var req = new CreateSessionRequest
        {
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start.AddMinutes(30),
            EndTime = start.AddMinutes(90),
            Capacity = 10
        };

        var result = await controller.CreateSession(req);
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task GetSessions_ReturnsOnlyActiveSessionsForMembers()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);

        var activeId = Guid.NewGuid();
        db.Sessions.Add(new Session
        {
            SessionId = activeId,
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = NextDay(DayOfWeek.Monday, 9),
            EndTime = NextDay(DayOfWeek.Monday, 9).AddHours(1),
            Capacity = 15,
            IsActive = true
        });
        db.Sessions.Add(new Session
        {
            SessionId = Guid.NewGuid(),
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = NextDay(DayOfWeek.Monday, 11),
            EndTime = NextDay(DayOfWeek.Monday, 11).AddHours(1),
            Capacity = 10,
            IsActive = false
        });
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        SetUserContext(controller, "Member");

        var result = await controller.GetSessions(null, null, false);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable>(ok.Value);
        int count = 0;
        foreach (var _ in list) count++;
        Assert.Equal(1, count); // Asserts exactly 1 active session returned
    }

    [Fact]
    public async Task GetSessions_IncludeInactiveAsMember_ReturnsForbid()
    {
        using var db = CreateDbContext();
        var controller = new SessionsController(db);
        SetUserContext(controller, "Member");

        var result = await controller.GetSessions(null, null, includeInactive: true);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateSession_ValidChange_ReturnsOk()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);
        var start = NextDay(DayOfWeek.Monday, 9);

        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start,
            EndTime = start.AddHours(1),
            Capacity = 10,
            IsActive = true
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        SetUserContext(controller, "Admin");

        var req = new UpdateSessionRequest
        {
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start.AddMinutes(30),
            EndTime = start.AddHours(1).AddMinutes(30),
            Capacity = 12,
            IsActive = true
        };

        var result = await controller.UpdateSession(session.SessionId, req);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateSession_PastStartTime_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);
        var start = NextDay(DayOfWeek.Monday, 9);

        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start,
            EndTime = start.AddHours(1),
            Capacity = 10,
            IsActive = true
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        SetUserContext(controller, "Admin");

        var pastStart = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-1), DateTimeKind.Utc);
        var req = new UpdateSessionRequest
        {
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = pastStart,
            EndTime = pastStart.AddHours(1),
            Capacity = 10,
            IsActive = true
        };

        var result = await controller.UpdateSession(session.SessionId, req);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateSession_SpansMidnight_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);
        var start = NextDay(DayOfWeek.Monday, 9);

        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start,
            EndTime = start.AddHours(1),
            Capacity = 10,
            IsActive = true
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        SetUserContext(controller, "Admin");

        var req = new UpdateSessionRequest
        {
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start.AddHours(14),
            EndTime = start.AddDays(1).AddHours(1),
            Capacity = 10,
            IsActive = true
        };

        var result = await controller.UpdateSession(session.SessionId, req);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateSession_CapacityBelowBookings_ReturnsConflict()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);
        var start = NextDay(DayOfWeek.Monday, 9);

        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start,
            EndTime = start.AddHours(1),
            Capacity = 10,
            IsActive = true
        };
        db.Sessions.Add(session);

        // Add 5 confirmed bookings
        for (int i = 0; i < 5; i++)
        {
            db.Bookings.Add(new Booking
            {
                BookingId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                SessionId = session.SessionId,
                BookedAt = DateTime.UtcNow,
                Status = "Confirmed"
            });
        }
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        SetUserContext(controller, "Admin");

        // Attempting to reduce capacity to 3 (below 5 confirmed bookings)
        var req = new UpdateSessionRequest
        {
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start,
            EndTime = start.AddHours(1),
            Capacity = 3,
            IsActive = true
        };

        var result = await controller.UpdateSession(session.SessionId, req);
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task DeleteSession_CancelsConfirmedBookings()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);
        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = NextDay(DayOfWeek.Monday, 9),
            EndTime = NextDay(DayOfWeek.Monday, 9).AddHours(1),
            Capacity = 10,
            IsActive = true
        };
        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SessionId = session.SessionId,
            Status = BookingStatus.Confirmed
        };
        db.Sessions.Add(session);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        SetUserContext(controller, "Admin");

        var result = await controller.DeleteSession(session.SessionId);

        Assert.IsType<NoContentResult>(result);
        Assert.False(session.IsActive);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }
}
