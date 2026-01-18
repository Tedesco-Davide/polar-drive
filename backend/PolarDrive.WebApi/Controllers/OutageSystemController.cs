using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.Constants;
using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Services;

namespace PolarDrive.WebApi.Controllers;

/// <summary>
/// Controller per statistiche e monitoraggio del sistema outages
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OutageSystemController(PolarDriveDbContext db) : ControllerBase
{
    private readonly PolarDriveDbContext _db = db;
    private readonly PolarDriveLogger _logger = new();

    /// <summary>
    /// Ottiene statistiche complete del sistema outages
    /// GET /api/outagesystem/stats
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetSystemStats()
    {
        try
        {
            await _logger.Debug("OutageSystemController", "Fetching outage system statistics (optimized)");

            // Query aggregate SQL - molto più veloce di ToListAsync()
            var totalOutages = await _db.OutagePeriods.CountAsync();

            if (totalOutages == 0)
            {
                return Ok(new
                {
                    TotalOutages = 0,
                    OngoingOutages = 0,
                    ResolvedOutages = 0,
                    AutoDetectedCount = 0,
                    ManualCount = 0,
                    VehicleOutages = 0,
                    FleetApiOutages = 0,
                    OutagesByBrand = new List<object>(),
                    RecentOutages = new List<object>(),
                    AvgOutageDurationMinutes = 0.0,
                    TotalDowntimeMinutes = 0.0
                });
            }

            // Conteggi aggregati via SQL (sequenziali - DbContext non supporta query parallele)
            var ongoingCount = await _db.OutagePeriods.CountAsync(o => o.OutageEnd == null);
            var resolvedCount = await _db.OutagePeriods.CountAsync(o => o.OutageEnd != null);
            var autoDetectedCount = await _db.OutagePeriods.CountAsync(o => o.AutoDetected);
            var manualCount = await _db.OutagePeriods.CountAsync(o => !o.AutoDetected);
            var vehicleOutages = await _db.OutagePeriods.CountAsync(o => o.OutageType == OutageConstants.OUTAGE_VEHICLE);
            var fleetApiOutages = await _db.OutagePeriods.CountAsync(o => o.OutageType == OutageConstants.OUTAGE_FLEET_API);

            // Statistiche durata - query aggregata
            var durationStats = await _db.OutagePeriods
                .Where(o => o.OutageEnd != null)
                .Select(o => EF.Functions.DateDiffMinute(o.OutageStart, o.OutageEnd!.Value))
                .ToListAsync();

            var avgDuration = durationStats.Any() ? durationStats.Average() : 0.0;
            var totalDowntime = durationStats.Any() ? durationStats.Sum() : 0.0;

            // Statistiche per brand - query aggregata SQL
            var brandStats = await _db.OutagePeriods
                .GroupBy(o => o.OutageBrand)
                .Select(g => new
                {
                    Brand = g.Key,
                    Count = g.Count(),
                    Ongoing = g.Count(o => o.OutageEnd == null),
                    Resolved = g.Count(o => o.OutageEnd != null)
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            // Solo ultimi 10 outages (query leggera)
            var recentOutages = await _db.OutagePeriods
                .AsNoTracking()
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .Select(o => new
                {
                    o.Id,
                    o.OutageType,
                    o.OutageBrand,
                    o.AutoDetected,
                    o.OutageStart,
                    o.OutageEnd,
                    o.CreatedAt
                })
                .ToListAsync();

            // Proiezione finale in memoria (solo 10 record)
            var recentOutagesFormatted = recentOutages.Select(o => new
            {
                o.Id,
                o.OutageType,
                o.OutageBrand,
                o.AutoDetected,
                Status = o.OutageEnd == null ? OutageConstants.STATUS_ONGOING : OutageConstants.STATUS_RESOLVED,
                CreatedAt = o.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                DurationMinutes = o.OutageEnd != null
                    ? (int)(o.OutageEnd.Value - o.OutageStart).TotalMinutes
                    : (int)(DateTime.Now - o.OutageStart).TotalMinutes
            }).ToList();

            var stats = new
            {
                TotalOutages = totalOutages,
                OngoingOutages = ongoingCount,
                ResolvedOutages = resolvedCount,
                AutoDetectedCount = autoDetectedCount,
                ManualCount = manualCount,
                VehicleOutages = vehicleOutages,
                FleetApiOutages = fleetApiOutages,
                OutagesByBrand = brandStats,
                RecentOutages = recentOutagesFormatted,
                AvgOutageDurationMinutes = Math.Round(avgDuration, 2),
                TotalDowntimeMinutes = Math.Round(totalDowntime, 2)
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            await _logger.Error("OutageSystemController", "Failed to get system stats", ex.ToString());
            return StatusCode(500, new { error = "Failed to get system statistics", details = ex.Message });
        }
    }

    /// <summary>
    /// Ottiene statistiche per un periodo specifico
    /// GET /api/outagesystem/stats/period?from=2024-01-01&to=2024-12-31
    /// </summary>
    [HttpGet("stats/period")]
    public async Task<IActionResult> GetPeriodStats(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            // Default ultimi 30 giorni usando DateTime.Now
            var startDate = from ?? DateTime.Now.AddDays(-30);
            var endDate = to ?? DateTime.Now;

            await _logger.Debug("OutageSystemController",
                $"Fetching period stats from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} (optimized)");

            // Query base filtrata per periodo
            var baseQuery = _db.OutagePeriods
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate);

            // Conteggio totale via SQL
            var totalCount = await baseQuery.CountAsync();

            if (totalCount == 0)
            {
                return Ok(new
                {
                    Period = new { From = startDate.ToString("yyyy-MM-dd"), To = endDate.ToString("yyyy-MM-dd") },
                    TotalOutages = 0,
                    OutagesByDay = new List<object>(),
                    OutagesByType = new List<object>(),
                    OutagesByBrand = new List<object>()
                });
            }

            var outagesByDayRaw = await baseQuery
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count(),
                    VehicleOutages = g.Count(o => o.OutageType == OutageConstants.OUTAGE_VEHICLE),
                    FleetApiOutages = g.Count(o => o.OutageType == OutageConstants.OUTAGE_FLEET_API)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var outagesByType = await baseQuery
                .GroupBy(o => o.OutageType)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count(),
                    AutoDetected = g.Count(o => o.AutoDetected),
                    Manual = g.Count(o => !o.AutoDetected)
                })
                .ToListAsync();

            var outagesByBrand = await baseQuery
                .GroupBy(o => o.OutageBrand)
                .Select(g => new
                {
                    Brand = g.Key,
                    Count = g.Count(),
                    VehicleOutages = g.Count(o => o.OutageType == OutageConstants.OUTAGE_VEHICLE),
                    FleetApiOutages = g.Count(o => o.OutageType == OutageConstants.OUTAGE_FLEET_API)
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var outagesByDay = outagesByDayRaw.Select(x => new
            {
                Date = x.Date.ToString("yyyy-MM-dd"),
                x.Count,
                x.VehicleOutages,
                x.FleetApiOutages
            }).ToList();

            var periodStats = new
            {
                Period = new { From = startDate.ToString("yyyy-MM-dd"), To = endDate.ToString("yyyy-MM-dd") },
                TotalOutages = totalCount,
                OutagesByDay = outagesByDay,
                OutagesByType = outagesByType,
                OutagesByBrand = outagesByBrand
            };

            return Ok(periodStats);
        }
        catch (Exception ex)
        {
            await _logger.Error("OutageSystemController", "Failed to get period stats", ex.ToString());
            return StatusCode(500, new { error = "Failed to get period statistics", details = ex.Message });
        }
    }

    /// <summary>
    /// Ottiene lo stato corrente del sistema (healthcheck)
    /// GET /api/outagesystem/health
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetSystemHealth()
    {
        try
        {
            var ongoingOutages = await _db.OutagePeriods
                .Where(o => o.OutageEnd == null)
                .Select(o => new
                {
                    o.Id,
                    o.OutageType,
                    o.OutageBrand,
                    o.OutageStart,
                    // usa DateTime.Now per calcolare la durata
                    DurationMinutes = (int)(DateTime.Now - o.OutageStart).TotalMinutes,
                    VehicleVin = o.ClientVehicle != null ? o.ClientVehicle.Vin : null
                })
                .ToListAsync();

            // Determina lo stato generale del sistema
            var systemStatus = "HEALTHY";
            var criticalIssues = new List<string>();

            // Critico: più di 3 outages ongoing
            if (ongoingOutages.Count > 3)
            {
                systemStatus = "CRITICAL";
                criticalIssues.Add($"{ongoingOutages.Count} outages in corso");
            }
            // Warning: outages che durano più di 24 ore
            else if (ongoingOutages.Any(o => o.DurationMinutes > 1440))
            {
                systemStatus = "WARNING";
                var longOutages = ongoingOutages.Count(o => o.DurationMinutes > 1440);
                criticalIssues.Add($"{longOutages} outages da più di 24 ore");
            }
            // Warning: almeno un outage ongoing
            else if (ongoingOutages.Any())
            {
                systemStatus = "WARNING";
                criticalIssues.Add($"{ongoingOutages.Count} outages in corso");
            }

            var health = new
            {
                SystemStatus = systemStatus,
                // usa DateTime.Now per il timestamp
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                OngoingOutagesCount = ongoingOutages.Count,
                OngoingOutages = ongoingOutages,
                CriticalIssues = criticalIssues,
                SystemUptime = "OK" // Potresti calcolare l'uptime reale se necessario
            };

            // Restituisce status code appropriato
            var statusCode = systemStatus switch
            {
                "HEALTHY" => 200,
                "WARNING" => 200, // Warning è comunque OK
                "CRITICAL" => 503, // Service Unavailable per stati critici
                _ => 200
            };

            return StatusCode(statusCode, health);
        }
        catch (Exception ex)
        {
            await _logger.Error("OutageSystemController", "Failed to get system health", ex.ToString());
            return StatusCode(500, new
            {
                SystemStatus = "ERROR",
                error = "Failed to get system health",
                details = ex.Message
            });
        }
    }

    /// <summary>
    /// Forza un controllo manuale degli outages
    /// POST /api/outagesystem/check
    /// </summary>
    [HttpPost("check")]
    public async Task<IActionResult> ForceOutageCheck([FromServices] IServiceProvider serviceProvider)
    {
        try
        {
            await _logger.Info("OutageSystemController", "Manual outage check requested");

            // Crea uno scope per ottenere il servizio
            using var scope = serviceProvider.CreateScope();
            var outageDetectionService = scope.ServiceProvider.GetService<IOutageDetectionService>();

            if (outageDetectionService == null)
            {
                return BadRequest(new { errorCode = "SERVICE_NOT_AVAILABLE" });
            }

            // Esegui tutti i controlli
            await outageDetectionService.CheckFleetApiOutagesAsync();
            await outageDetectionService.CheckVehicleOutagesAsync();
            await outageDetectionService.ResolveOutagesAsync();

            await _logger.Info("OutageSystemController", "Manual outage check completed");

            return Ok(new
            {
                message = "Outage check completed successfully",
                // usa DateTime.Now per il timestamp
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("OutageSystemController", "Failed to perform manual outage check", ex.ToString());
            return StatusCode(500, new { error = "Failed to perform outage check", details = ex.Message });
        }
    }
}