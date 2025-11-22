using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Globalization;
using PolarDrive.Data.Constants;
using System.IO.Compression;

namespace PolarDrive.WebApi.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[ApiController]
[Route("api/[controller]")]
public class UploadOutageZipController(PolarDriveDbContext db) : ControllerBase
{
    private readonly PolarDriveLogger _logger = new();

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
                await _logger.Warning("UploadOutageZipController", "Company not found or VAT mismatch.", $"ClientCompanyId: {clientCompanyId}, VAT: {companyVatNumber}");
                return NotFound("SERVER ERROR → NOT FOUND: Company not found or VAT mismatch!");
            }

            var vehicle = await db.ClientVehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && v.Vin == vin && v.ClientCompanyId == clientCompanyId);
            if (vehicle == null)
            {
                await _logger.Warning("UploadOutageZipController", "Vehicle not found or mismatched.", $"VehicleId: {vehicleId}, VIN: {vin}");
                return NotFound("SERVER ERROR → NOT FOUND: vehicle not found or mismatched!");
            }
        }

        byte[]? zipContent = null;
        string zipHash = "";

        if (zipFile != null && zipFile.Length > 0)
        {
            using var ms = new MemoryStream();
            await zipFile.CopyToAsync(ms);

            ms.Position = 0;
            try { using var _ = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true); }
            catch { return BadRequest("SERVER ERROR → BAD REQUEST: Invalid ZIP file!"); }

            ms.Position = 0;
            using var sha = SHA256.Create();
            zipHash = Convert.ToHexStringLower(await sha.ComputeHashAsync(ms));

            var duplicate = await db.OutagePeriods.FirstOrDefaultAsync(o => o.ZipHash == zipHash);
            if (duplicate != null)
                return BadRequest($"SERVER ERROR → File already exists for outage ID {duplicate.Id}!");

            ms.Position = 0;
            zipContent = ms.ToArray();
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
            if (zipFile != null)
            {
                if (existingOutage.ZipContent != null && existingOutage.ZipContent.Length > 0)
                {
                    await _logger.Warning("UploadOutageZipController", "Outage already has a ZIP file.");
                    return BadRequest("SERVER ERROR → OUTAGE ALREADY HAS A ZIP FILE!");
                }

                if (zipContent != null)
                {
                    existingOutage.ZipContent = zipContent;
                    existingOutage.ZipHash = zipHash;
                    await db.SaveChangesAsync();

                    await _logger.Info("UploadOutageZipController", "ZIP file added to existing outage.", $"OutageId: {existingOutage.Id}, File: {existingOutage.ZipContent}");

                    return Ok(new
                    {
                        id = existingOutage.Id,
                        outageType,
                        outageBrand,
                        outageStart = parsedStart.ToString("dd/MM/yyyy"),
                        outageEnd = parsedEnd?.ToString("dd/MM/yyyy"),
                        hasZip = existingOutage.ZipContent != null && existingOutage.ZipContent.Length > 0,
                        zipSize = existingOutage.ZipContent?.Length ?? 0,
                        isNew = false
                    });
                }
            }

            return Ok(new
            {
                id = existingOutage.Id,
                outageType,
                outageBrand,
                outageStart = parsedStart.ToString("dd/MM/yyyy"),
                outageEnd = parsedEnd?.ToString("dd/MM/yyyy"),
                hasZip = existingOutage.ZipContent != null && existingOutage.ZipContent.Length > 0,
                zipSize = existingOutage.ZipContent?.Length ?? 0,
                isNew = false
            });
        }
        else
        {
            var outage = new OutagePeriod
            {
                OutageType = sanitizedOutageType,
                OutageBrand = sanitizedOutageBrand,
                CreatedAt = DateTime.Now,
                OutageStart = parsedStart,
                OutageEnd = parsedEnd,
                AutoDetected = autoDetected,
                ClientCompanyId = clientCompanyId,
                VehicleId = vehicleId,
                ZipContent = zipContent,
                ZipHash = zipHash,
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
                hasZip = outage.ZipContent != null && outage.ZipContent.Length > 0,
                zipSize = outage.ZipContent?.Length ?? 0,
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
            return NotFound("SERVER ERROR → NOT FOUND: Outage not found!");

        if (outage.ZipContent != null && outage.ZipContent.Length > 0 && !replaceExisting)
            return Conflict("SERVER ERROR → CONFLICT: Outage already has a ZIP file. Use replaceExisting=true to replace it.");

        if (zipFile == null || zipFile.Length == 0)
            return BadRequest("SERVER ERROR → BAD REQUEST: No ZIP file provided!");

        using var ms = new MemoryStream();
        await zipFile.CopyToAsync(ms);

        ms.Position = 0;
        try { using var _ = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true); }
        catch { return BadRequest("SERVER ERROR → BAD REQUEST: Invalid ZIP file!"); }

        ms.Position = 0;
        using var sha = SHA256.Create();
        var hash = Convert.ToHexStringLower(await sha.ComputeHashAsync(ms));

        var duplicate = await db.OutagePeriods
            .FirstOrDefaultAsync(o => o.ZipHash == hash && o.Id != outageId);
        if (duplicate != null)
            return Conflict($"SERVER ERROR → File already exists for outage ID {duplicate.Id}!");

        ms.Position = 0;
        outage.ZipContent = ms.ToArray();
        outage.ZipHash = hash;
        await db.SaveChangesAsync();

        return Ok(new
        {
            id = outage.Id,
            zipHash = hash,
            size = outage.ZipContent.Length,
            replaced = replaceExisting,
            message = replaceExisting ? "ZIP file replaced successfully" : "ZIP file uploaded successfully"
        });
    }
}