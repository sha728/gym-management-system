using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;

namespace GymApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly GymDbContext _db;

    public SessionsController(GymDbContext db)
    {
        _db = db;
    }

    // Members can browse active sessions. Admins can pass includeInactive=true to inspect soft-deleted sessions.
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetSessions(
        [FromQuery] Guid? programId,
        [FromQuery] Guid? trainerId,
        [FromQuery] bool includeInactive = false)
    {
        // Only admins are allowed to request soft-deleted sessions.
        if (includeInactive && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var query = _db.Sessions.AsQueryable();

        if (!includeInactive)
            query = query.Where(s => s.IsActive);

        if (programId.HasValue)
            query = query.Where(s => s.FitnessProgramId == programId.Value);

        if (trainerId.HasValue)
            query = query.Where(s => s.TrainerId == trainerId.Value);

        var sessions = await query
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                s.SessionId,
                s.StartTime,
                s.EndTime,
                s.Capacity,
                s.IsActive,
                FitnessProgram = new { s.FitnessProgram.FitnessProgramId, s.FitnessProgram.Name },
                Trainer = new { s.Trainer.TrainerId, s.Trainer.Name, s.Trainer.Specialization },
                // Calculate remaining open seats based on active confirmed bookings.
                AvailableSlots = Math.Max(0, s.Capacity - s.Bookings.Count(b => b.Status == BookingStatus.Confirmed))
            })
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetSession(Guid id)
    {
        var session = await _db.Sessions
            .Where(s => s.SessionId == id)
            .Select(s => new
            {
                s.SessionId,
                s.StartTime,
                s.EndTime,
                s.Capacity,
                s.IsActive,
                FitnessProgram = new { s.FitnessProgram.FitnessProgramId, s.FitnessProgram.Name, s.FitnessProgram.Description },
                Trainer = new { s.Trainer.TrainerId, s.Trainer.Name, s.Trainer.Specialization },
                AvailableSlots = Math.Max(0, s.Capacity - s.Bookings.Count(b => b.Status == BookingStatus.Confirmed))
            })
            .FirstOrDefaultAsync();

        // Soft-deleted sessions are hidden from regular members.
        if (session == null || (!session.IsActive && !User.IsInRole("Admin")))
            return NotFound(new { message = "Session not found." });

        return Ok(session);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest req)
    {
        // Require explicit UTC dates to prevent timezone mismatch errors when storing in PostgreSQL.
        if (req.StartTime.Kind != DateTimeKind.Utc || req.EndTime.Kind != DateTimeKind.Utc)
        {
            return BadRequest(new { message = "Session StartTime and EndTime must be in UTC timezone." });
        }

        var startTime = req.StartTime.ToUniversalTime();
        var endTime = req.EndTime.ToUniversalTime();

        if (startTime >= endTime)
            return BadRequest(new { message = "Session StartTime must be before EndTime." });

        if (startTime <= DateTime.UtcNow)
            return BadRequest(new { message = "Session must be scheduled in the future." });

        // Sessions must start and end on the same calendar day to align cleanly with trainer availability.
        if (startTime.Date != endTime.Date)
            return BadRequest(new { message = "Sessions cannot cross midnight or span multiple days." });

        var programExists = await _db.FitnessPrograms.AnyAsync(p => p.FitnessProgramId == req.FitnessProgramId && p.IsActive);
        if (!programExists)
            return BadRequest(new { message = "Fitness program not found or is inactive." });

        var trainer = await _db.Trainers.FindAsync(req.TrainerId);
        if (trainer == null || !trainer.IsActive)
            return BadRequest(new { message = "Trainer not found or is inactive." });

        var sessionDay = startTime.DayOfWeek;
        var sessionStart = startTime.TimeOfDay;
        var sessionEnd = endTime.TimeOfDay;

        // Ensure the session fits completely inside one of the trainer's availability windows.
        var trainerIsFree = await _db.TrainerAvailabilities.AnyAsync(a =>
            a.TrainerId == req.TrainerId &&
            a.DayOfWeek == sessionDay &&
            a.StartTime <= sessionStart &&
            a.EndTime >= sessionEnd);

        if (!trainerIsFree)
            return Conflict(new
            {
                message = "The trainer has no availability window that covers this session time."
            });

        // Use a serializable transaction when on a real relational DB to prevent concurrent double-booking.
        try
        {
            using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                : null;

            var trainerConflict = await _db.Sessions.AnyAsync(s =>
                s.TrainerId == req.TrainerId &&
                s.IsActive &&
                s.StartTime < endTime &&
                s.EndTime > startTime);

            if (trainerConflict)
                return Conflict(new { message = "The trainer already has a session scheduled during this time." });

            var session = new Session
            {
                SessionId = Guid.NewGuid(),
                FitnessProgramId = req.FitnessProgramId,
                TrainerId = req.TrainerId,
                StartTime = startTime,
                EndTime = endTime,
                Capacity = req.Capacity,
                IsActive = true
            };

            _db.Sessions.Add(session);
            await _db.SaveChangesAsync();

            if (transaction != null)
                await transaction.CommitAsync();

            return CreatedAtAction(nameof(GetSession), new { id = session.SessionId }, new { session.SessionId });
        }
        catch (Exception ex) when (IsConcurrencyFailure(ex))
        {
            return Conflict(new { message = "The trainer schedule changed while this session was being saved. Please try again." });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSession(Guid id, [FromBody] UpdateSessionRequest req)
    {
        var session = await _db.Sessions.FindAsync(id);
        if (session == null)
            return NotFound(new { message = "Session not found." });

        if (req.StartTime.Kind != DateTimeKind.Utc || req.EndTime.Kind != DateTimeKind.Utc)
        {
            return BadRequest(new { message = "Session StartTime and EndTime must be in UTC timezone." });
        }

        var startTime = req.StartTime.ToUniversalTime();
        var endTime = req.EndTime.ToUniversalTime();

        if (startTime >= endTime)
            return BadRequest(new { message = "Session StartTime must be before EndTime." });

        // Updating a session into the past is not allowed.
        if (startTime <= DateTime.UtcNow)
            return BadRequest(new { message = "Session must be scheduled in the future." });

        if (startTime.Date != endTime.Date)
            return BadRequest(new { message = "Sessions cannot cross midnight or span multiple days." });

        var programExists = await _db.FitnessPrograms.AnyAsync(p => p.FitnessProgramId == req.FitnessProgramId && p.IsActive);
        if (!programExists)
            return BadRequest(new { message = "Fitness program not found or is inactive." });

        var trainer = await _db.Trainers.FindAsync(req.TrainerId);
        if (trainer == null || !trainer.IsActive)
            return BadRequest(new { message = "Trainer not found or is inactive." });

        // Ensure new capacity is not lower than members who have already booked this session.
        var confirmedBookingsCount = await _db.Bookings.CountAsync(b => b.SessionId == id && b.Status == BookingStatus.Confirmed);
        if (req.Capacity < confirmedBookingsCount)
        {
            return Conflict(new { message = $"Cannot reduce capacity below existing confirmed bookings ({confirmedBookingsCount})." });
        }

        var sessionDay = startTime.DayOfWeek;
        var sessionStart = startTime.TimeOfDay;
        var sessionEnd = endTime.TimeOfDay;

        var trainerIsFree = await _db.TrainerAvailabilities.AnyAsync(a =>
            a.TrainerId == req.TrainerId &&
            a.DayOfWeek == sessionDay &&
            a.StartTime <= sessionStart &&
            a.EndTime >= sessionEnd);

        if (!trainerIsFree)
            return Conflict(new { message = "The trainer has no availability window that covers this session time." });

        try
        {
            using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                : null;

            var trainerConflict = await _db.Sessions.AnyAsync(s =>
                s.TrainerId == req.TrainerId &&
                s.SessionId != id &&
                s.IsActive &&
                s.StartTime < endTime &&
                s.EndTime > startTime);

            if (trainerConflict)
                return Conflict(new { message = "The trainer already has a session scheduled during this time." });

            // Note: Updating session details preserves any attached member bookings so members keep their seats.
            session.FitnessProgramId = req.FitnessProgramId;
            session.TrainerId = req.TrainerId;
            session.StartTime = startTime;
            session.EndTime = endTime;
            session.Capacity = req.Capacity;
            session.IsActive = req.IsActive;

            await _db.SaveChangesAsync();

            if (transaction != null)
                await transaction.CommitAsync();

            return Ok(new { session.SessionId });
        }
        catch (Exception ex) when (IsConcurrencyFailure(ex))
        {
            return Conflict(new { message = "The trainer schedule changed while this session was being saved. Please try again." });
        }
    }

    // Soft-delete the session and cancel its bookings so history remains available without showing cancelled seats as confirmed.
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSession(Guid id)
    {
        var session = await _db.Sessions.FindAsync(id);
        if (session == null)
            return NotFound(new { message = "Session not found." });

        session.IsActive = false;
        var confirmedBookings = await _db.Bookings
            .Where(b => b.SessionId == id && b.Status == BookingStatus.Confirmed)
            .ToListAsync();

        foreach (var booking in confirmedBookings)
            booking.Status = BookingStatus.Cancelled;

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
