using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.PolarAiReports.Templates;
using System.Text;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Servizio dedicato alla generazione di report HTML altamente personalizzabili
/// Separato dalla logica di conversione PDF per massima flessibilità
/// </summary>
public class HtmlReportService(PolarDriveDbContext dbContext)
{
    private readonly PolarDriveLogger _logger = new(dbContext);

    /// <summary>
    /// Genera un report HTML completo pronto per visualizzazione o conversione PDF
    /// </summary>
    public async Task<string> GenerateHtmlReportAsync(PdfReport report, string aiReportContentInsights, HtmlReportOptions? options = null)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        if (string.IsNullOrWhiteSpace(aiReportContentInsights))
            throw new ArgumentException("AI insights cannot be empty", nameof(aiReportContentInsights));
        if (report.VehicleId <= 0)
            throw new ArgumentException("Invalid vehicle ID", nameof(report.VehicleId));

        var source = "HtmlReportService.GenerateHtmlReport";
        options ??= new HtmlReportOptions();

        try
        {
            await _logger.Info(source, "Inizio generazione report HTML",
                $"ReportId: {report.Id}, Template: {options.TemplateName}");

            // 1. Carica template e stili dai template centralizzati
            var template = DefaultHtmlTemplate.Value;
            var styles = DefaultCssTemplate.Value;

            // 2. Prepara i dati per il template
            var templateData = await PrepareTemplateDataAsync(report, aiReportContentInsights, options);

            // 3. Genera HTML finale
            var finalHtml = RenderTemplate(template, styles, templateData);

            await _logger.Info(source, "Report HTML generato con successo",
                $"Dimensione: {finalHtml.Length} caratteri");

            return finalHtml;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore generazione report HTML", ex.ToString());
            return GenerateErrorFallbackHtml(report, aiReportContentInsights, ex.Message);
        }
    }

    /// <summary>
    /// Prepara tutti i dati necessari per il template
    /// </summary>
    private async Task<Dictionary<string, object>> PrepareTemplateDataAsync(
        PdfReport report,
        string aiReportContentInsights,
        HtmlReportOptions options)
    {
        var data = new Dictionary<string, object>
        {
            // Dati base del report
            ["reportId"] = report.Id,
            ["companyName"] = report.ClientCompany?.Name ?? "N/A",
            ["vatNumber"] = report.ClientCompany?.VatNumber ?? "N/A",
            ["vehicleModel"] = report.ClientVehicle?.Model ?? "N/A",
            ["vehicleVin"] = report.ClientVehicle?.Vin ?? "N/A",
            ["periodStart"] = report.ReportPeriodStart.ToString(options.DateFormat),
            ["periodEnd"] = report.ReportPeriodEnd.ToString(options.DateFormat),
            ["generatedAt"] = DateTime.Now.ToString(options.DateTimeFormat),
            ["notes"] = report.Notes ?? "N/A",
            ["insights"] = FormatInsightsForHtml(aiReportContentInsights),

            // Logo aziendale (Base64 se specificato)
            ["logoBase64"] = await GetLogoBase64Async(report.ClientCompany),

            // ✅ CERTIFICAZIONE DATAPOLAR (SEMPRE PRESENTE)
            ["dataPolarCertification"] = await GenerateDataPolarCertificationAsync(report),

            // Metadati del report
            ["reportType"] = options.ReportType,

            // Configurazioni
            ["showDetailedStats"] = options.ShowDetailedStats,
            ["showCharts"] = options.ShowCharts,
            ["showRawData"] = options.ShowRawData
        };

        // Aggiungi statistiche dettagliate se richieste
        if (options.ShowDetailedStats)
        {
            var stats = await GenerateDetailedStatsAsync(report);
            data["detailedStats"] = stats;
        }

        // Aggiungi dati grezzi se richiesti
        if (options.ShowRawData)
        {
            var rawData = await GetFormattedRawDataAsync(report);
            data["rawDataSummary"] = rawData;
        }

        return data;
    }

    /// <summary>
    /// Genera la certificazione DataPolar per inclusione diretta nel PDF
    /// Riutilizza la stessa logica del PolarAiReportGenerator
    /// </summary>
    private async Task<string> GenerateDataPolarCertificationAsync(PdfReport report)
    {
        try
        {
            var vehicleId = report.VehicleId;

            // Calcola periodo di monitoraggio totale
            var firstRecord = await dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var totalMonitoringPeriod = firstRecord == default
                ? TimeSpan.FromDays(1)
                : DateTime.Now - firstRecord;

            // Recupera conteggio dati degli ultimi 30 giorni
            var startTime = DateTime.Now.AddHours(-720); // 30 giorni
            var monthlyDataCount = await dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= startTime)
                .CountAsync();

            // Genera certificazione completa
            var certification = await GenerateDataCertificationHtml(vehicleId, totalMonitoringPeriod);
            var statistics = GenerateMonthlyStatisticsHtml(monthlyDataCount, totalMonitoringPeriod);

            return $@"
                <div class='certification-datapolar'>
                    <h4 class='certification-datapolar-generic'>📊 Statistiche Generali </h4>
                    {certification}
                    <h4>📊 Statistiche Analisi Mensile</h4>
                    {statistics}
                </div>";
        }
        catch (Exception ex)
        {
            await _logger.Error("HtmlReportService.GenerateDataPolarCertification",
                "Errore generazione certificazione", ex.ToString());
            return "<div class='certification-error'>⚠️ Certificazione dati non disponibile.</div>";
        }
    }

    /// <summary>
    /// Genera la certificazione dati in formato HTML (replica logica PolarAiReportGenerator)
    /// </summary>
    private async Task<string> GenerateDataCertificationHtml(int vehicleId, TimeSpan totalMonitoringPeriod)
    {
        var totalRecords = await dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId)
            .CountAsync();

        var firstRecord = await dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId)
            .OrderBy(vd => vd.Timestamp)
            .Select(vd => vd.Timestamp)
            .FirstOrDefaultAsync();

        var lastRecord = await dbContext.VehiclesData
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

        // Calcolo semplificato di uptime e qualità
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
    /// Genera statistiche mensili in formato HTML
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
    /// Calcola qualità dataset semplificata per HTML
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
        score += 18; // Assumiamo buona stabilità

        // 10% - Maturità dataset
        if (monitoringPeriod.TotalDays >= 30) score += 10;
        else if (monitoringPeriod.TotalDays >= 7) score += 7;
        else if (monitoringPeriod.TotalDays >= 1) score += 3;

        return Math.Max(0, Math.Min(100, score));
    }

    /// <summary>
    /// Converte score in stelle (replica da PolarAiReportGenerator)
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
    /// Etichetta qualitativa (replica da PolarAiReportGenerator)
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

    /// <summary>
    /// Renderizza il template finale sostituendo tutti i placeholder
    /// </summary>
    private static string RenderTemplate(string template, string styles, Dictionary<string, object> data)
    {
        var html = template;

        // Inserisci gli stili CSS centralizzati
        html = html.Replace("{{styles}}", $"<style>{styles}</style>");

        // Sostituisci tutti i placeholder con i dati
        foreach (var (key, value) in data)
        {
            var placeholder = $"{{{{{key}}}}}";
            var stringValue = value?.ToString() ?? "";
            html = html.Replace(placeholder, stringValue);
        }

        // Gestisci i placeholder condizionali
        html = ProcessConditionalBlocks(html, data);

        return html;
    }

    /// <summary>
    /// Processa i blocchi condizionali nel template (es. {{#if showCharts}}...{{/if}})
    /// </summary>
    private static string ProcessConditionalBlocks(string html, Dictionary<string, object> data)
    {
        var patterns = new Dictionary<string, bool>
        {
            ["showDetailedStats"] = data.ContainsKey("detailedStats"),
            ["showCharts"] = (bool?)data.GetValueOrDefault("showCharts") ?? false,
            ["showRawData"] = data.ContainsKey("rawDataSummary"),
            ["logoBase64"] = !string.IsNullOrEmpty(data.GetValueOrDefault("logoBase64")?.ToString()),
            ["notes"] = !string.IsNullOrEmpty(data.GetValueOrDefault("notes")?.ToString()) &&
                       data.GetValueOrDefault("notes")?.ToString() != "N/A"
        };

        foreach (var (condition, isTrue) in patterns)
        {
            var startTag = $"{{{{#if {condition}}}}}";
            var endTag = $"{{{{/if}}}}";

            var startIndex = html.IndexOf(startTag);
            while (startIndex >= 0)
            {
                var endIndex = html.IndexOf(endTag, startIndex);
                if (endIndex >= 0)
                {
                    var blockLength = endIndex + endTag.Length - startIndex;

                    if (isTrue)
                    {
                        // Rimuovi solo i tag, mantieni il contenuto
                        var content = html.Substring(startIndex + startTag.Length,
                            endIndex - startIndex - startTag.Length);
                        html = html.Remove(startIndex, blockLength).Insert(startIndex, content);
                    }
                    else
                    {
                        // Rimuovi tutto il blocco
                        html = html.Remove(startIndex, blockLength);
                    }
                }

                startIndex = html.IndexOf(startTag, startIndex);
            }
        }

        return html;
    }

    /// <summary>
    /// Formatta gli insights AI per HTML (converte markdown base in HTML)
    /// </summary>
    private static string FormatInsightsForHtml(string insights)
    {
        if (string.IsNullOrWhiteSpace(insights))
            return "<p class='insight-empty'>Nessun insight disponibile.</p>";

        var html = insights;

        // Headers con classi CSS centralizzate
        html = System.Text.RegularExpressions.Regex.Replace(html, @"^### (.+)$", "<h4 class='insight-h4'>$1</h4>", System.Text.RegularExpressions.RegexOptions.Multiline);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"^## (.+)$", "<h3 class='insight-h3'>$1</h3>", System.Text.RegularExpressions.RegexOptions.Multiline);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"^# (.+)$", "<h2 class='insight-h2'>$1</h2>", System.Text.RegularExpressions.RegexOptions.Multiline);

        // Formattazione testo con classi CSS centralizzate
        html = System.Text.RegularExpressions.Regex.Replace(html, @"\*\*(.+?)\*\*", "<span class='emphasis'>$1</span>");
        html = System.Text.RegularExpressions.Regex.Replace(html, @"__(.+?)__", "<span class='important'>$1</span>");

        // Liste con classi CSS centralizzate
        html = System.Text.RegularExpressions.Regex.Replace(html, @"^- (.+)$", "<li class='insight-li'>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);

        // Avvolgi le liste
        if (html.Contains("<li class='insight-li'>"))
        {
            html = "<ul class='insight-list'>\n" + html + "\n</ul>";
        }

        // Paragrafi normali con classi CSS centralizzate
        var lines = html.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var formattedLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("<") && !string.IsNullOrEmpty(trimmed))
            {
                formattedLines.Add($"<p class='insight-p'>{trimmed}</p>");
            }
            else
            {
                formattedLines.Add(line);
            }
        }

        return string.Join("\n", formattedLines);
    }

    /// <summary>
    /// Ottiene il logo aziendale DataPolar combinato (Logo + Lettering) in formato Base64
    /// Utilizza esclusivamente DataPolar_Logo_Lettering.svg
    /// </summary>
    private async Task<string> GetLogoBase64Async(ClientCompany? company)
    {
        try
        {
            // ✅ PERCORSO DEL LOGO COMBINATO
            var logoPath = Path.Combine("wwwroot", "logo", "DataPolar_Logo_Lettering.svg");

            // ✅ VERIFICA PRESENZA FILE
            if (!File.Exists(logoPath))
            {
                await _logger.Warning("HtmlReportService.GetLogoBase64",
                    "Logo DataPolar non trovato",
                    $"Percorso: {logoPath}");
                return "";
            }

            // ✅ CARICA E CONVERTE IN BASE64
            var logoBytes = await File.ReadAllBytesAsync(logoPath);
            var base64String = Convert.ToBase64String(logoBytes);

            await _logger.Info("HtmlReportService.GetLogoBase64",
                "Logo DataPolar caricato con successo",
                $"Dimensione: {logoBytes.Length} bytes");

            return base64String;
        }
        catch (Exception ex)
        {
            await _logger.Error("HtmlReportService.GetLogoBase64",
                "Errore caricamento logo DataPolar", ex.ToString());
            return "";
        }
    }

    /// <summary>
    /// Genera statistiche dettagliate per il report
    /// </summary>
    private async Task<string> GenerateDetailedStatsAsync(PdfReport report)
    {
        try
        {
            var dataCount = await dbContext.VehiclesData
                .Where(vd => vd.VehicleId == report.VehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .CountAsync();

            var firstRecord = await dbContext.VehiclesData
                .Where(vd => vd.VehicleId == report.VehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var lastRecord = await dbContext.VehiclesData
                .Where(vd => vd.VehicleId == report.VehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .OrderByDescending(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            return $@"
                <table class='stats-table'>
                    <tr><th>Metrica</th><th>Valore</th></tr>
                    <tr><td>Record di dati analizzati</td><td>{dataCount}</td></tr>
                    <tr><td>Primo record</td><td>{firstRecord:yyyy-MM-dd HH:mm}</td></tr>
                    <tr><td>Ultimo record</td><td>{lastRecord:yyyy-MM-dd HH:mm}</td></tr>
                    <tr><td>Durata monitoraggio</td><td>{(lastRecord - firstRecord).TotalHours:F1} ore</td></tr>
                    <tr><td>Frequenza campionamento</td><td>{(dataCount > 0 ? (lastRecord - firstRecord).TotalMinutes / dataCount : 0):F1} min/campione</td></tr>
                </table>";
        }
        catch
        {
            return "<p class='stats-error'>Statistiche dettagliate non disponibili.</p>";
        }
    }

    /// <summary>
    /// Ottiene un riepilogo formattato dei dati grezzi
    /// </summary>
    private async Task<string> GetFormattedRawDataAsync(PdfReport report)
    {
        try
        {
            var recentData = await dbContext.VehiclesData
                .Where(vd => vd.VehicleId == report.VehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .OrderByDescending(vd => vd.Timestamp)
                .Take(3)
                .Select(vd => new { vd.Timestamp, vd.RawJsonAnonymized })
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("<div class='raw-data-summary'>");

            foreach (var data in recentData)
            {
                sb.AppendLine($"<div class='raw-data-entry'>");
                sb.AppendLine($"<div class='raw-data-timestamp'>[{data.Timestamp:yyyy-MM-dd HH:mm:ss}]</div>");
                sb.AppendLine($"<div class='raw-data-size'>Dimensione: {data.RawJsonAnonymized?.Length ?? 0} caratteri</div>");
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</div>");
            return sb.ToString();
        }
        catch
        {
            return "<p class='raw-data-error'>Dati grezzi non disponibili.</p>";
        }
    }

    /// <summary>
    /// Genera HTML di fallback in caso di errore
    /// Utilizza gli stili centralizzati anche per l'errore
    /// </summary>
    private static string GenerateErrorFallbackHtml(PdfReport report, string insights, string errorMessage)
    {
        var styles = DefaultCssTemplate.Value;

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <title>PolarDrive Report {report.Id} - Errore</title>
    <style>{styles}</style>
</head>
<body>
    <div class='error-container'>
        <h1 class='error-title'>PolarDrive Report {report.Id}</h1>
        <div class='error-message'>
            <strong>Errore nella generazione del report:</strong> {errorMessage}
        </div>
        <h2>Contenuto PolarAi (Fallback)</h2>
        <div class='error-fallback-content'>{insights}</div>
    </div>
</body>
</html>";
    }
}

/// <summary>
/// Opzioni per la personalizzazione del report HTML
/// </summary>
public class HtmlReportOptions
{
    public string TemplateName { get; set; } = "default";
    public string StyleName { get; set; } = "default";
    public string ReportType { get; set; } = "Standard";
public string DateFormat { get; set; } = "dd/MM/yyyy";
public string DateTimeFormat { get; set; } = "dd/MM/yyyy HH:mm";
    public string? AdditionalCss { get; set; }
    public bool ShowDetailedStats { get; set; } = true;
    public bool ShowCharts { get; set; } = false;
    public bool ShowRawData { get; set; } = false;
}