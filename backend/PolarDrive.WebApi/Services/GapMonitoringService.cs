using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.Constants;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Services;

/// <summary>
/// Servizio per il monitoraggio proattivo delle anomalie Gap.
/// Rileva automaticamente situazioni critiche e crea alert.
/// Valuta soglie configurate in app-config.json.
/// </summary>
public class GapMonitoringService(PolarDriveDbContext dbContext, GapAnalysisService gapAnalysisService) : IGapMonitoringService
{
    private readonly PolarDriveDbContext _db = dbContext;
    private readonly GapAnalysisService _gapAnalysis = gapAnalysisService;
    private readonly PolarDriveLogger _logger = new();

    /// <summary>
    /// Scansiona tutti i veicoli attivi per anomalie Gap
    /// </summary>
    public async Task CheckAllVehiclesAsync()
    {
        const string source = "GapMonitoringService.CheckAllVehicles";

        try
        {
            // Recupera tutti i veicoli attivi
            var activeVehicles = await _db.ClientVehicles
                .Where(v => v.IsFetchingDataFlag)
                .Select(v => v.Id)
                .ToListAsync();

            await _logger.Info(source, $"Starting gap monitoring for {activeVehicles.Count} active vehicles");

            foreach (var vehicleId in activeVehicles)
            {
                try
                {
                    await CheckVehicleAsync(vehicleId);
                }
                catch (Exception ex)
                {
                    await _logger.Error(source, $"Error checking vehicle {vehicleId}", ex.ToString());
                }
            }

            await _logger.Info(source, $"Gap monitoring completed for {activeVehicles.Count} vehicles");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error during gap monitoring", ex.ToString());
        }
    }

    /// <summary>
    /// Analizza un singolo veicolo per anomalie Gap
    /// </summary>
    public async Task CheckVehicleAsync(int vehicleId)
    {
        const string source = "GapMonitoringService.CheckVehicle";

        try
        {
            var lookbackDays = AppConfig.GAP_MONITORING_LOOKBACK_DAYS;
            var startTime = DateTime.Now.AddDays(-lookbackDays);
            var endTime = DateTime.Now;

            // Analizza i gap nel periodo
            var gaps = await _gapAnalysis.AnalyzeGapsAsync(vehicleId, startTime, endTime);

            if (gaps.Count == 0)
            {
                return; // Nessun gap da analizzare
            }

            // Carica sessioni ADAPTIVE_PROFILE attive nel periodo per rilevare anomalie profilazione
            var profileSessions = await LoadActiveProfileSessionsAsync(vehicleId, startTime, endTime);

            // Applica ADAPTIVE_PROFILE ai gap
            await ApplyAdaptiveProfileToGapsAsync(gaps, profileSessions, vehicleId);

            // Valuta soglie e crea alert se necessario
            await EvaluateThresholdsAsync(vehicleId, gaps, startTime, endTime);
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error checking vehicle {vehicleId}", ex.ToString());
        }
    }

    /// <summary>
    /// Carica sessioni ADAPTIVE_PROFILE attive nel periodo
    /// </summary>
    private async Task<List<SmsAdaptiveProfile>> LoadActiveProfileSessionsAsync(
        int vehicleId, DateTime startTime, DateTime endTime)
    {
        return await _db.SmsAdaptiveProfile
            .Where(p =>
                p.VehicleId == vehicleId &&
                p.ParsedCommand == "ADAPTIVE_PROFILE_ON" &&
                p.ReceivedAt <= endTime &&
                (p.ExpiresAt == null || p.ExpiresAt >= startTime))
            .ToListAsync();
    }

    /// <summary>
    /// Applica bonus/malus ADAPTIVE_PROFILE ai gap
    /// </summary>
    private async Task ApplyAdaptiveProfileToGapsAsync(
        List<GapAnalysisResult> gaps,
        List<SmsAdaptiveProfile> profileSessions,
        int vehicleId)
    {
        const string source = "GapMonitoringService.ApplyAdaptiveProfile";

        foreach (var gap in gaps)
        {
            // Verifica se il gap è avvenuto durante una sessione ADAPTIVE_PROFILE attiva
            var activeSession = profileSessions.FirstOrDefault(p =>
                p.ReceivedAt <= gap.GapTimestamp &&
                (p.ExpiresAt == null || p.ExpiresAt >= gap.GapTimestamp));

            if (activeSession != null)
            {
                // Gap durante profilazione attiva = ANOMALIA GRAVE
                gap.Factors.WasProfiledDuringGap = true;
                gap.Factors.ProfiledUserName = activeSession.AdaptiveSurnameName; // Già cifrato GDPR
                gap.Factors.ProfileSessionStart = activeSession.ReceivedAt;
                gap.Factors.ProfileSessionEnd = activeSession.ExpiresAt;
                gap.Factors.AdaptiveProfileImpact = AppConfig.GAP_ADAPTIVE_PROFILED_MALUS;

                // Applica malus alla confidenza
                gap.ConfidencePercentage = Math.Max(0, gap.ConfidencePercentage + AppConfig.GAP_ADAPTIVE_PROFILED_MALUS);
            }
            else
            {
                // Gap durante periodo NON profilato = bonus
                gap.Factors.WasProfiledDuringGap = false;
                gap.Factors.AdaptiveProfileImpact = AppConfig.GAP_ADAPTIVE_NOT_PROFILED_BONUS;

                // Applica bonus alla confidenza
                gap.ConfidencePercentage = Math.Min(100, gap.ConfidencePercentage + AppConfig.GAP_ADAPTIVE_NOT_PROFILED_BONUS);
            }
        }

        var profiledGaps = gaps.Count(g => g.Factors.WasProfiledDuringGap);
        if (profiledGaps > 0)
        {
            await _logger.Warning(source,
                $"Vehicle {vehicleId}: {profiledGaps} gaps occurred during active ADAPTIVE_PROFILE sessions (ANOMALY)");
        }
    }

    /// <summary>
    /// Valuta le soglie configurate e crea alert se necessario
    /// </summary>
    private async Task EvaluateThresholdsAsync(
        int vehicleId, List<GapAnalysisResult> gaps, DateTime startTime, DateTime endTime)
    {
        const string source = "GapMonitoringService.EvaluateThresholds";

        // Calcola metriche
        var totalHours = (endTime - startTime).TotalHours;
        var gapHours = gaps.Count;
        var gapPercentage = totalHours > 0 ? (gapHours / totalHours) * 100 : 0;
        var avgConfidence = gaps.Count > 0 ? gaps.Average(g => g.ConfidencePercentage) : 100;
        var maxConsecutiveGaps = CalculateMaxConsecutiveGaps(gaps);
        var profiledAnomalies = gaps.Where(g => g.Factors.WasProfiledDuringGap).ToList();
        var lowConfidenceGaps = gaps.Where(g => g.ConfidencePercentage < AppConfig.GAP_THRESHOLD_MIN_CONFIDENCE).ToList();

        // 1. PROFILED_ANOMALY (CRITICAL) - Gap durante ADAPTIVE_PROFILE attivo
        if (profiledAnomalies.Count > 0)
        {
            await CreateAlertIfNotExistsAsync(
                vehicleId,
                GapAlertTypes.PROFILED_ANOMALY,
                GapAlertSeverity.CRITICAL,
                $"Rilevati {profiledAnomalies.Count} gap durante sessioni ADAPTIVE_PROFILE attive. " +
                $"Questo indica che un utente stava usando il veicolo ma non sono stati registrati dati.",
                new
                {
                    ProfiledGapCount = profiledAnomalies.Count,
                    AffectedTimestamps = profiledAnomalies.Select(g => g.GapTimestamp).ToList(),
                    AverageConfidence = profiledAnomalies.Average(g => g.ConfidencePercentage)
                });
        }

        // 2. LOW_CONFIDENCE (WARNING) - Confidenza media sotto soglia
        if (avgConfidence < AppConfig.GAP_THRESHOLD_MIN_CONFIDENCE)
        {
            await CreateAlertIfNotExistsAsync(
                vehicleId,
                GapAlertTypes.LOW_CONFIDENCE,
                GapAlertSeverity.WARNING,
                $"Confidenza media gap {avgConfidence:F1}% sotto la soglia minima di {AppConfig.GAP_THRESHOLD_MIN_CONFIDENCE}%.",
                new
                {
                    AverageConfidence = avgConfidence,
                    Threshold = AppConfig.GAP_THRESHOLD_MIN_CONFIDENCE,
                    LowConfidenceGapCount = lowConfidenceGaps.Count,
                    TotalGaps = gapHours
                });
        }

        // 3. CONSECUTIVE_GAPS (WARNING) - Gap consecutivi oltre soglia
        if (maxConsecutiveGaps > AppConfig.GAP_THRESHOLD_MAX_CONSECUTIVE_HOURS)
        {
            await CreateAlertIfNotExistsAsync(
                vehicleId,
                GapAlertTypes.CONSECUTIVE_GAPS,
                GapAlertSeverity.WARNING,
                $"Rilevati {maxConsecutiveGaps} ore di gap consecutivi (soglia: {AppConfig.GAP_THRESHOLD_MAX_CONSECUTIVE_HOURS} ore).",
                new
                {
                    MaxConsecutiveHours = maxConsecutiveGaps,
                    Threshold = AppConfig.GAP_THRESHOLD_MAX_CONSECUTIVE_HOURS
                });
        }

        // 4. HIGH_GAP_PERCENTAGE (WARNING) - Percentuale gap sul periodo oltre soglia
        if (gapPercentage > AppConfig.GAP_THRESHOLD_MAX_GAP_PERCENT)
        {
            await CreateAlertIfNotExistsAsync(
                vehicleId,
                GapAlertTypes.HIGH_GAP_PERCENTAGE,
                GapAlertSeverity.WARNING,
                $"Percentuale gap {gapPercentage:F1}% del periodo supera la soglia di {AppConfig.GAP_THRESHOLD_MAX_GAP_PERCENT}%.",
                new
                {
                    GapPercentage = gapPercentage,
                    Threshold = AppConfig.GAP_THRESHOLD_MAX_GAP_PERCENT,
                    GapHours = gapHours,
                    TotalHours = totalHours
                });
        }

        // 5. MONTHLY_THRESHOLD (INFO) - Downtime mensile oltre soglia
        // Calcola solo se siamo nel contesto di un mese intero
        var monthlyDowntimePercent = (gapHours / AppConfig.MONTHLY_HOURS_THRESHOLD) * 100;
        if (monthlyDowntimePercent > AppConfig.GAP_THRESHOLD_MAX_MONTHLY_DOWNTIME)
        {
            await CreateAlertIfNotExistsAsync(
                vehicleId,
                GapAlertTypes.MONTHLY_THRESHOLD,
                GapAlertSeverity.INFO,
                $"Downtime mensile {monthlyDowntimePercent:F1}% supera la soglia di {AppConfig.GAP_THRESHOLD_MAX_MONTHLY_DOWNTIME}%.",
                new
                {
                    MonthlyDowntimePercent = monthlyDowntimePercent,
                    Threshold = AppConfig.GAP_THRESHOLD_MAX_MONTHLY_DOWNTIME,
                    GapHours = gapHours,
                    MonthlyHoursThreshold = AppConfig.MONTHLY_HOURS_THRESHOLD
                });
        }

        await _logger.Info(source, $"Threshold evaluation completed for vehicle {vehicleId}",
            $"Gaps: {gapHours}, AvgConf: {avgConfidence:F1}%, MaxConsec: {maxConsecutiveGaps}, " +
            $"GapPct: {gapPercentage:F1}%, ProfiledAnomalies: {profiledAnomalies.Count}");
    }

    /// <summary>
    /// Calcola il massimo numero di gap consecutivi
    /// </summary>
    private static int CalculateMaxConsecutiveGaps(List<GapAnalysisResult> gaps)
    {
        if (gaps.Count == 0) return 0;

        var sorted = gaps.OrderBy(g => g.GapTimestamp).ToList();
        var maxConsecutive = 1;
        var currentConsecutive = 1;

        for (int i = 1; i < sorted.Count; i++)
        {
            var diff = (sorted[i].GapTimestamp - sorted[i - 1].GapTimestamp).TotalHours;
            if (Math.Abs(diff - 1) < 0.1) // Consecutivi (1 ora di differenza)
            {
                currentConsecutive++;
                maxConsecutive = Math.Max(maxConsecutive, currentConsecutive);
            }
            else
            {
                currentConsecutive = 1;
            }
        }

        return maxConsecutive;
    }

    /// <summary>
    /// Crea un alert se non esiste già uno OPEN per lo stesso veicolo e tipo
    /// </summary>
    private async Task CreateAlertIfNotExistsAsync(
        int vehicleId,
        string alertType,
        string severity,
        string description,
        object metrics)
    {
        const string source = "GapMonitoringService.CreateAlertIfNotExists";

        // Verifica se esiste già un alert OPEN per questo veicolo e tipo
        var existingAlert = await _db.GapAlerts
            .FirstOrDefaultAsync(a =>
                a.VehicleId == vehicleId &&
                a.AlertType == alertType &&
                a.Status == GapAlertStatus.OPEN);

        if (existingAlert != null)
        {
            await _logger.Info(source,
                $"Alert {alertType} already exists for vehicle {vehicleId} (ID: {existingAlert.Id})");
            return;
        }

        // Crea nuovo alert
        var alert = new GapAlert
        {
            VehicleId = vehicleId,
            AlertType = alertType,
            Severity = severity,
            DetectedAt = DateTime.UtcNow,
            Description = description,
            MetricsJson = JsonSerializer.Serialize(metrics),
            Status = GapAlertStatus.OPEN
        };

        _db.GapAlerts.Add(alert);

        // Crea audit log
        var auditLog = new GapAuditLog
        {
            VehicleId = vehicleId,
            ActionAt = DateTime.UtcNow,
            ActionType = GapAuditActionTypes.AUTO_DETECTED,
            ActionNotes = $"Alert {alertType} ({severity}) rilevato automaticamente"
        };

        _db.GapAuditLogs.Add(auditLog);

        await _db.SaveChangesAsync();

        // Aggiorna audit log con l'ID dell'alert
        auditLog.GapAlertId = alert.Id;
        await _db.SaveChangesAsync();

        await _logger.Warning(source,
            $"Created {severity} alert {alertType} for vehicle {vehicleId}",
            description);
    }

    /// <summary>
    /// Ottiene statistiche aggregate degli alert
    /// </summary>
    public async Task<GapAlertStats> GetAlertStatsAsync()
    {
        var alerts = await _db.GapAlerts
            .GroupBy(a => 1)
            .Select(g => new GapAlertStats
            {
                TotalAlerts = g.Count(),
                OpenAlerts = g.Count(a => a.Status == GapAlertStatus.OPEN),
                EscalatedAlerts = g.Count(a => a.Status == GapAlertStatus.ESCALATED),
                CompletedAlerts = g.Count(a => a.Status == GapAlertStatus.COMPLETED),
                ContractBreachAlerts = g.Count(a => a.Status == GapAlertStatus.CONTRACT_BREACH),
                CriticalAlerts = g.Count(a => a.Severity == GapAlertSeverity.CRITICAL),
                WarningAlerts = g.Count(a => a.Severity == GapAlertSeverity.WARNING),
                InfoAlerts = g.Count(a => a.Severity == GapAlertSeverity.INFO)
            })
            .FirstOrDefaultAsync();

        return alerts ?? new GapAlertStats();
    }
}
