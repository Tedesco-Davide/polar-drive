using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Globalization;
using PolarDrive.Data.Constants;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadOutageZipController(PolarDriveDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private readonly PolarDriveLogger _logger = new(db);

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

        string? relativeZipPath = null;
        if (zipFile != null && zipFile.Length > 0)
        {
            if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                await _logger.Warning("UploadOutageZipController", "Uploaded file is not a .zip.", zipFile.FileName);
                return BadRequest("SERVER ERROR → BAD REQUEST: ZIP file must end in .zip!");
            }

            using var zipStream = new MemoryStream();
            await zipFile.CopyToAsync(zipStream);
            zipStream.Position = 0;

            try
            {
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
                bool hasPdf = archive.Entries.Any(e =>
                    Path.GetExtension(e.FullName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) &&
                    !e.FullName.EndsWith("/")
                );

                if (!hasPdf)
                {
                    await _logger.Warning("UploadOutageZipController", "ZIP file does not contain a PDF.", zipFile.FileName);
                    return BadRequest("SERVER ERROR → BAD REQUEST: ZIP must contain at least one .pdf!");
                }
            }
            catch (InvalidDataException)
            {
                await _logger.Error("UploadOutageZipController", "ZIP file corrupted or invalid.");
                return BadRequest("SERVER ERROR → BAD REQUEST: ZIP file is corrupted or invalid!");
            }

            zipStream.Position = 0;

            var zipsDir = Path.Combine(env.WebRootPath ?? "wwwroot", "zips-outages");
            Directory.CreateDirectory(zipsDir);

            var zipFileName = $"manual_outage_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
            var finalPath = Path.Combine(zipsDir, zipFileName);
            await using var fileStream = new FileStream(finalPath, FileMode.Create);
            await zipStream.CopyToAsync(fileStream);

            relativeZipPath = Path.Combine("zips-outages", zipFileName).Replace("\\", "/");
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

            if (zipFile != null && !string.IsNullOrWhiteSpace(relativeZipPath))
            {
                existingOutage.ZipFilePath = relativeZipPath;
                await db.SaveChangesAsync();

                await _logger.Info("UploadOutageZipController", "ZIP file added to existing outage.", $"OutageId: {existingOutage.Id}, File: {relativeZipPath}");

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
                ZipFilePath = relativeZipPath,
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
                zipFilePath = relativeZipPath,
                isNew = true
            });
        }
    }
}