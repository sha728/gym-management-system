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
public class MembershipsController : ControllerBase
{
    private readonly GymDbContext _db;

    public MembershipsController(GymDbContext db)
    {
        _db = db;
    }

    [HttpPost("requests")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> RequestMembership([FromBody] CreateMembershipRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid user identity." });
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { message = "User not found." });
        }

        var plan = await _db.MembershipPlans
            .FirstOrDefaultAsync(item => item.MembershipPlanId == request.MembershipPlanId && item.IsActive);
        if (plan == null)
        {
            return NotFound(new { message = "Membership plan not found or inactive." });
        }

        var now = DateTime.UtcNow;
        var expiredMemberships = await _db.Memberships
            .Where(item => item.UserId == userId &&
                           item.Status == MembershipStatus.Active &&
                           item.EndDate != null &&
                           item.EndDate <= now)
            .ToListAsync();

        foreach (var expiredMembership in expiredMemberships)
        {
            expiredMembership.Status = MembershipStatus.Expired;
        }

        if (expiredMemberships.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        var hasPendingRequest = await _db.Memberships.AnyAsync(item =>
            item.UserId == userId && item.Status == MembershipStatus.Pending);
        if (hasPendingRequest)
        {
            return Conflict(new { message = "You already have a membership request waiting for review." });
        }

        var hasActiveMembership = await _db.Memberships.AnyAsync(item =>
            item.UserId == userId && item.Status == MembershipStatus.Active && item.EndDate > now);
        if (hasActiveMembership)
        {
            return Conflict(new { message = "You already have an active membership." });
        }

        var membership = new Membership
        {
            MembershipId = Guid.NewGuid(),
            UserId = userId,
            MembershipPlanId = plan.MembershipPlanId,
            RequestedAt = now,
            Status = MembershipStatus.Pending
        };

        _db.Memberships.Add(membership);
        await _db.SaveChangesAsync();

        membership.MembershipPlan = plan;
        membership.User = user;
        return CreatedAtAction(nameof(GetMyMemberships), ToResponse(membership));
    }

    [HttpGet("my")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> GetMyMemberships()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid user identity." });
        }

        var memberships = await _db.Memberships
            .Include(item => item.MembershipPlan)
            .Include(item => item.User)
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.RequestedAt)
            .ToListAsync();

        return Ok(memberships.Select(ToResponse));
    }

    // Admins use this to see pending requests and the membership history for every member.
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetMemberships([FromQuery] string? status = null)
    {
        var query = _db.Memberships
            .Include(item => item.MembershipPlan)
            .Include(item => item.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status == status);
        }

        var memberships = await query
            .OrderByDescending(item => item.RequestedAt)
            .ToListAsync();

        return Ok(memberships.Select(ToResponse));
    }

    [HttpPut("{id:guid}/review")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReviewMembership(Guid id, [FromBody] ReviewMembershipRequest request)
    {
        var membership = await _db.Memberships
            .Include(item => item.MembershipPlan)
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.MembershipId == id);

        if (membership == null)
        {
            return NotFound(new { message = "Membership request not found." });
        }

        if (membership.Status != MembershipStatus.Pending)
        {
            return BadRequest(new { message = "Only pending membership requests can be reviewed." });
        }

        membership.ReviewedAt = DateTime.UtcNow;
        membership.ReviewNote = request.ReviewNote?.Trim();

        if (request.Approve)
        {
            if (!membership.MembershipPlan.IsActive)
            {
                return Conflict(new { message = "This membership plan is no longer active." });
            }

            var hasActiveMembership = await _db.Memberships.AnyAsync(item =>
                item.UserId == membership.UserId &&
                item.MembershipId != membership.MembershipId &&
                item.Status == MembershipStatus.Active &&
                item.EndDate > DateTime.UtcNow);
            if (hasActiveMembership)
            {
                return Conflict(new { message = "This member already has an active membership." });
            }

            membership.Status = MembershipStatus.Active;
            membership.StartDate = DateTime.UtcNow.Date;
            membership.EndDate = membership.StartDate.Value.AddDays(membership.MembershipPlan.DurationInDays);
        }
        else
        {
            membership.Status = MembershipStatus.Rejected;
        }

        await _db.SaveChangesAsync();
        return Ok(ToResponse(membership));
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private static MembershipResponse ToResponse(Membership membership)
    {
        return new MembershipResponse
        {
            MembershipId = membership.MembershipId,
            UserId = membership.UserId,
            MemberEmail = membership.User.Email,
            MembershipPlanId = membership.MembershipPlanId,
            PlanName = membership.MembershipPlan.Name,
            Price = membership.MembershipPlan.Price,
            DurationInDays = membership.MembershipPlan.DurationInDays,
            Benefits = membership.MembershipPlan.Benefits,
            Status = membership.Status,
            RequestedAt = membership.RequestedAt,
            ReviewedAt = membership.ReviewedAt,
            StartDate = membership.StartDate,
            EndDate = membership.EndDate,
            ReviewNote = membership.ReviewNote
        };
    }
}
