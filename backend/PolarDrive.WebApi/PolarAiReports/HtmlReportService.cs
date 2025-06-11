using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
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
        var source = "HtmlReportService.GenerateHtmlReport";
        options ??= new HtmlReportOptions();

        try
        {
            await _logger.Info(source, "Inizio generazione report HTML",
                $"ReportId: {report.Id}, Template: {options.TemplateName}");

            // 1. Carica template e stili
            var template = await LoadTemplateAsync(options.TemplateName);
            var styles = await LoadStylesAsync(options.StyleName);

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
    /// Carica il template HTML (con fallback integrato)
    /// </summary>
    private async Task<string> LoadTemplateAsync(string templateName)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "PolarAiReports", "templates");
        var templatePath = Path.Combine(basePath, $"{templateName}.html");

        if (File.Exists(templatePath))
        {
            return await File.ReadAllTextAsync(templatePath);
        }

        // Fallback template integrato
        return GetDefaultTemplate();
    }

    /// <summary>
    /// Carica gli stili CSS (con fallback integrato)
    /// </summary>
    private async Task<string> LoadStylesAsync(string styleName)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "PolarAiReports", "styles");
        var stylePath = Path.Combine(basePath, $"{styleName}.css");

        if (File.Exists(stylePath))
        {
            return await File.ReadAllTextAsync(stylePath);
        }

        // Fallback stili integrati
        return GetDefaultStyles();
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
            ["generatedAt"] = DateTime.UtcNow.ToString(options.DateTimeFormat),
            ["notes"] = report.Notes ?? "N/A",
            ["insights"] = FormatInsightsForHtml(aiReportContentInsights),

            // Logo aziendale (Base64 se specificato)
            ["logoBase64"] = await GetCompanyLogoBase64Async(report.ClientCompany),

            // Metadati del report
            ["reportType"] = options.ReportType,
            ["reportVersion"] = options.ReportVersion,

            // Stili personalizzati
            ["customStyles"] = options.AdditionalCss ?? "",

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
    /// Renderizza il template finale sostituendo tutti i placeholder
    /// </summary>
    private string RenderTemplate(string template, string styles, Dictionary<string, object> data)
    {
        var html = template;

        // Inserisci gli stili
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
    private string ProcessConditionalBlocks(string html, Dictionary<string, object> data)
    {
        // Implementazione semplificata per blocchi condizionali
        // Puoi estendere questo per logica più complessa

        // Esempio: {{#if showDetailedStats}}contenuto{{/if}}
        var patterns = new Dictionary<string, bool>
        {
            ["showDetailedStats"] = data.ContainsKey("detailedStats"),
            ["showCharts"] = (bool?)data.GetValueOrDefault("showCharts") ?? false,
            ["showRawData"] = data.ContainsKey("rawDataSummary")
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
    /// Template HTML di default integrato nel codice
    /// </summary>
    private string GetDefaultTemplate()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <title>PolarDrive Report {{reportId}}</title>
    {{styles}}
</head>
<body>
    <div class=""header"">
        {{#if logoBase64}}
        <img src=""data:image/png;base64,{{logoBase64}}"" alt=""Company Logo"" class=""logo"" />
        {{/if}}
        <h1>PolarDrive Report</h1>
        <div class=""report-info"">
            <div><strong>Azienda:</strong> {{companyName}} ({{vatNumber}})</div>
            <div><strong>Veicolo:</strong> {{vehicleModel}} - {{vehicleVin}}</div>
            <div><strong>Periodo:</strong> {{periodStart}} → {{periodEnd}}</div>
            <div><strong>Generato:</strong> {{generatedAt}}</div>
            {{#if notes}}
            <div><strong>Note:</strong> {{notes}}</div>
            {{/if}}
        </div>
    </div>

    <hr />

    <div class=""ai-insights section"">
        <h2>Analisi Intelligente del Veicolo</h2>
        <div class=""insights-content"">
            {{insights}}
        </div>
    </div>

    {{#if showDetailedStats}}
    <div class=""detailed-stats section"">
        <h2>Statistiche Dettagliate</h2>
        <div class=""stats-content"">
            {{detailedStats}}
        </div>
    </div>
    {{/if}}

    {{#if showRawData}}
    <div class=""raw-data section"">
        <h2>Riepilogo Dati Tecnici</h2>
        <div class=""raw-data-content"">
            {{rawDataSummary}}
        </div>
    </div>
    {{/if}}

    <div class=""footer"">
        <p><small>Report generato da PolarDrive v{{reportVersion}} - {{generatedAt}}</small></p>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// Stili CSS di default integrati nel codice
    /// </summary>
    private string GetDefaultStyles()
    {
        return @"
        html, body, table {
            font-size: 12px !important;
        }

        body {
            font-family: Arial, sans-serif;
            margin: 20px;
            line-height: 1.4;
        }

        .header {
            border-bottom: 3px solid #004E92;
            padding-bottom: 20px;
            margin-bottom: 30px;
        }

        .logo {
            width: 120px;
            height: auto;
            margin-bottom: 12px;
            float: right;
        }

        h1 {
            color: #004E92;
            margin-bottom: 15px;
            font-size: 24px;
        }

        h2 {
            color: #004E92;
            margin-top: 25px;
            margin-bottom: 15px;
            font-size: 18px;
            border-bottom: 1px solid #ddd;
            padding-bottom: 5px;
        }

        .report-info {
            background-color: #f8f9fa;
            padding: 15px;
            border-radius: 5px;
            margin-top: 10px;
        }

        .report-info div {
            margin-bottom: 5px;
        }

        .section {
            margin-bottom: 30px;
            page-break-inside: avoid;
        }

        .insights-content {
            background-color: #ffffff;
            padding: 20px;
            border-left: 4px solid #004E92;
            margin: 15px 0;
        }

        .stats-content {
            background-color: #f8f9fa;
            padding: 15px;
            border-radius: 5px;
        }

        .raw-data-content {
            font-family: 'Courier New', monospace;
            background-color: #f4f4f4;
            padding: 15px;
            border-radius: 5px;
            font-size: 11px;
            overflow-x: auto;
        }

        table {
            width: 100%;
            max-width: 100%;
            table-layout: fixed;
            border-collapse: collapse;
            margin-bottom: 20px;
        }

        th, td {
            border: 1px solid #ddd;
            padding: 8px;
            text-align: left;
        }

        td {
            overflow: visible;
            white-space: normal;
            word-break: break-word;
        }

        th {
            background-color: #f2f2f2;
            font-weight: bold;
        }

        .footer {
            margin-top: 40px;
            padding-top: 20px;
            border-top: 1px solid #ddd;
            text-align: center;
            color: #666;
        }

        /* Stili specifici per la stampa */
        @media print {
            html, body {
                font-size: 12px !important;
            }

            .section {
                page-break-inside: avoid;
            }

            .header {
                page-break-after: avoid;
            }

            table {
                page-break-inside: auto;
            }

            tr {
                page-break-inside: avoid;
            }

            .logo {
                max-width: 100px;
            }
        }";
    }

    /// <summary>
    /// Formatta gli insights AI per HTML (converte markdown base in HTML)
    /// </summary>
    private string FormatInsightsForHtml(string insights)
    {
        if (string.IsNullOrWhiteSpace(insights))
            return "<p>Nessun insight disponibile.</p>";

        var html = insights;

        // Conversioni markdown base
        html = html.Replace("# ", "<h3>").Replace("\n", "</h3>\n");
        html = html.Replace("## ", "<h4>").Replace("\n", "</h4>\n");
        html = html.Replace("**", "<strong>").Replace("**", "</strong>");
        html = html.Replace("- ", "<li>").Replace("\n", "</li>\n");

        // Avvolgi le liste
        if (html.Contains("<li>"))
        {
            html = "<ul>\n" + html + "\n</ul>";
        }

        // Avvolgi in paragrafi i blocchi di testo
        var lines = html.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var formattedLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("<") && !string.IsNullOrEmpty(trimmed))
            {
                formattedLines.Add($"<p>{trimmed}</p>");
            }
            else
            {
                formattedLines.Add(line);
            }
        }

        return string.Join("\n", formattedLines);
    }

    /// <summary>
    /// Ottiene il logo aziendale in formato Base64
    /// </summary>
    private async Task<string> GetCompanyLogoBase64Async(ClientCompany? company)
    {
        if (company == null) return "";

        try
        {
            var logoPath = Path.Combine("wwwroot", "images", "logos", $"{company.Id}.png");

            if (File.Exists(logoPath))
            {
                var logoBytes = await File.ReadAllBytesAsync(logoPath);
                return Convert.ToBase64String(logoBytes);
            }
        }
        catch (Exception ex)
        {
            await _logger.Debug("HtmlReportService.GetCompanyLogoBase64",
                "Errore caricamento logo", ex.Message);
        }

        return "";
    }

    /// <summary>
    /// Genera statistiche dettagliate per il report
    /// </summary>
    private async Task<string> GenerateDetailedStatsAsync(PdfReport report)
    {
        try
        {
            var dataCount = await dbContext.VehiclesData
                .Where(vd => vd.VehicleId == report.ClientVehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .CountAsync();

            var firstRecord = await dbContext.VehiclesData
                .Where(vd => vd.VehicleId == report.ClientVehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var lastRecord = await dbContext.VehiclesData
                .Where(vd => vd.VehicleId == report.ClientVehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .OrderByDescending(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            return $@"
                <table>
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
            return "<p>Statistiche dettagliate non disponibili.</p>";
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
                .Where(vd => vd.VehicleId == report.ClientVehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .OrderByDescending(vd => vd.Timestamp)
                .Take(3)
                .Select(vd => new { vd.Timestamp, vd.RawJson })
                .ToListAsync();

            var sb = new StringBuilder();

            foreach (var data in recentData)
            {
                sb.AppendLine($"[{data.Timestamp:yyyy-MM-dd HH:mm:ss}]");
                sb.AppendLine($"Dimensione: {data.RawJson?.Length ?? 0} caratteri");
                sb.AppendLine("---");
            }

            return sb.ToString();
        }
        catch
        {
            return "Dati grezzi non disponibili.";
        }
    }

    /// <summary>
    /// Genera HTML di fallback in caso di errore
    /// </summary>
    private string GenerateErrorFallbackHtml(PdfReport report, string insights, string errorMessage)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <title>PolarDrive Report {report.Id} - Errore</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; }}
        .error {{ color: red; background-color: #ffe6e6; padding: 15px; border-radius: 5px; }}
        h1 {{ color: #004E92; }}
    </style>
</head>
<body>
    <h1>PolarDrive Report {report.Id}</h1>
    <div class=""error"">
        <strong>Errore nella generazione del report:</strong> {errorMessage}
    </div>
    <h2>Contenuto AI (Fallback)</h2>
    <div>{insights}</div>
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
    public string ReportVersion { get; set; } = "1.0";
    public string DateFormat { get; set; } = "yyyy-MM-dd";
    public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm";
    public string? AdditionalCss { get; set; }
    public bool ShowDetailedStats { get; set; } = true;
    public bool ShowCharts { get; set; } = false;
    public bool ShowRawData { get; set; } = false;
}