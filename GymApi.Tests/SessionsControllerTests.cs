using GymApi.Controllers;
using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    // Seeds a trainer with an availability window on the given day between startHour and endHour.
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

    // Returns the next occurrence of the given day of the week at a future date.
    private DateTime NextDay(DayOfWeek day, int startHour)
    {
        var today = DateTime.UtcNow.Date;
        int daysUntil = ((int)day - (int)today.DayOfWeek + 7) % 7;
        if (daysUntil == 0) daysUntil = 7; // always at least tomorrow
        return today.AddDays(daysUntil).AddHours(startHour);
    }

    [Fact]
    public async Task CreateSession_ValidRequest_ReturnsCreated()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);
        var controller = new SessionsController(db);

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
    public async Task CreateSession_TrainerOutsideAvailability_ReturnsConflict()
    {
        // Seed trainer with availability only on Monday.
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);
        var controller = new SessionsController(db);

        // Schedule on Wednesday, which is not in the trainer's availability.
        var start = NextDay(DayOfWeek.Wednesday, 9);

        var req = new CreateSessionRequest
        {
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start,
            EndTime = start.AddHours(1),
            Capacity = 10
        };

        var result = await controller.CreateSession(req);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task CreateSession_TrainerAlreadyBooked_ReturnsConflict()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);

        var start = NextDay(DayOfWeek.Monday, 9);

        // Seed an existing session for the same trainer at the same time.
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

        var req = new CreateSessionRequest
        {
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start,
            EndTime = start.AddHours(1),
            Capacity = 5
        };

        var result = await controller.CreateSession(req);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task CreateSession_StartTimeInPast_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);
        var controller = new SessionsController(db);

        var req = new CreateSessionRequest
        {
            FitnessProgramId = program.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = DateTime.UtcNow.AddDays(-1),
            EndTime = DateTime.UtcNow.AddDays(-1).AddHours(1),
            Capacity = 10
        };

        var result = await controller.CreateSession(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateSession_InactiveFitnessProgram_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var (trainer, _) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);

        var inactiveProgram = new FitnessProgram
        {
            FitnessProgramId = Guid.NewGuid(),
            Name = "Inactive Program",
            Description = "",
            DurationInMinutes = 45,
            IsActive = false
        };
        db.FitnessPrograms.Add(inactiveProgram);
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        var start = NextDay(DayOfWeek.Monday, 9);

        var req = new CreateSessionRequest
        {
            FitnessProgramId = inactiveProgram.FitnessProgramId,
            TrainerId = trainer.TrainerId,
            StartTime = start,
            EndTime = start.AddHours(1),
            Capacity = 10
        };

        var result = await controller.CreateSession(req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetSessions_ReturnsAllActiveSessions()
    {
        using var db = CreateDbContext();
        var (trainer, program) = await SeedTrainerAndProgram(db, DayOfWeek.Monday);

        db.Sessions.Add(new Session
        {
            SessionId = Guid.NewGuid(),
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
            IsActive = false   // this one should be excluded by default
        });
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        var result = await controller.GetSessions(null, null, false);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task GetSession_NotFound_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var controller = new SessionsController(db);

        var result = await controller.GetSession(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeleteSession_SoftDeletesSession()
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
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        var result = await controller.DeleteSession(session.SessionId);

        Assert.IsType<NoContentResult>(result);

        // Record should still exist, just marked inactive.
        var remaining = await db.Sessions.FindAsync(session.SessionId);
        Assert.NotNull(remaining);
        Assert.False(remaining!.IsActive);
    }
}
