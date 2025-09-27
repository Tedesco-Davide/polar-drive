using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Services;

/// <summary>
/// Servizio per gestire le sessioni di Adaptive Profiling
/// </summary>
public interface ISmsAdaptiveProfilingService
{
    Task<bool> IsVehicleInAdaptiveProfilingAsync(int vehicleId);
    Task<SmsAdaptiveProfilingEvent?> GetActiveSessionAsync(int vehicleId);
    Task MarkVehicleDataAsAdaptiveAsync(VehicleData vehicleData);
}

public class SmsAdaptiveProfilingService(PolarDriveDbContext db) : ISmsAdaptiveProfilingService
{
    private readonly PolarDriveLogger _logger = new(db);

    /// <summary>
    /// Verifica se un veicolo è attualmente in modalità Adaptive Profiling
    /// </summary>
    public async Task<bool> IsVehicleInAdaptiveProfilingAsync(int vehicleId)
    {
        var activeSession = await GetActiveSessionAsync(vehicleId);
        return activeSession != null;
    }

    /// <summary>
    /// Ottiene la sessione attiva di Adaptive Profiling per un veicolo
    /// </summary>
    public async Task<SmsAdaptiveProfilingEvent?> GetActiveSessionAsync(int vehicleId)
    {
        var fourHoursAgo = DateTime.Now.AddHours(-4);

        return await db.SmsAdaptiveProfilingEvents
            .Where(e => e.VehicleId == vehicleId
                     && e.ParsedCommand == "ADAPTIVE_PROFILING_ON"
                     && e.ReceivedAt >= fourHoursAgo)
            .OrderByDescending(e => e.ReceivedAt)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Marca automaticamente i dati del veicolo come Adaptive se c'è una sessione attiva
    /// </summary>
    public async Task MarkVehicleDataAsAdaptiveAsync(VehicleData vehicleData)
    {
        try
        {
            var isInAdaptiveMode = await IsVehicleInAdaptiveProfilingAsync(vehicleData.VehicleId);
            vehicleData.IsSmsAdaptiveProfiling = isInAdaptiveMode;

            if (isInAdaptiveMode)
            {
                await _logger.Info("AdaptiveProfilingService.MarkData",
                    "Vehicle data marked as Adaptive Profiling.",
                    $"VehicleId: {vehicleData.VehicleId}, Timestamp: {vehicleData.Timestamp}");
            }
        }
        catch (Exception ex)
        {
            await _logger.Error("AdaptiveProfilingService.MarkData",
                "Error marking vehicle data as adaptive.",
                $"Error: {ex.Message}, VehicleId: {vehicleData.VehicleId}");

            // In caso di errore, meglio non marcare come adaptive
            vehicleData.IsSmsAdaptiveProfiling = false;
        }
    }

    /// <summary>
    /// Ottiene statistiche delle sessioni Adaptive per un veicolo
    /// </summary>
    public async Task<AdaptiveProfilingStatsDTO> GetVehicleStatsAsync(int vehicleId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = db.SmsAdaptiveProfilingEvents
            .Where(e => e.VehicleId == vehicleId && e.ParsedCommand == "ADAPTIVE_PROFILING_ON");

        if (fromDate.HasValue)
            query = query.Where(e => e.ReceivedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.ReceivedAt <= toDate.Value);

        var sessions = await query.ToListAsync();

        var totalSessions = sessions.Count;
        var totalHours = totalSessions * 4;
        var lastSession = sessions.MaxBy(s => s.ReceivedAt)?.ReceivedAt;
        var firstSession = sessions.MinBy(s => s.ReceivedAt)?.ReceivedAt;

        // Conta i dati raccolti durante le sessioni
        var adaptiveDataCount = await db.VehiclesData
            .Where(d => d.VehicleId == vehicleId && d.IsSmsAdaptiveProfiling)
            .CountAsync();

        return new AdaptiveProfilingStatsDTO
        {
            VehicleId = vehicleId,
            TotalSessions = totalSessions,
            TotalHours = totalHours,
            FirstSession = firstSession,
            LastSession = lastSession,
            AdaptiveDataPointsCollected = adaptiveDataCount
        };
    }
}

/// <summary>
/// DTO per statistiche Adaptive Profiling
/// </summary>
public class AdaptiveProfilingStatsDTO
{
    public int VehicleId { get; set; }
    public int TotalSessions { get; set; }
    public int TotalHours { get; set; }
    public DateTime? FirstSession { get; set; }
    public DateTime? LastSession { get; set; }
    public int AdaptiveDataPointsCollected { get; set; }
}