using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MembershipPlansController : ControllerBase
{
    private readonly GymDbContext _db;

    public MembershipPlansController(GymDbContext db)
    {
        _db = db;
    }

    // Members see active plans. Admins can include inactive plans for management.
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetPlans([FromQuery] bool includeInactive = false)
    {
        if (includeInactive && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var query = _db.MembershipPlans.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(plan => plan.IsActive);
        }

        var plans = await query.OrderBy(plan => plan.Price).ToListAsync();
        return Ok(plans.Select(ToResponse));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetPlan(Guid id)
    {
        var plan = await _db.MembershipPlans.FindAsync(id);
        if (plan == null || (!plan.IsActive && !User.IsInRole("Admin")))
        {
            return NotFound(new { message = "Membership plan not found." });
        }

        return Ok(ToResponse(plan));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreatePlan([FromBody] CreateMembershipPlanRequest request)
    {
        var plan = new MembershipPlan
        {
            MembershipPlanId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Price = request.Price,
            DurationInDays = request.DurationInDays,
            Benefits = request.Benefits.Trim(),
            IsActive = true
        };

        _db.MembershipPlans.Add(plan);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPlan), new { id = plan.MembershipPlanId }, ToResponse(plan));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdateMembershipPlanRequest request)
    {
        var plan = await _db.MembershipPlans.FindAsync(id);
        if (plan == null)
        {
            return NotFound(new { message = "Membership plan not found." });
        }

        plan.Name = request.Name.Trim();
        plan.Price = request.Price;
        plan.DurationInDays = request.DurationInDays;
        plan.Benefits = request.Benefits.Trim();
        plan.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return Ok(ToResponse(plan));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePlan(Guid id)
    {
        var plan = await _db.MembershipPlans.FindAsync(id);
        if (plan == null)
        {
            return NotFound(new { message = "Membership plan not found." });
        }

        // Keep old memberships connected to the plan while hiding it from new requests.
        plan.IsActive = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static MembershipPlanResponse ToResponse(MembershipPlan plan)
    {
        return new MembershipPlanResponse
        {
            MembershipPlanId = plan.MembershipPlanId,
            Name = plan.Name,
            Price = plan.Price,
            DurationInDays = plan.DurationInDays,
            Benefits = plan.Benefits,
            IsActive = plan.IsActive
        };
    }
}
