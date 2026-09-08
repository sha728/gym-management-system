using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
            .Select(a => new
            {
                a.TrainerAvailabilityId,
                a.TrainerId,
                DayOfWeek = (int)a.DayOfWeek,
                a.StartTime,
                a.EndTime
            })
            .ToListAsync();

        return Ok(availability);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid trainerId, Guid id)
    {
        var record = await _db.TrainerAvailabilities
            .Where(a => a.TrainerAvailabilityId == id && a.TrainerId == trainerId)
            .Select(a => new
            {
                a.TrainerAvailabilityId,
                a.TrainerId,
                DayOfWeek = (int)a.DayOfWeek,
                a.StartTime,
                a.EndTime
            })
            .FirstOrDefaultAsync();

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

        var record = new TrainerAvailability
        {
            TrainerAvailabilityId = Guid.NewGuid(),
            TrainerId = trainerId,
            DayOfWeek = (DayOfWeek)req.DayOfWeek,
            StartTime = req.StartTime,
            EndTime = req.EndTime
        };

        try
        {
            using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                : null;

            var overlap = await _db.TrainerAvailabilities.AnyAsync(a =>
                a.TrainerId == trainerId &&
                a.DayOfWeek == (DayOfWeek)req.DayOfWeek &&
                a.StartTime < req.EndTime &&
                a.EndTime > req.StartTime);

            if (overlap)
                return Conflict(new { message = "This availability window overlaps with an existing one for this trainer." });

            _db.TrainerAvailabilities.Add(record);
            await _db.SaveChangesAsync();

            if (transaction != null)
                await transaction.CommitAsync();
        }
        catch (Exception ex) when (IsConcurrencyFailure(ex))
        {
            return Conflict(new { message = "The trainer availability changed while this window was being saved. Please try again." });
        }

        var response = new
        {
            record.TrainerAvailabilityId,
            record.TrainerId,
            DayOfWeek = (int)record.DayOfWeek,
            record.StartTime,
            record.EndTime
        };

        return CreatedAtAction(nameof(GetById), new { trainerId, id = record.TrainerAvailabilityId }, response);
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

        try
        {
            using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                : null;

            // Do not strand future sessions outside the proposed working-hours window.
            var hasActiveSessions = await _db.Sessions.AnyAsync(s =>
                s.TrainerId == trainerId &&
                s.IsActive &&
                s.StartTime > DateTime.UtcNow &&
                s.StartTime.DayOfWeek == record.DayOfWeek &&
                s.StartTime.TimeOfDay >= record.StartTime &&
                s.EndTime.TimeOfDay <= record.EndTime &&
                (s.StartTime.DayOfWeek != (DayOfWeek)req.DayOfWeek ||
                 s.StartTime.TimeOfDay < req.StartTime ||
                 s.EndTime.TimeOfDay > req.EndTime));

            if (hasActiveSessions)
                return Conflict(new { message = "Cannot update availability while active upcoming sessions fall outside the proposed window." });

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

            if (transaction != null)
                await transaction.CommitAsync();
        }
        catch (Exception ex) when (IsConcurrencyFailure(ex))
        {
            return Conflict(new { message = "The trainer availability changed while this window was being saved. Please try again." });
        }

        var response = new
        {
            record.TrainerAvailabilityId,
            record.TrainerId,
            DayOfWeek = (int)record.DayOfWeek,
            record.StartTime,
            record.EndTime
        };

        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAvailability(Guid trainerId, Guid id)
    {
        var record = await _db.TrainerAvailabilities
            .FirstOrDefaultAsync(a => a.TrainerAvailabilityId == id && a.TrainerId == trainerId);

        if (record == null)
            return NotFound(new { message = "Availability record not found." });

        // Prevent deleting working hours if there are active upcoming sessions scheduled inside them.
        var hasActiveSessions = await _db.Sessions.AnyAsync(s =>
            s.TrainerId == trainerId &&
            s.IsActive &&
            s.StartTime > DateTime.UtcNow &&
            s.StartTime.DayOfWeek == record.DayOfWeek &&
            s.StartTime.TimeOfDay >= record.StartTime &&
            s.EndTime.TimeOfDay <= record.EndTime);

        if (hasActiveSessions)
        {
            return Conflict(new { message = "Cannot delete availability window that has active upcoming sessions." });
        }

        _db.TrainerAvailabilities.Remove(record);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static bool IsConcurrencyFailure(Exception exception)
    {
        return exception switch
        {
            PostgresException postgresException => postgresException.SqlState is "40001" or "40P01",
            DbUpdateException { InnerException: PostgresException postgresException } =>
                postgresException.SqlState is "40001" or "40P01",
            _ => false
        };
    }
}
