using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.Data.Constants;
using System.Text.Json;
using System.IO.Compression;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OutagePeriodsController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly PolarDriveLogger _logger;

    //  usa la stessa struttura degli altri controller
    private readonly string _outageZipStoragePath;

    public OutagePeriodsController(PolarDriveDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
        _logger = new PolarDriveLogger(db);

        _outageZipStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "storage", "outages-zips");
    }

    /// <summary>
    /// Ottiene tutti gli outages con informazioni dettagliate
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> Get()
    {
        await _logger.Info("OutagePeriodsController.Get", "Requested list of outage periods");

        var outages = await _db.OutagePeriods
            .Include(o => o.ClientCompany)
            .Include(o => o.ClientVehicle)
            .OrderByDescending(o => o.OutageStart)
            .Select(o => new
            {
                o.Id,
                o.AutoDetected,
                o.OutageType,
                o.OutageBrand,
                o.CreatedAt,
                o.OutageStart,
                o.OutageEnd,
                o.Notes,
                o.ZipFilePath,
                o.VehicleId,
                o.ClientCompanyId,
                // Campi calcolati per il frontend
                Status = o.OutageEnd.HasValue ? "OUTAGE-RESOLVED" : "OUTAGE-ONGOING",
                Vin = o.ClientVehicle != null ? o.ClientVehicle.Vin : null,
                CompanyVatNumber = o.ClientCompany != null ? o.ClientCompany.VatNumber : null,
                // Durata calcolata - usa DateTime.Now per coerenza
                DurationMinutes = o.OutageEnd.HasValue
                    ? (int)(o.OutageEnd.Value - o.OutageStart).TotalMinutes
                    : (int)(DateTime.Now - o.OutageStart).TotalMinutes,
                // Informazioni di display
                HasZipFile = !string.IsNullOrWhiteSpace(o.ZipFilePath)
            })
            .ToListAsync();

        return Ok(outages);
    }

    /// <summary>
    /// Aggiunge un nuovo outage manualmente
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CreateOutageRequest request)
    {
        try
        {
            await _logger.Info("OutagePeriodsController.Post", "Creating new outage manually",
                JsonSerializer.Serialize(request));

            // Validazione tipo outage
            if (!OutageConstants.ValidOutageTypes.Contains(request.OutageType))
            {
                return BadRequest($"Invalid outage type. Valid types: {string.Join(", ", OutageConstants.ValidOutageTypes)}");
            }

            // Validazione brand
            if (!VehicleConstants.ValidBrands.Contains(request.OutageBrand))
            {
                return BadRequest($"Invalid brand. Valid brands: {string.Join(", ", VehicleConstants.ValidBrands)}");
            }

            // Converti le date in UTC se necessario (solo per confronti DB)
            var outageStartUtc = request.OutageStart.Kind == DateTimeKind.Utc
                ? request.OutageStart
                : request.OutageStart.ToUniversalTime();

            DateTime? outageEndUtc = null;
            if (request.OutageEnd.HasValue)
            {
                outageEndUtc = request.OutageEnd.Value.Kind == DateTimeKind.Utc
                    ? request.OutageEnd.Value
                    : request.OutageEnd.Value.ToUniversalTime();
            }

            // Validazioni specifiche per Outage Vehicle
            if (request.OutageType == "Outage Vehicle")
            {
                if (request.VehicleId == null || request.ClientCompanyId == null)
                {
                    return BadRequest("Vehicle ID and Company ID are required for Outage Vehicle");
                }

                var vehicle = await _db.ClientVehicles
                    .Include(v => v.ClientCompany)
                    .FirstOrDefaultAsync(v => v.Id == request.VehicleId);

                if (vehicle == null)
                {
                    return NotFound("Vehicle not found");
                }

                if (vehicle.ClientCompanyId != request.ClientCompanyId)
                {
                    return BadRequest("Vehicle does not belong to the specified company");
                }

                if (!string.Equals(vehicle.Brand, request.OutageBrand, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest($"Vehicle brand ({vehicle.Brand}) does not match outage brand ({request.OutageBrand})");
                }
            }
            else // Outage Fleet Api
            {
                // Per Fleet API, vehicle e company devono essere null
                if (request.VehicleId.HasValue || request.ClientCompanyId.HasValue)
                {
                    return BadRequest("Vehicle ID and Company ID must be null for Outage Fleet Api");
                }
            }

            // Controlla sovrapposizioni - usa le date UTC per confronti DB
            var hasOverlap = await CheckOutageOverlapAsync(
                request.OutageType,
                request.OutageBrand,
                outageStartUtc,
                outageEndUtc,
                request.VehicleId);

            if (hasOverlap)
            {
                return Conflict("An overlapping outage already exists for this period");
            }

            // Crea il nuovo outage - usa DateTime.Now
            var outage = new OutagePeriod
            {
                AutoDetected = false, // Sempre false per inserimenti manuali
                OutageType = request.OutageType,
                OutageBrand = request.OutageBrand,
                CreatedAt = DateTime.Now,
                OutageStart = request.OutageStart, // Data originale dal frontend
                OutageEnd = request.OutageEnd, // Data originale dal frontend
                VehicleId = request.VehicleId,
                ClientCompanyId = request.ClientCompanyId,
                Notes = request.Notes ?? "Manually inserted"
            };

            _db.OutagePeriods.Add(outage);
            await _db.SaveChangesAsync();

            await _logger.Info("OutagePeriodsController.Post",
                $"Created new manual outage with ID {outage.Id}");

            return CreatedAtAction(nameof(Get), new { id = outage.Id }, outage);
        }
        catch (Exception ex)
        {
            await _logger.Error("OutagePeriodsController.Post", "Error creating outage", ex.ToString());
            return StatusCode(500, "Internal server error while creating outage");
        }
    }

    /// <summary>
    /// Aggiorna le note di un outage
    /// </summary>
    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        try
        {
            var outage = await _db.OutagePeriods.FindAsync(id);
            if (outage == null)
            {
                return NotFound("Outage not found");
            }

            if (!body.TryGetProperty("notes", out var notesProp))
            {
                return BadRequest("Missing 'notes' field");
            }

            var newNotes = notesProp.GetString() ?? string.Empty;
            outage.Notes = newNotes;

            await _db.SaveChangesAsync();

            await _logger.Info("OutagePeriodsController.PatchNotes",
                $"Updated notes for outage {id}");

            return NoContent();
        }
        catch (Exception ex)
        {
            await _logger.Error("OutagePeriodsController.PatchNotes",
                $"Error updating notes for outage {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Upload di un file ZIP per un outage
    /// </summary>
    [HttpPost("{id}/upload-zip")]
    public async Task<IActionResult> UploadZip(int id, IFormFile zipFile)
    {
        try
        {
            var outage = await _db.OutagePeriods.FindAsync(id);
            if (outage == null)
            {
                return NotFound("Outage not found");
            }

            if (zipFile == null || zipFile.Length == 0)
            {
                return BadRequest("No file provided");
            }

            // ✅ Usa il metodo helper per processare il file ZIP
            var zipFilePath = await ProcessZipFileAsync(zipFile, $"outage_{id}_");
            if (zipFilePath == null)
            {
                return BadRequest("Invalid ZIP file");
            }

            // Elimina il file precedente se esiste
            if (!string.IsNullOrWhiteSpace(outage.ZipFilePath) && System.IO.File.Exists(outage.ZipFilePath))
            {
                try
                {
                    System.IO.File.Delete(outage.ZipFilePath);
                    await _logger.Info("OutagePeriodsController.UploadZip", "Old ZIP file deleted.", outage.ZipFilePath);
                }
                catch (Exception ex)
                {
                    await _logger.Warning("OutagePeriodsController.UploadZip", "Failed to delete old ZIP file.",
                        $"Path: {outage.ZipFilePath}, Error: {ex.Message}");
                }
            }

            // Aggiorna il database con il path completo
            outage.ZipFilePath = zipFilePath;
            await _db.SaveChangesAsync();

            await _logger.Info("OutagePeriodsController.UploadZip",
                $"Uploaded ZIP file for outage {id}: {Path.GetFileName(zipFilePath)}");

            return Ok(new { zipFilePath = zipFilePath });
        }
        catch (Exception ex)
        {
            await _logger.Error("OutagePeriodsController.UploadZip",
                $"Error uploading ZIP for outage {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Download di un file ZIP di un outage
    /// </summary>
    [HttpGet("{id}/download-zip")]
    public async Task<IActionResult> DownloadZip(int id)
    {
        try
        {
            var outage = await _db.OutagePeriods.FindAsync(id);
            if (outage == null)
            {
                return NotFound("Outage not found");
            }

            if (string.IsNullOrWhiteSpace(outage.ZipFilePath))
            {
                return NotFound("No ZIP file associated with this outage");
            }

            //  usa il path completo direttamente
            if (!System.IO.File.Exists(outage.ZipFilePath))
            {
                return NotFound("ZIP file not found on server");
            }

            var fileName = Path.GetFileName(outage.ZipFilePath);
            var fileBytes = await System.IO.File.ReadAllBytesAsync(outage.ZipFilePath);

            await _logger.Info("OutagePeriodsController.DownloadZip",
                $"Downloaded ZIP file for outage {id}: {fileName}");

            return File(fileBytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            await _logger.Error("OutagePeriodsController.DownloadZip",
                $"Error downloading ZIP for outage {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Elimina il file ZIP di un outage
    /// </summary>
    [HttpDelete("{id}/delete-zip")]
    public async Task<IActionResult> DeleteZip(int id)
    {
        try
        {
            var outage = await _db.OutagePeriods.FindAsync(id);
            if (outage == null)
            {
                return NotFound("Outage not found");
            }

            if (string.IsNullOrWhiteSpace(outage.ZipFilePath))
            {
                return BadRequest("No ZIP file associated with this outage");
            }

            //  usa il path completo direttamente
            if (System.IO.File.Exists(outage.ZipFilePath))
            {
                try
                {
                    System.IO.File.Delete(outage.ZipFilePath);
                    await _logger.Info("OutagePeriodsController.DeleteZip", "ZIP file deleted from filesystem.", outage.ZipFilePath);
                }
                catch (Exception ex)
                {
                    await _logger.Warning("OutagePeriodsController.DeleteZip", "Failed to delete ZIP file from filesystem.",
                        $"Path: {outage.ZipFilePath}, Error: {ex.Message}");
                }
            }

            // Rimuovi il riferimento dal database
            outage.ZipFilePath = null;
            await _db.SaveChangesAsync();

            await _logger.Info("OutagePeriodsController.DeleteZip", "ZIP file reference removed from database.",
                $"OutageId: {id}");

            return Ok(new { message = "ZIP file deleted successfully" });
        }
        catch (Exception ex)
        {
            await _logger.Error("OutagePeriodsController.DeleteZip",
                $"Error deleting ZIP for outage {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Forza la risoluzione di un outage ongoing
    /// </summary>
    [HttpPatch("{id}/resolve")]
    public async Task<IActionResult> ResolveOutage(int id)
    {
        try
        {
            var outage = await _db.OutagePeriods.FindAsync(id);
            if (outage == null)
            {
                return NotFound("Outage not found");
            }

            if (outage.OutageEnd.HasValue)
            {
                return BadRequest("Outage is already resolved");
            }

            // usa DateTime.Now
            outage.OutageEnd = DateTime.Now;
            await _db.SaveChangesAsync();

            await _logger.Info("OutagePeriodsController.ResolveOutage",
                $"Manually resolved outage {id}");

            return NoContent();
        }
        catch (Exception ex)
        {
            await _logger.Error("OutagePeriodsController.ResolveOutage",
                $"Error resolving outage {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    #region Private Methods

    /// <summary>
    /// ✅ MODIFICATO: ProcessZipFileAsync ora accetta qualsiasi contenuto
    /// </summary>
    private async Task<string?> ProcessZipFileAsync(IFormFile zipFile, string? filePrefix = null)
    {
        // ✅ Controlla solo che sia un file .zip
        if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await _logger.Warning("ProcessZipFileAsync", "Uploaded file is not a .zip.", zipFile.FileName);
            return null;
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
            return null;
        }
        catch (Exception ex)
        {
            await _logger.Error("ProcessZipFileAsync", "Unexpected error processing ZIP file.", ex.Message);
            return null;
        }

        zipStream.Position = 0;

        // ✅ Crea la directory se non esiste
        if (!Directory.Exists(_outageZipStoragePath))
        {
            Directory.CreateDirectory(_outageZipStoragePath);
        }

        // ✅ Genera il nome del file
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = string.IsNullOrWhiteSpace(filePrefix)
            ? $"outage_{timestamp}.zip"
            : $"{filePrefix}{timestamp}.zip";

        var finalPath = Path.Combine(_outageZipStoragePath, fileName);

        // ✅ Salva il file
        await using var fileStream = new FileStream(finalPath, FileMode.Create);
        await zipStream.CopyToAsync(fileStream);

        await _logger.Info("ProcessZipFileAsync", "ZIP file saved successfully.", finalPath);

        return finalPath;
    }

    private async Task<bool> CheckOutageOverlapAsync(
        string outageType,
        string brand,
        DateTime start,
        DateTime? end,
        int? vehicleId)
    {
        var query = _db.OutagePeriods.AsQueryable()
            .Where(o => o.OutageType == outageType && o.OutageBrand == brand);

        if (outageType == "Outage Vehicle" && vehicleId.HasValue)
        {
            query = query.Where(o => o.VehicleId == vehicleId);
        }

        // Controlla sovrapposizioni
        var overlapping = await query
            .Where(o =>
                // Caso 1: Il nuovo outage inizia prima che finisca un outage esistente
                (o.OutageEnd == null || o.OutageEnd > start) &&
                // Caso 2: Il nuovo outage finisce dopo che inizia un outage esistente
                (end == null || o.OutageStart < end))
            .AnyAsync();

        return overlapping;
    }

    #endregion
}

/// <summary>
/// Request per creare un nuovo outage
/// </summary>
public class CreateOutageRequest
{
    public string OutageType { get; set; } = string.Empty;
    public string OutageBrand { get; set; } = string.Empty;
    public DateTime OutageStart { get; set; }
    public DateTime? OutageEnd { get; set; }
    public int? VehicleId { get; set; }
    public int? ClientCompanyId { get; set; }
    public string? Notes { get; set; }
}