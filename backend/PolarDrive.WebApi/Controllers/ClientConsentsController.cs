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
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClientConsentDTO>>> Get()
    {
        var items = await db.ClientConsents
            .Include(c => c.ClientCompany)
            .Include(c => c.ClientVehicle)
            .Select(c => new ClientConsentDTO
            {
                Id = c.Id,
                ClientCompanyId = c.ClientCompanyId,
                CompanyVatNumber = c.ClientCompany!.VatNumber,
                TeslaVehicleId = c.VehicleId,
                TeslaVehicleVIN = c.ClientVehicle!.Vin,
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
            return NotFound("SERVER ERROR → NOT FOUND: Client Company not found!");

        if (!await db.ClientVehicles.AnyAsync(v => v.Id == dto.TeslaVehicleId))
            return NotFound("SERVER ERROR → NOT FOUND: Tesla vehicle not found!");

        var entity = new ClientConsent
        {
            ClientCompanyId = dto.ClientCompanyId,
            VehicleId = dto.TeslaVehicleId,
            UploadDate = ParseDate(dto.UploadDate),
            ZipFilePath = dto.ZipFilePath,
            ConsentHash = dto.ConsentHash,
            ConsentType = dto.ConsentType,
            Notes = dto.Notes
        };

        db.ClientConsents.Add(entity);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity.Id);
    }

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        var entity = await db.ClientConsents.FindAsync(id);
        if (entity == null)
            return NotFound();

        if (!body.TryGetProperty("notes", out var notesProp))
            return BadRequest("SERVER ERROR → BAD REQUEST: Notes filed missing!");

        entity.Notes = notesProp.GetString() ?? string.Empty;
        await db.SaveChangesAsync();

        return NoContent();
    }   

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadZip(int id)
    {
        var consent = await db.ClientConsents.FindAsync(id);
        if (consent == null)
            return NotFound("SERVER ERROR → NOT FOUND: Client Consent not found!");

        if (string.IsNullOrWhiteSpace(env.WebRootPath))
            return StatusCode(500, "SERVER ERROR → STATUS CODE: WebRootPath not configured!");

        var fullPath = Path.Combine(env.WebRootPath, consent.ZipFilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (!System.IO.File.Exists(fullPath))
            return NotFound("SERVER ERROR → NOT FOUND: .zip file not found on the server!");

        var fileName = Path.GetFileName(fullPath);
        var contentType = "application/zip";

        var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
        return File(fileBytes, contentType, fileName);
    }

    [HttpGet("resolve-ids")]
    public async Task<IActionResult> ResolveIds([FromQuery] string vatNumber, [FromQuery] string vin)
    {
        var company = await db.ClientCompanies.FirstOrDefaultAsync(c => c.VatNumber == vatNumber);
        if (company == null)
            return NotFound("SERVER ERROR → NOT FOUND: Client Company not found!");

        var vehicle = await db.ClientVehicles.FirstOrDefaultAsync(v => v.Vin == vin);
        if (vehicle == null)
            return NotFound("SERVER ERROR → NOT FOUND: Tesla vehicle not found!");

        if (vehicle.ClientCompanyId != company.Id)
            return BadRequest("SERVER ERROR → BAD REQUEST: This Tesla vehicle does not belong to the company you are trying to associate!");

        return Ok(new
        {
            clientCompanyId = company.Id,
            teslaVehicleId = vehicle.Id
        });
    }

    private static DateTime ParseDate(string date)
    {
        return DateTime.ParseExact(date, "dd/MM/yyyy", null);
    }
}