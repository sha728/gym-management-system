using GymApi.Controllers;
using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymApi.Tests;

public class TrainersControllerTests
{
    private GymDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GymDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new GymDbContext(options);
    }

    [Fact]
    public async Task CreateTrainer_ReturnsCreatedAtAction_WithValidModel()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext();
        var controller = new TrainersController(dbContext);
        var request = new CreateTrainerRequest
        {
            Name = "John Doe",
            Specialization = "Strength",
            IsActive = true
        };

        // Act
        var result = await controller.AddTrainer(request);

        // Assert
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
        var trainer = Assert.IsType<Trainer>(createdAtActionResult.Value);
        Assert.Equal("John Doe", trainer.Name);
    }

    [Fact]
    public async Task DeleteTrainer_SoftDeletesTrainer_SetsIsActiveToFalse()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext();
        var trainerId = Guid.NewGuid();
        dbContext.Trainers.Add(new Trainer
        {
            TrainerId = trainerId,
            Name = "Jane Smith",
            Specialization = "Cardio",
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var controller = new TrainersController(dbContext);
        
        // Act
        var result = await controller.RemoveTrainer(trainerId);

        // Assert - Verify soft delete behavior for trainer entities
        Assert.IsType<NoContentResult>(result);
        var updatedTrainer = await dbContext.Trainers.FindAsync(trainerId);
        Assert.NotNull(updatedTrainer);
        Assert.False(updatedTrainer.IsActive);
    }
}