using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.Helpers;
using PolarDrive.WebApi.PolarAiReports.Templates;
using PolarDrive.WebApi.Services;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Servizio per la generazione di PDF di certificazione probabilistica dei gap
/// </summary>
public class GapCertificationPdfService(
    PolarDriveDbContext dbContext,
    GapAnalysisService gapAnalysisService,
    PdfGenerationService pdfGenerationService)
{
    private readonly PolarDriveDbContext _db = dbContext;
    private readonly GapAnalysisService _gapAnalysisService = gapAnalysisService;
    private readonly PdfGenerationService _pdfGenerationService = pdfGenerationService;
    private readonly PolarDriveLogger _logger = new();

    /// <summary>
    /// Genera il PDF di certificazione per un report
    /// </summary>
    public async Task<GapCertificationPdfResult> GenerateCertificationPdfAsync(int pdfReportId)
    {
        const string source = "GapCertificationPdfService.GenerateCertificationPdf";

        try
        {
            // 1. Recupera il report e i dati correlati
            var report = await _db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == pdfReportId);

            if (report == null)
            {
                return new GapCertificationPdfResult
                {
                    Success = false,
                    ErrorMessage = "Report not found"
                };
            }

            // 2. Certifica i gap e salvali nel database
            var certifications = await _gapAnalysisService.CertifyGapsForReportAsync(pdfReportId);

            if (certifications.Count == 0)
            {
                return new GapCertificationPdfResult
                {
                    Success = false,
                    ErrorMessage = "No gaps to certify found in the reporting period"
                };
            }

            // 3. Genera l'HTML della certificazione
            var htmlContent = GenerateCertificationHtml(report, certifications);

            // 4. Converti in PDF
            var pdfBytes = await _pdfGenerationService.ConvertHtmlToPdfAsync(htmlContent, report);

            // 5. Genera hash del PDF
            var pdfHash = GenericHelpers.ComputeContentHash(pdfBytes);

            // 6. Aggiorna le certificazioni con l'hash
            foreach (var cert in certifications)
            {
                cert.CertificationHash = pdfHash;
            }
            await _db.SaveChangesAsync();

            await _logger.Info(source, $"Generated certification PDF for report {pdfReportId}",
                $"Gaps: {certifications.Count}, PDF size: {pdfBytes.Length} bytes");

            return new GapCertificationPdfResult
            {
                Success = true,
                PdfContent = pdfBytes,
                PdfHash = pdfHash,
                GapsCertified = certifications.Count,
                AverageConfidence = certifications.Average(c => c.ConfidencePercentage)
            };
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error generating certification PDF for report {pdfReportId}", ex.ToString());
            return new GapCertificationPdfResult
            {
                Success = false,
                ErrorMessage = $"Error while generating: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Genera l'HTML della certificazione
    /// </summary>
    private static string GenerateCertificationHtml(PdfReport report, List<GapCertification> certifications)
    {
        var company = report.ClientCompany;
        var vehicle = report.ClientVehicle;

        var sb = new StringBuilder();

        // Header HTML
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='it'>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset='UTF-8'>");
        sb.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        sb.AppendLine("<title>Certificazione Probabilistica Gap - DataPolar</title>");
        sb.AppendLine($"<style>{GapCertificationTemplate.GetCss()}</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header documento
        sb.AppendLine("<div class='header'>");
        sb.AppendLine("<div class='logo-section'>");
        sb.AppendLine("<h1>DataPolar</h1>");
        sb.AppendLine("<p class='subtitle'>Certificazione Probabilistica Record da Validare</p>");
        sb.AppendLine("</div>");
        sb.AppendLine($"<div class='doc-info'>");
        sb.AppendLine($"<p><strong>Data generazione:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</p>");
        sb.AppendLine($"<p><strong>Report di riferimento:</strong> #{report.Id}</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        // Info azienda e veicolo
        sb.AppendLine("<div class='info-section'>");
        sb.AppendLine("<div class='info-box'>");
        sb.AppendLine("<h3>Informazioni Azienda</h3>");
        sb.AppendLine($"<p><strong>Ragione Sociale:</strong> {company?.Name ?? "N/A"}</p>");
        sb.AppendLine($"<p><strong>P.IVA:</strong> {company?.VatNumber ?? "N/A"}</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class='info-box'>");
        sb.AppendLine("<h3>Informazioni Laboratorio Mobile</h3>");
        sb.AppendLine($"<p><strong>VIN:</strong> {vehicle?.Vin ?? "N/A"}</p>");
        sb.AppendLine($"<p><strong>Modello:</strong> {vehicle?.Model ?? "N/A"}</p>");
        sb.AppendLine($"<p><strong>Brand:</strong> {vehicle?.Brand ?? "N/A"}</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        // Periodo analizzato
        sb.AppendLine("<div class='period-section'>");
        sb.AppendLine($"<p><strong>Periodo analizzato:</strong> {report.ReportPeriodStart:dd/MM/yyyy HH:mm} - {report.ReportPeriodEnd:dd/MM/yyyy HH:mm}</p>");
        sb.AppendLine($"<p><strong>Record da validare identificati:</strong> {certifications.Count}</p>");
        sb.AppendLine($"<p><strong>Confidenza media:</strong> {certifications.Average(c => c.ConfidencePercentage):F1}%</p>");
        sb.AppendLine("</div>");

        // Disclaimer
        sb.AppendLine("<div class='disclaimer'>");
        sb.AppendLine("<h3>DICHIARAZIONE DI CERTIFICAZIONE PROBABILISTICA</h3>");
        sb.AppendLine("<p>DataPolar S.r.l. certifica che, sulla base dell'analisi statistica dei dati telemetrici raccolti dal laboratorio mobile identificato in questo documento, per i periodi indicati nella tabella sottostante sussiste un'alta probabilità (espressa in percentuale) che il veicolo fosse operativo per le finalità contrattuali di raccolta dati.</p>");
        sb.AppendLine("<h4>NOTA METODOLOGICA</h4>");
        sb.AppendLine("<p>I valori di confidenza sono calcolati analizzando:</p>");
        sb.AppendLine("<ul>");
        sb.AppendLine("<li>Continuità temporale dei record adiacenti (30%)</li>");
        sb.AppendLine("<li>Progressione dei parametri operativi - batteria, stato veicolo (25%)</li>");
        sb.AppendLine("<li>Pattern storici di utilizzo del veicolo (20%)</li>");
        sb.AppendLine("<li>Durata del gap temporale (15%)</li>");
        sb.AppendLine("<li>Affidabilità storica complessiva (10%)</li>");
        sb.AppendLine("</ul>");
        sb.AppendLine("<p class='important'>IMPORTANTE: Questa certificazione si basa su inferenze statistiche e NON costituisce prova diretta dell'utilizzo effettivo. I dati mancanti non sono stati ricostruiti, ma la loro assenza è stata analizzata nel contesto dei dati disponibili.</p>");
        sb.AppendLine("</div>");

        // Tabella gap
        sb.AppendLine("<div class='gaps-section'>");
        sb.AppendLine("<h3>Dettaglio Record da Validare Certificati</h3>");
        sb.AppendLine("<table class='gaps-table'>");
        sb.AppendLine("<thead>");
        sb.AppendLine("<tr>");
        sb.AppendLine("<th>Timestamp</th>");
        sb.AppendLine("<th>Confidenza</th>");
        sb.AppendLine("<th>Giustificazione</th>");
        sb.AppendLine("</tr>");
        sb.AppendLine("</thead>");
        sb.AppendLine("<tbody>");

        foreach (var cert in certifications.OrderBy(c => c.GapTimestamp))
        {
            var confidenceClass = cert.ConfidencePercentage >= 80 ? "high" :
                                  cert.ConfidencePercentage >= 60 ? "medium" : "low";

            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{cert.GapTimestamp:dd/MM/yyyy HH:mm}</td>");
            sb.AppendLine($"<td class='confidence {confidenceClass}'>{cert.ConfidencePercentage:F1}%</td>");
            sb.AppendLine($"<td class='justification'>{cert.JustificationText}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");
        sb.AppendLine("</div>");

        // Riepilogo confidenza
        sb.AppendLine("<div class='summary-section'>");
        sb.AppendLine("<h3>Riepilogo Analisi</h3>");
        sb.AppendLine("<div class='summary-grid'>");

        var highConfidence = certifications.Count(c => c.ConfidencePercentage >= 80);
        var mediumConfidence = certifications.Count(c => c.ConfidencePercentage >= 60 && c.ConfidencePercentage < 80);
        var lowConfidence = certifications.Count(c => c.ConfidencePercentage < 60);

        sb.AppendLine($"<div class='summary-item high'><span class='value'>{highConfidence}</span><span class='label'>Alta confidenza (≥80%)</span></div>");
        sb.AppendLine($"<div class='summary-item medium'><span class='value'>{mediumConfidence}</span><span class='label'>Media confidenza (60-79%)</span></div>");
        sb.AppendLine($"<div class='summary-item low'><span class='value'>{lowConfidence}</span><span class='label'>Bassa confidenza (<60%)</span></div>");

        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        // Footer con hash e firma
        var certHash = GenerateCertificationDocumentHash(certifications);
        sb.AppendLine("<div class='footer'>");
        sb.AppendLine("<div class='signature-section'>");
        sb.AppendLine($"<p><strong>Hash documento (SHA-256):</strong></p>");
        sb.AppendLine($"<p class='hash'>{certHash}</p>");
        sb.AppendLine($"<p><strong>Generato automaticamente da PolarDrive™</strong></p>");
        sb.AppendLine($"<p>© {DateTime.Now.Year} DataPolar S.r.l. - Tutti i diritti riservati</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Genera hash del documento di certificazione
    /// </summary>
    private static string GenerateCertificationDocumentHash(List<GapCertification> certifications)
    {
        var data = string.Join("|", certifications.Select(c =>
            $"{c.GapTimestamp:O}:{c.ConfidencePercentage}"));
        data += $"|{DateTime.Now:O}";

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hashBytes).ToLower();
    }
}

/// <summary>
/// Risultato della generazione del PDF di certificazione
/// </summary>
public class GapCertificationPdfResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? PdfContent { get; set; }
    public string? PdfHash { get; set; }
    public int GapsCertified { get; set; }
    public double AverageConfidence { get; set; }
}