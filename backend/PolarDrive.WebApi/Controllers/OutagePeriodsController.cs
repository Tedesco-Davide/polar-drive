using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Text.Json;
using PolarDrive.Data.Constants;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OutagePeriodsController(PolarDriveDbContext db, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> Get()
    {

        var items = await db.OutagePeriods
            .Include(o => o.ClientCompany)
            .Include(o => o.ClientVehicle)
            .OrderByDescending(o => o.OutageStart)
            .Select(o => new
            {
                o.Id,
                o.VehicleId,
                o.ClientCompanyId,
                o.AutoDetected,
                o.OutageType,
                o.OutageBrand,
                o.CreatedAt,
                o.OutageStart,
                o.OutageEnd,
                o.ZipFilePath,
                o.Notes,
                vin = o.ClientVehicle != null ? o.ClientVehicle.Vin : "",
                companyVatNumber = o.ClientCompany != null ? o.ClientCompany.VatNumber : ""
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] OutagePeriod outage)
    {
        var sanitizedOutageType = outage.OutageType?.Trim();

        if (string.IsNullOrWhiteSpace(sanitizedOutageType) ||
            !OutageConstants.ValidOutageTypes.Contains(sanitizedOutageType))
        {
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid outage type!");
        }

        outage.OutageType = sanitizedOutageType;

        if (outage.OutageType == "Outage Vehicle")
        {
            if (!outage.ClientCompanyId.HasValue || !outage.VehicleId.HasValue)
                return BadRequest("SERVER ERROR → BAD REQUEST: Missing vehicle or company ID!");

            var company = await db.ClientCompanies.FirstOrDefaultAsync(c => c.Id == outage.ClientCompanyId);
            if (company == null)
                return NotFound("SERVER ERROR → NOT FOUND: Client company not found!");

            var vehicle = await db.ClientVehicles.FirstOrDefaultAsync(v => v.Id == outage.VehicleId);
            if (vehicle == null)
                return NotFound("SERVER ERROR → NOT FOUND: Vehicle not found!");

            if (vehicle.ClientCompanyId != company.Id)
                return BadRequest("SERVER ERROR → BAD REQUEST: Vehicle does not belong to the specified company!");
        }
        else
        {
            // Se è "Outage Fleet Api", consenti anche null
            if (outage.VehicleId.HasValue &&
                !await db.ClientVehicles.AnyAsync(v => v.Id == outage.VehicleId))
            {
                return NotFound("SERVER ERROR → NOT FOUND: Vehicle not found!");
            }

            if (outage.ClientCompanyId.HasValue &&
                !await db.ClientCompanies.AnyAsync(c => c.Id == outage.ClientCompanyId))
            {
                return NotFound("SERVER ERROR → NOT FOUND: Client company not found!");
            }
        }

        var sanitizedOutageBrand = outage.OutageBrand?.Trim();
        
        if (string.IsNullOrWhiteSpace(sanitizedOutageBrand) ||
            !VehicleConstants.ValidBrands.Contains(sanitizedOutageBrand))
        {
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid outage brand!");
        }

        outage.OutageBrand = sanitizedOutageBrand;

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
