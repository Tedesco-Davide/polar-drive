using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientConsentsController(PolarDriveDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private readonly PolarDriveLogger _logger = new(db);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClientConsentDTO>>> Get()
    {
        await _logger.Info("ClientConsentsController.Get", "Requested list of client consents.");

        var items = await db.ClientConsents
            .Include(c => c.ClientCompany)
            .Include(c => c.ClientVehicle)
            .Select(c => new ClientConsentDTO
            {
                Id = c.Id,
                ClientCompanyId = c.ClientCompanyId,
                CompanyVatNumber = c.ClientCompany!.VatNumber,
                VehicleId = c.VehicleId,
                VehicleVIN = c.ClientVehicle!.Vin,
                UploadDate = c.UploadDate.ToString("o"),
                ZipFilePath = c.ZipFilePath,
                ConsentHash = c.ConsentHash,
                ConsentType = c.ConsentType,
                Notes = c.Notes
            }).ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult> Post([FromBody] ClientConsentDTO dto)
    {
        if (!await db.ClientCompanies.AnyAsync(c => c.Id == dto.ClientCompanyId))
        {
            await _logger.Warning("ClientConsentsController.Post", "Client company not found.", $"CompanyId: {dto.ClientCompanyId}");
            return NotFound("SERVER ERROR → NOT FOUND: Client Company not found!");
        }

        if (!await db.ClientVehicles.AnyAsync(v => v.Id == dto.VehicleId))
        {
            await _logger.Warning("ClientConsentsController.Post", "Client vehicle not found.", $"VehicleId: {dto.VehicleId}");
            return NotFound("SERVER ERROR → NOT FOUND: Vehicle not found!");
        }

        var entity = new ClientConsent
        {
            ClientCompanyId = dto.ClientCompanyId,
            VehicleId = dto.VehicleId,
            UploadDate = ParseDate(dto.UploadDate),
            ZipFilePath = dto.ZipFilePath,
            ConsentHash = dto.ConsentHash,
            ConsentType = dto.ConsentType,
            Notes = dto.Notes ?? string.Empty
        };

        db.ClientConsents.Add(entity);
        await db.SaveChangesAsync();

        await _logger.Info("ClientConsentsController.Post", "Client consent inserted successfully.", $"ConsentId: {entity.Id}, Hash: {entity.ConsentHash}");

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity.Id);
    }

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        var entity = await db.ClientConsents.FindAsync(id);
        if (entity == null)
        {
            await _logger.Warning("ClientConsentsController.PatchNotes", "Consent not found.", $"ConsentId: {id}");
            return NotFound();
        }

        if (!body.TryGetProperty("notes", out var notesProp))
        {
            await _logger.Error("ClientConsentsController.PatchNotes", "Missing 'notes' property in body.");
            return BadRequest("SERVER ERROR → BAD REQUEST: Notes filed missing!");
        }

        entity.Notes = notesProp.GetString() ?? string.Empty;
        await db.SaveChangesAsync();

        await _logger.Debug("ClientConsentsController.PatchNotes", "Notes updated successfully.", $"ConsentId: {id}");
        return NoContent();
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadZip(int id)
    {
        var consent = await db.ClientConsents.FindAsync(id);
        if (consent == null)
        {
            await _logger.Warning("ClientConsentsController.DownloadZip", "Consent not found.", $"ConsentId: {id}");
            return NotFound("SERVER ERROR → NOT FOUND: Client Consent not found!");
        }

        if (string.IsNullOrWhiteSpace(env.WebRootPath))
        {
            await _logger.Error("ClientConsentsController.DownloadZip", "WebRootPath not configured.");
            return StatusCode(500, "SERVER ERROR → STATUS CODE: WebRootPath not configured!");
        }

        var fullPath = Path.Combine(env.WebRootPath, consent.ZipFilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (!System.IO.File.Exists(fullPath))
        {
            await _logger.Warning("ClientConsentsController.DownloadZip", ".zip file not found on disk.", $"Path: {fullPath}");
            return NotFound("SERVER ERROR → NOT FOUND: .zip file not found on the server!");
        }

        var fileName = Path.GetFileName(fullPath);
        var contentType = "application/zip";
        var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);

        await _logger.Info("ClientConsentsController.DownloadZip", "ZIP file downloaded successfully.", $"ConsentId: {id}, File: {fileName}");

        return File(fileBytes, contentType, fileName);
    }

    [HttpGet("resolve-ids")]
    public async Task<IActionResult> ResolveIds([FromQuery] string vatNumber, [FromQuery] string vin)
    {
        var company = await db.ClientCompanies.FirstOrDefaultAsync(c => c.VatNumber == vatNumber);
        if (company == null)
        {
            await _logger.Warning("ClientConsentsController.ResolveIds", "Company not found.", $"VAT: {vatNumber}");
            return NotFound("SERVER ERROR → NOT FOUND: Client Company not found!");
        }

        var vehicle = await db.ClientVehicles.FirstOrDefaultAsync(v => v.Vin == vin);
        if (vehicle == null)
        {
            await _logger.Warning("ClientConsentsController.ResolveIds", "Vehicle not found.", $"VIN: {vin}");
            return NotFound("SERVER ERROR → NOT FOUND: Vehicle not found!");
        }

        if (vehicle.ClientCompanyId != company.Id)
        {
            await _logger.Warning("ClientConsentsController.ResolveIds", "Vehicle not associated to this company.", $"CompanyId: {company.Id}, VehicleId: {vehicle.Id}");
            return BadRequest("SERVER ERROR → BAD REQUEST: This vehicle does not belong to the company you are trying to associate!");
        }

        await _logger.Info("ClientConsentsController.ResolveIds", "Company and vehicle IDs resolved successfully.", $"CompanyId: {company.Id}, VehicleId: {vehicle.Id}");

        return Ok(new
        {
            clientCompanyId = company.Id,
            vehicleId = vehicle.Id,
            vehicleBrand = vehicle.Brand
        });
    }

    private static DateTime ParseDate(string date)
    {
        return DateTime.ParseExact(date, "dd/MM/yyyy", null);
    }
}
