using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    // Members and admins can browse all upcoming or active sessions.
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetSessions(
        [FromQuery] Guid? programId,
        [FromQuery] Guid? trainerId,
        [FromQuery] bool includeInactive = false)
    {
        var query = _db.Sessions
            .Include(s => s.FitnessProgram)
            .Include(s => s.Trainer)
            .AsQueryable();

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
                // Calculate available slots from confirmed bookings at query time.
                AvailableSlots = s.Capacity - s.Bookings.Count(b => b.Status == "Confirmed")
            })
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetSession(Guid id)
    {
        var session = await _db.Sessions
            .Include(s => s.FitnessProgram)
            .Include(s => s.Trainer)
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
                AvailableSlots = s.Capacity - s.Bookings.Count(b => b.Status == "Confirmed")
            })
            .FirstOrDefaultAsync();

        if (session == null)
            return NotFound(new { message = "Session not found." });

        return Ok(session);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest req)
    {
        // Validate that the referenced program and trainer actually exist.
        var programExists = await _db.FitnessPrograms.AnyAsync(p => p.FitnessProgramId == req.FitnessProgramId && p.IsActive);
        if (!programExists)
            return BadRequest(new { message = "Fitness program not found or is inactive." });

        var trainer = await _db.Trainers.FindAsync(req.TrainerId);
        if (trainer == null || !trainer.IsActive)
            return BadRequest(new { message = "Trainer not found or is inactive." });

        if (req.StartTime >= req.EndTime)
            return BadRequest(new { message = "Session StartTime must be before EndTime." });

        if (req.StartTime <= DateTime.UtcNow)
            return BadRequest(new { message = "Session must be scheduled in the future." });

        // Check that the session falls within at least one of the trainer's availability windows.
        var sessionDay = req.StartTime.DayOfWeek;
        var sessionStart = req.StartTime.TimeOfDay;
        var sessionEnd = req.EndTime.TimeOfDay;

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

        // Prevent scheduling the same trainer in two overlapping sessions.
        var trainerConflict = await _db.Sessions.AnyAsync(s =>
            s.TrainerId == req.TrainerId &&
            s.IsActive &&
            s.StartTime < req.EndTime &&
            s.EndTime > req.StartTime);

        if (trainerConflict)
            return Conflict(new { message = "The trainer already has a session scheduled during this time." });

        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            FitnessProgramId = req.FitnessProgramId,
            TrainerId = req.TrainerId,
            StartTime = req.StartTime,
            EndTime = req.EndTime,
            Capacity = req.Capacity,
            IsActive = true
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSession), new { id = session.SessionId }, new { session.SessionId });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSession(Guid id, [FromBody] UpdateSessionRequest req)
    {
        var session = await _db.Sessions.FindAsync(id);
        if (session == null)
            return NotFound(new { message = "Session not found." });

        var programExists = await _db.FitnessPrograms.AnyAsync(p => p.FitnessProgramId == req.FitnessProgramId && p.IsActive);
        if (!programExists)
            return BadRequest(new { message = "Fitness program not found or is inactive." });

        var trainer = await _db.Trainers.FindAsync(req.TrainerId);
        if (trainer == null || !trainer.IsActive)
            return BadRequest(new { message = "Trainer not found or is inactive." });

        if (req.StartTime >= req.EndTime)
            return BadRequest(new { message = "Session StartTime must be before EndTime." });

        // Availability window check excludes the session being updated.
        var sessionDay = req.StartTime.DayOfWeek;
        var sessionStart = req.StartTime.TimeOfDay;
        var sessionEnd = req.EndTime.TimeOfDay;

        var trainerIsFree = await _db.TrainerAvailabilities.AnyAsync(a =>
            a.TrainerId == req.TrainerId &&
            a.DayOfWeek == sessionDay &&
            a.StartTime <= sessionStart &&
            a.EndTime >= sessionEnd);

        if (!trainerIsFree)
            return Conflict(new { message = "The trainer has no availability window that covers this session time." });

        // Schedule conflict check excludes the session being updated.
        var trainerConflict = await _db.Sessions.AnyAsync(s =>
            s.TrainerId == req.TrainerId &&
            s.SessionId != id &&
            s.IsActive &&
            s.StartTime < req.EndTime &&
            s.EndTime > req.StartTime);

        if (trainerConflict)
            return Conflict(new { message = "The trainer already has a session scheduled during this time." });

        session.FitnessProgramId = req.FitnessProgramId;
        session.TrainerId = req.TrainerId;
        session.StartTime = req.StartTime;
        session.EndTime = req.EndTime;
        session.Capacity = req.Capacity;
        session.IsActive = req.IsActive;

        await _db.SaveChangesAsync();
        return Ok(new { session.SessionId });
    }

    // Soft-delete the session so existing bookings still reference it for history.
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSession(Guid id)
    {
        var session = await _db.Sessions.FindAsync(id);
        if (session == null)
            return NotFound(new { message = "Session not found." });

        session.IsActive = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
