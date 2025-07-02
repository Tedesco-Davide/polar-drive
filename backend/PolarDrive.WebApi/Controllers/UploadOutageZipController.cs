using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Globalization;
using PolarDrive.Data.Constants;

namespace PolarDrive.WebApi.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[ApiController]
[Route("api/[controller]")]
public class UploadOutageZipController(PolarDriveDbContext db) : ControllerBase
{
    private readonly PolarDriveLogger _logger = new(db);

    //  usa la stessa struttura del FileManager
    private readonly string _outageZipStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "storage", "outages-zips");

    [HttpPost]
    public async Task<IActionResult> UploadOutage(
        [FromForm] string outageType,
        [FromForm] string outageBrand,
        [FromForm] string outageStart,
        [FromForm] string? outageEnd,
        [FromForm] string status,
        [FromForm] string? vin,
        [FromForm] string? companyVatNumber,
        [FromForm] bool autoDetected,
        [FromForm] int? clientCompanyId,
        [FromForm] int? vehicleId,
        [FromForm] IFormFile? zipFile
    )
    {
        var sanitizedOutageType = outageType?.Trim();
        if (string.IsNullOrWhiteSpace(sanitizedOutageType) || !OutageConstants.ValidOutageTypes.Contains(sanitizedOutageType))
        {
            await _logger.Warning("UploadOutageZipController", "Invalid outage type.", outageType);
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid outage type!");
        }

        var sanitizedOutageBrand = outageBrand?.Trim();
        if (string.IsNullOrWhiteSpace(sanitizedOutageBrand) || !VehicleConstants.ValidBrands.Contains(sanitizedOutageBrand))
        {
            await _logger.Warning("UploadOutageZipController", "Invalid outage brand.", outageBrand);
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid outage brand!");
        }

        if (!DateTime.TryParseExact(outageStart, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStart))
        {
            await _logger.Warning("UploadOutageZipController", "Invalid outageStart format.", outageStart);
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid outage start date format!");
        }

        DateTime? parsedEnd = null;
        if (!string.IsNullOrWhiteSpace(outageEnd))
        {
            if (!DateTime.TryParseExact(outageEnd, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tmpEnd))
            {
                await _logger.Warning("UploadOutageZipController", "Invalid outageEnd format.", outageEnd);
                return BadRequest("SERVER ERROR → BAD REQUEST: Invalid outage end date format!");
            }
            parsedEnd = tmpEnd;
        }

        if (status == "OUTAGE-RESOLVED" && parsedEnd is null)
        {
            await _logger.Warning("UploadOutageZipController", "Resolved outage without end date.");
            return BadRequest("SERVER ERROR → BAD REQUEST: OUTAGE-RESOLVED requires outageEnd!");
        }

        if (outageType == "Outage Vehicle")
        {
            if (clientCompanyId is null || vehicleId is null)
                return BadRequest("SERVER ERROR → BAD REQUEST: Missing vehicle or company ID!");

            var company = await db.ClientCompanies.FirstOrDefaultAsync(c => c.Id == clientCompanyId && c.VatNumber == companyVatNumber);
            if (company == null)
            {
                await _logger.Warning("UploadOutageZipController", "Company not found or VAT mismatch.", $"CompanyId: {clientCompanyId}, VAT: {companyVatNumber}");
                return NotFound("SERVER ERROR → NOT FOUND: Company not found or VAT mismatch!");
            }

            var vehicle = await db.ClientVehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && v.Vin == vin && v.ClientCompanyId == clientCompanyId);
            if (vehicle == null)
            {
                await _logger.Warning("UploadOutageZipController", "Vehicle not found or mismatched.", $"VehicleId: {vehicleId}, VIN: {vin}");
                return NotFound("SERVER ERROR → NOT FOUND: vehicle not found or mismatched!");
            }
        }

        string? zipFilePath = null;
        if (zipFile != null && zipFile.Length > 0)
        {
            zipFilePath = await ProcessZipFileAsync(zipFile);
            if (zipFilePath == null)
            {
                return BadRequest("SERVER ERROR → BAD REQUEST: Invalid ZIP file!");
            }
        }

        var allOutages = await db.OutagePeriods.ToListAsync();

        var existingOutage = allOutages.FirstOrDefault(o =>
            o.OutageStart == parsedStart &&
            o.OutageEnd == parsedEnd &&
            string.Equals(o.OutageType?.Trim(), sanitizedOutageType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(o.OutageBrand?.Trim(), sanitizedOutageBrand, StringComparison.OrdinalIgnoreCase) &&
            (
                (!vehicleId.HasValue && !clientCompanyId.HasValue &&
                 o.VehicleId == null && o.ClientCompanyId == null)
                ||
                (vehicleId.HasValue && clientCompanyId.HasValue &&
                 o.VehicleId == vehicleId && o.ClientCompanyId == clientCompanyId)
            )
        );

        if (existingOutage != null)
        {
            if (zipFile != null && !string.IsNullOrWhiteSpace(existingOutage.ZipFilePath))
            {
                await _logger.Warning("UploadOutageZipController", "Outage already has a ZIP file.");
                return BadRequest("SERVER ERROR → OUTAGE ALREADY HAS A ZIP FILE!");
            }

            if (zipFile != null && !string.IsNullOrWhiteSpace(zipFilePath))
            {
                existingOutage.ZipFilePath = zipFilePath;
                await db.SaveChangesAsync();

                await _logger.Info("UploadOutageZipController", "ZIP file added to existing outage.", $"OutageId: {existingOutage.Id}, File: {zipFilePath}");

                return Ok(new
                {
                    id = existingOutage.Id,
                    outageType,
                    outageBrand,
                    outageStart = parsedStart.ToString("dd/MM/yyyy"),
                    outageEnd = parsedEnd?.ToString("dd/MM/yyyy"),
                    zipFilePath = existingOutage.ZipFilePath,
                    isNew = false
                });
            }

            return Ok(new
            {
                id = existingOutage.Id,
                outageType,
                outageBrand,
                outageStart = parsedStart.ToString("dd/MM/yyyy"),
                outageEnd = parsedEnd?.ToString("dd/MM/yyyy"),
                zipFilePath = existingOutage.ZipFilePath,
                isNew = false
            });
        }
        else
        {
            var outage = new OutagePeriod
            {
                OutageType = sanitizedOutageType,
                OutageBrand = sanitizedOutageBrand,
                CreatedAt = DateTime.UtcNow,
                OutageStart = parsedStart,
                OutageEnd = parsedEnd,
                AutoDetected = autoDetected,
                ClientCompanyId = clientCompanyId,
                VehicleId = vehicleId,
                ZipFilePath = zipFilePath,
                Notes = ""
            };

            db.OutagePeriods.Add(outage);
            await db.SaveChangesAsync();

            await _logger.Info("UploadOutageZipController", "New outage record created.", $"OutageId: {outage.Id}");

            return Ok(new
            {
                id = outage.Id,
                outageType,
                outageStart = parsedStart.ToString("dd/MM/yyyy"),
                outageEnd = parsedEnd?.ToString("dd/MM/yyyy"),
                zipFilePath = zipFilePath,
                isNew = true
            });
        }
    }

    [HttpPost("{outageId}/upload-zip")]
    public async Task<IActionResult> UploadZipToExistingOutage(
        int outageId,
        [FromForm] IFormFile zipFile,
        [FromQuery] bool replaceExisting = false)
    {
        var outage = await db.OutagePeriods.FirstOrDefaultAsync(o => o.Id == outageId);
        if (outage == null)
        {
            await _logger.Warning("UploadZipToExistingOutage", "Outage not found.", $"OutageId: {outageId}");
            return NotFound("SERVER ERROR → NOT FOUND: Outage not found!");
        }

        // Controlla se esiste già un ZIP e se non è autorizzata la sostituzione
        if (!string.IsNullOrWhiteSpace(outage.ZipFilePath) && !replaceExisting)
        {
            await _logger.Warning("UploadZipToExistingOutage", "Outage already has ZIP, replacement not authorized.",
                $"OutageId: {outageId}, ExistingPath: {outage.ZipFilePath}");
            return Conflict("SERVER ERROR → CONFLICT: Outage already has a ZIP file. Use replaceExisting=true to replace it.");
        }

        if (zipFile == null || zipFile.Length == 0)
        {
            return BadRequest("SERVER ERROR → BAD REQUEST: No ZIP file provided!");
        }

        // Elimina il file precedente se esiste
        if (!string.IsNullOrWhiteSpace(outage.ZipFilePath) && System.IO.File.Exists(outage.ZipFilePath))
        {
            try
            {
                System.IO.File.Delete(outage.ZipFilePath);
                await _logger.Info("UploadZipToExistingOutage", "Old ZIP file deleted.", outage.ZipFilePath);
            }
            catch (Exception ex)
            {
                await _logger.Warning("UploadZipToExistingOutage", "Failed to delete old ZIP file.",
                    $"Path: {outage.ZipFilePath}, Error: {ex.Message}");
            }
        }

        // Processa il nuovo file
        var zipFilePath = await ProcessZipFileAsync(zipFile, $"outage_{outageId}_");
        if (zipFilePath == null)
        {
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid ZIP file!");
        }

        // Aggiorna il database
        outage.ZipFilePath = zipFilePath;
        await db.SaveChangesAsync();

        await _logger.Info("UploadZipToExistingOutage", "ZIP file uploaded successfully.",
            $"OutageId: {outageId}, File: {zipFilePath}, Replaced: {replaceExisting}");

        return Ok(new
        {
            id = outage.Id,
            zipFilePath = zipFilePath,
            replaced = replaceExisting,
            message = replaceExisting ? "ZIP file replaced successfully" : "ZIP file uploaded successfully"
        });
    }

    [HttpDelete("{outageId}/delete-zip")]
    public async Task<IActionResult> DeleteZipFromOutage(int outageId)
    {
        var outage = await db.OutagePeriods.FirstOrDefaultAsync(o => o.Id == outageId);
        if (outage == null)
        {
            await _logger.Warning("DeleteZipFromOutage", "Outage not found.", $"OutageId: {outageId}");
            return NotFound("SERVER ERROR → NOT FOUND: Outage not found!");
        }

        if (string.IsNullOrWhiteSpace(outage.ZipFilePath))
        {
            await _logger.Warning("DeleteZipFromOutage", "No ZIP file to delete.", $"OutageId: {outageId}");
            return BadRequest("SERVER ERROR → BAD REQUEST: No ZIP file associated with this outage!");
        }

        // Elimina il file fisico
        if (System.IO.File.Exists(outage.ZipFilePath))
        {
            try
            {
                System.IO.File.Delete(outage.ZipFilePath);
                await _logger.Info("DeleteZipFromOutage", "ZIP file deleted from filesystem.", outage.ZipFilePath);
            }
            catch (Exception ex)
            {
                await _logger.Warning("DeleteZipFromOutage", "Failed to delete ZIP file from filesystem.",
                    $"Path: {outage.ZipFilePath}, Error: {ex.Message}");
            }
        }

        // Rimuovi il riferimento dal database
        outage.ZipFilePath = null;
        await db.SaveChangesAsync();

        await _logger.Info("DeleteZipFromOutage", "ZIP file reference removed from database.",
            $"OutageId: {outageId}");

        return Ok(new
        {
            id = outage.Id,
            message = "ZIP file deleted successfully"
        });
    }

    // ✅ MODIFICATO: ProcessZipFileAsync ora accetta qualsiasi contenuto
    private async Task<string?> ProcessZipFileAsync(IFormFile zipFile, string? filePrefix = null)
    {
        // ✅ Controlla solo che sia un file .zip
        if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await _logger.Warning("ProcessZipFileAsync", "Uploaded file is not a .zip.", zipFile.FileName);
            return null;
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
            return null;
        }
        catch (Exception ex)
        {
            await _logger.Error("ProcessZipFileAsync", "Unexpected error processing ZIP file.", ex.Message);
            return null;
        }

        zipStream.Position = 0;

        // ✅ Crea la directory se non esiste
        if (!Directory.Exists(_outageZipStoragePath))
        {
            Directory.CreateDirectory(_outageZipStoragePath);
        }

        // ✅ Genera il nome del file
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = string.IsNullOrWhiteSpace(filePrefix)
            ? $"outage_{timestamp}.zip"
            : $"{filePrefix}{timestamp}.zip";

        var finalPath = Path.Combine(_outageZipStoragePath, fileName);

        // ✅ Salva il file
        await using var fileStream = new FileStream(finalPath, FileMode.Create);
        await zipStream.CopyToAsync(fileStream);

        await _logger.Info("ProcessZipFileAsync", "ZIP file saved successfully.", finalPath);

        return finalPath;
    }
}