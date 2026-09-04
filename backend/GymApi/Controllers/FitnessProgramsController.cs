using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FitnessProgramsController : ControllerBase
{
    private readonly GymDbContext _db;

    public FitnessProgramsController(GymDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var query = _db.FitnessPrograms.AsQueryable();

        // Default to showing active programs only for regular catalog views
        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        var list = await query.ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var program = await _db.FitnessPrograms.FindAsync(id);
        if (program == null)
            return NotFound(new { message = $"Program with ID {id} not found." });

        return Ok(program);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateFitnessProgramRequest dto)
    {
        var newProgram = new FitnessProgram
        {
            FitnessProgramId = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            DurationInMinutes = dto.DurationInMinutes,
            IsActive = dto.IsActive
        };

        _db.FitnessPrograms.Add(newProgram);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = newProgram.FitnessProgramId }, newProgram);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFitnessProgramRequest dto)
    {
        var existing = await _db.FitnessPrograms.FindAsync(id);
        if (existing == null)
            return NotFound(new { message = "Program not found." });

        existing.Name = dto.Name.Trim();
        existing.Description = dto.Description?.Trim() ?? string.Empty;
        existing.DurationInMinutes = dto.DurationInMinutes;
        existing.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var program = await _db.FitnessPrograms.FindAsync(id);
        if (program == null)
            return NotFound(new { message = "Program not found." });

        // Using soft delete here so past session histories don't throw foreign key errors
        program.IsActive = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}