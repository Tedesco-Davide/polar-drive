using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Globalization;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadOutageZipController(PolarDriveDbContext db, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> UploadOutage(
        [FromForm] string outageType,
        [FromForm] string outageStart,
        [FromForm] string? outageEnd,
        [FromForm] string status,
        [FromForm] string? vin,
        [FromForm] string? companyVatNumber,
        [FromForm] bool autoDetected,
        [FromForm] int? clientCompanyId,
        [FromForm] int? teslaVehicleId,
        [FromForm] IFormFile? zipFile
    )
    {
        if (string.IsNullOrWhiteSpace(outageType) || !new[] { "Outage Vehicle", "Outage Fleet Api" }.Contains(outageType))
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid outage type!");

        if (!DateTime.TryParseExact(outageStart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStart))
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid outage start date format!");

        DateTime? parsedEnd = null;
        if (!string.IsNullOrWhiteSpace(outageEnd))
        {
            if (!DateTime.TryParseExact(outageEnd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tmpEnd))
                return BadRequest("SERVER ERROR → BAD REQUEST: Invalid outage end date format!");
            parsedEnd = tmpEnd;
        }

        if (status == "OUTAGE-RESOLVED" && parsedEnd is null)
        {
            return BadRequest("SERVER ERROR → BAD REQUEST: OUTAGE-RESOLVED requires outageEnd!");
        }

        if (outageType == "Outage Vehicle")
        {
            if (clientCompanyId is null || teslaVehicleId is null)
                return BadRequest("SERVER ERROR → BAD REQUEST: Missing Tesla vehicle or company ID!");

            var company = await db.ClientCompanies.FirstOrDefaultAsync(c => c.Id == clientCompanyId && c.VatNumber == companyVatNumber);
            if (company == null)
                return NotFound("SERVER ERROR → NOT FOUND: Company not found or VAT mismatch!");

            var vehicle = await db.ClientVehicles.FirstOrDefaultAsync(v => v.Id == teslaVehicleId && v.Vin == vin && v.ClientCompanyId == clientCompanyId);
            if (vehicle == null)
                return NotFound("SERVER ERROR → NOT FOUND: Tesla vehicle not found or mismatched!");
        }

        string? relativeZipPath = null;
        if (zipFile != null && zipFile.Length > 0)
        {
            if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                return BadRequest("SERVER ERROR → BAD REQUEST: ZIP file must end in .zip!");

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
                    return BadRequest("SERVER ERROR → BAD REQUEST: ZIP must contain at least one .pdf!");
            }
            catch (InvalidDataException)
            {
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
            o.OutageStart.Date == parsedStart.Date &&
            o.OutageEnd?.Date == parsedEnd?.Date &&
            o.OutageType.Trim() == outageType.Trim() &&
            (
                (!teslaVehicleId.HasValue && !clientCompanyId.HasValue &&
                o.VehicleId == null && o.ClientCompanyId == null)
                ||
                (teslaVehicleId.HasValue && clientCompanyId.HasValue &&
                o.VehicleId == teslaVehicleId && o.ClientCompanyId == clientCompanyId)
            )
        );

        if (existingOutage != null)
        {
            // Solo se hai inviato un nuovo file, controlla se l'outage ne ha già uno
            if (zipFile != null && !string.IsNullOrWhiteSpace(existingOutage.ZipFilePath))
            {
                return BadRequest("SERVER ERROR → OUTAGE ALREADY HAS A ZIP FILE!");
            }

            // Se stai caricando un nuovo ZIP e prima era vuoto, aggiorna
            if (zipFile != null && !string.IsNullOrWhiteSpace(relativeZipPath))
            {
                existingOutage.ZipFilePath = relativeZipPath;
            }

            await db.SaveChangesAsync();

            return Ok(new
            {
                id = existingOutage.Id,
                outageType,
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
                OutageType = outageType,
                CreatedAt = DateTime.UtcNow,
                OutageStart = parsedStart,
                OutageEnd = parsedEnd,
                AutoDetected = autoDetected,
                ClientCompanyId = clientCompanyId,
                VehicleId = teslaVehicleId,
                ZipFilePath = relativeZipPath,
                Notes = ""
            };

            db.OutagePeriods.Add(outage);
            await db.SaveChangesAsync();

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