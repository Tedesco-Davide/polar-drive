using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Globalization;
using System.Text;
using PolarDrive.WebApi.Helpers;
using System.Security.Cryptography;

namespace PolarDrive.WebApi.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[ApiController]
[Route("api/[controller]")]
public class UploadConsentZipController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly PolarDriveLogger _logger;

    public UploadConsentZipController(PolarDriveDbContext db)
    {
        _db = db;
        _logger = new PolarDriveLogger(db);
    }

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

        // === Hash (SHA-256)
        ms.Position = 0;
        using var sha = SHA256.Create();
        var hash = Convert.ToHexStringLower(await sha.ComputeHashAsync(ms));

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
    /// Upload ZIP a consent esistente (allineato agli outages)
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

        // Hash & dup check (escludendo il consenso corrente)
        ms.Position = 0;
        using var sha = SHA256.Create();
        var hash = Convert.ToHexStringLower(await sha.ComputeHashAsync(ms));

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

    #region Private Methods

    /// <summary>
    /// ProcessZipFileAsync ora accetta qualsiasi contenuto (allineato agli outages)
    /// </summary>
    private async Task<(string? zipFilePath, string hash)> ProcessZipFileAsync(IFormFile zipFile, int companyId, string? filePrefix = null)
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

        // Calcola l'hash del file ZIP
        string hash;
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        byte[] hashBytes = await sha256.ComputeHashAsync(zipStream);
        hash = Convert.ToHexStringLower(hashBytes);

        zipStream.Position = 0;

        // ✅ Usa il percorso specifico della company
        var companyConsentsPath = GetCompanyConsentsPath(companyId);

        // ✅ Genera il nome del file
        var timestamp = DateTime.Now.ToString("ddMMyyyy_HHmmss");
        var fileName = string.IsNullOrWhiteSpace(filePrefix)
            ? $"consent_{timestamp}.zip"
            : $"{filePrefix}_{timestamp}.zip"; // Aggiunto underscore per coerenza

        var finalPath = Path.Combine(companyConsentsPath, fileName);

        // ✅ Salva il file
        await using var fileStream = new FileStream(finalPath, FileMode.Create);
        await zipStream.CopyToAsync(fileStream);

        await _logger.Info("ProcessZipFileAsync", "ZIP file saved successfully.", finalPath);

        return (finalPath, hash);
    }

    /// <summary>
    /// Helper method per ottenere il percorso della company
    /// </summary>
    private string GetCompanyConsentsPath(int companyId)
    {
        var storageBasePath = Path.Combine(Directory.GetCurrentDirectory(), "storage");
        var companiesBasePath = Path.Combine(storageBasePath, "companies");
        var companyConsentsPath = Path.Combine(companiesBasePath, $"company-{companyId}", "consents-zip");
        
        // Crea la directory se non esiste
        Directory.CreateDirectory(companyConsentsPath);
        
        return companyConsentsPath;
    }

    #endregion
}