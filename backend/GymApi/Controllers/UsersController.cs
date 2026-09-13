using GymApi.Data;
using GymApi.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly GymDbContext _db;

    public UsersController(GymDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetMembers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(new { message = "Page must be at least 1 and pageSize must be between 1 and 100." });
        }

        var query = _db.Users
            .AsNoTracking()
            .Where(user => user.Role == "Member");

        var totalCount = await query.CountAsync();
        var members = await query
            .OrderBy(user => user.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new MemberResponse
            {
                UserId = user.UserId,
                Email = user.Email,
                CreatedAt = user.CreatedAt
            })
            .ToListAsync();

        // Return only member information an admin needs, never the password hash stored on User.
        return Ok(new { items = members, page, pageSize, totalCount });
    }
}
