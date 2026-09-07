using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymApi.Controllers;

[ApiController]
[Route("api/trainers/{trainerId:guid}/availability")]
public class TrainerAvailabilityController : ControllerBase
{
    private readonly GymDbContext _db;

    public TrainerAvailabilityController(GymDbContext db)
    {
        _db = db;
    }

    // Members can view a trainer's availability so they know when sessions might be offered.
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAvailability(Guid trainerId)
    {
        var trainerExists = await _db.Trainers.AnyAsync(t => t.TrainerId == trainerId);
        if (!trainerExists)
            return NotFound(new { message = "Trainer not found." });

        var availability = await _db.TrainerAvailabilities
            .Where(a => a.TrainerId == trainerId)
            .OrderBy(a => a.DayOfWeek)
            .ThenBy(a => a.StartTime)
            .ToListAsync();

        return Ok(availability);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid trainerId, Guid id)
    {
        var record = await _db.TrainerAvailabilities
            .FirstOrDefaultAsync(a => a.TrainerAvailabilityId == id && a.TrainerId == trainerId);

        if (record == null)
            return NotFound(new { message = "Availability record not found." });

        return Ok(record);
    }

    // Only admins can define or modify a trainer's available time windows.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddAvailability(
        Guid trainerId,
        [FromBody] CreateTrainerAvailabilityRequest req)
    {
        var trainerExists = await _db.Trainers.AnyAsync(t => t.TrainerId == trainerId);
        if (!trainerExists)
            return NotFound(new { message = "Trainer not found." });

        if (req.StartTime >= req.EndTime)
            return BadRequest(new { message = "StartTime must be before EndTime." });

        // Prevent overlapping availability windows for the same trainer on the same day.
        var overlap = await _db.TrainerAvailabilities.AnyAsync(a =>
            a.TrainerId == trainerId &&
            a.DayOfWeek == (DayOfWeek)req.DayOfWeek &&
            a.StartTime < req.EndTime &&
            a.EndTime > req.StartTime);

        if (overlap)
            return Conflict(new { message = "This availability window overlaps with an existing one for this trainer." });

        var record = new TrainerAvailability
        {
            TrainerAvailabilityId = Guid.NewGuid(),
            TrainerId = trainerId,
            DayOfWeek = (DayOfWeek)req.DayOfWeek,
            StartTime = req.StartTime,
            EndTime = req.EndTime
        };

        _db.TrainerAvailabilities.Add(record);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { trainerId, id = record.TrainerAvailabilityId }, record);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAvailability(
        Guid trainerId,
        Guid id,
        [FromBody] UpdateTrainerAvailabilityRequest req)
    {
        var record = await _db.TrainerAvailabilities
            .FirstOrDefaultAsync(a => a.TrainerAvailabilityId == id && a.TrainerId == trainerId);

        if (record == null)
            return NotFound(new { message = "Availability record not found." });

        if (req.StartTime >= req.EndTime)
            return BadRequest(new { message = "StartTime must be before EndTime." });

        // Overlap check excludes the record being updated.
        var overlap = await _db.TrainerAvailabilities.AnyAsync(a =>
            a.TrainerId == trainerId &&
            a.TrainerAvailabilityId != id &&
            a.DayOfWeek == (DayOfWeek)req.DayOfWeek &&
            a.StartTime < req.EndTime &&
            a.EndTime > req.StartTime);

        if (overlap)
            return Conflict(new { message = "This availability window overlaps with an existing one for this trainer." });

        record.DayOfWeek = (DayOfWeek)req.DayOfWeek;
        record.StartTime = req.StartTime;
        record.EndTime = req.EndTime;

        await _db.SaveChangesAsync();
        return Ok(record);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAvailability(Guid trainerId, Guid id)
    {
        var record = await _db.TrainerAvailabilities
            .FirstOrDefaultAsync(a => a.TrainerAvailabilityId == id && a.TrainerId == trainerId);

        if (record == null)
            return NotFound(new { message = "Availability record not found." });

        _db.TrainerAvailabilities.Remove(record);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
