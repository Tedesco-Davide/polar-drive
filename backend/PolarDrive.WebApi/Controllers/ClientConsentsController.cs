using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;
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

    // ✅ AGGIORNATO: usa storage/consents-zips invece di wwwroot
    private readonly string _consentZipStoragePath;

    public ClientConsentsController(PolarDriveDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
        _logger = new PolarDriveLogger(db);

        // ✅ AGGIORNATO: usa storage/consents-zips come negli outages
        _consentZipStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "storage", "consents-zips");
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClientConsentDTO>>> Get()
    {
        await _logger.Info("ClientConsentsController.Get", "Requested list of client consents.");

        var items = await _db.ClientConsents
            .Include(c => c.ClientCompany)
            .Include(c => c.ClientVehicle)
            .OrderByDescending(c => c.UploadDate)
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
                Notes = c.Notes,
                // ✅ NUOVO: Campo calcolato per presenza ZIP (allineato agli outages)
                HasZipFile = !string.IsNullOrWhiteSpace(c.ZipFilePath)
            }).ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// ✅ NUOVO: Crea un nuovo consent manualmente (allineato agli outages)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CreateConsentRequest request)
    {
        try
        {
            await _logger.Info("ClientConsentsController.Post", "Creating new consent manually",
                JsonSerializer.Serialize(request));

            // Validazione company
            var company = await _db.ClientCompanies.FindAsync(request.ClientCompanyId);
            if (company == null)
            {
                return NotFound("Company not found");
            }

            // Validazione vehicle
            var vehicle = await _db.ClientVehicles
                .FirstOrDefaultAsync(v => v.Id == request.VehicleId && v.ClientCompanyId == request.ClientCompanyId);
            if (vehicle == null)
            {
                return NotFound("Vehicle not found or not associated with company");
            }

            // Validazione consent type
            var validConsentTypes = new[] {
                "Consent Activation",
                "Consent Deactivation",
                "Consent Stop Data Fetching",
                "Consent Reactivation"
            };
            if (!validConsentTypes.Contains(request.ConsentType))
            {
                return BadRequest($"Invalid consent type. Valid types: {string.Join(", ", validConsentTypes)}");
            }

            var consent = new ClientConsent
            {
                ClientCompanyId = request.ClientCompanyId,
                VehicleId = request.VehicleId,
                UploadDate = request.UploadDate,
                ConsentType = request.ConsentType,
                Notes = request.Notes ?? "Manually inserted",
                ConsentHash = "", // Verrà aggiornato quando si carica il ZIP
                ZipFilePath = "" // Verrà aggiornato quando si carica il ZIP
            };

            _db.ClientConsents.Add(consent);
            await _db.SaveChangesAsync();

            await _logger.Info("ClientConsentsController.Post",
                $"Created new manual consent with ID {consent.Id}");

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
            if (entity == null)
            {
                return NotFound("Consent not found");
            }

            if (!body.TryGetProperty("notes", out var notesProp))
            {
                return BadRequest("Missing 'notes' field");
            }

            entity.Notes = notesProp.GetString() ?? string.Empty;
            await _db.SaveChangesAsync();

            await _logger.Info("ClientConsentsController.PatchNotes",
                $"Updated notes for consent {id}");

            return NoContent();
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.PatchNotes",
                $"Error updating notes for consent {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// ✅ NUOVO: Upload di un file ZIP per un consent (allineato agli outages)
    /// </summary>
    [HttpPost("{id}/upload-zip")]
    public async Task<IActionResult> UploadZip(int id, IFormFile zipFile)
    {
        try
        {
            var consent = await _db.ClientConsents
                .Include(c => c.ClientVehicle)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (consent == null)
            {
                return NotFound("Consent not found");
            }

            if (zipFile == null || zipFile.Length == 0)
            {
                return BadRequest("No file provided");
            }

            // ✅ Usa il metodo helper per processare il file ZIP
            var (zipFilePath, hash) = await ProcessZipFileAsync(zipFile, $"consent_{id}_");
            if (zipFilePath == null)
            {
                return BadRequest("Invalid ZIP file");
            }

            // Elimina il file precedente se esiste
            if (!string.IsNullOrWhiteSpace(consent.ZipFilePath) && System.IO.File.Exists(consent.ZipFilePath))
            {
                try
                {
                    System.IO.File.Delete(consent.ZipFilePath);
                    await _logger.Info("ClientConsentsController.UploadZip", "Old ZIP file deleted.", consent.ZipFilePath);
                }
                catch (Exception ex)
                {
                    await _logger.Warning("ClientConsentsController.UploadZip", "Failed to delete old ZIP file.",
                        $"Path: {consent.ZipFilePath}, Error: {ex.Message}");
                }
            }

            // Verifica hash duplicato (escludendo il consent corrente)
            var existingWithHash = await _db.ClientConsents
                .FirstOrDefaultAsync(c => c.ConsentHash == hash && c.Id != id);

            if (existingWithHash != null)
            {
                // Elimina il nuovo file caricato
                if (System.IO.File.Exists(zipFilePath))
                {
                    System.IO.File.Delete(zipFilePath);
                }
                return Conflict(new
                {
                    message = "This file has already been uploaded for another consent!",
                    existingId = existingWithHash.Id
                });
            }

            // Aggiorna il database
            consent.ZipFilePath = zipFilePath;
            consent.ConsentHash = hash;
            await _db.SaveChangesAsync();

            await _logger.Info("ClientConsentsController.UploadZip",
                $"Uploaded ZIP file for consent {id}: {Path.GetFileName(zipFilePath)}");

            return Ok(new { zipFilePath = zipFilePath, consentHash = hash });
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.UploadZip",
                $"Error uploading ZIP for consent {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// ✅ AGGIORNATO: Download di un file ZIP (allineato agli outages)
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadZip(int id)
    {
        try
        {
            var consent = await _db.ClientConsents.FindAsync(id);
            if (consent == null)
            {
                return NotFound("Consent not found");
            }

            if (string.IsNullOrWhiteSpace(consent.ZipFilePath))
            {
                return NotFound("No ZIP file associated with this consent");
            }

            // ✅ AGGIORNATO: usa il path completo direttamente
            if (!System.IO.File.Exists(consent.ZipFilePath))
            {
                return NotFound("ZIP file not found on server");
            }

            var fileName = Path.GetFileName(consent.ZipFilePath);
            var fileBytes = await System.IO.File.ReadAllBytesAsync(consent.ZipFilePath);

            await _logger.Info("ClientConsentsController.DownloadZip",
                $"Downloaded ZIP file for consent {id}: {fileName}");

            return File(fileBytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.DownloadZip",
                $"Error downloading ZIP for consent {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// ✅ NUOVO: Elimina il file ZIP di un consent (allineato agli outages)
    /// </summary>
    [HttpDelete("{id}/delete-zip")]
    public async Task<IActionResult> DeleteZip(int id)
    {
        try
        {
            var consent = await _db.ClientConsents.FindAsync(id);
            if (consent == null)
            {
                return NotFound("Consent not found");
            }

            if (string.IsNullOrWhiteSpace(consent.ZipFilePath))
            {
                return BadRequest("No ZIP file associated with this consent");
            }

            // ✅ AGGIORNATO: usa il path completo direttamente
            if (System.IO.File.Exists(consent.ZipFilePath))
            {
                try
                {
                    System.IO.File.Delete(consent.ZipFilePath);
                    await _logger.Info("ClientConsentsController.DeleteZip", "ZIP file deleted from filesystem.", consent.ZipFilePath);
                }
                catch (Exception ex)
                {
                    await _logger.Warning("ClientConsentsController.DeleteZip", "Failed to delete ZIP file from filesystem.",
                        $"Path: {consent.ZipFilePath}, Error: {ex.Message}");
                }
            }

            // Rimuovi il riferimento dal database
            consent.ZipFilePath = "";
            consent.ConsentHash = "";
            await _db.SaveChangesAsync();

            await _logger.Info("ClientConsentsController.DeleteZip", "ZIP file reference removed from database.",
                $"ConsentId: {id}");

            return Ok(new { message = "ZIP file deleted successfully" });
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.DeleteZip",
                $"Error deleting ZIP for consent {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("resolve-ids")]
    public async Task<IActionResult> ResolveIds([FromQuery] string vatNumber, [FromQuery] string vin)
    {
        var company = await _db.ClientCompanies.FirstOrDefaultAsync(c => c.VatNumber == vatNumber);
        if (company == null)
        {
            await _logger.Warning("ClientConsentsController.ResolveIds", "Company not found.", $"VAT: {vatNumber}");
            return NotFound("Company not found");
        }

        var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Vin == vin);
        if (vehicle == null)
        {
            await _logger.Warning("ClientConsentsController.ResolveIds", "Vehicle not found.", $"VIN: {vin}");
            return NotFound("Vehicle not found");
        }

        if (vehicle.ClientCompanyId != company.Id)
        {
            await _logger.Warning("ClientConsentsController.ResolveIds", "Vehicle not associated to this company.", $"CompanyId: {company.Id}, VehicleId: {vehicle.Id}");
            return BadRequest("This vehicle does not belong to the company");
        }

        await _logger.Info("ClientConsentsController.ResolveIds", "Company and vehicle IDs resolved successfully.", $"CompanyId: {company.Id}, VehicleId: {vehicle.Id}");

        return Ok(new
        {
            clientCompanyId = company.Id,
            vehicleId = vehicle.Id,
            vehicleBrand = vehicle.Brand
        });
    }

    #region Private Methods

    /// <summary>
    /// ✅ AGGIORNATO: ProcessZipFileAsync ora accetta qualsiasi contenuto (allineato agli outages)
    /// </summary>
    private async Task<(string? zipFilePath, string hash)> ProcessZipFileAsync(IFormFile zipFile, string? filePrefix = null)
    {
        // ✅ Controlla che sia un file .zip
        if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await _logger.Warning("ProcessZipFileAsync", "Uploaded file is not a .zip.", zipFile.FileName);
            return (null, "");
        }

        using var zipStream = new MemoryStream();
        await zipFile.CopyToAsync(zipStream);
        zipStream.Position = 0;

        try
        {
            // ✅ Verifica solo che sia un ZIP valido, senza controllare il contenuto
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

            // ✅ Log del contenuto per debug (opzionale)
            var fileCount = archive.Entries.Count(e => !e.FullName.EndsWith("/"));
            await _logger.Info("ProcessZipFileAsync", "ZIP file processed successfully.",
                $"FileName: {zipFile.FileName}, Files count: {fileCount}");
        }
        catch (InvalidDataException ex)
        {
            await _logger.Error("ProcessZipFileAsync", "ZIP file corrupted or invalid.", ex.Message);
            return (null, "");
        }
        catch (Exception ex)
        {
            await _logger.Error("ProcessZipFileAsync", "Unexpected error processing ZIP file.", ex.Message);
            return (null, "");
        }

        zipStream.Position = 0;

        // ✅ Calcola l'hash SHA256
        string hash;
        using (var sha = SHA256.Create())
        {
            var hashBytes = await sha.ComputeHashAsync(zipStream);
            hash = Convert.ToHexStringLower(hashBytes);
        }

        zipStream.Position = 0;

        // ✅ Crea la directory se non esiste
        if (!Directory.Exists(_consentZipStoragePath))
        {
            Directory.CreateDirectory(_consentZipStoragePath);
        }

        // ✅ Genera il nome del file
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = string.IsNullOrWhiteSpace(filePrefix)
            ? $"consent_{timestamp}.zip"
            : $"{filePrefix}{timestamp}.zip";

        var finalPath = Path.Combine(_consentZipStoragePath, fileName);

        // ✅ Salva il file
        await using var fileStream = new FileStream(finalPath, FileMode.Create);
        await zipStream.CopyToAsync(fileStream);

        await _logger.Info("ProcessZipFileAsync", "ZIP file saved successfully.", finalPath);

        return (finalPath, hash);
    }

    #endregion

    // Aggiungi questo metodo al ClientConsentsController esistente

    /// <summary>
    /// Download di tutti i consensi per un'azienda specifica in un unico ZIP
    /// </summary>
    [HttpGet("download-all-by-company")]
    public async Task<IActionResult> DownloadAllConsentsByCompany([FromQuery] string vatNumber)
    {
        try
        {
            await _logger.Info("ClientConsentsController.DownloadAllConsentsByCompany",
                "Started download all consents for company", $"VAT: {vatNumber}");

            // Trova l'azienda
            var company = await _db.ClientCompanies.FirstOrDefaultAsync(c => c.VatNumber == vatNumber);
            if (company == null)
            {
                await _logger.Warning("ClientConsentsController.DownloadAllConsentsByCompany",
                    "Company not found", $"VAT: {vatNumber}");
                return NotFound("Company not found");
            }

            // Trova tutti i consensi dell'azienda che hanno un file ZIP
            var consents = await _db.ClientConsents
                .Include(c => c.ClientVehicle)
                .Where(c => c.ClientCompanyId == company.Id &&
                           !string.IsNullOrWhiteSpace(c.ZipFilePath))
                .OrderBy(c => c.UploadDate)
                .ToListAsync();

            if (!consents.Any())
            {
                await _logger.Warning("ClientConsentsController.DownloadAllConsentsByCompany",
                    "No consents with ZIP files found for company", $"CompanyId: {company.Id}");
                return NotFound("No consent files found for this company");
            }

            // Crea un ZIP temporaneo contenente tutti i consensi
            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                var processedCount = 0;

                foreach (var consent in consents)
                {
                    if (string.IsNullOrWhiteSpace(consent.ZipFilePath) ||
                        !System.IO.File.Exists(consent.ZipFilePath))
                    {
                        await _logger.Warning("ClientConsentsController.DownloadAllConsentsByCompany",
                            "Consent ZIP file not found", $"ConsentId: {consent.Id}, Path: {consent.ZipFilePath}");
                        continue;
                    }

                    try
                    {
                        // Leggi il file ZIP del consenso
                        var consentZipBytes = await System.IO.File.ReadAllBytesAsync(consent.ZipFilePath);

                        // Crea un nome file descrittivo per il consenso
                        var consentFileName = $"{consent.ConsentType.Replace(" ", "_")}_{consent.UploadDate:yyyyMMdd}_{consent.ClientVehicle?.Vin ?? "unknown"}_{consent.Id}.zip";

                        // Aggiungi il file ZIP del consenso al ZIP principale
                        var entry = archive.CreateEntry(consentFileName);
                        using var entryStream = entry.Open();
                        await entryStream.WriteAsync(consentZipBytes);

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        await _logger.Error("ClientConsentsController.DownloadAllConsentsByCompany",
                            $"Error processing consent {consent.Id}", ex.ToString());
                        // Continua con gli altri consensi
                    }
                }

                if (processedCount == 0)
                {
                    await _logger.Warning("ClientConsentsController.DownloadAllConsentsByCompany",
                        "No valid consent files could be processed", $"CompanyId: {company.Id}");
                    return NotFound("No valid consent files could be processed");
                }

                await _logger.Info("ClientConsentsController.DownloadAllConsentsByCompany",
                    $"Successfully processed {processedCount} consent files for company {company.Id}");
            }

            // Genera il nome del file ZIP finale
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var sanitizedCompanyName = System.Text.RegularExpressions.Regex.Replace(
                company.Name, @"[^a-zA-Z0-9]", "_");
            var fileName = $"consensi_{sanitizedCompanyName}_{company.VatNumber}_{timestamp}.zip";

            await _logger.Info("ClientConsentsController.DownloadAllConsentsByCompany",
                "All consents ZIP created successfully",
                $"Company: {company.Name}, FileName: {fileName}, Size: {zipStream.Length} bytes");

            // Ritorna il file ZIP
            zipStream.Position = 0;
            return File(zipStream.ToArray(), "application/zip", fileName);
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.DownloadAllConsentsByCompany",
                $"Error downloading all consents for company with VAT {vatNumber}", ex.ToString());
            return StatusCode(500, "Internal server error while creating consents archive");
        }
    }
}

/// <summary>
/// Request per creare un nuovo consent
/// </summary>
public class CreateConsentRequest
{
    public int ClientCompanyId { get; set; }
    public int VehicleId { get; set; }
    public string ConsentType { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }
    public string? Notes { get; set; }
}