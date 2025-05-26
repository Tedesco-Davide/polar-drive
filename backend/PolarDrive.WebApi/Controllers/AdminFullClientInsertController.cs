using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Entities;
using System.IO.Compression;
using System.Security.Cryptography;

namespace PolarDrive.WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminFullClientInsertController(PolarDriveDbContext dbContext, IWebHostEnvironment env) : ControllerBase
{
    private readonly PolarDriveDbContext _dbContext = dbContext;
    private readonly IWebHostEnvironment _env = env;

    [HttpPost]
    [RequestSizeLimit(100_000_000)] // ZIP fino a 100MB
    public async Task<IActionResult> Post([FromForm] AdminFullClientInsertRequest request)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            // ─────────────────────
            // 1. Azienda Cliente - Riusa se già esiste per VatNumber
            // ─────────────────────
            var company = await _dbContext.ClientCompanies
                .FirstOrDefaultAsync(c => c.VatNumber == request.CompanyVatNumber);

            if (company == null)
            {
                company = new ClientCompany
                {
                    Name = request.CompanyName,
                    VatNumber = request.CompanyVatNumber,
                    ReferentName = request.ReferentName,
                    ReferentEmail = request.ReferentEmail,
                    ReferentMobileNumber = request.ReferentMobile,
                };
                _dbContext.ClientCompanies.Add(company);
                await _dbContext.SaveChangesAsync();
            }

            // Check: se esiste già una veicolo con quel VIN
            var existingVehicle = await _dbContext.ClientVehicles
                .FirstOrDefaultAsync(v => v.Vin == request.VehicleVIN);

            if (existingVehicle != null)
            {
                // È già associata a un'altra azienda?
                if (existingVehicle.ClientCompanyId != company.Id)
                {
                    return BadRequest("SERVER ERROR → BAD REQUEST: This vehicle VIN is already registered and assigned to another company!");
                }

                // Se invece è già associata alla stessa azienda, blocca per evitare duplicato
                return BadRequest("SERVER ERROR → BAD REQUEST: This vehicle VIN is already associated with this company!");
            }

            // ─────────────────────
            // 2. Veicolo
            // ─────────────────────
            var vehicle = new ClientVehicle
            {
                ClientCompanyId = company.Id,
                Vin = request.VehicleVIN,
                FuelType = request.VehicleFuelType,
                Brand = request.VehicleBrand,
                Model = request.VehicleModel,
                IsActiveFlag = true,
                IsFetchingDataFlag = true,
                FirstActivationAt = request.UploadDate
            };
            _dbContext.ClientVehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            // 2b. Salva token associato
            var token = new ClientToken
            {
                VehicleId = vehicle.Id,
                AccessToken = request.AccessToken,
                RefreshToken = request.RefreshToken,
                AccessTokenExpiresAt = DateTime.UtcNow.AddHours(8),
                RefreshTokenExpiresAt = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.ClientTokens.Add(token);
            await _dbContext.SaveChangesAsync();

            // ─────────────────────
            // 3. Estrazione ZIP e salvataggio consenso
            // ─────────────────────
            if (request.ConsentZip == null || !request.ConsentZip.FileName.EndsWith(".zip"))
                return BadRequest("SERVER ERROR → BAD REQUEST: The file you are trying to upload must be a .zip file!");

            using var zip = new ZipArchive(request.ConsentZip.OpenReadStream());
            var pdfEntry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
            if (pdfEntry == null)
                return BadRequest("SERVER ERROR → BAD REQUEST: The .zip you are trying to upload must contain at least one .pdf file!");

            // Percorso base della company
            var companyBasePath = Path.Combine(_env.WebRootPath, "companies", $"company-{company.Id}");

            // Crea la cartella base company-{id} solo se non esiste
            if (!Directory.Exists(companyBasePath))
            {
                Directory.CreateDirectory(companyBasePath);
            }

            // Percorsi sottocartelle
            var consentsDir = Path.Combine(companyBasePath, "consents-zip");
            var historyDir = Path.Combine(companyBasePath, "history-pdf");
            var reportsDir = Path.Combine(companyBasePath, "reports-pdf");

            // Crea le sottocartelle se mancano (CreateDirectory è idempotente: non lancia errore se già esiste)
            Directory.CreateDirectory(consentsDir);
            Directory.CreateDirectory(historyDir);
            Directory.CreateDirectory(reportsDir);

            // 2. Salva lo ZIP del consenso in consents-zip/
            var zipFilename = Path.GetFileNameWithoutExtension(request.ConsentZip.FileName);
            var uniqueName = $"{zipFilename}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
            var zipPath = Path.Combine(consentsDir, uniqueName);
            using (var fs = new FileStream(zipPath, FileMode.Create))
            {
                await request.ConsentZip.CopyToAsync(fs);
            }

            // Calcolo SHA-256 hash del PDF estratto
            string hash;
            using (var stream = pdfEntry.Open())
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                var hashBytes = SHA256.HashData(ms.ToArray());
                hash = Convert.ToHexStringLower(hashBytes);
            }

            // ✅ Check se già esiste un consenso identico
            var existingConsent = await _dbContext.ClientConsents
                .FirstOrDefaultAsync(c => c.ConsentHash == hash);

            if (existingConsent != null)
            {
                return BadRequest("SERVER ERROR → BAD REQUEST: This file has an existing and validated Hash, therefore has already been uploaded!");
            }

            // ─────────────────────
            // 4. Salva consenso
            // ─────────────────────
            var consent = new ClientConsent
            {
                ClientCompanyId = company.Id,
                VehicleId = vehicle.Id,
                ConsentType = "Consent Activation",
                UploadDate = request.UploadDate,
                ZipFilePath = zipPath,
                ConsentHash = hash,
                Notes = ""
            };
            _dbContext.ClientConsents.Add(consent);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok("SERVER MESSAGE: Workflow executed successfully!");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, $"SERVER ERROR → STATUS CODE: Error while executing the Workflow! {ex.Message}");
        }
    }
}