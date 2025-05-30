using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Security.Cryptography;
using System.Globalization;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadConsentZipController(PolarDriveDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private readonly PolarDriveLogger _logger = new(db);

    [HttpPost]
    public async Task<IActionResult> UploadConsent(
        [FromForm] int clientCompanyId,
        [FromForm] int vehicleId,
        [FromForm] string consentType,
        [FromForm] string uploadDate,
        [FromForm] string companyVatNumber,
        [FromForm] string vehicleVIN,
        [FromForm] IFormFile zipFile
    )
    {
        if (zipFile == null || zipFile.Length == 0)
        {
            await _logger.Warning("UploadConsentZipController", "ZIP file missing or empty.");
            return BadRequest("SERVER ERROR → BAD REQUEST: File .zip missing!");
        }

        if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await _logger.Warning("UploadConsentZipController", "Invalid file extension.", zipFile.FileName);
            return BadRequest("SERVER ERROR → BAD REQUEST: File type not valid. It must be a .zip!");
        }

        var allowedTypes = new[] {
            "Consent Activation",
            "Consent Deactivation",
            "Consent Stop Data Fetching",
            "Consent Reactivation"
        };

        if (string.IsNullOrWhiteSpace(consentType) || !allowedTypes.Contains(consentType))
        {
            await _logger.Warning("UploadConsentZipController", "Invalid consent type received.", consentType);
            return BadRequest("SERVER ERROR → BAD REQUEST: Consent Type not valid!");
        }

        await using var memoryStream = new MemoryStream();
        await zipFile.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        try
        {
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read, leaveOpen: true);
            bool containsPdf = archive.Entries.Any(entry =>
                Path.GetExtension(entry.FullName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                && !entry.FullName.EndsWith("/")
            );

            if (!containsPdf)
            {
                await _logger.Warning("UploadConsentZipController", "ZIP file does not contain any PDF.", zipFile.FileName);
                return BadRequest("SERVER ERROR → BAD REQUEST: The .zip file must contain at least a single .pdf file!");
            }
        }
        catch (InvalidDataException)
        {
            await _logger.Error("UploadConsentZipController", "Invalid ZIP file format or corrupted.");
            return BadRequest("SERVER ERROR → BAD REQUEST: The .zip file is either damaged/broken or not valid!");
        }

        var company = await db.ClientCompanies.FirstOrDefaultAsync(c => c.Id == clientCompanyId && c.VatNumber == companyVatNumber);
        if (company == null)
        {
            await _logger.Warning("UploadConsentZipController", "Company not found or VAT mismatch.", $"CompanyId: {clientCompanyId}, VAT: {companyVatNumber}");
            return NotFound("SERVER ERROR → NOT FOUND: Company not found or invalid VAT number!");
        }

        var vehicle = await db.ClientVehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && v.Vin == vehicleVIN && v.ClientCompanyId == clientCompanyId);
        if (vehicle == null)
        {
            await _logger.Warning("UploadConsentZipController", "Vehicle not found or mismatch.", $"VehicleId: {vehicleId}, VIN: {vehicleVIN}");
            return NotFound("SERVER ERROR → NOT FOUND: Vehicle not found or not associated with the company!");
        }

        // 🔐 SHA256
        string hash;
        memoryStream.Position = 0;
        using var sha = SHA256.Create();
        var hashBytes = await sha.ComputeHashAsync(memoryStream);
        hash = Convert.ToHexStringLower(hashBytes);
        memoryStream.Position = 0;

        var existingConsent = await db.ClientConsents.FirstOrDefaultAsync(c => c.ConsentHash == hash);
        if (existingConsent != null)
        {
            await _logger.Warning("UploadConsentZipController", "Duplicate consent hash detected.", $"ExistingId: {existingConsent.Id}, Hash: {hash}");
            return Conflict(new { message = "CONFLICT - SERVER ERROR: This file has an existing and validated Hash, therefore has already been uploaded!", existingId = existingConsent.Id });
        }

        // 📁 Save ZIP
        var safeVin = vehicleVIN.ToUpper().Trim();
        var companyBasePath = Path.Combine(env.WebRootPath ?? "wwwroot", "companies", $"company-{clientCompanyId}");
        var consentsDir = Path.Combine(companyBasePath, "consents-zip");
        Directory.CreateDirectory(consentsDir);

        var zipFilename = $"manual_upload_{safeVin}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
        var finalZipPath = Path.Combine(consentsDir, zipFilename);

        await using (var fileStream = new FileStream(finalZipPath, FileMode.Create))
        {
            await memoryStream.CopyToAsync(fileStream);
        }

        if (!DateTime.TryParseExact(uploadDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            await _logger.Warning("UploadConsentZipController", "Invalid upload date format.", uploadDate);
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid signature date format. Expected server-side format: 'yyyy-MM-dd'!");
        }

        var consent = new ClientConsent
        {
            ClientCompanyId = clientCompanyId,
            VehicleId = vehicleId,
            UploadDate = parsedDate,
            ZipFilePath = Path.Combine("companies", $"company-{clientCompanyId}", "consents-zip", zipFilename).Replace("\\", "/"),
            ConsentHash = hash,
            ConsentType = consentType,
            Notes = ""
        };

        db.ClientConsents.Add(consent);
        await db.SaveChangesAsync();

        await _logger.Info("UploadConsentZipController", "Consent ZIP successfully uploaded and registered.", $"ConsentId: {consent.Id}, Hash: {hash}");

        return Ok(new
        {
            id = consent.Id,
            consentType,
            uploadDate = parsedDate.ToString("dd/MM/yyyy"),
            zipFilePath = consent.ZipFilePath,
            consentHash = hash,
            companyVatNumber,
            vehicleVIN
        });
    }
}