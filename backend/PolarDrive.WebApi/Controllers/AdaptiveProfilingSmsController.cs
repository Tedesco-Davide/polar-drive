using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SmsAdaptiveProfilingController : ControllerBase
{
    private readonly PolarDriveDbContext db;
    private readonly PolarDriveLogger _logger;

    public SmsAdaptiveProfilingController(PolarDriveDbContext db)
    {
        this.db = db;
        this._logger = new PolarDriveLogger(db);
    }

    /// <summary>
    /// Riceve SMS con comando ADAPTIVE e attiva modalità Adaptive Profiling per n. ore (check parametro in CommonConstants)
    /// </summary>
    [HttpPost("receive-sms")]
    public async Task<ActionResult> ReceiveSms([FromBody] ReceiveSmsDTO dto)
    {
        try
        {
            // Valida che il veicolo esista e sia attivo
            var vehicle = await db.ClientVehicles
                .Include(v => v.ClientCompany)
                .FirstOrDefaultAsync(v => v.Id == dto.VehicleId);

            if (vehicle == null)
            {
                await _logger.Warning("SmsAdaptiveProfiling.ReceiveSms",
                    "Vehicle not found.", $"VehicleId: {dto.VehicleId}");
                return NotFound("Vehicle not found!");
            }

            if (!vehicle.IsActiveFlag || !vehicle.IsFetchingDataFlag)
            {
                await _logger.Warning("SmsAdaptiveProfiling.ReceiveSms",
                    "Vehicle not active or not fetching data.",
                    $"VehicleId: {dto.VehicleId}, Active: {vehicle.IsActiveFlag}, Fetching: {vehicle.IsFetchingDataFlag}");
                return BadRequest("Vehicle must be active and fetching data for Adaptive Profiling!");
            }

            // Parsing del comando
            string parsedCommand = ParseSmsCommand(dto.MessageContent);

            if (parsedCommand == "INVALID")
            {
                await _logger.Warning("SmsAdaptiveProfiling.ReceiveSms",
                    "Invalid SMS command format.", $"Message: {dto.MessageContent}");
                return BadRequest("Invalid SMS format. Use 'ADAPTIVE [description]' to start profiling.");
            }

            // Crea evento SMS
            var smsEvent = new SmsAdaptiveProfilingEvent
            {
                VehicleId = dto.VehicleId,
                ReceivedAt = DateTime.UtcNow,
                MessageContent = dto.MessageContent,
                ParsedCommand = parsedCommand
            };

            db.SmsAdaptiveProfilingEvents.Add(smsEvent);

            // Se è un comando ON, verifica che non ci sia già una sessione attiva
            if (parsedCommand == "ADAPTIVE_PROFILING_ON")
            {
                var activeSession = await GetActiveAdaptiveProfilingSession(dto.VehicleId);
                if (activeSession != null)
                {
                    await _logger.Info("SmsAdaptiveProfiling.ReceiveSms",
                        "Adaptive Profiling session extended.",
                        $"VehicleId: {dto.VehicleId}, Previous session started at: {activeSession.ReceivedAt}");

                    await db.SaveChangesAsync();
                    return Ok(new
                    {
                        message = "Adaptive Profiling session extended for 4 more hours",
                        sessionStartedAt = activeSession.ReceivedAt,
                        newEndTime = DateTime.UtcNow.AddHours(4)
                    });
                }

                await _logger.Info("SmsAdaptiveProfiling.ReceiveSms",
                    "New Adaptive Profiling session started.",
                    $"VehicleId: {dto.VehicleId}, VIN: {vehicle.Vin}, Company: {vehicle.ClientCompany?.Name}, Description: {dto.MessageContent}");
            }

            await db.SaveChangesAsync();

            return Ok(new
            {
                message = parsedCommand == "ADAPTIVE_PROFILING_ON"
                    ? "Adaptive Profiling activated for 4 hours"
                    : "Adaptive Profiling deactivated",
                sessionStartedAt = DateTime.UtcNow,
                sessionEndTime = parsedCommand == "ADAPTIVE_PROFILING_ON"
                    ? DateTime.UtcNow.AddHours(SMS_ADPATIVE_HOURS_THRESHOLD)
                    : (DateTime?)null
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("SmsAdaptiveProfiling.ReceiveSms",
                "Error processing SMS command.", $"Error: {ex.Message}, VehicleId: {dto.VehicleId}");
            return StatusCode(500, "Internal server error processing SMS command.");
        }
    }

    /// <summary>
    /// Verifica se un veicolo è attualmente in modalità Adaptive Profiling
    /// </summary>
    [HttpGet("{vehicleId}/status")]
    public async Task<ActionResult> GetAdaptiveProfilingStatus(int vehicleId)
    {
        try
        {
            var vehicle = await db.ClientVehicles.FindAsync(vehicleId);
            if (vehicle == null)
            {
                return NotFound("Vehicle not found!");
            }

            var activeSession = await GetActiveAdaptiveProfilingSession(vehicleId);

            if (activeSession != null)
            {
                var endTime = activeSession.ReceivedAt.AddHours(4);
                var remainingMinutes = (int)(endTime - DateTime.UtcNow).TotalMinutes;

                return Ok(new
                {
                    isActive = true,
                    sessionStartedAt = activeSession.ReceivedAt,
                    sessionEndTime = endTime,
                    remainingMinutes = Math.Max(0, remainingMinutes),
                    description = activeSession.MessageContent
                });
            }

            return Ok(new
            {
                isActive = false,
                sessionStartedAt = (DateTime?)null,
                sessionEndTime = (DateTime?)null,
                remainingMinutes = 0,
                description = (string?)null
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("SmsAdaptiveProfiling.GetStatus",
                "Error getting Adaptive Profiling status.", $"Error: {ex.Message}, VehicleId: {vehicleId}");
            return StatusCode(500, "Internal server error.");
        }
    }

    /// <summary>
    /// Ottieni storico delle sessioni Adaptive Profiling per un veicolo
    /// </summary>
    [HttpGet("{vehicleId}/history")]
    public async Task<ActionResult> GetAdaptiveProfilingHistory(int vehicleId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var vehicle = await db.ClientVehicles.FindAsync(vehicleId);
            if (vehicle == null)
            {
                return NotFound("Vehicle not found!");
            }

            var query = db.SmsAdaptiveProfilingEvents
                .Where(e => e.VehicleId == vehicleId);

            if (fromDate.HasValue)
                query = query.Where(e => e.ReceivedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.ReceivedAt <= toDate.Value);

            var sessions = await query
                .OrderByDescending(e => e.ReceivedAt)
                .Select(e => new
                {
                    id = e.Id,
                    receivedAt = e.ReceivedAt,
                    messageContent = e.MessageContent,
                    command = e.ParsedCommand,
                    sessionEndTime = e.ParsedCommand == "ADAPTIVE_PROFILING_ON"
                        ? e.ReceivedAt.AddHours(4)
                        : (DateTime?)null
                })
                .ToListAsync();

            return Ok(sessions);
        }
        catch (Exception ex)
        {
            await _logger.Error("SmsAdaptiveProfiling.GetHistory",
                "Error getting Adaptive Profiling history.", $"Error: {ex.Message}, VehicleId: {vehicleId}");
            return StatusCode(500, "Internal server error.");
        }
    }

    /// <summary>
    /// Ottieni statistiche Adaptive Profiling per tutti i veicoli
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult> GetAdaptiveProfilingStatistics([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var query = db.SmsAdaptiveProfilingEvents.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(e => e.ReceivedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.ReceivedAt <= toDate.Value);

            var stats = await query
                .Where(e => e.ParsedCommand == "ADAPTIVE_PROFILING_ON")
                .GroupBy(e => e.VehicleId)
                .Select(g => new
                {
                    vehicleId = g.Key,
                    totalSessions = g.Count(),
                    totalHours = g.Count() * 4, // Ogni sessione dura 4 ore
                    lastSession = g.Max(e => e.ReceivedAt),
                    firstSession = g.Min(e => e.ReceivedAt)
                })
                .ToListAsync();

            var totalSessions = stats.Sum(s => s.totalSessions);
            var totalHours = stats.Sum(s => s.totalHours);
            var activeVehicles = stats.Count;

            return Ok(new
            {
                summary = new
                {
                    totalSessions,
                    totalHours,
                    activeVehicles,
                    averageSessionsPerVehicle = activeVehicles > 0 ? (double)totalSessions / activeVehicles : 0
                },
                vehicleStats = stats
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("SmsAdaptiveProfiling.GetStatistics",
                "Error getting Adaptive Profiling statistics.", $"Error: {ex.Message}");
            return StatusCode(500, "Internal server error.");
        }
    }

    /// <summary>
    /// Metodo helper per verificare se c'è una sessione attiva
    /// </summary>
    private async Task<SmsAdaptiveProfilingEvent?> GetActiveAdaptiveProfilingSession(int vehicleId)
    {
        var fourHoursAgo = DateTime.UtcNow.AddHours(-4);

        return await db.SmsAdaptiveProfilingEvents
            .Where(e => e.VehicleId == vehicleId
                     && e.ParsedCommand == "ADAPTIVE_PROFILING_ON"
                     && e.ReceivedAt >= fourHoursAgo)
            .OrderByDescending(e => e.ReceivedAt)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Parsing del comando SMS
    /// </summary>
    private static string ParseSmsCommand(string messageContent)
    {
        if (string.IsNullOrWhiteSpace(messageContent))
            return "INVALID";

        var normalizedMessage = messageContent.Trim().ToUpperInvariant();

        // Accetta "ADAPTIVE" all'inizio del messaggio
        if (normalizedMessage.StartsWith("ADAPTIVE"))
        {
            return "ADAPTIVE_PROFILING_ON";
        }

        // Accetta comandi espliciti di stop (opzionale)
        if (normalizedMessage.StartsWith("STOP") || normalizedMessage.StartsWith("OFF"))
        {
            return "ADAPTIVE_PROFILING_OFF";
        }

        return "INVALID";
    }
}

/// <summary>
/// DTO per ricevere SMS
/// </summary>
public class ReceiveSmsDTO
{
    public int VehicleId { get; set; }
    public string MessageContent { get; set; } = string.Empty;
}