using System.Text;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Sistema di certificazione DataPolar per la qualità e tracciabilità dei dati telemetrici
/// </summary>
public class DataPolarCertification
{
    private readonly PolarDriveDbContext _dbContext;
    private readonly PolarDriveLogger _logger;

    public DataPolarCertification(PolarDriveDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = new PolarDriveLogger(_dbContext);
    }

    /// <summary>
    /// Genera il report completo di certificazione DataPolar con tabella dettagliata dei 720 record
    /// </summary>
    public async Task<string> GenerateCompleteCertificationReport(int vehicleId, TimeSpan totalMonitoringPeriod, int dataHours)
    {
        var sb = new StringBuilder();

        // 1. Certificazione qualità dati
        var certification = await GenerateDataCertification(vehicleId, totalMonitoringPeriod);
        sb.AppendLine(certification);
        sb.AppendLine();

        // 2. Statistiche analisi mensile
        sb.AppendLine(await GenerateMonthlyStatistics(vehicleId, totalMonitoringPeriod, dataHours));
        sb.AppendLine();

        // 3. Tabella dettagliata 720 record certificati
        sb.AppendLine(await GenerateDetailedDataTable(vehicleId, dataHours));

        return sb.ToString();
    }

    /// <summary>
    /// 🏆 CERTIFICAZIONE DATAPOLAR: Genera certificazione completa qualità dati
    /// </summary>
    private async Task<string> GenerateDataCertification(int vehicleId, TimeSpan totalMonitoringPeriod)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("📋 CERTIFICAZIONE DATI DATAPOLAR:");

            // 1️⃣ CALCOLO ORE TOTALI CERTIFICATE
            var totalRecords = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .CountAsync();

            var firstRecord = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var lastRecord = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .OrderByDescending(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            if (firstRecord == default || lastRecord == default)
            {
                sb.AppendLine("• Status: ⚠️ Dati insufficienti per certificazione");
                return sb.ToString();
            }

            var actualMonitoringPeriod = lastRecord - firstRecord;
            var totalCertifiedHours = actualMonitoringPeriod.TotalHours;

            // 2️⃣ CALCOLO UPTIME E GAP ANALYSIS
            var gaps = await AnalyzeDataGaps(vehicleId, firstRecord, lastRecord);
            var uptimePercentage = CalculateUptimePercentage(gaps, actualMonitoringPeriod);

            // 3️⃣ QUALITÀ DATASET
            var qualityScore = CalculateQualityScore(totalRecords, uptimePercentage, gaps.majorGaps, actualMonitoringPeriod);
            var qualityStars = GetQualityStars(qualityScore);

            // 4️⃣ OUTPUT CERTIFICAZIONE
            sb.AppendLine($"• Ore totali certificate: {totalCertifiedHours:F0} ore ({totalCertifiedHours / 24:F1} giorni)");
            sb.AppendLine($"• Uptime raccolta: {uptimePercentage:F1}%");
            sb.AppendLine($"• Gap maggiori: {gaps.majorGaps} interruzioni > 2h");
            sb.AppendLine($"• Qualità dataset: {qualityStars} ({GetQualityLabel(qualityScore)})");
            sb.AppendLine($"• Primo record: {firstRecord:yyyy-MM-dd HH:mm} UTC");
            sb.AppendLine($"• Ultimo record: {lastRecord:yyyy-MM-dd HH:mm} UTC");
            sb.AppendLine($"• Records totali: {totalRecords:N0}");
            sb.AppendLine($"• Frequenza media: {(totalRecords / Math.Max(totalCertifiedHours, 1)):F1} campioni/ora");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            await _logger.Error("DataPolarCertification.GenerateDataCertification",
                "Errore generazione certificazione", ex.ToString());
            return "📋 CERTIFICAZIONE DATI: ⚠️ Errore durante la certificazione";
        }
    }

    /// <summary>
    /// 📊 Genera statistiche di analisi mensile
    /// </summary>
    private async Task<string> GenerateMonthlyStatistics(int vehicleId, TimeSpan totalMonitoringPeriod, int dataHours)
    {
        var sb = new StringBuilder();
        
        var startTime = DateTime.Now.AddHours(-dataHours);
        var monthlyRecords = await _dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= startTime)
            .CountAsync();

        sb.AppendLine("📊 STATISTICHE ANALISI MENSILE:");
        sb.AppendLine($"• Durata monitoraggio totale: {totalMonitoringPeriod.TotalDays:F1} giorni");
        sb.AppendLine($"• Campioni mensili analizzati: {monthlyRecords:N0}");
        sb.AppendLine($"• Finestra unificata: {dataHours} ore (30 giorni)");
        sb.AppendLine($"• Densità dati mensile: {monthlyRecords / Math.Max(dataHours, 1):F1} campioni/ora");
        sb.AppendLine($"• Copertura dati: {Math.Min(100, (dataHours / Math.Max(totalMonitoringPeriod.TotalHours, 1)) * 100):F1}% del periodo totale");
        sb.AppendLine($"• Strategia: Analisi mensile consistente con context evolutivo");

        return sb.ToString();
    }

    /// <summary>
    /// 📋 Genera tabella dettagliata dei 720 record certificati (30 giorni x 24 ore)
    /// </summary>
    private async Task<string> GenerateDetailedDataTable(int vehicleId, int dataHours)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📋 TABELLA DETTAGLIATA DATI CERTIFICATI (720 ORE):");
        sb.AppendLine();

        // Header tabella
        sb.AppendLine("| Timestamp (UTC) | Laboratorio Mobile | Dati Operativi |");
        sb.AppendLine("|----------------|-------------------|----------------|");

        var startTime = DateTime.Now.AddHours(-dataHours);
        
        // Recupera tutti i record del periodo
        var actualRecords = await _dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= startTime)
            .OrderBy(vd => vd.Timestamp)
            .Select(vd => new { vd.Timestamp, vd.IsSmsAdaptiveProfiling, vd.RawJsonAnonymized })
            .ToListAsync();

        // Crea un dizionario per lookup rapido
        var recordLookup = actualRecords
            .GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, 0, 0))
            .ToDictionary(g => g.Key, g => g.First());

        // Genera 720 righe (30 giorni x 24 ore)
        for (int day = 0; day < 30; day++)
        {
            var currentDay = startTime.AddDays(day);
            
            for (int hour = 0; hour < 24; hour++)
            {
                var expectedTime = new DateTime(currentDay.Year, currentDay.Month, currentDay.Day, hour, 0, 0);
                
                // Verifica se esiste un record per questa ora
                if (recordLookup.TryGetValue(expectedTime, out var record))
                {
                    // Record esistente
                    var laboratorioMobile = record.IsSmsAdaptiveProfiling ? 
                        "<span style='background-color: #90EE90; padding: 2px 4px;'>Sì</span>" : "No";
                    
                    var datiOperativi = !string.IsNullOrEmpty(record.RawJsonAnonymized) ?
                        "Dati operativi raccolti" :
                        "<span style='background-color: #FFFF99; padding: 2px 4px;'>Dati operativi da validare</span>";

                    sb.AppendLine($"| {record.Timestamp:yyyy-MM-dd HH:mm} | {laboratorioMobile} | {datiOperativi} |");
                }
                else
                {
                    // Record mancante - usa sempre la logica standard
                    var datiOperativi = "<span style='background-color: #FFFF99; padding: 2px 4px;'>Dati operativi da validare</span>";
                    
                    sb.AppendLine($"| {expectedTime:yyyy-MM-dd HH:mm} | No | {datiOperativi} |");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine($"**TOTALE RECORD CERTIFICATI: {actualRecords.Count}/720**");
        sb.AppendLine($"**PERCENTUALE COMPLETEZZA: {(actualRecords.Count / 720.0 * 100):F1}%**");

        return sb.ToString();
    }

    /// <summary>
    /// 🔍 ANALISI GAP: Identifica interruzioni nella raccolta dati
    /// </summary>
    private async Task<(int totalGaps, int majorGaps, TimeSpan totalGapTime)> AnalyzeDataGaps(int vehicleId, DateTime firstRecord, DateTime lastRecord)
    {
        try
        {
            var timestamps = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .ToListAsync();

            if (timestamps.Count < 2)
                return (0, 0, TimeSpan.Zero);

            int totalGaps = 0;
            int majorGaps = 0; // > 2 ore
            TimeSpan totalGapTime = TimeSpan.Zero;

            for (int i = 1; i < timestamps.Count; i++)
            {
                var gap = timestamps[i] - timestamps[i - 1];

                // Considera gap se > 30 minuti (normale intervallo telemetria Tesla ~5-15 min)
                if (gap.TotalMinutes > 30)
                {
                    totalGaps++;
                    totalGapTime = totalGapTime.Add(gap);

                    // Gap maggiore se > 2 ore
                    if (gap.TotalHours > 2)
                    {
                        majorGaps++;
                    }
                }
            }

            return (totalGaps, majorGaps, totalGapTime);
        }
        catch (Exception ex)
        {
            await _logger.Error("DataPolarCertification.AnalyzeDataGaps",
                "Errore analisi gap", ex.ToString());
            return (0, 0, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// 📊 CALCOLO UPTIME: Percentuale di copertura temporale effettiva
    /// </summary>
    private double CalculateUptimePercentage((int totalGaps, int majorGaps, TimeSpan totalGapTime) gaps, TimeSpan actualMonitoringPeriod)
    {
        if (actualMonitoringPeriod.TotalHours <= 0)
            return 0;

        var activeTime = actualMonitoringPeriod - gaps.totalGapTime;
        return Math.Max(0, Math.Min(100, (activeTime.TotalHours / actualMonitoringPeriod.TotalHours) * 100));
    }

    /// <summary>
    /// ⭐ QUALITY SCORE: Calcola punteggio qualità dataset (0-100)
    /// </summary>
    private double CalculateQualityScore(int totalRecords, double uptimePercentage, int majorGaps, TimeSpan monitoringPeriod)
    {
        double score = 0;

        // 40% - Uptime (più importante)
        score += (uptimePercentage / 100) * 40;

        // 30% - Densità records (target: 1+ record/ora)
        var recordDensity = totalRecords / Math.Max(monitoringPeriod.TotalHours, 1);
        var densityScore = Math.Min(1, recordDensity / 1.0); // Normalizzato a 1 record/ora
        score += densityScore * 30;

        // 20% - Stabilità (penalità per gap maggiori)
        var stabilityPenalty = Math.Min(20, majorGaps * 2); // -2 punti per gap maggiore
        score += Math.Max(0, 20 - stabilityPenalty);

        // 10% - Maturità dataset (bonus per dataset maturi)
        if (monitoringPeriod.TotalDays >= 30) score += 10;
        else if (monitoringPeriod.TotalDays >= 7) score += 7;
        else if (monitoringPeriod.TotalDays >= 1) score += 3;

        return Math.Max(0, Math.Min(100, score));
    }

    /// <summary>
    /// ⭐ QUALITY STARS: Converte score in stelle visuali
    /// </summary>
    private string GetQualityStars(double score)
    {
        return score switch
        {
            >= 90 => "⭐⭐⭐⭐⭐",
            >= 80 => "⭐⭐⭐⭐⚪",
            >= 70 => "⭐⭐⭐⚪⚪",
            >= 60 => "⭐⭐⚪⚪⚪",
            >= 50 => "⭐⚪⚪⚪⚪",
            _ => "⚪⚪⚪⚪⚪"
        };
    }

    /// <summary>
    /// 🏷️ QUALITY LABEL: Etichetta qualitativa per il punteggio
    /// </summary>
    private string GetQualityLabel(double score)
    {
        return score switch
        {
            >= 90 => "Eccellente",
            >= 80 => "Ottimo",
            >= 70 => "Buono",
            >= 60 => "Discreto",
            >= 50 => "Sufficiente",
            _ => "Migliorabile"
        };
    }
}