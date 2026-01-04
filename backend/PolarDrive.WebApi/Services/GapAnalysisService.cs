using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.Data.Constants;
using PolarDrive.WebApi.Helpers;

namespace PolarDrive.WebApi.Services;

/// <summary>
/// Servizio per l'analisi e la certificazione probabilistica dei gap temporali
/// Calcola la confidenza che il veicolo fosse operativo durante i periodi senza dati
/// </summary>
public class GapAnalysisService(PolarDriveDbContext dbContext)
{
    private readonly PolarDriveDbContext _db = dbContext;
    private readonly PolarDriveLogger _logger = new();

    /// <summary>
    /// Analizza tutti i gap per un report PDF specifico.
    /// Usa la stessa finestra temporale del PDF.
    /// </summary>
    public async Task<List<GapAnalysisResult>> AnalyzeGapsForReportAsync(int pdfReportId)
    {
        var (gaps, _, _) = await AnalyzeGapsForReportWithPeriodAsync(pdfReportId);
        return gaps;
    }

    /// <summary>
    /// Analizza tutti i gap per un report PDF specifico e restituisce anche il periodo effettivo usato.
    /// Usa la stessa finestra temporale del PDF.
    /// </summary>
    public async Task<(List<GapAnalysisResult> gaps, DateTime periodStart, DateTime periodEnd)> AnalyzeGapsForReportWithPeriodAsync(int pdfReportId)
    {
        const string source = "GapAnalysisService.AnalyzeGapsForReport";

        try
        {
            var report = await _db.PdfReports
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == pdfReportId);

            if (report == null)
            {
                await _logger.Warning(source, $"Report {pdfReportId} not found");
                return ([], DateTime.Now, DateTime.Now);
            }

            // Calcola la finestra temporale esattamente come fa il PDF
            // (vedi DataPolarCertification.GenerateDetailedLogTableAsync)
            var now = DateTime.Now;
            var maxStartTime = now.AddHours(-AppConfig.MONTHLY_HOURS_THRESHOLD);

            // Trova il primo record effettivo del veicolo
            var firstRecordTime = await _db.VehiclesData
                .Where(vd => vd.VehicleId == report.VehicleId)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            if (firstRecordTime == default)
            {
                await _logger.Info(source, $"No data records found for vehicle {report.VehicleId}");
                return ([], DateTime.Now, DateTime.Now);
            }

            // Usa la stessa logica del PDF per il periodo effettivo
            var effectiveStartTime = firstRecordTime > maxStartTime ? firstRecordTime : maxStartTime;

            // Arrotonda all'ora per avere timestamp puliti (come nel PDF)
            var startTime = new DateTime(
                effectiveStartTime.Year,
                effectiveStartTime.Month,
                effectiveStartTime.Day,
                effectiveStartTime.Hour,
                0, 0);

            await _logger.Info(source, $"Analyzing gaps for report {pdfReportId}",
                $"Period: {startTime:dd/MM/yyyy HH:mm} - {now:dd/MM/yyyy HH:mm} (aligned with PDF table)");

            var gaps = await AnalyzeGapsAsync(report.VehicleId, startTime, now);
            return (gaps, startTime, now);
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error analyzing gaps for report {pdfReportId}", ex.ToString());
            return ([], DateTime.Now, DateTime.Now);
        }
    }

    /// <summary>
    /// Analizza tutti i gap per un veicolo in un periodo specifico.
    /// OTTIMIZZATO: carica solo timestamp inizialmente, poi JSON solo per record adiacenti ai gap.
    /// </summary>
    public async Task<List<GapAnalysisResult>> AnalyzeGapsAsync(int vehicleId, DateTime startTime, DateTime endTime)
    {
        const string source = "GapAnalysisService.AnalyzeGaps";
        var results = new List<GapAnalysisResult>();

        try
        {
            // PASSO 1: Recupera SOLO i timestamp (niente JSON pesanti!)
            var existingTimestamps = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= startTime && vd.Timestamp <= endTime)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .ToListAsync();

            // Crea lookup per ora (solo per verificare esistenza)
            var hoursWithData = existingTimestamps
                .Select(t => new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0))
                .ToHashSet();

            // PASSO 2: Identifica tutti i gap
            var gapTimestamps = new List<DateTime>();
            var currentHour = new DateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, 0, 0);
            var endHour = new DateTime(endTime.Year, endTime.Month, endTime.Day, endTime.Hour, 0, 0);

            while (currentHour <= endHour)
            {
                if (!hoursWithData.Contains(currentHour))
                {
                    gapTimestamps.Add(currentHour);
                }
                currentHour = currentHour.AddHours(1);
            }

            // Se non ci sono gap, ritorna subito
            if (gapTimestamps.Count == 0)
            {
                await _logger.Info(source, $"No gaps found for vehicle {vehicleId} in period {startTime:dd/MM/yyyy} - {endTime:dd/MM/yyyy}");
                return results;
            }

            // PASSO 3: Identifica quali ore necessitano JSON per analisi batteria/km
            // (solo le ore immediatamente prima e dopo ogni gap)
            var hoursNeedingJson = new HashSet<DateTime>();
            foreach (var gapTime in gapTimestamps)
            {
                // Trova l'ora con dati più vicina prima del gap
                var prevHour = gapTime.AddHours(-1);
                while (prevHour >= startTime && !hoursWithData.Contains(prevHour))
                    prevHour = prevHour.AddHours(-1);
                if (hoursWithData.Contains(prevHour))
                    hoursNeedingJson.Add(prevHour);

                // Trova l'ora con dati più vicina dopo del gap
                var nextHour = gapTime.AddHours(1);
                while (nextHour <= endTime && !hoursWithData.Contains(nextHour))
                    nextHour = nextHour.AddHours(1);
                if (hoursWithData.Contains(nextHour))
                    hoursNeedingJson.Add(nextHour);
            }

            // PASSO 4: Carica JSON solo per le ore necessarie
            // Filtra i timestamp già presenti nel database invece di caricare tutto e filtrare in memoria
            var recordsWithJson = new Dictionary<DateTime, VehicleDataRecord>();
            if (hoursNeedingJson.Count > 0)
            {
                // Trova i timestamp esatti da caricare (solo uno per ogni ora necessaria)
                var timestampsToLoad = existingTimestamps
                    .Where(t => hoursNeedingJson.Contains(new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0)))
                    .GroupBy(t => new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0))
                    .Select(g => g.First())  // Solo il primo timestamp per ogni ora
                    .ToList();

                if (timestampsToLoad.Count > 0)
                {
                    // Query mirata: carica JSON solo per i timestamp specifici
                    var jsonRecords = await _db.VehiclesData
                        .Where(vd => vd.VehicleId == vehicleId && timestampsToLoad.Contains(vd.Timestamp))
                        .Select(vd => new { vd.Timestamp, vd.RawJsonAnonymized })
                        .ToListAsync();

                    foreach (var record in jsonRecords)
                    {
                        var hourKey = new DateTime(record.Timestamp.Year, record.Timestamp.Month, record.Timestamp.Day, record.Timestamp.Hour, 0, 0);
                        recordsWithJson[hourKey] = new VehicleDataRecord(record.Timestamp, record.RawJsonAnonymized);
                    }
                }
            }

            // Recupera log di fallimento nel periodo
            var failureLogs = await _db.FetchFailureLogs
                .Where(f => f.VehicleId == vehicleId && f.AttemptedAt >= startTime && f.AttemptedAt <= endTime)
                .ToListAsync();

            var failureLookup = failureLogs
                .GroupBy(f => new DateTime(f.AttemptedAt.Year, f.AttemptedAt.Month, f.AttemptedAt.Day, f.AttemptedAt.Hour, 0, 0))
                .ToDictionary(g => g.Key, g => g.First());

            // Calcola pattern storici del veicolo
            var historicalPattern = await CalculateHistoricalPatternAsync(vehicleId);

            // PASSO 5: Analizza ogni gap
            foreach (var gapTime in gapTimestamps)
            {
                var gapResult = AnalyzeSingleGap(
                    gapTime,
                    recordsWithJson,
                    hoursWithData,
                    failureLookup,
                    historicalPattern
                );

                results.Add(gapResult);
            }

            await _logger.Info(source, $"Found {results.Count} gaps for vehicle {vehicleId} in period {startTime:dd/MM/yyyy} - {endTime:dd/MM/yyyy}",
                $"Loaded JSON for {recordsWithJson.Count} records (optimized from {existingTimestamps.Count} total)");

            return results;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error analyzing gaps for vehicle {vehicleId}", ex.ToString());
            return results;
        }
    }

    /// <summary>
    /// Analizza un singolo gap e calcola la confidenza.
    /// Usa hoursWithData (HashSet leggero) per verifiche di esistenza,
    /// e recordsWithJson (solo record necessari) per le analisi richieste.
    /// </summary>
    private static GapAnalysisResult AnalyzeSingleGap(
        DateTime gapTimestamp,
        Dictionary<DateTime, VehicleDataRecord> recordsWithJson,
        HashSet<DateTime> hoursWithData,
        Dictionary<DateTime, FetchFailureLog> failureLookup,
        HistoricalUsagePattern historicalPattern)
    {
        var factors = new GapAnalysisFactors();

        // 1. Analizza continuità temporale
        var previousHour = gapTimestamp.AddHours(-1);
        var nextHour = gapTimestamp.AddHours(1);

        factors.HasPreviousRecord = hoursWithData.Contains(previousHour);
        factors.HasNextRecord = hoursWithData.Contains(nextHour);

        double continuityScore;
        if (factors.HasPreviousRecord && factors.HasNextRecord)
            continuityScore = 1.0;  // Record sia prima che dopo
        else if (factors.HasPreviousRecord || factors.HasNextRecord)
            continuityScore = 0.6;  // Solo uno dei due
        else
            continuityScore = 0.2;  // Nessuno dei due

        // 2. Analizza progressione batteria e km (usa recordsWithJson con solo i JSON necessari)
        var (batteryScore, kmBonus) = AnalyzeBatteryAndKmProgression(gapTimestamp, recordsWithJson, factors);

        // 3. Analizza pattern di utilizzo
        double patternScore = AnalyzeUsagePattern(gapTimestamp, historicalPattern, factors);

        // 4. Analizza lunghezza gap
        double gapLengthScore = AnalyzeGapLength(gapTimestamp, hoursWithData, factors);

        // 5. Analizza storico veicolo
        double historicalScore = historicalPattern.OverallReliability;

        // 6. Bonus se c'è un failure log documentato (problema tecnico)
        double technicalBonus = 0;
        if (failureLookup.TryGetValue(gapTimestamp, out var failureLog))
        {
            factors.FailureReason = failureLog.FailureReason;
            factors.IsTechnicalFailure = FetchFailureReason.IsTechnicalFailure(failureLog.FailureReason);

            if (factors.IsTechnicalFailure)
            {
                technicalBonus = AppConfig.GAP_ANALYSIS_WEIGHT_TECH_PROBLEM;
            }
        }

        // Calcola confidenza totale
        double confidence =
            (continuityScore * AppConfig.GAP_ANALYSIS_WEIGHT_CONTINUITY * 100) +
            (batteryScore * AppConfig.GAP_ANALYSIS_WEIGHT_BATTERY * 100) +
            (patternScore * AppConfig.GAP_ANALYSIS_WEIGHT_PATTERN * 100) +
            (gapLengthScore * AppConfig.GAP_ANALYSIS_WEIGHT_GAP_LENGTH * 100) +
            (historicalScore * AppConfig.GAP_ANALYSIS_WEIGHT_HISTORICAL * 100) +
            technicalBonus +
            kmBonus;

        // Limita tra 0 e 100
        confidence = Math.Max(0, Math.Min(100, confidence));

        // Genera giustificazione testuale
        var justification = GenerateJustification(factors);

        return new GapAnalysisResult
        {
            GapTimestamp = gapTimestamp,
            ConfidencePercentage = Math.Round(confidence, 1),
            Justification = justification,
            Factors = factors
        };
    }

    /// <summary>
    /// Analizza la progressione della batteria e km tra i record adiacenti.
    /// Restituisce lo score batteria e imposta un eventuale bonus km nei factors.
    /// </summary>
    private static (double batteryScore, double kmBonus) AnalyzeBatteryAndKmProgression(
        DateTime gapTimestamp,
        Dictionary<DateTime, VehicleDataRecord> recordLookup,
        GapAnalysisFactors factors)
    {
        double batteryScore = 0.5;
        double kmBonus = 0;

        try
        {
            // Trova record più vicini prima e dopo
            var previousRecord = recordLookup
                .Where(r => r.Key < gapTimestamp)
                .OrderByDescending(r => r.Key)
                .FirstOrDefault();

            var nextRecord = recordLookup
                .Where(r => r.Key > gapTimestamp)
                .OrderBy(r => r.Key)
                .FirstOrDefault();

            if (previousRecord.Value == null || nextRecord.Value == null)
                return (0.5, 0); // Score neutro se mancano dati

            // Estrai livello batteria dai JSON
            var prevBattery = ExtractBatteryLevel(previousRecord.Value.RawJsonAnonymized);
            var nextBattery = ExtractBatteryLevel(nextRecord.Value.RawJsonAnonymized);

            if (prevBattery.HasValue && nextBattery.HasValue)
            {
                factors.BatteryDeltaPrevious = prevBattery;
                factors.BatteryDeltaNext = nextBattery;

                // Calcola se la progressione è coerente
                var batteryDrop = prevBattery.Value - nextBattery.Value;
                var hoursDiff = (nextRecord.Key - previousRecord.Key).TotalHours;

                if (hoursDiff > 0)
                {
                    var dropPerHour = batteryDrop / hoursDiff;

                    // Una progressione normale è 1-5% per ora di utilizzo, 0-1% in idle
                    if (dropPerHour >= -1 && dropPerHour <= 10)
                        batteryScore = 0.9; // Progressione molto coerente
                    else if (dropPerHour >= -5 && dropPerHour <= 15)
                        batteryScore = 0.7; // Progressione accettabile
                    else if (dropPerHour < 0)
                        batteryScore = 0.8; // Batteria aumentata (ricarica) - coerente
                    else
                        batteryScore = 0.4; // Progressione anomala
                }
            }

            // Estrai odometro per calcolare bonus km
            var prevOdometer = ExtractOdometer(previousRecord.Value.RawJsonAnonymized);
            var nextOdometer = ExtractOdometer(nextRecord.Value.RawJsonAnonymized);

            if (prevOdometer.HasValue && nextOdometer.HasValue)
            {
                var kmDelta = nextOdometer.Value - prevOdometer.Value;
                factors.OdometerDelta = kmDelta;

                // Se il veicolo ha percorso almeno AppConfig.GAP_ANALYSIS_KM_THRESHOLD km, applica bonus
                if (kmDelta >= AppConfig.GAP_ANALYSIS_KM_THRESHOLD)
                {
                    kmBonus = AppConfig.GAP_ANALYSIS_KM_BONUS;
                }
            }

            return (batteryScore, kmBonus);
        }
        catch
        {
            return (0.5, 0); // Score neutro in caso di errore
        }
    }

    /// <summary>
    /// Estrae il livello batteria da un JSON di dati veicolo
    /// </summary>
    private static double? ExtractBatteryLevel(string rawJson)
    {
        try
        {
            var doc = JsonDocument.Parse(rawJson);

            // Cerca in vari possibili path
            if (doc.RootElement.TryGetProperty("response", out var response))
            {
                if (response.TryGetProperty("charge_state", out var chargeState))
                {
                    if (chargeState.TryGetProperty("battery_level", out var battery))
                    {
                        return battery.GetDouble();
                    }
                }
            }

            // Cerca direttamente
            if (doc.RootElement.TryGetProperty("charge_state", out var cs))
            {
                if (cs.TryGetProperty("battery_level", out var b))
                {
                    return b.GetDouble();
                }
            }

            // Cerca battery_level diretto
            if (doc.RootElement.TryGetProperty("battery_level", out var bl))
            {
                return bl.GetDouble();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Estrae l'odometro (km) da un JSON di dati veicolo
    /// </summary>
    private static double? ExtractOdometer(string rawJson)
    {
        try
        {
            var doc = JsonDocument.Parse(rawJson);

            // Cerca in vari possibili path
            if (doc.RootElement.TryGetProperty("response", out var response))
            {
                if (response.TryGetProperty("vehicle_state", out var vehicleState))
                {
                    if (vehicleState.TryGetProperty("odometer", out var odometer))
                    {
                        // Tesla restituisce odometro in miglia, convertiamo in km
                        return odometer.GetDouble() * 1.60934;
                    }
                }
            }

            // Cerca direttamente
            if (doc.RootElement.TryGetProperty("vehicle_state", out var vs))
            {
                if (vs.TryGetProperty("odometer", out var o))
                {
                    return o.GetDouble() * 1.60934;
                }
            }

            // Cerca odometer diretto
            if (doc.RootElement.TryGetProperty("odometer", out var od))
            {
                return od.GetDouble() * 1.60934;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Analizza se l'ora del gap rientra nei pattern tipici di utilizzo
    /// </summary>
    private static double AnalyzeUsagePattern(DateTime gapTimestamp, HistoricalUsagePattern pattern, GapAnalysisFactors factors)
    {
        var hour = gapTimestamp.Hour;
        var dayOfWeek = gapTimestamp.DayOfWeek;

        // Controlla se è un'ora tipica di utilizzo
        factors.IsWithinTypicalUsageHours = pattern.TypicalHours.Contains(hour);

        if (factors.IsWithinTypicalUsageHours)
            return 0.9; // Alta probabilità durante ore tipiche

        // Ore notturne (00-06) hanno minore probabilità di utilizzo attivo
        if (hour >= 0 && hour < 6)
            return 0.6; // Ma il veicolo potrebbe comunque essere in sosta/ricarica

        // Weekend potrebbero avere pattern diversi
        if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            return 0.7;

        return 0.5; // Score neutro per altri casi
    }

    /// <summary>
    /// Analizza la lunghezza del gap (gap singoli sono più probabili).
    /// OTTIMIZZATO: usa HashSet per O(1) lookup invece di Dictionary.
    /// </summary>
    private static double AnalyzeGapLength(DateTime gapTimestamp, HashSet<DateTime> hoursWithData, GapAnalysisFactors factors)
    {
        // Conta gap consecutivi
        int consecutiveGaps = 1;

        // Conta gap precedenti
        var checkTime = gapTimestamp.AddHours(-1);
        while (!hoursWithData.Contains(checkTime) && consecutiveGaps < 24)
        {
            consecutiveGaps++;
            checkTime = checkTime.AddHours(-1);
        }

        // Conta gap successivi
        checkTime = gapTimestamp.AddHours(1);
        while (!hoursWithData.Contains(checkTime) && consecutiveGaps < 48)
        {
            consecutiveGaps++;
            checkTime = checkTime.AddHours(1);
        }

        factors.ConsecutiveGapHours = consecutiveGaps;

        // Gap singoli o brevi sono più probabilmente problemi tecnici temporanei
        return consecutiveGaps switch
        {
            1 => 0.95,      // Gap singolo - molto probabilmente problema tecnico
            2 => 0.85,      // 2 ore consecutive
            <= 4 => 0.70,   // Fino a 4 ore
            <= 8 => 0.50,   // Fino a 8 ore
            <= 24 => 0.30,  // Fino a un giorno
            _ => 0.15       // Più di un giorno - problematico
        };
    }

    /// <summary>
    /// Calcola il pattern storico di utilizzo del veicolo
    /// </summary>
    private async Task<HistoricalUsagePattern> CalculateHistoricalPatternAsync(int vehicleId)
    {
        var pattern = new HistoricalUsagePattern();

        try
        {
            // Analisi storica in base ad un periodo parametrizzato
            var historicalDaysAgo = DateTime.Now.AddHours(-AppConfig.MONTHLY_HOURS_THRESHOLD);
            var records = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= historicalDaysAgo)
                .Select(vd => vd.Timestamp)
                .ToListAsync();

            if (records.Count == 0)
            {
                pattern.OverallReliability = 0.5;
                return pattern;
            }

            // Calcola ore tipiche di utilizzo
            var hourCounts = records
                .GroupBy(r => r.Hour)
                .OrderByDescending(g => g.Count())
                .Take(12)
                .Select(g => g.Key)
                .ToList();

            pattern.TypicalHours = hourCounts;

            // Calcola affidabilità complessiva (quante ore su TOT ore, hanno dati)
            var expectedHours = Math.Min(AppConfig.MONTHLY_HOURS_THRESHOLD, (DateTime.Now - records.Min()).TotalHours);
            var actualHours = records.Select(r => new DateTime(r.Year, r.Month, r.Day, r.Hour, 0, 0)).Distinct().Count();

            pattern.OverallReliability = expectedHours > 0 ? Math.Min(1, actualHours / expectedHours) : 0.5;
        }
        catch
        {
            pattern.OverallReliability = 0.5;
        }

        return pattern;
    }

    /// <summary>
    /// Genera una giustificazione testuale per il gap
    /// </summary>
    private static string GenerateJustification(GapAnalysisFactors factors)
    {
        var parts = new List<string>();

        // Continuità
        if (factors.HasPreviousRecord && factors.HasNextRecord)
            parts.Add("dati disponibili nelle ore adiacenti");
        else if (factors.HasPreviousRecord)
            parts.Add("dato disponibile nell'ora precedente");
        else if (factors.HasNextRecord)
            parts.Add("dato disponibile nell'ora successiva");

        // Problema tecnico documentato
        if (factors.IsTechnicalFailure && !string.IsNullOrEmpty(factors.FailureReason))
            parts.Add($"causa tecnica documentata: {FetchFailureReason.GetDescription(factors.FailureReason)}");

        // Gap length
        if (factors.ConsecutiveGapHours == 1)
            parts.Add("gap isolato di una singola ora");
        else if (factors.ConsecutiveGapHours <= 3)
            parts.Add($"gap breve di {factors.ConsecutiveGapHours} ore consecutive");

        // Pattern
        if (factors.IsWithinTypicalUsageHours)
            parts.Add("orario rientra nel pattern tipico di utilizzo");

        // Batteria
        if (factors.BatteryDeltaPrevious.HasValue && factors.BatteryDeltaNext.HasValue)
            parts.Add("progressione batteria coerente con utilizzo normale");

        // Km percorsi
        if (factors.OdometerDelta.HasValue && factors.OdometerDelta.Value >= AppConfig.GAP_ANALYSIS_KM_THRESHOLD)
            parts.Add($"veicolo in movimento ({factors.OdometerDelta.Value:F1} km percorsi)");

        var justification = parts.Count > 0
            ? $"Analisi basata su: {string.Join("; ", parts)}."
            : "Analisi basata su dati storici del veicolo.";

        return justification;
    }

    /// <summary>
    /// Verifica se un report ha gap non certificati.
    /// Usa la stessa finestra temporale del PDF.
    /// </summary>
    public async Task<GapCertificationStatus> GetGapStatusForReportAsync(int pdfReportId)
    {
        const string source = "GapAnalysisService.GetGapStatusForReport";

        try
        {
            var report = await _db.PdfReports.FirstOrDefaultAsync(r => r.Id == pdfReportId);
            if (report == null)
            {
                return new GapCertificationStatus { HasUncertifiedGaps = false };
            }

            // Usa AnalyzeGapsForReportAsync che calcola la finestra corretta
            var gaps = await AnalyzeGapsForReportAsync(pdfReportId);

            // Controlla quanti sono già certificati
            var certifiedGaps = await _db.GapCertifications
                .Where(gc => gc.PdfReportId == pdfReportId)
                .Select(gc => gc.GapTimestamp)
                .ToListAsync();

            var uncertifiedCount = gaps.Count(g => !certifiedGaps.Contains(g.GapTimestamp));

            // Controlla se esiste già un PDF di certificazione
            var hasCertificationPdf = await _db.GapCertifications
                .AnyAsync(gc => gc.PdfReportId == pdfReportId && !string.IsNullOrEmpty(gc.CertificationHash));

            return new GapCertificationStatus
            {
                HasUncertifiedGaps = uncertifiedCount > 0,
                TotalGaps = gaps.Count,
                UncertifiedGaps = uncertifiedCount,
                CertifiedGaps = certifiedGaps.Count,
                HasCertificationPdf = hasCertificationPdf
            };
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error getting gap status for report {pdfReportId}", ex.ToString());
            return new GapCertificationStatus { HasUncertifiedGaps = false };
        }
    }

    /// <summary>
    /// Certifica i gap per un report e li salva nel database.
    /// Usa la stessa finestra temporale del PDF.
    /// </summary>
    public async Task<List<GapCertification>> CertifyGapsForReportAsync(int pdfReportId)
    {
        const string source = "GapAnalysisService.CertifyGapsForReport";
        var certifications = new List<GapCertification>();

        try
        {
            var report = await _db.PdfReports.FirstOrDefaultAsync(r => r.Id == pdfReportId);
            if (report == null)
            {
                await _logger.Warning(source, $"Report {pdfReportId} not found");
                return certifications;
            }

            // Usa AnalyzeGapsForReportAsync che calcola la finestra corretta
            var gaps = await AnalyzeGapsForReportAsync(pdfReportId);

            // Rimuovi certificazioni esistenti per questo report (ricertificazione)
            var existingCerts = await _db.GapCertifications
                .Where(gc => gc.PdfReportId == pdfReportId)
                .ToListAsync();

            if (existingCerts.Count != 0)
            {
                _db.GapCertifications.RemoveRange(existingCerts);
                await _db.SaveChangesAsync();
            }

            // Crea nuove certificazioni
            foreach (var gap in gaps)
            {
                var certifiedAt = DateTime.Now;
                var hashData = $"{gap.GapTimestamp:O}|{gap.ConfidencePercentage}|{gap.Justification}|{certifiedAt:O}";

                var certification = new GapCertification
                {
                    VehicleId = report.VehicleId,
                    PdfReportId = pdfReportId,
                    GapTimestamp = gap.GapTimestamp,
                    ConfidencePercentage = gap.ConfidencePercentage,
                    JustificationText = gap.Justification,
                    AnalysisFactorsJson = JsonSerializer.Serialize(gap.Factors),
                    CertifiedAt = certifiedAt,
                    CertificationHash = GenericHelpers.ComputeContentHash(hashData)
                };

                certifications.Add(certification);
                _db.GapCertifications.Add(certification);
            }

            await _db.SaveChangesAsync();

            await _logger.Info(source, $"Certified {certifications.Count} gaps for report {pdfReportId}");

            return certifications;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error certifying gaps for report {pdfReportId}", ex.ToString());
            return certifications;
        }
    }
}

/// <summary>
/// Pattern storico di utilizzo del veicolo
/// </summary>
public class HistoricalUsagePattern
{
    public List<int> TypicalHours { get; set; } = [];
    public double OverallReliability { get; set; } = 0.5;
}

/// <summary>
/// Record dati veicolo per l'analisi gap (sostituisce tipo anonimo)
/// </summary>
public record VehicleDataRecord(DateTime Timestamp, string RawJsonAnonymized);

/// <summary>
/// Status della certificazione gap per un report
/// </summary>
public class GapCertificationStatus
{
    public bool HasUncertifiedGaps { get; set; }
    public int TotalGaps { get; set; }
    public int UncertifiedGaps { get; set; }
    public int CertifiedGaps { get; set; }
    public bool HasCertificationPdf { get; set; }
}