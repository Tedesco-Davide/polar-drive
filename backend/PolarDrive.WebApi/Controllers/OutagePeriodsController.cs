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

    public OutagePeriodsController(PolarDriveDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
        _logger = new PolarDriveLogger(db);
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
                Notes = request.Notes ?? "Inserito manualmente"
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

            if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("File must be a .zip file");
            }

            // Validazione contenuto ZIP
            using var zipStream = new MemoryStream();
            await zipFile.CopyToAsync(zipStream);
            zipStream.Position = 0;

            try
            {
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
                bool hasPdf = archive.Entries.Any(e =>
                    Path.GetExtension(e.FullName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) &&
                    !e.FullName.EndsWith("/"));

                if (!hasPdf)
                {
                    return BadRequest("ZIP file must contain at least one PDF file");
                }
            }
            catch (InvalidDataException)
            {
                return BadRequest("Invalid or corrupted ZIP file");
            }

            // Salvataggio file
            zipStream.Position = 0;
            var zipsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "zips-outages");
            Directory.CreateDirectory(zipsDir);

            // Elimina il file precedente se esiste
            if (!string.IsNullOrWhiteSpace(outage.ZipFilePath))
            {
                var oldFilePath = Path.Combine(_env.WebRootPath ?? "wwwroot",
                    outage.ZipFilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (System.IO.File.Exists(oldFilePath))
                {
                    System.IO.File.Delete(oldFilePath);
                }
            }

            // usa DateTime.Now per il nome del file
            var zipFileName = $"outage_{id}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
            var finalPath = Path.Combine(zipsDir, zipFileName);

            await using var fileStream = new FileStream(finalPath, FileMode.Create);
            await zipStream.CopyToAsync(fileStream);

            // Aggiorna il database
            var relativeZipPath = Path.Combine("zips-outages", zipFileName).Replace("\\", "/");
            outage.ZipFilePath = relativeZipPath;
            await _db.SaveChangesAsync();

            await _logger.Info("OutagePeriodsController.UploadZip",
                $"Uploaded ZIP file for outage {id}: {zipFileName}");

            return Ok(new { zipFilePath = relativeZipPath });
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

            var fullPath = Path.Combine(_env.WebRootPath ?? "wwwroot",
                outage.ZipFilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound("ZIP file not found on server");
            }

            var fileName = Path.GetFileName(fullPath);
            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);

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