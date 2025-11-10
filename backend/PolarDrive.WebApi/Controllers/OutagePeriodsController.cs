using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.Data.Constants;
using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;

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
                o.VehicleId,
                o.ClientCompanyId,
                Status = o.OutageEnd.HasValue ? "OUTAGE-RESOLVED" : "OUTAGE-ONGOING",
                Vin = o.ClientVehicle != null ? o.ClientVehicle.Vin : null,
                CompanyVatNumber = o.ClientCompany != null ? o.ClientCompany.VatNumber : null,
                DurationMinutes = o.OutageEnd.HasValue
                    ? (int)(o.OutageEnd.Value - o.OutageStart).TotalMinutes
                    : (int)(DateTime.Now - o.OutageStart).TotalMinutes,
                HasZipFile = o.ZipContent != null && o.ZipContent.Length > 0,
                ZipHash = string.IsNullOrEmpty(o.ZipHash) ? null : o.ZipHash
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

            ClientVehicle? vehicle = null;
            ClientCompany? company = null;
        
            // Validazioni specifiche per Outage Vehicle
            if (request.OutageType == "Outage Vehicle")
            {
                if (request.VehicleId == null || request.ClientCompanyId == null)
                {
                    return BadRequest("Vehicle ID and Company ID are required for Outage Vehicle");
                }

                vehicle = await _db.ClientVehicles
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

            return CreatedAtAction(nameof(Get), new { id = outage.Id }, new
            {
                outage.Id,
                outage.AutoDetected,
                outage.OutageType,
                outage.OutageBrand,
                outage.CreatedAt,
                outage.OutageStart,
                outage.OutageEnd,
                outage.Notes,
                outage.VehicleId,
                outage.ClientCompanyId,
                Status = outage.OutageEnd.HasValue ? "OUTAGE-RESOLVED" : "OUTAGE-ONGOING",
                Vin = vehicle?.Vin,
                CompanyVatNumber = company?.VatNumber,
                DurationMinutes = outage.OutageEnd.HasValue
                    ? (int)(outage.OutageEnd.Value - outage.OutageStart).TotalMinutes
                    : (int)(DateTime.Now - outage.OutageStart).TotalMinutes,
                HasZipFile = outage.ZipContent != null && outage.ZipContent.Length > 0
            });
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
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 100_000_000)]
    public async Task<IActionResult> Post(
        [FromForm] string outageType,
        [FromForm] string outageBrand,
        [FromForm] DateTime outageStart,
        [FromForm] DateTime? outageEnd,
        [FromForm] int? vehicleId,
        [FromForm] int? clientCompanyId,
        [FromForm] string? notes,
        [FromForm] IFormFile? zipFile) // ✅ Opzionale per outages
    {
        try {
            // Validazioni esistenti...
            var validTypes = new[] { "Outage Vehicle", "Outage All Tesla Brand" };
            if (!validTypes.Contains(outageType))
                return BadRequest("Invalid outage type");

            if (vehicleId.HasValue) {
                var vehicle = await _db.ClientVehicles.FindAsync(vehicleId.Value);
                if (vehicle == null) return NotFound("Vehicle not found");
            }

            // ✅ Gestione ZIP opzionale
            byte[]? zipContent = null;
            string? hash = null;

            if (zipFile != null && zipFile.Length > 0) {
                if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Invalid ZIP file");

                await using var ms = new MemoryStream();
                await zipFile.CopyToAsync(ms);
                ms.Position = 0;

                try { using var _ = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true); }
                catch (Exception ex) {
                    await _logger.Error("OutagePeriodsController.Post", "Invalid ZIP", ex.ToString());
                    return BadRequest("Corrupted or invalid ZIP");
                }

                ms.Position = 0;
                using var sha = SHA256.Create();
                hash = Convert.ToHexStringLower(await sha.ComputeHashAsync(ms));

                var duplicate = await _db.OutagePeriods.FirstOrDefaultAsync(o => o.ZipHash == hash);
                if (duplicate != null) {
                    await _logger.Warning("OutagePeriodsController.Post", "Duplicate ZIP", $"ExistingId: {duplicate.Id}");
                    return Conflict(new { 
                        message = $"File ZIP già caricato (hash: {hash.Substring(0, 8)}...)", 
                        existingId = duplicate.Id 
                    });
                }

                ms.Position = 0;
                zipContent = ms.ToArray();
            }

            // ✅ Crea outage atomicamente
            var outage = new OutagePeriod {
                OutageType = outageType,
                OutageBrand = outageBrand,
                OutageStart = outageStart,
                OutageEnd = outageEnd,
                VehicleId = vehicleId,
                ClientCompanyId = clientCompanyId,
                Notes = notes ?? "Manually inserted",
                ZipContent = zipContent,
                ZipHash = hash ?? ""
            };

            _db.OutagePeriods.Add(outage);
            await _db.SaveChangesAsync();

            await _logger.Info("OutagePeriodsController.Post", $"Created outage {outage.Id}");

            return CreatedAtAction(nameof(Get), new { id = outage.Id }, new { id = outage.Id });
        }
        catch (Exception ex) {
            await _logger.Error("OutagePeriodsController.Post", "Error", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Download di un file ZIP di un outage
    /// </summary>
    [HttpGet("{id}/download-zip")]
    public async Task<IActionResult> DownloadZip(int id)
    {
        var outage = await _db.OutagePeriods.FindAsync(id);
        if (outage == null) return NotFound("Outage not found");
        if (outage.ZipContent == null || outage.ZipContent.Length == 0)
            return NotFound("No ZIP file associated with this outage");
        
        var fileName = $"outage_{id}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
        
        await _logger.Info("OutagePeriodsController.DownloadZip",
            $"Downloaded ZIP for outage {id}");
        
        return File(outage.ZipContent, "application/zip", fileName);
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