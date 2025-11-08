using System.Text;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Servizio dedicato ESCLUSIVAMENTE alla certificazione DataPolar
/// Gestisce: certificazioni generali, statistiche mensili, tabelle dettagliate log
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
    /// Genera il blocco HTML completo della certificazione DataPolar
    /// Include: certificazione generale + statistiche mensili + tabella dettagliata
    /// </summary>
    public async Task<string> GenerateCompleteCertificationHtmlAsync(
        int vehicleId, 
        TimeSpan totalMonitoringPeriod, 
        DateTime lifetimeFirstRecord,        // ✅ Rinominato per chiarezza
        DateTime lifetimeLastRecord,         // ✅ Rinominato per chiarezza
        int lifetimeTotalRecords,            // ✅ Rinominato per chiarezza
        DateTime periodFirstRecord,          // ✅ NUOVO - periodo immutabile
        DateTime periodLastRecord,           // ✅ NUOVO - periodo immutabile
        int periodTotalRecords,              // ✅ NUOVO - periodo immutabile
        PdfReport report)
    {
        try
        {
            // ✅ Usa dati LIFETIME per certificazione generale
            var certification = GenerateDataCertificationHtml(
                lifetimeTotalRecords, 
                lifetimeFirstRecord, 
                lifetimeLastRecord, 
                totalMonitoringPeriod);

            // ✅ Usa dati PERIODO per statistiche mensili (IMMUTABILI)
            var statistics = await GenerateMonthlyStatisticsHtml(
                periodTotalRecords,          // ✅ Dati del periodo
                totalMonitoringPeriod, 
                periodFirstRecord,           // ✅ Dati del periodo
                periodLastRecord,            // ✅ Dati del periodo
                vehicleId,
                MONTHLY_HOURS_THRESHOLD
            );
            
            // ✅ Tabella dettagliata usa sempre il periodo del report
            var detailedTable = await GenerateDetailedLogTableAsync(
                vehicleId, 
                report.ReportPeriodStart,    // ✅ Periodo immutabile
                report.ReportPeriodEnd       // ✅ Periodo immutabile
            );

            return $@"
                <div class='certification-datapolar'>
                    <h4 class='certification-datapolar-generic'>📋 Statistiche Generali (Lifetime)</h4>
                    {certification}
                    
                    <h4>📊 Statistiche Analisi Mensile</h4>
                    {statistics}
                    
                    <h4 class='detailed-log-table-title'>💽 Tabella Dettagliata Log Timestamp Certificati - Periodo Report</h4>
                    {detailedTable}
                </div>";
        }
        catch (Exception ex)
        {
            await _logger.Error("DataPolarCertification.GenerateCompleteCertification",
                "Errore generazione certificazione", ex.ToString());
            return "<div class='certification-error'>⚠️ Certificazione dati non disponibile.</div>";
        }
    }

    /// <summary>
    /// 📊 Genera certificazione dati generale in formato HTML usando dati già calcolati
    /// </summary>
    public string GenerateDataCertificationHtml(
        int totalRecords,
        DateTime firstRecord,
        DateTime lastRecord,
        TimeSpan totalMonitoringPeriod)
    {
        if (firstRecord == default || lastRecord == default || totalRecords == 0)
        {
            return "<p class='cert-warning'>⚠️ Dati insufficienti per certificazione</p>";
        }

        var actualMonitoringPeriod = lastRecord - firstRecord;
        var totalCertifiedHours = Math.Max(actualMonitoringPeriod.TotalHours, 1); // Evita divisione per zero

        var uptimePercentage = Math.Min(95.0, 80.0 + (totalRecords / totalCertifiedHours) * 10);
        var qualityScore = CalculateQualityScore(totalRecords, uptimePercentage, totalMonitoringPeriod);
        var qualityStars = GetQualityStars(qualityScore);

        var firstRecordDate = firstRecord.ToString("dd/MM/yyyy").Replace("-", "/");
        var lastRecordDate = lastRecord.ToString("dd/MM/yyyy").Replace("-", "/");
    
        return $@"
            <table class='certification-table'>
                <tr><td>Ore totali certificate</td><td>{totalCertifiedHours:F0} ore ({totalCertifiedHours / 24:0} giorni)</td></tr>
                <tr><td>Uptime raccolta</td><td>{uptimePercentage:0}%</td></tr>
                <tr><td>Qualità dataset</td><td>{qualityStars} ({GetQualityLabel(qualityScore)})</td></tr>
                <tr><td>Primo record</td><td>{firstRecordDate:dd/MM/yyyy} alle {firstRecord:HH:mm}</td></tr>
                <tr><td>Ultimo record</td><td>{lastRecordDate:dd/MM/yyyy} alle {lastRecord:HH:mm}</td></tr>
                <tr><td>Records totali (lifetime)</td><td>{totalRecords:N0}</td></tr>
                <tr><td>Frequenza media</td><td>{(totalRecords / totalCertifiedHours):0} campioni/ora</td></tr>
            </table>";
    }

    /// <summary>
    /// 📊 Genera statistiche mensili in formato HTML
    /// </summary>
    private async Task<string> GenerateMonthlyStatisticsHtml(
        int monthlyRecords, 
        TimeSpan totalMonitoringPeriod, 
        DateTime firstRecord, 
        DateTime lastRecord,
        int vehicleId,
        int dataHours)
    {
        const int monthlyThreshold = MONTHLY_HOURS_THRESHOLD;
        
        // ✅ CONTROLLO SICUREZZA: Gestione valori di default
        if (firstRecord == default || lastRecord == default || monthlyRecords == 0)
        {
            return "<div class='detailed-log-table'><p class='cert-warning'>⚠️ Dati insufficienti per le statistiche mensili</p></div>";
        }

        // ✅ CONTROLLO SICUREZZA: Evita date non valide
        if (firstRecord > lastRecord)
        {
            // Se le date sono invertite, scambiale
            var temp = firstRecord;
            firstRecord = lastRecord;
            lastRecord = temp;
        }

        // ✅ CONTROLLO SICUREZZA: Calcola le ore effettive con limiti ragionevoli
        var timeDifference = lastRecord - firstRecord;
        var actualMonthlyHours = Math.Max(timeDifference.TotalHours, 1);

        // ✅ CONTROLLO SICUREZZA: Limita il periodo massimo per evitare calcoli eccessivi
        var maxAllowedHours = MAX_YEAR_DATA_PERIOD;
        actualMonthlyHours = Math.Min(actualMonthlyHours, maxAllowedHours);
        
        // ✅ CONTROLLO SICUREZZA: Gestione periodi temporali validi
        var now = DateTime.Now;
        var maxStartTime = now.AddHours(-dataHours);
        var effectiveStartTime = firstRecord > maxStartTime ? firstRecord : maxStartTime;
        
        // ✅ CONTROLLO SICUREZZA: Evita date future
        if (effectiveStartTime > now)
        {
            effectiveStartTime = now.AddHours(-1); // Usa un'ora indietro come fallback
        }
        
        var startTime = new DateTime(
            effectiveStartTime.Year,
            effectiveStartTime.Month,
            effectiveStartTime.Day,
            effectiveStartTime.Hour,
            0, 0
        );
        
        // ✅ CONTROLLO SICUREZZA: Limita le ore da mostrare
        var actualHoursToShow = (int)Math.Ceiling((now - startTime).TotalHours);
        actualHoursToShow = Math.Max(1, Math.Min(actualHoursToShow, maxAllowedHours));
        
        // Conta record certificati nel periodo della tabella dettagliata
        var certifiedRecords = await _dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= startTime)
            .CountAsync();
        
        var totalExpectedRecords = actualHoursToShow;
        
        // ✅ CONTROLLO SICUREZZA: Evita divisione per zero
        var completeness = totalExpectedRecords > 0 
            ? (certifiedRecords / (double)totalExpectedRecords * 100) 
            : 0;

        // ✅ CONTROLLO SICUREZZA: Calcoli protetti per frequenza campionamento
        var samplingFrequency = monthlyRecords > 0 && timeDifference.TotalMinutes > 0 
            ? timeDifference.TotalMinutes / monthlyRecords 
            : 0;

        // ✅ CONTROLLO SICUREZZA: Calcolo densità dati sicuro
        var dataDensity = actualMonthlyHours > 0 
            ? monthlyRecords / actualMonthlyHours 
            : 0;

        // ✅ CONTROLLO SICUREZZA: Calcolo copertura dati sicuro
        var totalMonitoringHours = Math.Max(totalMonitoringPeriod.TotalHours, 1);
        var coveragePercentage = Math.Min(100, (actualMonthlyHours / totalMonitoringHours) * 100);
        
        return $@"
            <table class='statistics-table'>
                <tr><td>Durata monitoraggio totale</td><td>{totalMonitoringPeriod.TotalDays:0} giorni</td></tr>
                <tr><td>Campioni mensili analizzati</td><td>{monthlyRecords:N0}</td></tr>
                <tr><td>Finestra unificada</td><td>{monthlyThreshold} ore ({PROD_MONTHLY_REPEAT_DAYS} giorni)</td></tr>
                <tr><td>Densità dati mensile</td><td>{dataDensity:0} campioni/ora</td></tr>
                <tr><td>Frequenza campionamento</td><td>{samplingFrequency:F2} min/campione</td></tr>
                <tr><td>Copertura dati</td><td>{coveragePercentage:0}% del periodo totale</td></tr>
                <tr><td>Ore monitorate</td><td>{actualHoursToShow} ore ({actualHoursToShow / 24.0:0} giorni)</td></tr>
                <tr><td>N. Record certificati</td><td>{certifiedRecords:N0} su {totalExpectedRecords}</td></tr>
                <tr><td>Percentuale completezza</td><td>{completeness:0}%</td></tr>
                <tr><td>Strategia</td><td>Analisi mensile consistente con context evolutivo</td></tr>
            </table>";
    }

    /// <summary>
    /// 📋 Genera tabella dettagliata dei record certificati per il periodo specifico
    /// </summary>
    private async Task<string> GenerateDetailedLogTableAsync(
        int vehicleId, 
        DateTime reportStart,    // ✅ NUOVO - periodo immutabile
        DateTime reportEnd)      // ✅ NUOVO - periodo immutabile
    {
        var sb = new StringBuilder();

        // ✅ Usa il periodo del report (immutabile)
        var startTime = new DateTime(
            reportStart.Year,
            reportStart.Month,
            reportStart.Day,
            reportStart.Hour,
            0, 0
        );

        var endTime = reportEnd;

        // Recupera i record effettivi del periodo
        var actualRecords = await _dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId && 
                        vd.Timestamp >= startTime && 
                        vd.Timestamp <= endTime)
            .OrderBy(vd => vd.Timestamp)
            .Select(vd => new { vd.Timestamp, vd.IsSmsAdaptiveProfile, vd.RawJsonAnonymized })
            .ToListAsync();

        if (!actualRecords.Any())
        {
            return "<div class='detailed-log-table'><p class='cert-warning'>⚠️ Nessun dato disponibile per il periodo del report</p></div>";
        }

        var recordLookup = actualRecords
            .GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, 0, 0))
            .ToDictionary(g => g.Key, g => g.First());

        // Genera tabella
        sb.AppendLine("<table class='detailed-log-table'>");
        sb.AppendLine("<thead>");
        sb.AppendLine("<tr>");
        sb.AppendLine("<th>Timestamp</th>");
        sb.AppendLine("<th>Adaptive Profile</th>");
        sb.AppendLine("<th>Dati Operativi</th>");
        sb.AppendLine("</tr>");
        sb.AppendLine("</thead>");
        sb.AppendLine("<tbody>");

        // ✅ Genera righe solo per il periodo del report
        var currentTime = startTime;
        while (currentTime <= endTime)
        {
            if (recordLookup.TryGetValue(currentTime, out var record))
            {
                var adaptiveProfile = record.IsSmsAdaptiveProfile
                    ? "<span class='detailed-log-badge-success-profile'>Sì</span>"
                    : "<span class='detailed-log-badge-default-profile'>No</span>";

                var datiOperativi = !string.IsNullOrEmpty(record.RawJsonAnonymized)
                    ? "<span class='detailed-log-badge-success-dataValidated'>Dati operativi raccolti</span>"
                    : "<span class='detailed-log-badge-warning-dataValidated'>Dati operativi da validare</span>";
                
                var formattedDate = $"{record.Timestamp:dd-MM-yyyy}".Replace("-", "/");
                
                sb.AppendLine("<tr class='record-present'>");
                sb.AppendLine($"<td>{formattedDate} - {record.Timestamp:HH:mm}</td>");
                sb.AppendLine($"<td>{adaptiveProfile}</td>");
                sb.AppendLine($"<td>{datiOperativi}</td>");
                sb.AppendLine("</tr>");
            }
            else
            {
                sb.AppendLine("<tr class='record-missing'>");
                sb.AppendLine($"<td>{currentTime:dd-MM-yyyy} - {currentTime:HH:mm}</td>");
                sb.AppendLine("<td><span class='badge-default'>No</span></td>");
                sb.AppendLine("<td><span class='badge-warning'>Dati operativi da validare</span></td>");
                sb.AppendLine("</tr>");
            }

            currentTime = currentTime.AddHours(1);
        }

        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");

        return sb.ToString();
    }

    /// <summary>
    /// Calcola qualità dataset
    /// </summary>
    private static double CalculateQualityScore(int totalRecords, double uptimePercentage, TimeSpan monitoringPeriod)
    {
        double score = 0;
        
        // 40% - Uptime
        score += (uptimePercentage / 100) * 40;
        
        // 30% - Densità records (migliorata)
        var recordDensity = totalRecords / Math.Max(monitoringPeriod.TotalHours, 1);
        var densityScore = Math.Min(1, recordDensity / 2.0); // ✅ Aumentato da 1.0 a 2.0
        score += densityScore * 30;
        
        // 20% - Stabilità (dinamica basata su continuità)
        var stabilityScore = CalculateStability(monitoringPeriod);
        score += stabilityScore * 20;
        
        // 10% - Maturità dataset
        var totalHours = monitoringPeriod.TotalHours;
        if (totalHours >= MONTHLY_HOURS_THRESHOLD) score += 10;      // 30+ giorni
        else if (totalHours >= WEEKLY_HOURS_THRESHOLD) score += 7;   // 7+ giorni  
        else if (totalHours >= DAILY_HOURS_THRESHOLD) score += 3;    // 1+ giorno
        
        return Math.Max(0, Math.Min(100, score));
    }

    /// <summary>
    /// Calcola stabilità basandosi sulla continuità del monitoraggio
    /// </summary>
    private static double CalculateStability(TimeSpan monitoringPeriod)
    {
        var hours = monitoringPeriod.TotalHours;
        
        // Più ore continuative = maggiore stabilità
        if (hours >= (MONTHLY_HOURS_THRESHOLD * 3)) return 1.0;   // 90+ giorni (3 mesi) = eccellente
        if (hours >= MONTHLY_HOURS_THRESHOLD) return 0.95;        // 30+ giorni (1 mese) = ottimo
        if (hours >= WEEKLY_HOURS_THRESHOLD) return 0.85;         // 7+ giorni (1 settimana) = buono
        if (hours >= (DAILY_HOURS_THRESHOLD * 3)) return 0.75;    // 3+ giorni = discreto
        if (hours >= DAILY_HOURS_THRESHOLD) return 0.60;          // 1+ giorno = sufficiente
        
        return 0.50; // < 24 ore = base
    }

    /// <summary>
    /// Converte score in stelle
    /// </summary>
    private static string GetQualityStars(double score)
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
    /// Etichetta qualitativa
    /// </summary>
    private static string GetQualityLabel(double score)
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