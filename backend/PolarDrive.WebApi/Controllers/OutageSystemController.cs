using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            await _logger.Debug("OutageSystemController", "Fetching outage system statistics");

            var outages = await _db.OutagePeriods.ToListAsync();

            if (!outages.Any())
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

            // Calcola statistiche base
            var ongoingOutages = outages.Where(o => o.OutageEnd == null).ToList();
            var resolvedOutages = outages.Where(o => o.OutageEnd != null).ToList();

            // Calcola durate per outages risolti
            var resolvedDurations = resolvedOutages
                .Select(o => (o.OutageEnd!.Value - o.OutageStart).TotalMinutes)
                .ToList();

            var avgDuration = resolvedDurations.Any() ? resolvedDurations.Average() : 0.0;
            var totalDowntime = resolvedDurations.Sum();

            // Statistiche per brand
            var brandStats = outages
                .GroupBy(o => o.OutageBrand)
                .Select(g => new
                {
                    Brand = g.Key,
                    Count = g.Count(),
                    Ongoing = g.Count(o => o.OutageEnd == null),
                    Resolved = g.Count(o => o.OutageEnd != null)
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            // Outages recenti (ultimi 10) - FIX UTC per DurationMinutes
            var recentOutages = outages
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .Select(o => new
                {
                    o.Id,
                    o.OutageType,
                    o.OutageBrand,
                    o.AutoDetected,
                    Status = o.OutageEnd == null ? "OUTAGE-ONGOING" : "OUTAGE-RESOLVED",
                    CreatedAt = o.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    DurationMinutes = o.OutageEnd != null
                        ? (int)(o.OutageEnd.Value - o.OutageStart).TotalMinutes
                        : (int)(DateTime.Now - o.OutageStart).TotalMinutes // usa DateTime.Now
                })
                .ToList();

            var stats = new
            {
                TotalOutages = outages.Count,
                OngoingOutages = ongoingOutages.Count,
                ResolvedOutages = resolvedOutages.Count,
                AutoDetectedCount = outages.Count(o => o.AutoDetected),
                ManualCount = outages.Count(o => !o.AutoDetected),
                VehicleOutages = outages.Count(o => o.OutageType == "Outage Vehicle"),
                FleetApiOutages = outages.Count(o => o.OutageType == "Outage Fleet Api"),
                OutagesByBrand = brandStats,
                RecentOutages = recentOutages,
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
                $"Fetching period stats from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            var outages = await _db.OutagePeriods
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .ToListAsync();

            if (!outages.Any())
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

            // Outages per giorno
            var outagesByDay = outages
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Count = g.Count(),
                    VehicleOutages = g.Count(o => o.OutageType == "Outage Vehicle"),
                    FleetApiOutages = g.Count(o => o.OutageType == "Outage Fleet Api")
                })
                .OrderBy(x => x.Date)
                .ToList();

            // Outages per tipo
            var outagesByType = outages
                .GroupBy(o => o.OutageType)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count(),
                    AutoDetected = g.Count(o => o.AutoDetected),
                    Manual = g.Count(o => !o.AutoDetected)
                })
                .ToList();

            // Outages per brand
            var outagesByBrand = outages
                .GroupBy(o => o.OutageBrand)
                .Select(g => new
                {
                    Brand = g.Key,
                    Count = g.Count(),
                    VehicleOutages = g.Count(o => o.OutageType == "Outage Vehicle"),
                    FleetApiOutages = g.Count(o => o.OutageType == "Outage Fleet Api")
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            var periodStats = new
            {
                Period = new { From = startDate.ToString("yyyy-MM-dd"), To = endDate.ToString("yyyy-MM-dd") },
                TotalOutages = outages.Count,
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
                return BadRequest(new { error = "OutageDetectionService not available" });
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