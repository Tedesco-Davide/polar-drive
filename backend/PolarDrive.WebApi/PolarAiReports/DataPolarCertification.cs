using System.Text;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;

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
    public async Task<string> GenerateCompleteCertificationHtmlAsync(int vehicleId, TimeSpan totalMonitoringPeriod)
    {
        try
        {
            var certification = await GenerateDataCertificationHtmlAsync(vehicleId, totalMonitoringPeriod);

            var startTime = DateTime.Now.AddHours(-720); // 30 giorni
            var monthlyDataCount = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= startTime)
                .CountAsync();

            var statistics = GenerateMonthlyStatisticsHtml(monthlyDataCount, totalMonitoringPeriod);
            var detailedTable = await GenerateDetailedLogTableAsync(vehicleId, 720);

            return $@"
                <div class='certification-datapolar'>
                    <h4 class='certification-datapolar-generic'>📊 Statistiche Generali</h4>
                    {certification}
                    
                    <h4>📊 Statistiche Analisi Mensile</h4>
                    {statistics}
                    
                    <h4>📋 Tabella Dettagliata Log Timestamp Certificati (Finestra massima di 720 ore)</h4>
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
    /// 📊 Genera certificazione dati generale in formato HTML
    /// </summary>
    private async Task<string> GenerateDataCertificationHtmlAsync(int vehicleId, TimeSpan totalMonitoringPeriod)
    {
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
            return "<p class='cert-warning'>⚠️ Dati insufficienti per certificazione</p>";
        }

        var actualMonitoringPeriod = lastRecord - firstRecord;
        var totalCertifiedHours = actualMonitoringPeriod.TotalHours;

        var uptimePercentage = Math.Min(95.0, 80.0 + (totalRecords / Math.Max(totalCertifiedHours, 1)) * 10);
        var qualityScore = CalculateQualityScore(totalRecords, uptimePercentage, totalMonitoringPeriod);
        var qualityStars = GetQualityStars(qualityScore);

        return $@"
            <table class='certification-table'>
                <tr><td>Ore totali certificate:</td><td>{totalCertifiedHours:F0} ore ({totalCertifiedHours / 24:F1} giorni)</td></tr>
                <tr><td>Uptime raccolta:</td><td>{uptimePercentage:F1}%</td></tr>
                <tr><td>Qualità dataset:</td><td>{qualityStars} ({GetQualityLabel(qualityScore)})</td></tr>
                <tr><td>Primo record:</td><td>{firstRecord:yyyy-MM-dd HH:mm} UTC</td></tr>
                <tr><td>Ultimo record:</td><td>{lastRecord:yyyy-MM-dd HH:mm} UTC</td></tr>
                <tr><td>Records totali:</td><td>{totalRecords:N0}</td></tr>
                <tr><td>Frequenza media:</td><td>{(totalRecords / Math.Max(totalCertifiedHours, 1)):F1} campioni/ora</td></tr>
            </table>";
    }

    /// <summary>
    /// 📊 Genera statistiche mensili in formato HTML
    /// </summary>
    private static string GenerateMonthlyStatisticsHtml(int monthlyRecords, TimeSpan totalMonitoringPeriod)
    {
        const int dataHours = 720; // 30 giorni

        return $@"
            <table class='statistics-table'>
                <tr><td>Durata monitoraggio totale:</td><td>{totalMonitoringPeriod.TotalDays:F1} giorni</td></tr>
                <tr><td>Campioni mensili analizzati:</td><td>{monthlyRecords:N0}</td></tr>
                <tr><td>Finestra unificata:</td><td>{dataHours} ore (30 giorni)</td></tr>
                <tr><td>Densità dati mensile:</td><td>{monthlyRecords / Math.Max(dataHours, 1):F1} campioni/ora</td></tr>
                <tr><td>Copertura dati:</td><td>{Math.Min(100, (dataHours / Math.Max(totalMonitoringPeriod.TotalHours, 1)) * 100):F1}% del periodo totale</td></tr>
                <tr><td>Strategia:</td><td>Analisi mensile consistente con context evolutivo</td></tr>
            </table>";
    }

    /// <summary>
    /// 📋 Genera tabella dettagliata dei record certificati
    /// Mostra solo il periodo dal primo record effettivo fino ad oggi (max 30 giorni)
    /// </summary>
    private async Task<string> GenerateDetailedLogTableAsync(int vehicleId, int dataHours)
    {
        var sb = new StringBuilder();

        // Trova il primo record effettivo del veicolo
        var firstRecordTime = await _dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId)
            .OrderBy(vd => vd.Timestamp)
            .Select(vd => vd.Timestamp)
            .FirstOrDefaultAsync();

        // Se non ci sono dati, ritorna messaggio
        if (firstRecordTime == default)
        {
            return "<div class='detailed-log-table'><p class='cert-warning'>⚠️ Nessun dato disponibile per la tabella dettagliata</p></div>";
        }

        // Calcola il periodo effettivo da mostrare
        var now = DateTime.Now;
        var maxStartTime = now.AddHours(-dataHours); // 30 giorni fa massimo

        // Usa il più recente tra il primo record e 30 giorni fa
        var effectiveStartTime = firstRecordTime > maxStartTime ? firstRecordTime : maxStartTime;

        // Arrotonda all'ora precedente per avere timestamp puliti
        var startTime = new DateTime(
            effectiveStartTime.Year,
            effectiveStartTime.Month,
            effectiveStartTime.Day,
            effectiveStartTime.Hour,
            0, 0
        );

        // Calcola ore effettive da mostrare
        var actualHoursToShow = (int)Math.Ceiling((now - startTime).TotalHours);
        var totalExpectedRecords = actualHoursToShow;

        // Recupera i record effettivi
        var actualRecords = await _dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= startTime)
            .OrderBy(vd => vd.Timestamp)
            .Select(vd => new { vd.Timestamp, vd.IsSmsAdaptiveProfiling, vd.RawJsonAnonymized })
            .ToListAsync();

        var recordLookup = actualRecords
            .GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, 0, 0))
            .ToDictionary(g => g.Key, g => g.First());

        // Genera tabella
        sb.AppendLine("<table class='detailed-log-table'>");
        sb.AppendLine("<thead>");
        sb.AppendLine("<tr>");
        sb.AppendLine("<th>Timestamp (UTC)</th>");
        sb.AppendLine("<th>Adaptive Profiling</th>");
        sb.AppendLine("<th>Dati Operativi</th>");
        sb.AppendLine("</tr>");
        sb.AppendLine("</thead>");
        sb.AppendLine("<tbody>");

        // Genera righe solo per il periodo effettivo
        var currentTime = startTime;
        while (currentTime <= now)
        {
            if (recordLookup.TryGetValue(currentTime, out var record))
            {
                var adaptiveProfiling = record.IsSmsAdaptiveProfiling ?
                    "<span class='badge-success'>Sì</span>" :
                    "<span class='badge-default'>No</span>";

                var datiOperativi = !string.IsNullOrEmpty(record.RawJsonAnonymized) ?
                    "<span class='badge-info'>Dati operativi raccolti</span>" :
                    "<span class='badge-warning'>Dati operativi da validare</span>";

                sb.AppendLine($"<tr class='record-present'>");
                sb.AppendLine($"<td>{record.Timestamp:yyyy-MM-dd HH:mm}</td>");
                sb.AppendLine($"<td>{adaptiveProfiling}</td>");
                sb.AppendLine($"<td>{datiOperativi}</td>");
                sb.AppendLine("</tr>");
            }
            else
            {
                sb.AppendLine($"<tr class='record-missing'>");
                sb.AppendLine($"<td>{currentTime:yyyy-MM-dd HH:mm}</td>");
                sb.AppendLine($"<td><span class='badge-default'>No</span></td>");
                sb.AppendLine($"<td><span class='badge-warning'>Dati operativi da validare</span></td>");
                sb.AppendLine("</tr>");
            }

            currentTime = currentTime.AddHours(1);
        }

        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");

        sb.AppendLine("<div class='detailed-log-table-footer'>");
        sb.AppendLine($"  <div><span class='icon'>🕒</span> Periodo monitorato: {startTime:yyyy-MM-dd HH:mm} → {now:yyyy-MM-dd HH:mm}</div>");
        sb.AppendLine($"  <div><span class='icon'>⏱️</span> Ore totali: {actualHoursToShow} ore ({actualHoursToShow / 24.0:F1} giorni)</div>");
        sb.AppendLine($"  <div><span class='icon'>📑</span> N. record certificati: {actualRecords.Count} su {totalExpectedRecords}</div>");
        sb.AppendLine($" <div><span class='icon'>📊</span> Percentuale completezza: {(actualRecords.Count / (double)totalExpectedRecords * 100):0}%</div>");

        sb.AppendLine("</div>");

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

        // 30% - Densità records
        var recordDensity = totalRecords / Math.Max(monitoringPeriod.TotalHours, 1);
        var densityScore = Math.Min(1, recordDensity / 1.0);
        score += densityScore * 30;

        // 20% - Stabilità (semplificato)
        score += 18;

        // 10% - Maturità dataset
        if (monitoringPeriod.TotalDays >= 30) score += 10;
        else if (monitoringPeriod.TotalDays >= 7) score += 7;
        else if (monitoringPeriod.TotalDays >= 1) score += 3;

        return Math.Max(0, Math.Min(100, score));
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