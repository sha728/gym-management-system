using GymApi.Controllers;
using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymApi.Tests;

public class TrainerAvailabilityControllerTests
{
    private GymDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GymDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GymDbContext(options);
    }

    private Trainer SeedTrainer(GymDbContext db)
    {
        var trainer = new Trainer
        {
            TrainerId = Guid.NewGuid(),
            Name = "Test Trainer",
            Specialization = "Yoga",
            IsActive = true
        };
        db.Trainers.Add(trainer);
        db.SaveChanges();
        return trainer;
    }

    [Fact]
    public async Task AddAvailability_ValidRequest_ReturnsCreated()
    {
        using var db = CreateDbContext();
        var trainer = SeedTrainer(db);
        var controller = new TrainerAvailabilityController(db);

        var req = new CreateTrainerAvailabilityRequest
        {
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(11, 0, 0)
        };

        var result = await controller.AddAvailability(trainer.TrainerId, req);

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(1, db.TrainerAvailabilities.Count());
    }

    [Fact]
    public async Task AddAvailability_StartTimeAfterEndTime_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var trainer = SeedTrainer(db);
        var controller = new TrainerAvailabilityController(db);

        var req = new CreateTrainerAvailabilityRequest
        {
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeSpan(11, 0, 0),
            EndTime = new TimeSpan(9, 0, 0)
        };

        var result = await controller.AddAvailability(trainer.TrainerId, req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddAvailability_OverlappingWindow_ReturnsConflict()
    {
        using var db = CreateDbContext();
        var trainer = SeedTrainer(db);
        var controller = new TrainerAvailabilityController(db);

        // Seed an existing availability window on Monday 9-11.
        db.TrainerAvailabilities.Add(new TrainerAvailability
        {
            TrainerAvailabilityId = Guid.NewGuid(),
            TrainerId = trainer.TrainerId,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(11, 0, 0)
        });
        await db.SaveChangesAsync();

        // Attempt to add an overlapping window on the same day.
        var req = new CreateTrainerAvailabilityRequest
        {
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0)
        };

        var result = await controller.AddAvailability(trainer.TrainerId, req);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task AddAvailability_TrainerNotFound_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var controller = new TrainerAvailabilityController(db);

        var req = new CreateTrainerAvailabilityRequest
        {
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(11, 0, 0)
        };

        var result = await controller.AddAvailability(Guid.NewGuid(), req);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetAvailability_ReturnsWindowsForTrainer()
    {
        using var db = CreateDbContext();
        var trainer = SeedTrainer(db);

        db.TrainerAvailabilities.Add(new TrainerAvailability
        {
            TrainerAvailabilityId = Guid.NewGuid(),
            TrainerId = trainer.TrainerId,
            DayOfWeek = DayOfWeek.Wednesday,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(16, 0, 0)
        });
        await db.SaveChangesAsync();

        var controller = new TrainerAvailabilityController(db);
        var result = await controller.GetAvailability(trainer.TrainerId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task DeleteAvailability_ExistingRecord_ReturnsNoContent()
    {
        using var db = CreateDbContext();
        var trainer = SeedTrainer(db);

        var record = new TrainerAvailability
        {
            TrainerAvailabilityId = Guid.NewGuid(),
            TrainerId = trainer.TrainerId,
            DayOfWeek = DayOfWeek.Friday,
            StartTime = new TimeSpan(18, 0, 0),
            EndTime = new TimeSpan(20, 0, 0)
        };
        db.TrainerAvailabilities.Add(record);
        await db.SaveChangesAsync();

        var controller = new TrainerAvailabilityController(db);
        var result = await controller.DeleteAvailability(trainer.TrainerId, record.TrainerAvailabilityId);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, db.TrainerAvailabilities.Count());
    }

    [Fact]
    public async Task UpdateAvailability_ValidChange_ReturnsOk()
    {
        using var db = CreateDbContext();
        var trainer = SeedTrainer(db);

        var record = new TrainerAvailability
        {
            TrainerAvailabilityId = Guid.NewGuid(),
            TrainerId = trainer.TrainerId,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(11, 0, 0)
        };
        db.TrainerAvailabilities.Add(record);
        await db.SaveChangesAsync();

        var controller = new TrainerAvailabilityController(db);

        var req = new UpdateTrainerAvailabilityRequest
        {
            DayOfWeek = (int)DayOfWeek.Tuesday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0)
        };

        var result = await controller.UpdateAvailability(trainer.TrainerId, record.TrainerAvailabilityId, req);

        Assert.IsType<OkObjectResult>(result);
    }
}
