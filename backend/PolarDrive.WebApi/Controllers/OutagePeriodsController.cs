using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Constants;
using System.Text.Json;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OutagePeriodsController(PolarDriveDbContext db) : ControllerBase
{
    private readonly PolarDriveDbContext _db = db;
    private readonly PolarDriveLogger _logger = new();

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<object>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? search = null,
        [FromQuery] string? searchType = "id")
    {
        try
        {
            await _logger.Info("OutagePeriodsController.Get", "Requested filtered list of outage periods",
                $"Page: {page}, PageSize: {pageSize}");

            var query = _db.OutagePeriods
                .Include(o => o.ClientCompany)
                .Include(o => o.ClientVehicle)
                .AsQueryable();

            // Filtro ricerca
if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmed = search.Trim();
                
                if (searchType == "id" && int.TryParse(trimmed, out int searchId))
                {
                    var searchIdStr = searchId.ToString();
                    query = query.Where(o => EF.Functions.Like(o.Id.ToString(), $"%{searchIdStr}%"));
                }
                else if (searchType == "status")
                {
                    var pattern = $"%{trimmed}%";
                    var statusValue = trimmed.ToUpper();
                    
                    if (statusValue.Contains("ONGOING"))
                    {
                        query = query.Where(o => o.OutageEnd == null);
                    }
                    else if (statusValue.Contains("RESOLVED"))
                    {
                        query = query.Where(o => o.OutageEnd != null);
                    }
                }
            }

            var totalCount = await query.CountAsync();

            var outages = await query
                .OrderByDescending(o => o.OutageStart)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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

            return Ok(new PaginatedResponse<object>
            {
                Data = outages.Cast<object>().ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("OutagePeriodsController.Get", "Error retrieving outages", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CreateOutageRequest request)
    {
        try
        {
            await _logger.Info("OutagePeriodsController.Post", "Creating new outage manually",
                JsonSerializer.Serialize(request));

            if (!OutageConstants.ValidOutageTypes.Contains(request.OutageType))
                return BadRequest($"Invalid outage type. Valid types: {string.Join(", ", OutageConstants.ValidOutageTypes)}");

            if (!VehicleConstants.ValidBrands.Contains(request.OutageBrand))
                return BadRequest($"Invalid brand. Valid brands: {string.Join(", ", VehicleConstants.ValidBrands)}");

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

            if (request.OutageType == "Outage Vehicle")
            {
                if (request.VehicleId == null || request.ClientCompanyId == null)
                    return BadRequest("Vehicle ID and Company ID are required for Outage Vehicle");

                vehicle = await _db.ClientVehicles
                    .Include(v => v.ClientCompany)
                    .FirstOrDefaultAsync(v => v.Id == request.VehicleId);

                if (vehicle == null) return NotFound("Vehicle not found");
                if (vehicle.ClientCompanyId != request.ClientCompanyId)
                    return BadRequest("Vehicle does not belong to the specified company");
                if (!string.Equals(vehicle.Brand, request.OutageBrand, StringComparison.OrdinalIgnoreCase))
                    return BadRequest($"Vehicle brand ({vehicle.Brand}) does not match outage brand ({request.OutageBrand})");
            }
            else
            {
                if (request.VehicleId.HasValue || request.ClientCompanyId.HasValue)
                    return BadRequest("Vehicle ID and Company ID must be null for Outage Fleet Api");
            }

            var hasOverlap = await CheckOutageOverlapAsync(
                request.OutageType,
                request.OutageBrand,
                outageStartUtc,
                outageEndUtc,
                request.VehicleId);

            if (hasOverlap)
                return Conflict("An overlapping outage already exists for this period");

            var outage = new OutagePeriod
            {
                AutoDetected = false,
                OutageType = request.OutageType,
                OutageBrand = request.OutageBrand,
                CreatedAt = DateTime.Now,
                OutageStart = request.OutageStart,
                OutageEnd = request.OutageEnd,
                VehicleId = request.VehicleId,
                ClientCompanyId = request.ClientCompanyId,
                Notes = request.Notes ?? "Manually inserted"
            };

            _db.OutagePeriods.Add(outage);
            await _db.SaveChangesAsync();

            await _logger.Info("OutagePeriodsController.Post", $"Created new manual outage with ID {outage.Id}");

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

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        try
        {
            var outage = await _db.OutagePeriods.FindAsync(id);
            if (outage == null) return NotFound("Outage not found");

            if (!body.TryGetProperty("notes", out var notesProp))
                return BadRequest("Missing 'notes' field");

            outage.Notes = notesProp.GetString() ?? string.Empty;
            await _db.SaveChangesAsync();

            await _logger.Info("OutagePeriodsController.PatchNotes", $"Updated notes for outage {id}");
            return NoContent();
        }
        catch (Exception ex)
        {
            await _logger.Error("OutagePeriodsController.PatchNotes", $"Error updating notes for outage {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}/download-zip")]
    public async Task<IActionResult> DownloadZip(int id)
    {
        var outage = await _db.OutagePeriods.FindAsync(id);
        if (outage == null) return NotFound("Outage not found");
        if (outage.ZipContent == null || outage.ZipContent.Length == 0)
            return NotFound("No ZIP file associated with this outage");

        var fileName = $"outage_{id}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
        await _logger.Info("OutagePeriodsController.DownloadZip", $"Downloaded ZIP for outage {id}");
        return File(outage.ZipContent, "application/zip", fileName);
    }

    [HttpPatch("{id}/resolve")]
    public async Task<IActionResult> ResolveOutage(int id)
    {
        try
        {
            var outage = await _db.OutagePeriods.FindAsync(id);
            if (outage == null) return NotFound("Outage not found");
            if (outage.OutageEnd.HasValue) return BadRequest("Outage is already resolved");

            outage.OutageEnd = DateTime.Now;
            await _db.SaveChangesAsync();

            await _logger.Info("OutagePeriodsController.ResolveOutage", $"Manually resolved outage {id}");
            return NoContent();
        }
        catch (Exception ex)
        {
            await _logger.Error("OutagePeriodsController.ResolveOutage", $"Error resolving outage {id}", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
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
            query = query.Where(o => o.VehicleId == vehicleId);

        var overlapping = await query
            .Where(o =>
                (o.OutageEnd == null || o.OutageEnd > start) &&
                (end == null || o.OutageStart < end))
            .AnyAsync();

        return overlapping;
    }
}

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