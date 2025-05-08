using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Security.Cryptography;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadConsentZipController(PolarDriveDbContext db, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)] // Max 20MB
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
        // 🔒 Validazioni base
        if (zipFile == null || zipFile.Length == 0)
            return BadRequest("File ZIP mancante.");

        if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Formato non valido. Serve un file .zip");

        // 🔍 Verifica che contenga almeno un PDF
        using (var archive = new ZipArchive(zipFile.OpenReadStream()))
        {
            bool containsPdf = archive.Entries.Any(entry =>
                Path.GetExtension(entry.FullName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                && !entry.FullName.EndsWith("/")
            );

            if (!containsPdf)
            {
                return BadRequest("Il file ZIP deve contenere almeno un file PDF.");
            }
        }

        if (!await db.ClientCompanies.AnyAsync(c => c.Id == clientCompanyId))
            return NotFound("Azienda cliente non trovata.");

        if (!await db.ClientTeslaVehicles.AnyAsync(v => v.Id == teslaVehicleId))
            return NotFound("Veicolo Tesla non trovato.");

        // 📁 Percorso di salvataggio
        var safeVin = teslaVehicleVIN.ToUpper().Trim();
        var zipFolder = Path.Combine(env.WebRootPath ?? "wwwroot", "pdfs", "consents");
        var finalZipPath = Path.Combine(zipFolder, $"{safeVin}.zip");

        Directory.CreateDirectory(zipFolder); // 🛡️ Garantisce che la cartella esista

        using (var stream = new FileStream(finalZipPath, FileMode.Create))
        {
            await zipFile.CopyToAsync(stream);
        }

        // 🔐 Calcolo SHA256 reale del file
        string hash;
        using (var sha = SHA256.Create())
        {
            using var stream = zipFile.OpenReadStream();
            var hashBytes = await sha.ComputeHashAsync(stream);
            hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        // 🚫 Evita duplicati: se già esiste un consenso con stesso hash, blocca
        var existingConsent = await db.ClientConsents
            .FirstOrDefaultAsync(c => c.ConsentHash == hash);

        if (existingConsent != null)
        {
            return Conflict(new
            {
                message = "Un file con lo stesso contenuto è già stato caricato.",
                existingId = existingConsent.Id
            });
        }

        // 🧠 Parsing data
        var parsedDate = DateTime.ParseExact(uploadDate, "dd/MM/yyyy", null);

        // 🧾 Inserimento record nel DB
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

        return Ok(new { id = consent.Id });
    }
}