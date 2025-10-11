using System.Text.Json;
using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Entities;
using System.Text;
using PolarDrive.WebApi.Helpers;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientConsentsController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly PolarDriveLogger _logger;

    public ClientConsentsController(PolarDriveDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
        _logger = new PolarDriveLogger(db);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClientConsentDTO>>> Get()
    {
        await _logger.Info("ClientConsentsController.Get", "Requested list of client consents.");

        var items = await _db.ClientConsents
            .Include(c => c.ClientCompany)
            .Include(c => c.ClientVehicle)
            .OrderByDescending(c => c.UploadDate)
            .Select(c => new ClientConsentDTO
            {
                Id = c.Id,
                ClientCompanyId = c.ClientCompanyId,
                CompanyVatNumber = c.ClientCompany!.VatNumber,
                VehicleId = c.VehicleId,
                VehicleVIN = c.ClientVehicle!.Vin,
                UploadDate = c.UploadDate.ToString("o"),
                ZipFilePath = c.ZipFilePath,
                ConsentHash = c.ConsentHash,
                ConsentType = c.ConsentType,
                Notes = c.Notes,
                // Campo calcolato per presenza ZIP (allineato agli outages)
                HasZipFile = !string.IsNullOrWhiteSpace(c.ZipFilePath)
            }).ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// Crea un nuovo consent manualmente (allineato agli outages)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CreateConsentRequest request)
    {
        try
        {
            await _logger.Info("ClientConsentsController.Post", "Creating new consent manually",
                JsonSerializer.Serialize(request));

            // Validazione company
            var company = await _db.ClientCompanies.FindAsync(request.ClientCompanyId);
            if (company == null)
            {
                return NotFound("Company not found");
            }

            // Validazione vehicle
            var vehicle = await _db.ClientVehicles
                .FirstOrDefaultAsync(v => v.Id == request.VehicleId && v.ClientCompanyId == request.ClientCompanyId);
            if (vehicle == null)
            {
                return NotFound("Vehicle not found or not associated with company");
            }

            // Validazione consent type
            var validConsentTypes = new[] {
                "Consent Activation",
                "Consent Deactivation",
                "Consent Stop Data Fetching",
                "Consent Reactivation"
            };
            if (!validConsentTypes.Contains(request.ConsentType))
            {
                return BadRequest($"Invalid consent type. Valid types: {string.Join(", ", validConsentTypes)}");
            }

            var consent = new ClientConsent
            {
                ClientCompanyId = request.ClientCompanyId,
                VehicleId = request.VehicleId,
                UploadDate = DateTime.Now,
                ConsentType = request.ConsentType,
                Notes = request.Notes ?? "Manually inserted",
                ConsentHash = "", // Verrà aggiornato quando si carica il ZIP
                ZipFilePath = "" // Verrà aggiornato quando si carica il ZIP
            };

            _db.ClientConsents.Add(consent);
            await _db.SaveChangesAsync();

            await _logger.Info("ClientConsentsController.Post",
                $"Created new manual consent with ID {consent.Id}");

            return CreatedAtAction(nameof(Get), new { id = consent.Id }, new { id = consent.Id });
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.Post", "Error creating consent", ex.ToString());
            return StatusCode(500, "Internal server error while creating consent");
        }
    }

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        try
        {
            var entity = await _db.ClientConsents.FindAsync(id);
            if (entity == null)
            {
                return NotFound("Consent not found");
            }

            if (!body.TryGetProperty("notes", out var notesProp))
            {
                return BadRequest("Missing 'notes' field");
            }

            entity.Notes = notesProp.GetString() ?? string.Empty;
            await _db.SaveChangesAsync();

            await _logger.Info("ClientConsentsController.PatchNotes",
                $"Updated notes for consent {id}");

            return NoContent();
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.PatchNotes",
                $"Error updating notes for consent {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Upload di un file ZIP per un consent (allineato agli outages)
    /// </summary>
    [HttpPost("{id}/upload-zip")]
    public async Task<IActionResult> UploadZip(int id, IFormFile zipFile)
    {
        try
        {
            var consent = await _db.ClientConsents
                .Include(c => c.ClientVehicle)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (consent == null)
            {
                return NotFound("Consent not found");
            }

            if (zipFile == null || zipFile.Length == 0)
            {
                return BadRequest("No file provided");
            }

            // ✅ Passa il ClientCompanyId del consent al metodo ProcessZipFileAsync
            var (zipFilePath, hash) = await ProcessZipFileAsync(zipFile, consent.ClientCompanyId, $"consent_{id}");
            if (zipFilePath == null)
            {
                return BadRequest("Invalid ZIP file");
            }

            // Elimina il file precedente se esiste
            if (!string.IsNullOrWhiteSpace(consent.ZipFilePath) && System.IO.File.Exists(consent.ZipFilePath))
            {
                try
                {
                    System.IO.File.Delete(consent.ZipFilePath);
                    await _logger.Info("ClientConsentsController.UploadZip", "Old ZIP file deleted.", consent.ZipFilePath);
                }
                catch (Exception ex)
                {
                    await _logger.Warning("ClientConsentsController.UploadZip", "Failed to delete old ZIP file.",
                        $"Path: {consent.ZipFilePath}, Error: {ex.Message}");
                }
            }

            // Verifica hash duplicato (escludendo il consent corrente)
            var existingWithHash = await _db.ClientConsents
                .FirstOrDefaultAsync(c => c.ConsentHash == hash && c.Id != id);

            if (existingWithHash != null)
            {
                // Elimina il nuovo file caricato
                if (System.IO.File.Exists(zipFilePath))
                {
                    System.IO.File.Delete(zipFilePath);
                }
                return Conflict(new
                {
                    message = "This file has already been uploaded for another consent!",
                    existingId = existingWithHash.Id
                });
            }

            // Aggiorna il database
            consent.ZipFilePath = zipFilePath;
            consent.ConsentHash = hash;
            await _db.SaveChangesAsync();

            await _logger.Info("ClientConsentsController.UploadZip",
                $"Uploaded ZIP file for consent {id}: {Path.GetFileName(zipFilePath)}");

            return Ok(new { zipFilePath = zipFilePath, consentHash = hash });
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.UploadZip",
                $"Error uploading ZIP for consent {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///  Download di un file ZIP (allineato agli outages)
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadZip(int id)
    {
        try
        {
            var consent = await _db.ClientConsents.FindAsync(id);
            if (consent == null)
            {
                return NotFound("Consent not found");
            }

            if (string.IsNullOrWhiteSpace(consent.ZipFilePath))
            {
                return NotFound("No ZIP file associated with this consent");
            }

            //  usa il path completo direttamente
            if (!System.IO.File.Exists(consent.ZipFilePath))
            {
                return NotFound("ZIP file not found on server");
            }

            var fileName = Path.GetFileName(consent.ZipFilePath);
            var fileBytes = await System.IO.File.ReadAllBytesAsync(consent.ZipFilePath);

            await _logger.Info("ClientConsentsController.DownloadZip",
                $"Downloaded ZIP file for consent {id}: {fileName}");

            return File(fileBytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.DownloadZip",
                $"Error downloading ZIP for consent {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Elimina il file ZIP di un consent (allineato agli outages)
    /// </summary>
    [HttpDelete("{id}/delete-zip")]
    public async Task<IActionResult> DeleteZip(int id)
    {
        try
        {
            var consent = await _db.ClientConsents.FindAsync(id);
            if (consent == null)
            {
                return NotFound("Consent not found");
            }

            if (string.IsNullOrWhiteSpace(consent.ZipFilePath))
            {
                return BadRequest("No ZIP file associated with this consent");
            }

            //  usa il path completo direttamente
            if (System.IO.File.Exists(consent.ZipFilePath))
            {
                try
                {
                    System.IO.File.Delete(consent.ZipFilePath);
                    await _logger.Info("ClientConsentsController.DeleteZip", "ZIP file deleted from filesystem.", consent.ZipFilePath);
                }
                catch (Exception ex)
                {
                    await _logger.Warning("ClientConsentsController.DeleteZip", "Failed to delete ZIP file from filesystem.",
                        $"Path: {consent.ZipFilePath}, Error: {ex.Message}");
                }
            }

            // Rimuovi il riferimento dal database
            consent.ZipFilePath = "";
            consent.ConsentHash = "";
            await _db.SaveChangesAsync();

            await _logger.Info("ClientConsentsController.DeleteZip", "ZIP file reference removed from database.",
                $"ConsentId: {id}");

            return Ok(new { message = "ZIP file deleted successfully" });
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.DeleteZip",
                $"Error deleting ZIP for consent {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("resolve-ids")]
    public async Task<IActionResult> ResolveIds([FromQuery] string vatNumber, [FromQuery] string vin)
    {
        var company = await _db.ClientCompanies.FirstOrDefaultAsync(c => c.VatNumber == vatNumber);
        if (company == null)
        {
            await _logger.Warning("ClientConsentsController.ResolveIds", "Company not found.", $"VAT: {vatNumber}");
            return NotFound("Company not found");
        }

        var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Vin == vin);
        if (vehicle == null)
        {
            await _logger.Warning("ClientConsentsController.ResolveIds", "Vehicle not found.", $"VIN: {vin}");
            return NotFound("Vehicle not found");
        }

        if (vehicle.ClientCompanyId != company.Id)
        {
            await _logger.Warning("ClientConsentsController.ResolveIds", "Vehicle not associated to this company.", $"ClientCompanyId: {company.Id}, VehicleId: {vehicle.Id}");
            return BadRequest("This vehicle does not belong to the company");
        }

        await _logger.Info("ClientConsentsController.ResolveIds", "Company and vehicle IDs resolved successfully.", $"ClientCompanyId: {company.Id}, VehicleId: {vehicle.Id}");

        return Ok(new
        {
            clientCompanyId = company.Id,
            vehicleId = vehicle.Id,
            vehicleBrand = vehicle.Brand
        });
    }

    #region Private Methods

    /// <summary>
    ///  ProcessZipFileAsync ora accetta qualsiasi contenuto (allineato agli outages)
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
        using (var reader = new StreamReader(
            zipStream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true))
        {
            string zipContent = await reader.ReadToEndAsync();
            hash = GenericHelpers.ComputeContentHash(zipContent);
        }

        zipStream.Position = 0;

        // ✅ Ottieni il percorso specifico della company
        var companyConsentsPath = GetCompanyConsentsPath(companyId);

        // ✅ Crea la directory se non esiste (già gestito in GetCompanyConsentsPath se hai messo Directory.CreateDirectory lì)
        if (!Directory.Exists(companyConsentsPath))
        {
            Directory.CreateDirectory(companyConsentsPath);
        }

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

    /// <summary>
    /// Download di tutti i consensi per un'azienda specifica in un unico ZIP
    /// </summary>
    [HttpGet("download-all-by-company")]
    public async Task<IActionResult> DownloadAllConsentsByCompany([FromQuery] string vatNumber)
    {
        try
        {
            await _logger.Info("ClientConsentsController.DownloadAllConsentsByCompany",
                "Started download all consents for company", $"VAT: {vatNumber}");

            // Trova l'azienda
            var company = await _db.ClientCompanies.FirstOrDefaultAsync(c => c.VatNumber == vatNumber);
            if (company == null)
            {
                await _logger.Warning("ClientConsentsController.DownloadAllConsentsByCompany",
                    "Company not found", $"VAT: {vatNumber}");
                return NotFound(new
                {
                    success = false,
                    message = "Azienda non trovata",
                    errorCode = "COMPANY_NOT_FOUND"
                });
            }

            // Trova tutti i consensi dell'azienda
            var allConsents = await _db.ClientConsents
                .Include(c => c.ClientVehicle)
                .Where(c => c.ClientCompanyId == company.Id)
                .OrderBy(c => c.UploadDate)
                .ToListAsync();

            // Separare consensi con e senza file ZIP
            var consentsWithZip = allConsents.Where(c => !string.IsNullOrWhiteSpace(c.ZipFilePath)).ToList();
            var consentsWithoutZip = allConsents.Where(c => string.IsNullOrWhiteSpace(c.ZipFilePath)).ToList();

            await _logger.Info("ClientConsentsController.DownloadAllConsentsByCompany",
                "Consents analysis",
                $"Total: {allConsents.Count}, WithZip: {consentsWithZip.Count}, WithoutZip: {consentsWithoutZip.Count}");

            // Se non ci sono consensi affatto
            if (!allConsents.Any())
            {
                await _logger.Info("ClientConsentsController.DownloadAllConsentsByCompany",
                    "No consents found for company", $"ClientCompanyId: {company.Id}");
                return Ok(new
                {
                    success = true,
                    hasData = false,
                    message = "Nessun consenso trovato per questa azienda",
                    totalConsents = 0,
                    availableForDownload = 0
                });
            }

            // Se non ci sono consensi con file ZIP
            if (!consentsWithZip.Any())
            {
                await _logger.Info("ClientConsentsController.DownloadAllConsentsByCompany",
                    "No consents with ZIP files found for company",
                    $"ClientCompanyId: {company.Id}, TotalConsents: {allConsents.Count}");
                return Ok(new
                {
                    success = true,
                    hasData = false,
                    message = "Nessun file di consenso disponibile per il download",
                    totalConsents = allConsents.Count,
                    availableForDownload = 0,
                    note = "I consensi esistono ma non hanno file allegati"
                });
            }

            // Crea un ZIP temporaneo contenente tutti i consensi
            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                var processedCount = 0;
                var skippedCount = 0;
                var errors = new List<string>();

                foreach (var consent in consentsWithZip)
                {
                    if (string.IsNullOrWhiteSpace(consent.ZipFilePath))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (!System.IO.File.Exists(consent.ZipFilePath))
                    {
                        await _logger.Warning("ClientConsentsController.DownloadAllConsentsByCompany",
                            "Consent ZIP file not found on disk",
                            $"ConsentId: {consent.Id}, Path: {consent.ZipFilePath}");
                        skippedCount++;
                        errors.Add($"File non trovato per consenso ID {consent.Id}");
                        continue;
                    }

                    try
                    {
                        // Leggi il file ZIP del consenso
                        var consentZipBytes = await System.IO.File.ReadAllBytesAsync(consent.ZipFilePath);

                        // Crea un nome file descrittivo per il consenso
                        var consentFileName = GenerateConsentFileName(consent);

                        // Aggiungi il file ZIP del consenso al ZIP principale
                        var entry = archive.CreateEntry(consentFileName);
                        using var entryStream = entry.Open();
                        await entryStream.WriteAsync(consentZipBytes);

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        await _logger.Error("ClientConsentsController.DownloadAllConsentsByCompany",
                            $"Error processing consent {consent.Id}", ex.ToString());
                        skippedCount++;
                        errors.Add($"Errore nel processare consenso ID {consent.Id}: {ex.Message}");
                    }
                }

                // Log del risultato del processamento
                await _logger.Info("ClientConsentsController.DownloadAllConsentsByCompany",
                    "Processing completed",
                    $"Processed: {processedCount}, Skipped: {skippedCount}, Errors: {errors.Count}");

                // Se nessun file è stato processato con successo
                if (processedCount == 0)
                {
                    await _logger.Warning("ClientConsentsController.DownloadAllConsentsByCompany",
                        "No valid consent files could be processed",
                        $"ClientCompanyId: {company.Id}, TotalAttempted: {consentsWithZip.Count}");

                    return Ok(new
                    {
                        success = true,
                        hasData = false,
                        message = "Nessun file di consenso valido trovato per il download",
                        totalConsents = allConsents.Count,
                        availableForDownload = 0,
                        attempted = consentsWithZip.Count,
                        errors = errors.Take(5).ToList(), // Limita gli errori mostrati
                        note = errors.Count > 5 ? $"e altri {errors.Count - 5} errori..." : null
                    });
                }

                // Se alcuni file sono stati processati ma altri hanno avuto errori
                if (skippedCount > 0)
                {
                    await _logger.Warning("ClientConsentsController.DownloadAllConsentsByCompany",
                        "Some consent files were skipped",
                        $"Processed: {processedCount}, Skipped: {skippedCount}");
                }
            }

            // Genera il nome del file ZIP finale
            var fileName = GenerateArchiveFileName(company);

            await _logger.Info("ClientConsentsController.DownloadAllConsentsByCompany",
                "All consents ZIP created successfully",
                $"Company: {company.Name}, FileName: {fileName}, Size: {zipStream.Length} bytes");

            // Ritorna il file ZIP
            zipStream.Position = 0;
            return File(zipStream.ToArray(), "application/zip", fileName);
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientConsentsController.DownloadAllConsentsByCompany",
                $"Error downloading all consents for company with VAT {vatNumber}", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                message = "Errore interno del server durante la creazione dell'archivio",
                errorCode = "INTERNAL_SERVER_ERROR"
            });
        }
    }

    /// <summary>
    /// Genera un nome file descrittivo per il consenso
    /// </summary>
    private static string GenerateConsentFileName(ClientConsent consent)
    {
        var consentType = consent.ConsentType?.Replace(" ", "_") ?? "unknown";
        var uploadDate = consent.UploadDate.ToString("yyyyMMdd");
        var vin = consent.ClientVehicle?.Vin ?? "unknown";
        var consentId = consent.Id;

        return $"{consentType}_{uploadDate}_{vin}_{consentId}.zip";
    }

    /// <summary>
    /// Genera il nome del file ZIP finale
    /// </summary>
    private static string GenerateArchiveFileName(ClientCompany company)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var sanitizedCompanyName = System.Text.RegularExpressions.Regex.Replace(
            company.Name, @"[^a-zA-Z0-9]", "_");

        return $"consensi_{sanitizedCompanyName}_{company.VatNumber}_{timestamp}.zip";
    }
}

/// <summary>
/// Request per creare un nuovo consent
/// </summary>
public class CreateConsentRequest
{
    public int ClientCompanyId { get; set; }
    public int VehicleId { get; set; }
    public string ConsentType { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }
    public string? Notes { get; set; }
}