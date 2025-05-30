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
    private readonly PolarDriveLogger _logger = new(db);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> Get()
    {
        await _logger.Info("OutagePeriodsController.Get", "Requested list of outage periods.");

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
            await _logger.Warning("OutagePeriodsController.Post", "Invalid outage type.", $"Type: {outage.OutageType}");
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid outage type!");
        }

        outage.OutageType = sanitizedOutageType;

        if (outage.OutageType == "Outage Vehicle")
        {
            if (!outage.ClientCompanyId.HasValue || !outage.VehicleId.HasValue)
            {
                await _logger.Warning("OutagePeriodsController.Post", "Missing vehicle or company ID for Outage Vehicle.");
                return BadRequest("SERVER ERROR → BAD REQUEST: Missing vehicle or company ID!");
            }

            var company = await db.ClientCompanies.FirstOrDefaultAsync(c => c.Id == outage.ClientCompanyId);
            if (company == null)
            {
                await _logger.Warning("OutagePeriodsController.Post", "Client company not found.", $"CompanyId: {outage.ClientCompanyId}");
                return NotFound("SERVER ERROR → NOT FOUND: Client company not found!");
            }

            var vehicle = await db.ClientVehicles.FirstOrDefaultAsync(v => v.Id == outage.VehicleId);
            if (vehicle == null)
            {
                await _logger.Warning("OutagePeriodsController.Post", "Vehicle not found.", $"VehicleId: {outage.VehicleId}");
                return NotFound("SERVER ERROR → NOT FOUND: Vehicle not found!");
            }

            if (vehicle.ClientCompanyId != company.Id)
            {
                await _logger.Warning("OutagePeriodsController.Post", "Vehicle-company mismatch.", $"VehicleId: {outage.VehicleId}, CompanyId: {outage.ClientCompanyId}");
                return BadRequest("SERVER ERROR → BAD REQUEST: Vehicle does not belong to the specified company!");
            }

            if (!string.Equals(vehicle.Brand.Trim(), outage.OutageBrand.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                await _logger.Warning("OutagePeriodsController.Post", "Brand mismatch.", $"Expected: {vehicle.Brand}, Received: {outage.OutageBrand}");
                return BadRequest("SERVER ERROR → BAD REQUEST: Brand does not match vehicle's actual brand!");
            }
        }
        else
        {
            if (outage.VehicleId.HasValue &&
                !await db.ClientVehicles.AnyAsync(v => v.Id == outage.VehicleId))
            {
                await _logger.Warning("OutagePeriodsController.Post", "Vehicle not found for Fleet API outage.", $"VehicleId: {outage.VehicleId}");
                return NotFound("SERVER ERROR → NOT FOUND: Vehicle not found!");
            }

            if (outage.ClientCompanyId.HasValue &&
                !await db.ClientCompanies.AnyAsync(c => c.Id == outage.ClientCompanyId))
            {
                await _logger.Warning("OutagePeriodsController.Post", "Client company not found for Fleet API outage.", $"CompanyId: {outage.ClientCompanyId}");
                return NotFound("SERVER ERROR → NOT FOUND: Client company not found!");
            }
        }

        var sanitizedOutageBrand = outage.OutageBrand?.Trim();

        if (string.IsNullOrWhiteSpace(sanitizedOutageBrand) ||
            !VehicleConstants.ValidBrands.Contains(sanitizedOutageBrand))
        {
            await _logger.Warning("OutagePeriodsController.Post", "Invalid outage brand.", $"Brand: {outage.OutageBrand}");
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid outage brand!");
        }

        outage.OutageBrand = sanitizedOutageBrand;
        outage.CreatedAt = DateTime.UtcNow;

        db.OutagePeriods.Add(outage);
        await db.SaveChangesAsync();

        await _logger.Info("OutagePeriodsController.Post", "Outage period registered successfully.", $"OutageId: {outage.Id}, Type: {outage.OutageType}");

        return CreatedAtAction(nameof(Get), new { id = outage.Id }, outage.Id);
    }

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        var entity = await db.OutagePeriods.FindAsync(id);
        if (entity == null)
        {
            await _logger.Warning("OutagePeriodsController.PatchNotes", "Outage not found.", $"OutageId: {id}");
            return NotFound();
        }

        if (!body.TryGetProperty("notes", out var notesProp))
        {
            await _logger.Warning("OutagePeriodsController.PatchNotes", "Missing 'notes' field in PATCH body.", $"OutageId: {id}");
            return BadRequest("SERVER ERROR → BAD REQUEST: Notes field missing!");
        }

        entity.Notes = notesProp.GetString() ?? string.Empty;
        await db.SaveChangesAsync();

        await _logger.Debug("OutagePeriodsController.PatchNotes", "Outage notes updated.", $"OutageId: {id}");

        return NoContent();
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadZip(int id)
    {
        var outage = await db.OutagePeriods.FindAsync(id);
        if (outage == null)
        {
            await _logger.Warning("OutagePeriodsController.DownloadZip", "Outage record not found.", $"OutageId: {id}");
            return NotFound("SERVER ERROR → NOT FOUND: Outage record not found!");
        }

        if (string.IsNullOrWhiteSpace(outage.ZipFilePath))
        {
            await _logger.Warning("OutagePeriodsController.DownloadZip", "No zip file path present.", $"OutageId: {id}");
            return NotFound("SERVER ERROR → NOT FOUND: No zip file associated with this outage!");
        }

        if (string.IsNullOrWhiteSpace(env.WebRootPath))
        {
            await _logger.Error("OutagePeriodsController.DownloadZip", "WebRootPath not configured.");
            return StatusCode(500, "SERVER ERROR → STATUS CODE: WebRootPath not configured!");
        }

        var fullPath = Path.Combine(env.WebRootPath, outage.ZipFilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (!System.IO.File.Exists(fullPath))
        {
            await _logger.Warning("OutagePeriodsController.DownloadZip", "ZIP file not found on disk.", $"Path: {fullPath}");
            return NotFound("SERVER ERROR → NOT FOUND: .zip file not found on the server!");
        }

        var fileName = Path.GetFileName(fullPath);
        var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);

        await _logger.Info("OutagePeriodsController.DownloadZip", "Outage ZIP file downloaded successfully.", $"OutageId: {id}, File: {fileName}");

        return File(fileBytes, "application/zip", fileName);
    }
}
