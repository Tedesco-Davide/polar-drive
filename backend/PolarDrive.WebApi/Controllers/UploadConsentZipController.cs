using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Security.Cryptography;
using System.Globalization;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/upload-consent-zip")]
public class UploadConsentZipController(PolarDriveDbContext db, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
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

        Console.WriteLine($"[UPLOAD ZIP] Start upload");
        Console.WriteLine($"ClientCompanyId: {clientCompanyId}");
        Console.WriteLine($"TeslaVehicleId: {teslaVehicleId}");
        Console.WriteLine($"ConsentType: {consentType}");
        Console.WriteLine($"UploadDate: {uploadDate}");
        Console.WriteLine($"CompanyVatNumber: {companyVatNumber}");
        Console.WriteLine($"TeslaVehicleVIN: {teslaVehicleVIN}");
        Console.WriteLine($"ZIP File: {zipFile?.FileName} | Size: {zipFile?.Length}");

        if (zipFile == null || zipFile.Length == 0)
            return BadRequest("File ZIP mancante.");

        if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Formato non valido. Serve un file .zip");

        using (var archive = new ZipArchive(zipFile.OpenReadStream()))
        {
            bool containsPdf = archive.Entries.Any(entry =>
                Path.GetExtension(entry.FullName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                && !entry.FullName.EndsWith("/")
            );

            if (!containsPdf)
                return BadRequest("Il file ZIP deve contenere almeno un file PDF.");
        }

        // 🔍 Validazione logica di coerenza
        var company = await db.ClientCompanies.FirstOrDefaultAsync(c => c.Id == clientCompanyId && c.VatNumber == companyVatNumber);
        if (company == null)
            return NotFound("Azienda non trovata o P.IVA errata.");

        var vehicle = await db.ClientTeslaVehicles.FirstOrDefaultAsync(v => v.Id == teslaVehicleId && v.Vin == teslaVehicleVIN && v.ClientCompanyId == clientCompanyId);
        if (vehicle == null)
            return NotFound("Veicolo Tesla non trovato o non associato all’azienda.");

        // 🔐 SHA256
        string hash;
        // Leggi tutto il contenuto del file ZIP in memoria UNA SOLA VOLTA
        using var memoryStream = new MemoryStream();
        await zipFile.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        // Calcola SHA256
        using var sha = SHA256.Create();
        var hashBytes = await sha.ComputeHashAsync(memoryStream);
        hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        // Reset posizione per salvataggio file
        memoryStream.Position = 0;

        var existingConsent = await db.ClientConsents.FirstOrDefaultAsync(c => c.ConsentHash == hash);
        if (existingConsent != null)
        {
            return Conflict(new { message = "File già caricato.", existingId = existingConsent.Id });
        }

        // 📁 Save ZIP
        var safeVin = teslaVehicleVIN.ToUpper().Trim();
        var zipFolder = Path.Combine(env.WebRootPath ?? "wwwroot", "pdfs", "consents");
        Directory.CreateDirectory(zipFolder);
        var finalZipPath = Path.Combine(zipFolder, $"{safeVin}.zip");

        await using (var fileStream = new FileStream(finalZipPath, FileMode.Create))
        {
            await memoryStream.CopyToAsync(fileStream);
        }

        if (!DateTime.TryParseExact(uploadDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            return BadRequest("Formato data firma non valido. Atteso 'yyyy-MM-dd'.");

        var consent = new ClientConsent
        {
            ClientCompanyId = clientCompanyId,
            TeslaVehicleId = teslaVehicleId,
            UploadDate = parsedDate,
            ZipFilePath = $"pdfs/consents/{safeVin}.zip",
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