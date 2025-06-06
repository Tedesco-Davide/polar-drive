using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScheduledFileJobsController(PolarDriveDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ScheduledFileJob>>> GetAll()
    {
        return await db.ScheduledFileJobs.OrderByDescending(j => j.RequestedAt).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ScheduledFileJob>> GetById(int id)
    {
        var job = await db.ScheduledFileJobs.FindAsync(id);
        if (job == null)
            return NotFound();

        return job;
    }

    [HttpPost]
    public async Task<ActionResult<ScheduledFileJob>> Create(ScheduledFileJob job)
    {
        job.RequestedAt = DateTime.UtcNow;
        job.Status = "QUEUE";
        db.ScheduledFileJobs.Add(job);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = job.Id }, job);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] string newStatus)
    {
        var job = await db.ScheduledFileJobs.FindAsync(id);
        if (job == null)
            return NotFound();

        job.Status = newStatus;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var job = await db.ScheduledFileJobs.FindAsync(id);
        if (job == null)
            return NotFound();

        db.ScheduledFileJobs.Remove(job);
        await db.SaveChangesAsync();

        return NoContent();
    }
}