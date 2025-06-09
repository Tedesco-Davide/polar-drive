using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminScheduler(PolarDriveDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Data.Entities.AdminScheduler>>> GetAll()
    {
        return await db.ScheduledFileJobs.OrderByDescending(j => j.RequestedAt).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Data.Entities.AdminScheduler>> GetById(int id)
    {
        var job = await db.ScheduledFileJobs.FindAsync(id);
        if (job == null)
            return NotFound();

        return job;
    }

    [HttpPost]
    public async Task<ActionResult<Data.Entities.AdminScheduler>> Create(Data.Entities.AdminScheduler job)
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
}