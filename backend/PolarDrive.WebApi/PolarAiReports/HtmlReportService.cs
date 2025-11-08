using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.Helpers;
using PolarDrive.WebApi.PolarAiReports.Templates;
using System.Text;
using static PolarDrive.WebApi.Constants.CommonConstants;

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
        var vehicleId = report.VehicleId;

        // Trova il primo record effettivo del veicolo
        var firstRecordTime = await _dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId)
            .OrderBy(vd => vd.Timestamp)
            .Select(vd => vd.Timestamp)
            .FirstOrDefaultAsync();

        // Calcola il periodo effettivo da mostrare
        var now = DateTime.Now;
        var maxStartTime = now.AddHours(-MONTHLY_HOURS_THRESHOLD); // 30 giorni fa massimo

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

        // Se l'HASH non esiste, viene calcolato adesso, prima di preparare i dati
        if (string.IsNullOrEmpty(report.PdfHash))
        {
            // Crea un "contenuto deterministico" da cui calcolare l'hash
            var contentForHash = $"{report.Id}|{report.VehicleId}|{report.ClientCompanyId}|" +
                            $"{startTime:O}|{now:O}|{aiReportContentInsights}|" +
                            $"{report.ClientCompany?.Name}|{report.ClientVehicle?.Vin}";
            
            var contentHash = GenericHelpers.ComputeContentHash(contentForHash);
            
            report.PdfHash = contentHash;
            await _dbContext.SaveChangesAsync();
            
            await _logger.Info("PrepareTemplateDataAsync", "Hash calcolato e salvato",
                $"ReportId: {report.Id}, Hash: {contentHash}");
        }

        var data = new Dictionary<string, object>
        {
            // Dati base del report
            ["reportId"] = report.Id,
            ["companyName"] = report.ClientCompany?.Name ?? "N/A",
            ["vatNumber"] = report.ClientCompany?.VatNumber ?? "N/A",
            ["vehicleModel"] = report.ClientVehicle?.Model ?? "N/A",
            ["vehicleVin"] = report.ClientVehicle?.Vin ?? "N/A",
            ["periodStart"] = startTime.ToString("dd/MM/yyyy").Replace("-", "/"),
            ["periodEnd"] = now.ToString("dd/MM/yyyy").Replace("-", "/"),
            ["generatedAtDays"] = DateTime.Now.ToString(options.DateTimeFormatDays).Replace("-", "/"),
            ["generatedAtHours"] = DateTime.Now.ToString(options.DateTimeFormatHours),
            ["pdfHash"] = report.PdfHash,
            ["notes"] = report.Notes ?? "N/A",
            ["insights"] = FormatInsightsForHtml(aiReportContentInsights),

            // Logo aziendale
            ["logoBase64"] = await GetLogoBase64Async(report.ClientCompany),

            // Certificazione DataPolar
            ["dataPolarCertification"] = await GenerateDataPolarCertificationBlockAsync(report),

            // Metadati
            ["reportType"] = options.ReportType,
            ["showCharts"] = options.ShowCharts,
        };

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
                timeRange.FirstRecord,
                timeRange.LastRecord,
                timeRange.TotalCount,
                report
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
            ["showCharts"] = (bool?)data.GetValueOrDefault("showCharts") ?? false,
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
    /// Qui vengono gestiti anche eventuali simboli stampati erroneamente dall AI nei PDF
    /// </summary>
    private static string FormatInsightsForHtml(string insights)
    {
        if (string.IsNullOrWhiteSpace(insights))
            return "<p class='insight-empty'>Nessun insight disponibile.</p>";

        insights = System.Text.RegularExpressions.Regex.Replace(
            insights,
            @"\*\*(.+?)\*\*", // **bold**
            "<strong>$1</strong>"
        );
        
        insights = System.Text.RegularExpressions.Regex.Replace(
            insights,
            @"\*(.+?)\*", // *italic*
            "<em>$1</em>"
        );

        // Ora processa le righe normalmente
        var lines = insights.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var formattedLines = new List<string>();
        bool inList = false;
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            
            // ✅ 1. Gestione HEADERS - con validazione contenuto
            if (trimmed.StartsWith("### "))
            {
                var headerText = trimmed.Substring(4).Trim();
                if (string.IsNullOrEmpty(headerText)) continue;
                
                if (inList) { formattedLines.Add("</ul>"); inList = false; }
                formattedLines.Add($"<h4 class='insight-h4'>{headerText}</h4>");
            }
            else if (trimmed.StartsWith("## "))
            {
                var headerText = trimmed.Substring(3).Trim();
                if (string.IsNullOrEmpty(headerText)) continue;
                
                if (inList) { formattedLines.Add("</ul>"); inList = false; }
                formattedLines.Add($"<h3 class='insight-h3'>{headerText}</h3>");
            }
            else if (trimmed.StartsWith("# "))
            {
                var headerText = trimmed.Substring(2).Trim();
                if (string.IsNullOrEmpty(headerText)) continue;
                
                if (inList) { formattedLines.Add("</ul>"); inList = false; }
                formattedLines.Add($"<h2 class='insight-h2'>{headerText}</h2>");
            }
            // ✅ 2. Ignora simboli markdown orfani e separatori
            else if (trimmed == "####" || trimmed == "###" || trimmed == "##" || trimmed == "#" || 
                    trimmed == "---" || trimmed == "___" || trimmed == "***")
            {
                continue; // ❌ Salta separatori markdown
            }
            // ✅ 3. Gestione LISTE - con validazione contenuto
            else if (trimmed.StartsWith("- ") || trimmed.StartsWith("• ") || trimmed.StartsWith("* "))
            {
                var listItem = trimmed.Substring(2).Trim();
                if (string.IsNullOrEmpty(listItem)) continue;
                
                if (!inList)
                {
                    formattedLines.Add("<ul class='insight-list'>");
                    inList = true;
                }
                formattedLines.Add($"<li class='insight-li'>{listItem}</li>");
            }
            // ✅ 4. Ignora bullet points orfani
            else if (trimmed == "-" || trimmed == "•" || trimmed == "*")
            {
                continue;
            }
            // ✅ 5. Paragrafi normali (solo se NON è già HTML)
            else if (!trimmed.StartsWith("<"))
            {
                if (inList) { formattedLines.Add("</ul>"); inList = false; }
                formattedLines.Add($"<p class='insight-p'>{trimmed}</p>");
            }
            // ✅ 6. Mantieni HTML esistente
            else
            {
                if (inList && !trimmed.StartsWith("<li")) { formattedLines.Add("</ul>"); inList = false; }
                formattedLines.Add(trimmed);
            }
        }
        
        if (inList) formattedLines.Add("</ul>");
        
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
        // ✅ Path assoluto nel container Docker
        var basePath = "/app/wwwroot/fonts/satoshi";
        
        var satoshiRegular = File.ReadAllText(Path.Combine(basePath, "Satoshi-Regular.b64"));
        var satoshiBold = File.ReadAllText(Path.Combine(basePath, "Satoshi-Bold.b64"));
        var satoshiBlack = File.ReadAllText(Path.Combine(basePath, "Satoshi-Black.b64"));

        var styles = $@"
            @font-face {{
                font-family: 'Satoshi';
                src: url(data:font/woff2;base64,{satoshiRegular}) format('woff2');
                font-weight: 400;
                font-style: normal;
            }}
            @font-face {{
                font-family: 'Satoshi';
                src: url(data:font/woff2;base64,{satoshiBold}) format('woff2');
                font-weight: 700;
                font-style: normal;
            }}
            body {{
                font-family: 'Satoshi', 'Noto Color Emoji', sans-serif;
                letter-spacing: normal;
                word-spacing: normal;
            }}
            {DefaultCssTemplate.Value}
        ";

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
                <h2>Contenuto PolarAi™ (Fallback)</h2>
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
    public string DateTimeFormatDays { get; set; } = "dd/MM/yyyy";
    public string DateTimeFormatHours { get; set; } = "HH:mm";
    public string? AdditionalCss { get; set; }
    public bool ShowCharts { get; set; } = false;
}