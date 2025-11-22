using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Globalization;
using System.Security.Cryptography;
using PolarDrive.WebApi.Helpers;

namespace PolarDrive.WebApi.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[ApiController]
[Route("api/[controller]")]
public class UploadConsentZipController(PolarDriveDbContext db) : ControllerBase
{
    private readonly PolarDriveDbContext _db = db;
    private readonly PolarDriveLogger _logger = new();

    /// <summary>
    ///  Upload consent con nuova gestione storage
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UploadConsent(
        int clientCompanyId,
        int vehicleId,
        string consentType,
        string uploadDate,
        string companyVatNumber,
        string vehicleVIN,
        IFormFile zipFile
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

        // Company & Vehicle validation
        var company = await _db.ClientCompanies.FirstOrDefaultAsync(c => c.Id == clientCompanyId && c.VatNumber == companyVatNumber);
        if (company == null)
        {
            await _logger.Warning("UploadConsentZipController", "Company not found or VAT mismatch.", $"ClientCompanyId: {clientCompanyId}, VAT: {companyVatNumber}");
            return NotFound("SERVER ERROR → NOT FOUND: Company not found or invalid VAT number!");
        }

        var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && v.Vin == vehicleVIN && v.ClientCompanyId == clientCompanyId);
        if (vehicle == null)
        {
            await _logger.Warning("UploadConsentZipController", "Vehicle not found or mismatch.", $"VehicleId: {vehicleId}, VIN: {vehicleVIN}");
            return NotFound("SERVER ERROR → NOT FOUND: Vehicle not found or not associated with the company!");
        }

        if (!DateTime.TryParseExact(uploadDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            await _logger.Warning("UploadConsentZipController", "Invalid upload date format.", uploadDate);
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid signature date format. Expected server-side format: 'yyyy-MM-dd'!");
        }

        // === Read ZIP into memory & validate
        await using var ms = new MemoryStream();
        await zipFile.CopyToAsync(ms);

        ms.Position = 0;
        try
        {
            using var _ = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch
        {
            await _logger.Warning("UploadConsentZipController", "Invalid ZIP payload.");
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid ZIP file!");
        }

        // === Hash (SHA-256) usando GenericHelpers
        ms.Position = 0;
        string hash = GenericHelpers.ComputeContentHash(ms);

        // Duplicate check
        var existingConsent = await _db.ClientConsents.FirstOrDefaultAsync(c => c.ConsentHash == hash);
        if (existingConsent != null)
        {
            await _logger.Warning("UploadConsentZipController", "Duplicate consent hash detected.", $"ExistingId: {existingConsent.Id}, Hash: {hash}");
            return Conflict(new { message = "CONFLICT - SERVER ERROR: This file has an existing and validated Hash, therefore has already been uploaded!", existingId = existingConsent.Id });
        }

        // Save to DB (BLOB)
        ms.Position = 0;
        var consent = new ClientConsent
        {
            ClientCompanyId = clientCompanyId,
            VehicleId = vehicleId,
            UploadDate = DateTime.Now,
            ZipContent = ms.ToArray(),
            ConsentHash = hash,
            ConsentType = consentType,
            Notes = ""
        };

        _db.ClientConsents.Add(consent);
        await _db.SaveChangesAsync();

        await _logger.Info("UploadConsentZipController", "Consent ZIP successfully uploaded and registered.", $"ConsentId: {consent.Id}, Hash: {hash}");

        return Ok(new
        {
            id = consent.Id,
            consentType,
            uploadDate = parsedDate.ToString("dd/MM/yyyy"),
            consentHash = hash,
            companyVatNumber,
            vehicleVIN
        });
    }

    /// <summary>
    /// Upload ZIP a consent esistente
    /// </summary>
    [HttpPost("{consentId}/upload-zip")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadZipToExistingConsent(int consentId, IFormFile zipFile, [FromQuery] bool replaceExisting = false)
    {
        var consent = await _db.ClientConsents.FirstOrDefaultAsync(c => c.Id == consentId);
        if (consent == null)
        {
            await _logger.Warning("UploadZipToExistingConsent", "Consent not found.", $"ConsentId: {consentId}");
            return NotFound("SERVER ERROR → NOT FOUND: Consent not found!");
        }

        // Se c'è già un BLOB e non è autorizzata la sostituzione
        if (consent.ZipContent != null && consent.ZipContent.Length > 0 && !replaceExisting)
        {
            await _logger.Warning("UploadZipToExistingConsent", "Consent already has ZIP, replacement not authorized.",
                $"ConsentId: {consentId}");
            return Conflict("SERVER ERROR → CONFLICT: Consent already has a ZIP file. Use replaceExisting=true to replace it.");
        }

        if (zipFile == null || zipFile.Length == 0)
        {
            return BadRequest("SERVER ERROR → BAD REQUEST: No ZIP file provided!");
        }

        if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await _logger.Warning("UploadZipToExistingConsent", "Invalid file extension.", zipFile.FileName);
            return BadRequest("SERVER ERROR → BAD REQUEST: File type not valid. It must be a .zip!");
        }

        // Leggi & valida ZIP
        await using var ms = new MemoryStream();
        await zipFile.CopyToAsync(ms);

        ms.Position = 0;
        try
        {
            using var _ = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch
        {
            await _logger.Warning("UploadZipToExistingConsent", "Invalid ZIP payload.");
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid ZIP file!");
        }

        // Hash & dup check
        ms.Position = 0;
        string hash = GenericHelpers.ComputeContentHash(ms);

        var existingWithHash = await _db.ClientConsents
            .FirstOrDefaultAsync(c => c.ConsentHash == hash && c.Id != consentId);

        if (existingWithHash != null)
        {
            return Conflict(new
            {
                message = "CONFLICT - This file has already been uploaded for another consent!",
                existingId = existingWithHash.Id
            });
        }

        // Aggiorna il BLOB
        ms.Position = 0;
        consent.ZipContent = ms.ToArray();
        consent.ConsentHash = hash;
        await _db.SaveChangesAsync();

        await _logger.Info("UploadZipToExistingConsent", "ZIP file uploaded successfully.",
            $"ConsentId: {consentId}, Replaced: {replaceExisting}");

        return Ok(new
        {
            id = consent.Id,
            consentHash = hash,
            replaced = replaceExisting,
            message = replaceExisting ? "ZIP file replaced successfully" : "ZIP file uploaded successfully"
        });
    }
}