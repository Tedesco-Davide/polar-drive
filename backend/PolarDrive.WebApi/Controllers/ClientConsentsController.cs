using System.Text.Json;
using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientConsentsController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly PolarDriveLogger _logger;

    public ClientConsentsController(PolarDriveDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
        _logger = new PolarDriveLogger(db);
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ClientConsentDTO>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? search = null)
    {
        try
        {
            await _logger.Info("ClientConsentsController.Get", "Requested list of client consents",
                $"Page: {page}, PageSize: {pageSize}, Search: {search ?? "none"}");

            var query = _db.ClientConsents
                .Include(c => c.ClientCompany)
                .Include(c => c.ClientVehicle)
                .AsQueryable();

            // Filtro ricerca
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c =>
                    (c.ClientCompany != null && c.ClientCompany.VatNumber.Contains(search)) ||
                    (c.ClientVehicle != null && c.ClientVehicle.Vin.Contains(search)) ||
                    c.ConsentType.Contains(search) ||
                    c.ConsentHash.Contains(search));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(c => c.UploadDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new ClientConsentDTO
                {
                    Id = c.Id,
                    ClientCompanyId = c.ClientCompanyId,
                    CompanyVatNumber = c.ClientCompany!.VatNumber,
                    VehicleId = c.VehicleId,
                    VehicleVIN = c.ClientVehicle!.Vin,
                    UploadDate = c.UploadDate.ToString("o"),
                    ZipFileSize = c.ZipContent != null ? c.ZipContent.Length : 0,
                    HasZipFile = c.ZipContent != null && c.ZipContent.Length > 0,
                    ConsentHash = c.ConsentHash,
                    ConsentType = c.ConsentType,
                    Notes = c.Notes
                })
                .ToListAsync();

            return Ok(new PaginatedResponse<ClientConsentDTO>
            {
                Data = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.Get", "Error retrieving consents", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CreateConsentRequest request)
    {
        try
        {
            await _logger.Info("ClientConsentsController.Post", "Creating new consent manually",
                JsonSerializer.Serialize(request));

            var company = await _db.ClientCompanies.FindAsync(request.ClientCompanyId);
            if (company == null) return NotFound("Company not found");

            var vehicle = await _db.ClientVehicles
                .FirstOrDefaultAsync(v => v.Id == request.VehicleId && v.ClientCompanyId == request.ClientCompanyId);
            if (vehicle == null) return NotFound("Vehicle not found or not associated with company");

            var validConsentTypes = new[] {
                "Consent Activation",
                "Consent Deactivation",
                "Consent Stop Data Fetching",
                "Consent Reactivation"
            };
            if (!validConsentTypes.Contains(request.ConsentType))
                return BadRequest($"Invalid consent type. Valid types: {string.Join(", ", validConsentTypes)}");

            var consent = new ClientConsent
            {
                ClientCompanyId = request.ClientCompanyId,
                VehicleId = request.VehicleId,
                UploadDate = DateTime.Now,
                ConsentType = request.ConsentType,
                Notes = request.Notes ?? "Manually inserted",
                ConsentHash = "",
            };

            _db.ClientConsents.Add(consent);
            await _db.SaveChangesAsync();

            await _logger.Info("ClientConsentsController.Post", $"Created new manual consent with ID {consent.Id}");
            return CreatedAtAction(nameof(Get), new { id = consent.Id }, new { id = consent.Id });
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.Post", "Error creating consent", ex.ToString());
            return StatusCode(500, "Internal server error while creating consent");
        }
    }

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        try
        {
            var entity = await _db.ClientConsents.FindAsync(id);
            if (entity == null) return NotFound("Consent not found");

            if (!body.TryGetProperty("notes", out var notesProp))
                return BadRequest("Missing 'notes' field");

            entity.Notes = notesProp.GetString() ?? string.Empty;
            await _db.SaveChangesAsync();

            await _logger.Info("ClientConsentsController.PatchNotes", $"Updated notes for consent {id}");
            return NoContent();
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.PatchNotes", $"Error updating notes for consent {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadZip(int id)
    {
        var consent = await _db.ClientConsents
            .Include(c => c.ClientVehicle)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (consent == null) return NotFound("Consent not found");
        if (consent.ZipContent == null || consent.ZipContent.Length == 0)
            return NotFound("No ZIP file associated with this consent");

        var fileName = GenerateConsentFileName(consent);
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        if (!string.IsNullOrWhiteSpace(consent.ConsentHash))
            Response.Headers.ETag = $"W/\"{consent.ConsentHash}\"";

        return File(consent.ZipContent, "application/zip", fileName);
    }

    [HttpGet("resolve-ids")]
    public async Task<IActionResult> ResolveIds([FromQuery] string vatNumber, [FromQuery] string vin)
    {
        var company = await _db.ClientCompanies.FirstOrDefaultAsync(c => c.VatNumber == vatNumber);
        if (company == null) return NotFound("Company not found");

        var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Vin == vin);
        if (vehicle == null) return NotFound("Vehicle not found");

        if (vehicle.ClientCompanyId != company.Id)
            return BadRequest("This vehicle does not belong to the company");

        return Ok(new
        {
            clientCompanyId = company.Id,
            vehicleId = vehicle.Id,
            vehicleBrand = vehicle.Brand
        });
    }

    [HttpGet("download-all-by-company")]
    public async Task<IActionResult> DownloadAllConsentsByCompany([FromQuery] string vatNumber)
    {
        try
        {
            var company = await _db.ClientCompanies.FirstOrDefaultAsync(c => c.VatNumber == vatNumber);
            if (company == null) return NotFound(new { success = false, message = "Azienda non trovata" });

            var allConsents = await _db.ClientConsents
                .Include(c => c.ClientVehicle)
                .Where(c => c.ClientCompanyId == company.Id)
                .OrderBy(c => c.UploadDate)
                .ToListAsync();

            if (!allConsents.Any())
                return Ok(new { success = true, hasData = false, message = "Nessun consenso trovato" });

            var consentsWithZip = allConsents.Where(c => c.ZipContent != null && c.ZipContent.Length > 0).ToList();
            if (!consentsWithZip.Any())
                return Ok(new { success = true, hasData = false, message = "Nessun file disponibile" });

            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var consent in consentsWithZip)
                {
                    var consentFileName = GenerateConsentFileName(consent);
                    var entry = archive.CreateEntry(consentFileName, CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await entryStream.WriteAsync(consent.ZipContent!, 0, consent.ZipContent!.Length);
                }
            }

            var fileName = GenerateArchiveFileName(company);
            zipStream.Position = 0;
            return File(zipStream.ToArray(), "application/zip", fileName);
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.DownloadAllConsentsByCompany", "Error", ex.ToString());
            return StatusCode(500, new { success = false, message = "Errore interno" });
        }
    }

    private static string GenerateConsentFileName(ClientConsent consent)
    {
        var consentType = consent.ConsentType?.Replace(" ", "_") ?? "unknown";
        var uploadDate = consent.UploadDate.ToString("yyyyMMdd");
        var vin = consent.ClientVehicle?.Vin ?? "unknown";
        return $"{consentType}_{uploadDate}_{vin}_{consent.Id}.zip";
    }

    private static string GenerateArchiveFileName(ClientCompany company)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var sanitizedCompanyName = System.Text.RegularExpressions.Regex.Replace(company.Name, @"[^a-zA-Z0-9]", "_");
        return $"consensi_{sanitizedCompanyName}_{company.VatNumber}_{timestamp}.zip";
    }
}

public class CreateConsentRequest
{
    public int ClientCompanyId { get; set; }
    public int VehicleId { get; set; }
    public string ConsentType { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }
    public string? Notes { get; set; }
}