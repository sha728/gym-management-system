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
            return Unauthorized(new { message = "Invalid user identity." });

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return Unauthorized(new { message = "User not found." });

        try
        {
            // Keep the checks and insert together so two quick requests cannot both become pending.
            using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                : null;

            var plan = await _db.MembershipPlans
                .FirstOrDefaultAsync(item => item.MembershipPlanId == request.MembershipPlanId && item.IsActive);
            if (plan == null)
                return NotFound(new { message = "Membership plan not found or inactive." });

            var now = DateTime.UtcNow;
            var hasPendingRequest = await _db.Memberships.AnyAsync(item =>
                item.UserId == userId && item.Status == MembershipStatus.Pending);
            if (hasPendingRequest)
                return Conflict(new { message = "You already have a membership request waiting for review." });

            var hasActiveMembership = await _db.Memberships.AnyAsync(item =>
                item.UserId == userId &&
                item.Status == MembershipStatus.Approved &&
                item.EndDate > now);
            if (hasActiveMembership)
                return Conflict(new { message = "You already have an active membership." });

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

            if (transaction != null)
                await transaction.CommitAsync();

            membership.MembershipPlan = plan;
            membership.User = user;
            return CreatedAtAction(nameof(GetMyMemberships), ToResponse(membership, now));
        }
        catch (Exception exception) when (IsMembershipConflict(exception))
        {
            return Conflict(new { message = "Your membership request changed while it was being saved. Please try again." });
        }
    }

    [HttpGet("my")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> GetMyMemberships()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid user identity." });

        var now = DateTime.UtcNow;
        var memberships = await _db.Memberships
            .Include(item => item.MembershipPlan)
            .Include(item => item.User)
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.RequestedAt)
            .ToListAsync();

        return Ok(memberships.Select(item => ToResponse(item, now)));
    }

    // Admins can filter the queue without reading an unbounded membership history at once.
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetMemberships(
        [FromQuery] MembershipViewStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(new { message = "Page must be at least 1 and pageSize must be between 1 and 100." });

        var now = DateTime.UtcNow;
        var query = _db.Memberships
            .Include(item => item.MembershipPlan)
            .Include(item => item.User)
            .AsQueryable();

        query = status switch
        {
            MembershipViewStatus.Pending => query.Where(item => item.Status == MembershipStatus.Pending),
            MembershipViewStatus.Rejected => query.Where(item => item.Status == MembershipStatus.Rejected),
            MembershipViewStatus.Active => query.Where(item => item.Status == MembershipStatus.Approved && item.EndDate > now),
            MembershipViewStatus.Expired => query.Where(item => item.Status == MembershipStatus.Approved && item.EndDate <= now),
            _ => query
        };

        var totalCount = await query.CountAsync();
        var memberships = await query
            .OrderByDescending(item => item.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            items = memberships.Select(item => ToResponse(item, now)),
            page,
            pageSize,
            totalCount
        });
    }

    [HttpPut("{id:guid}/review")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReviewMembership(Guid id, [FromBody] ReviewMembershipRequest request)
    {
        try
        {
            using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                : null;

            var membership = await _db.Memberships
                .Include(item => item.MembershipPlan)
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.MembershipId == id);

            if (membership == null)
                return NotFound(new { message = "Membership request not found." });

            if (membership.Status != MembershipStatus.Pending)
                return BadRequest(new { message = "Only pending membership requests can be reviewed." });

            var now = DateTime.UtcNow;
            if (request.Approve)
            {
                if (!membership.MembershipPlan.IsActive)
                    return Conflict(new { message = "This membership plan is no longer active." });

                var hasActiveMembership = await _db.Memberships.AnyAsync(item =>
                    item.UserId == membership.UserId &&
                    item.MembershipId != membership.MembershipId &&
                    item.Status == MembershipStatus.Approved &&
                    item.EndDate > now);
                if (hasActiveMembership)
                    return Conflict(new { message = "This member already has an active membership." });

                membership.Status = MembershipStatus.Approved;
                membership.StartDate = now.Date;
                membership.EndDate = membership.StartDate.Value.AddDays(membership.MembershipPlan.DurationInDays);
                membership.PlanName = membership.MembershipPlan.Name;
                membership.PlanPrice = membership.MembershipPlan.Price;
                membership.PlanDurationInDays = membership.MembershipPlan.DurationInDays;
                membership.PlanBenefits = membership.MembershipPlan.Benefits;
            }
            else
            {
                membership.Status = MembershipStatus.Rejected;
            }

            // Validation is complete, so this review information can now be safely stored.
            membership.ReviewedAt = now;
            membership.ReviewNote = request.ReviewNote?.Trim();
            await _db.SaveChangesAsync();

            if (transaction != null)
                await transaction.CommitAsync();

            return Ok(ToResponse(membership, now));
        }
        catch (Exception exception) when (IsMembershipConflict(exception))
        {
            return Conflict(new { message = "This membership request changed while it was being reviewed. Please try again." });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private static MembershipViewStatus GetViewStatus(Membership membership, DateTime now)
    {
        return membership.Status switch
        {
            MembershipStatus.Pending => MembershipViewStatus.Pending,
            MembershipStatus.Rejected => MembershipViewStatus.Rejected,
            MembershipStatus.Approved when membership.EndDate > now => MembershipViewStatus.Active,
            _ => MembershipViewStatus.Expired
        };
    }

    private static MembershipResponse ToResponse(Membership membership, DateTime now)
    {
        var useSnapshot = membership.Status == MembershipStatus.Approved;
        return new MembershipResponse
        {
            MembershipId = membership.MembershipId,
            UserId = membership.UserId,
            MemberEmail = membership.User.Email,
            MembershipPlanId = membership.MembershipPlanId,
            PlanName = useSnapshot ? membership.PlanName ?? membership.MembershipPlan.Name : membership.MembershipPlan.Name,
            Price = useSnapshot ? membership.PlanPrice ?? membership.MembershipPlan.Price : membership.MembershipPlan.Price,
            DurationInDays = useSnapshot ? membership.PlanDurationInDays ?? membership.MembershipPlan.DurationInDays : membership.MembershipPlan.DurationInDays,
            Benefits = useSnapshot ? membership.PlanBenefits ?? membership.MembershipPlan.Benefits : membership.MembershipPlan.Benefits,
            Status = GetViewStatus(membership, now).ToString(),
            RequestedAt = membership.RequestedAt,
            ReviewedAt = membership.ReviewedAt,
            StartDate = membership.StartDate,
            EndDate = membership.EndDate,
            ReviewNote = membership.ReviewNote
        };
    }

    private static bool IsMembershipConflict(Exception exception)
    {
        return exception switch
        {
            PostgresException postgresException => postgresException.SqlState is "40001" or "40P01" or "23505",
            DbUpdateException { InnerException: PostgresException postgresException } =>
                postgresException.SqlState is "40001" or "40P01" or "23505",
            _ => false
        };
    }
}
