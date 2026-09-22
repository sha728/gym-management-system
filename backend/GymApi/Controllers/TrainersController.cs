using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrainersController : ControllerBase
{
    private readonly GymDbContext _db;

    public TrainersController(GymDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetTrainers([FromQuery] bool showAll = false)
    {
        var query = _db.Trainers.AsNoTracking();

        if (!showAll)
        {
            query = query.Where(t => t.IsActive);
        }

        var trainers = await query.ToListAsync();
        return Ok(trainers);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetTrainer(Guid id)
    {
        var trainer = await _db.Trainers.FindAsync(id);
        if (trainer == null)
            return NotFound(new { message = "Trainer not found." });

        return Ok(trainer);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddTrainer([FromBody] CreateTrainerRequest req)
    {
        var trainer = new Trainer
        {
            TrainerId = Guid.NewGuid(),
            Name = req.Name.Trim(),
            Specialization = req.Specialization.Trim(),
            IsActive = req.IsActive
        };

        _db.Trainers.Add(trainer);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTrainer), new { id = trainer.TrainerId }, trainer);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateTrainer(Guid id, [FromBody] UpdateTrainerRequest req)
    {
        var trainer = await _db.Trainers.FindAsync(id);
        if (trainer == null)
            return NotFound(new { message = "Trainer record not found." });

        trainer.Name = req.Name.Trim();
        trainer.Specialization = req.Specialization.Trim();
        trainer.IsActive = req.IsActive;

        await _db.SaveChangesAsync();
        return Ok(trainer);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveTrainer(Guid id)
    {
        var trainer = await _db.Trainers.FindAsync(id);
        if (trainer == null)
            return NotFound(new { message = "Trainer not found." });

        // Soft delete: keep trainer record intact for previous scheduled sessions
        trainer.IsActive = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}