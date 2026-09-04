using GymApi.Controllers;
using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymApi.Tests;

public class FitnessProgramsControllerTests
{
    // Helper to generate a isolated clean database instance per test run
    private GymDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GymDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new GymDbContext(options);
    }

    [Fact]
    public async Task CreateProgram_ReturnsCreatedAtAction_WithValidModel()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext();
        var controller = new FitnessProgramsController(dbContext);
        var request = new CreateFitnessProgramRequest
        {
            Name = "Yoga",
            Description = "Flexibility training",
            DurationInMinutes = 60,
            IsActive = true
        };

        // Act
        var result = await controller.Create(request);

        // Assert - Verify status 201 Created and correct payload mapping
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
        var program = Assert.IsType<FitnessProgram>(createdAtActionResult.Value);
        Assert.Equal("Yoga", program.Name);
    }

    [Fact]
    public async Task DeleteProgram_SoftDeletesProgram_SetsIsActiveToFalse()
    {
        // Arrange - Seed test record into in-memory store
        var dbContext = GetInMemoryDbContext();
        var programId = Guid.NewGuid();
        dbContext.FitnessPrograms.Add(new FitnessProgram
        {
            FitnessProgramId = programId,
            Name = "Pilates",
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var controller = new FitnessProgramsController(dbContext);
        
        // Act
        var result = await controller.Delete(programId);

        // Assert - Ensure record remains in DB but flag is toggled off
        Assert.IsType<NoContentResult>(result);
        var updatedProgram = await dbContext.FitnessPrograms.FindAsync(programId);
        Assert.NotNull(updatedProgram);
        Assert.False(updatedProgram.IsActive);
    }
}