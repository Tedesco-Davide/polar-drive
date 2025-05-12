using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Text.Json;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OutagePeriodsController(PolarDriveDbContext db, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OutagePeriod>>> Get()
    {
        var items = await db.OutagePeriods
            .Include(o => o.ClientCompany)
            .Include(o => o.ClientTeslaVehicle)
            .OrderByDescending(o => o.OutageStart)
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] OutagePeriod outage)
    {
        if (string.IsNullOrWhiteSpace(outage.OutageType) || 
            !new[] { "Outage Vehicle", "Outage Fleet Api" }.Contains(outage.OutageType))
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid outage type.");

        if (outage.TeslaVehicleId.HasValue && !await db.ClientTeslaVehicles.AnyAsync(v => v.Id == outage.TeslaVehicleId))
            return NotFound("SERVER ERROR → NOT FOUND: Tesla vehicle not found!");

        if (outage.ClientCompanyId.HasValue && !await db.ClientCompanies.AnyAsync(c => c.Id == outage.ClientCompanyId))
            return NotFound("SERVER ERROR → NOT FOUND: Client company not found!");

        outage.CreatedAt = DateTime.UtcNow;

        db.OutagePeriods.Add(outage);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = outage.Id }, outage.Id);
    }

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        var entity = await db.OutagePeriods.FindAsync(id);
        if (entity == null)
            return NotFound();

        if (!body.TryGetProperty("notes", out var notesProp))
            return BadRequest("SERVER ERROR → BAD REQUEST: Notes field missing!");

        entity.Notes = notesProp.GetString() ?? string.Empty;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadZip(int id)
    {
        var outage = await db.OutagePeriods.FindAsync(id);
        if (outage == null)
            return NotFound("SERVER ERROR → NOT FOUND: Outage record not found!");

        if (string.IsNullOrWhiteSpace(outage.ZipFilePath))
            return NotFound("SERVER ERROR → NOT FOUND: No zip file associated with this outage!");

        if (string.IsNullOrWhiteSpace(env.WebRootPath))
            return StatusCode(500, "SERVER ERROR → STATUS CODE: WebRootPath not configured!");

        var fullPath = Path.Combine(env.WebRootPath, outage.ZipFilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (!System.IO.File.Exists(fullPath))
            return NotFound("SERVER ERROR → NOT FOUND: .zip file not found on the server!");

        var fileName = Path.GetFileName(fullPath);
        var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);

        return File(fileBytes, "application/zip", fileName);
    }
}
