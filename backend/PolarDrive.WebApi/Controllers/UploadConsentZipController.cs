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
    [HttpPost]
    public async Task<IActionResult> UploadConsent(
        [FromForm] int clientCompanyId,
        [FromForm] int teslaVehicleId,
        [FromForm] string consentType,
        [FromForm] string uploadDate,
        [FromForm] string companyVatNumber,
        [FromForm] string teslaVehicleVIN,
        [FromForm] IFormFile zipFile
    )
    {
        if (zipFile == null || zipFile.Length == 0)
            return BadRequest("SERVER ERROR → BAD REQUEST: File .zip missing!");

        if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return BadRequest("SERVER ERROR → BAD REQUEST: File type not valid. It must be a .zip!");

        // ✅ Validazione tipo consenso
        var allowedTypes = new[] {
            "Consent Activation",
            "Consent Deactivation",
            "Consent Stop Data Fetching",
            "Consent Reactivation"
        };

        if (string.IsNullOrWhiteSpace(consentType) || !allowedTypes.Contains(consentType))
            return BadRequest("SERVER ERROR → BAD REQUEST: Consent Type not valid!");

        // 🔄 Carica tutto il file ZIP in memoria
        using var memoryStream = new MemoryStream();
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
                return BadRequest("SERVER ERROR → BAD REQUEST: The .zip file must contain at least a single .pdf file!");
        }
        catch (InvalidDataException)
        {
            return BadRequest("SERVER ERROR → BAD REQUEST: The .zip file is either damaged/broken or not valid!");
        }

        // 🔍 Validazione logica di coerenza
        var company = await db.ClientCompanies.FirstOrDefaultAsync(c => c.Id == clientCompanyId && c.VatNumber == companyVatNumber);
        if (company == null)
            return NotFound("SERVER ERROR → NOT FOUND: Company not found or invalid VAT number!");

        var vehicle = await db.ClientTeslaVehicles.FirstOrDefaultAsync(v => v.Id == teslaVehicleId && v.Vin == teslaVehicleVIN && v.ClientCompanyId == clientCompanyId);
        if (vehicle == null)
            return NotFound("SERVER ERROR → NOT FOUND: Tesla vehicle not found or not associated with the company!");

        // 🔐 SHA256
        string hash;
        
        // Calcola SHA256 direttamente dal memoryStream già pieno
        memoryStream.Position = 0;
        using var sha = SHA256.Create();
        var hashBytes = await sha.ComputeHashAsync(memoryStream);
        hash = Convert.ToHexStringLower(hashBytes);

        // Reset posizione per salvataggio file
        memoryStream.Position = 0;

        var existingConsent = await db.ClientConsents.FirstOrDefaultAsync(c => c.ConsentHash == hash);
        if (existingConsent != null)
        {
            return Conflict(new { message = "CONFLICT - SERVER ERROR: This file has an existing and validated Hash, therefore has already been uploaded!", existingId = existingConsent.Id });
        }

        // 📁 Salva ZIP
        var safeVin = teslaVehicleVIN.ToUpper().Trim();
        var companyBasePath = Path.Combine(env.WebRootPath ?? "wwwroot", "companies", $"company-{clientCompanyId}");
        var consentsDir = Path.Combine(companyBasePath, "consents-zip");
        Directory.CreateDirectory(consentsDir);

        // Evita sovrascrittura con timestamp
        var zipFilename = $"manual_upload_{safeVin}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
        var finalZipPath = Path.Combine(consentsDir, zipFilename);

        await using (var fileStream = new FileStream(finalZipPath, FileMode.Create))
        {
            await memoryStream.CopyToAsync(fileStream);
        }

        if (!DateTime.TryParseExact(uploadDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            return BadRequest("SERVER ERROR → BAD REQUEST: Invalid signature date format. Expected server-side format: 'yyyy-MM-dd'!");

        var consent = new ClientConsent
        {
            ClientCompanyId = clientCompanyId,
            TeslaVehicleId = teslaVehicleId,
            UploadDate = parsedDate,
            ZipFilePath = Path.Combine("companies", $"company-{clientCompanyId}", "consents-zip", zipFilename).Replace("\\", "/"),
            ConsentHash = hash,
            ConsentType = consentType,
            Notes = ""
        };

        db.ClientConsents.Add(consent);
        await db.SaveChangesAsync();

        return Ok(new
        {
            id = consent.Id,
            consentType,
            uploadDate = parsedDate.ToString("dd/MM/yyyy"),
            zipFilePath = consent.ZipFilePath,
            consentHash = hash,
            companyVatNumber,
            teslaVehicleVIN
        });
    }
}