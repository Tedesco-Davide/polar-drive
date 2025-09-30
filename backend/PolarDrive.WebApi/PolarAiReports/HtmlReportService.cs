using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.PolarAiReports.Templates;
using System.Text;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Servizio dedicato esclusivamente alla generazione di report HTML
/// Responsabilità: templating, rendering, formattazione output finale
/// Non contiene logica di business delle certificazioni
/// </summary>
public class HtmlReportService
{
    private readonly PolarDriveDbContext _dbContext;
    private readonly PolarDriveLogger _logger;
    private readonly DataPolarCertification _certification;

    public HtmlReportService(PolarDriveDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = new PolarDriveLogger(_dbContext);
        _certification = new DataPolarCertification(dbContext);
    }

    /// <summary>
    /// Genera un report HTML completo pronto per visualizzazione o conversione PDF
    /// </summary>
    public async Task<string> GenerateHtmlReportAsync(
        PdfReport report,
        string aiReportContentInsights,
        HtmlReportOptions? options = null)
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

            // 1. Carica template e stili
            var template = DefaultHtmlTemplate.Value;
            var styles = DefaultCssTemplate.Value;

            // 2. Prepara dati per template
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

            // Logo aziendale
            ["logoBase64"] = await GetLogoBase64Async(report.ClientCompany),

            // ✅ CERTIFICAZIONE DATAPOLAR (delegata al servizio dedicato)
            ["dataPolarCertification"] = await GenerateDataPolarCertificationBlockAsync(report),

            // Metadati
            ["reportType"] = options.ReportType,
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
    /// Genera il blocco HTML completo della certificazione DataPolar
    /// Delega TUTTA la logica al servizio DataPolarCertification
    /// </summary>
    private async Task<string> GenerateDataPolarCertificationBlockAsync(PdfReport report)
    {
        try
        {
            var vehicleId = report.VehicleId;

            // ✅ Query unica ottimizzata: prende tutto in una volta sola
            var timeRange = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .GroupBy(vd => vd.VehicleId)
                .Select(g => new
                {
                    FirstRecord = g.Min(vd => vd.Timestamp),
                    LastRecord = g.Max(vd => vd.Timestamp),
                    TotalCount = g.Count()
                })
                .FirstOrDefaultAsync();

            // Se non ci sono dati, restituisci un messaggio
            if (timeRange == null || timeRange.TotalCount == 0)
            {
                return "<div class='certification-warning'>⚠️ Nessun dato disponibile per la certificazione</div>";
            }

            // Calcola periodo di monitoraggio totale (dal primo record ever fino ad ora)
            var totalMonitoringPeriod = DateTime.Now - timeRange.FirstRecord;

            // 🏆 CERTIFICAZIONE DATAPOLAR - Passa tutti i dati già calcolati
            return await _certification.GenerateCompleteCertificationHtmlAsync(
                vehicleId,
                totalMonitoringPeriod,
                timeRange.FirstRecord,   // ✅ Dati già disponibili
                timeRange.LastRecord,    // ✅ Dati già disponibili
                timeRange.TotalCount     // ✅ Dati già disponibili
            );
        }
        catch (Exception ex)
        {
            await _logger.Error("HtmlReportService.GenerateDataPolarCertificationBlock",
                "Errore generazione certificazione", ex.ToString());
            return "<div class='certification-error'>⚠️ Certificazione dati non disponibile.</div>";
        }
    }

    /// <summary>
    /// Renderizza il template finale sostituendo tutti i placeholder
    /// </summary>
    private static string RenderTemplate(string template, string styles, Dictionary<string, object> data)
    {
        var html = template;

        // Inserisci stili CSS
        html = html.Replace("{{styles}}", $"<style>{styles}</style>");

        // Sostituisci placeholder
        foreach (var (key, value) in data)
        {
            var placeholder = $"{{{{{key}}}}}";
            var stringValue = value?.ToString() ?? "";
            html = html.Replace(placeholder, stringValue);
        }

        // Gestisci blocchi condizionali
        html = ProcessConditionalBlocks(html, data);

        return html;
    }

    /// <summary>
    /// Processa blocchi condizionali nel template
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
                        var content = html.Substring(startIndex + startTag.Length,
                            endIndex - startIndex - startTag.Length);
                        html = html.Remove(startIndex, blockLength).Insert(startIndex, content);
                    }
                    else
                    {
                        html = html.Remove(startIndex, blockLength);
                    }
                }

                startIndex = html.IndexOf(startTag, startIndex);
            }
        }

        return html;
    }

    /// <summary>
    /// Formatta insights AI per HTML
    /// </summary>
    private static string FormatInsightsForHtml(string insights)
    {
        if (string.IsNullOrWhiteSpace(insights))
            return "<p class='insight-empty'>Nessun insight disponibile.</p>";

        var html = insights;

        // Headers
        html = System.Text.RegularExpressions.Regex.Replace(html, @"^### (.+)$",
            "<h4 class='insight-h4'>$1</h4>", System.Text.RegularExpressions.RegexOptions.Multiline);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"^## (.+)$",
            "<h3 class='insight-h3'>$1</h3>", System.Text.RegularExpressions.RegexOptions.Multiline);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"^# (.+)$",
            "<h2 class='insight-h2'>$1</h2>", System.Text.RegularExpressions.RegexOptions.Multiline);

        // Formattazione testo
        html = System.Text.RegularExpressions.Regex.Replace(html, @"\*\*(.+?)\*\*",
            "<span class='emphasis'>$1</span>");
        html = System.Text.RegularExpressions.Regex.Replace(html, @"__(.+?)__",
            "<span class='important'>$1</span>");

        // Liste
        html = System.Text.RegularExpressions.Regex.Replace(html, @"^- (.+)$",
            "<li class='insight-li'>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);

        if (html.Contains("<li class='insight-li'>"))
        {
            html = "<ul class='insight-list'>\n" + html + "\n</ul>";
        }

        // Paragrafi
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
    /// Carica logo DataPolar in Base64
    /// </summary>
    private async Task<string> GetLogoBase64Async(ClientCompany? company)
    {
        try
        {
            var logoPath = Path.Combine("wwwroot", "logo", "DataPolar_Logo_Lettering.svg");

            if (!File.Exists(logoPath))
            {
                await _logger.Warning("HtmlReportService.GetLogoBase64",
                    "Logo DataPolar non trovato", $"Percorso: {logoPath}");
                return "";
            }

            var logoBytes = await File.ReadAllBytesAsync(logoPath);
            var base64String = Convert.ToBase64String(logoBytes);

            await _logger.Info("HtmlReportService.GetLogoBase64",
                "Logo DataPolar caricato con successo", $"Dimensione: {logoBytes.Length} bytes");

            return base64String;
        }
        catch (Exception ex)
        {
            await _logger.Error("HtmlReportService.GetLogoBase64",
                "Errore caricamento logo", ex.ToString());
            return "";
        }
    }

    /// <summary>
    /// Genera statistiche dettagliate
    /// </summary>
    private async Task<string> GenerateDetailedStatsAsync(PdfReport report)
    {
        try
        {
            var dataCount = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == report.VehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .CountAsync();

            var firstRecord = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == report.VehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var lastRecord = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == report.VehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .OrderByDescending(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            return $@"
                <table class='other-stats-table'>
                    <tr><th>Metrica</th><th>Valore</th></tr>
                    <tr><td>Record analizzati</td><td>{dataCount}</td></tr>
                    <tr><td>Primo record</td><td>{firstRecord:yyyy-MM-dd HH:mm}</td></tr>
                    <tr><td>Ultimo record</td><td>{lastRecord:yyyy-MM-dd HH:mm}</td></tr>
                    <tr><td>Durata monitoraggio</td><td>{(lastRecord - firstRecord).TotalHours:0} ore</td></tr>
                    <tr><td>Frequenza campionamento</td><td>{(dataCount > 0 ? (lastRecord - firstRecord).TotalMinutes / dataCount : 0):0} min/campione</td></tr>
                </table>";
        }
        catch
        {
            return "<p class='stats-error'>Statistiche non disponibili.</p>";
        }
    }

    /// <summary>
    /// Ottiene riepilogo dati grezzi
    /// </summary>
    private async Task<string> GetFormattedRawDataAsync(PdfReport report)
    {
        try
        {
            var recentData = await _dbContext.VehiclesData
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
/// Opzioni per personalizzazione report HTML
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