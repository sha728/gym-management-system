using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly GymDbContext _db;

    public BookingsController(GymDbContext db)
    {
        _db = db;
    }

    // Authenticated members can create a booking for an active future session.
    [HttpPost]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest req)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { message = "Invalid user identity." });
        }

        var userExists = await _db.Users.AnyAsync(u => u.UserId == userId);
        if (!userExists)
        {
            return Unauthorized(new { message = "User not found." });
        }

        var session = await _db.Sessions
            .Include(s => s.FitnessProgram)
            .Include(s => s.Trainer)
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.SessionId == req.SessionId);

        if (session == null)
        {
            return NotFound(new { message = "Session not found." });
        }

        if (!session.IsActive)
        {
            return BadRequest(new { message = "Session is inactive." });
        }

        if (session.StartTime <= DateTime.UtcNow)
        {
            return BadRequest(new { message = "Cannot book a session that has already started or passed." });
        }

        var confirmedCount = session.Bookings.Count(b => b.Status == "Confirmed");
        if (confirmedCount >= session.Capacity)
        {
            return BadRequest(new { message = "Session is fully booked." });
        }

        var alreadyBooked = session.Bookings.Any(b => b.UserId == userId && b.Status == "Confirmed");
        if (alreadyBooked)
        {
            return BadRequest(new { message = "You have already booked this session." });
        }

        // Prevent schedule conflicts for the member.
        var memberConflict = await _db.Bookings
            .Include(b => b.Session)
            .AnyAsync(b => b.UserId == userId &&
                           b.Status == "Confirmed" &&
                           b.Session.IsActive &&
                           b.Session.StartTime < session.EndTime &&
                           b.Session.EndTime > session.StartTime);

        if (memberConflict)
        {
            return Conflict(new { message = "You already have another session booked during this time frame." });
        }

        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            UserId = userId,
            SessionId = req.SessionId,
            BookedAt = DateTime.UtcNow,
            Status = "Confirmed"
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        var response = new BookingResponse
        {
            BookingId = booking.BookingId,
            SessionId = session.SessionId,
            ProgramTitle = session.FitnessProgram?.Name ?? string.Empty,
            TrainerName = session.Trainer?.Name ?? string.Empty,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            BookedAt = booking.BookedAt,
            Status = booking.Status
        };

        return CreatedAtAction(nameof(GetBooking), new { id = booking.BookingId }, response);
    }

    // Get all bookings for the currently authenticated user.
    [HttpGet("my-bookings")]
    [Authorize]
    public async Task<IActionResult> GetMyBookings()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { message = "Invalid user identity." });
        }

        var bookings = await _db.Bookings
            .Include(b => b.Session)
                .ThenInclude(s => s.FitnessProgram)
            .Include(b => b.Session)
                .ThenInclude(s => s.Trainer)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.BookedAt)
            .Select(b => new BookingResponse
            {
                BookingId = b.BookingId,
                SessionId = b.SessionId,
                ProgramTitle = b.Session.FitnessProgram.Name,
                TrainerName = b.Session.Trainer.Name,
                StartTime = b.Session.StartTime,
                EndTime = b.Session.EndTime,
                BookedAt = b.BookedAt,
                Status = b.Status
            })
            .ToListAsync();

        return Ok(bookings);
    }

    // Get booking details by ID.
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetBooking(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { message = "Invalid user identity." });
        }

        var booking = await _db.Bookings
            .Include(b => b.Session)
                .ThenInclude(s => s.FitnessProgram)
            .Include(b => b.Session)
                .ThenInclude(s => s.Trainer)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            return NotFound(new { message = "Booking not found." });
        }

        var userRole = User.FindFirstValue(ClaimTypes.Role);
        if (booking.UserId != userId && userRole != "Admin")
        {
            return Forbid();
        }

        var response = new BookingResponse
        {
            BookingId = booking.BookingId,
            SessionId = booking.SessionId,
            ProgramTitle = booking.Session.FitnessProgram.Name,
            TrainerName = booking.Session.Trainer.Name,
            StartTime = booking.Session.StartTime,
            EndTime = booking.Session.EndTime,
            BookedAt = booking.BookedAt,
            Status = booking.Status
        };

        return Ok(response);
    }

    // Cancel a booking (soft cancel by changing status to "Cancelled").
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> CancelBooking(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { message = "Invalid user identity." });
        }

        var booking = await _db.Bookings
            .Include(b => b.Session)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            return NotFound(new { message = "Booking not found." });
        }

        var userRole = User.FindFirstValue(ClaimTypes.Role);
        if (booking.UserId != userId && userRole != "Admin")
        {
            return Forbid();
        }

        if (booking.Status == "Cancelled")
        {
            return BadRequest(new { message = "Booking is already cancelled." });
        }

        if (booking.Session != null && booking.Session.StartTime <= DateTime.UtcNow)
        {
            return BadRequest(new { message = "Cannot cancel a session that has already started or passed." });
        }

        booking.Status = "Cancelled";
        await _db.SaveChangesAsync();

        return Ok(new { message = "Booking cancelled successfully.", bookingId = booking.BookingId, status = booking.Status });
    }
}
