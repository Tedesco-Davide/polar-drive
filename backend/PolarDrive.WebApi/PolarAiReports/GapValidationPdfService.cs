using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.Constants;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.Helpers;
using PolarDrive.WebApi.PolarAiReports.Templates;
using PolarDrive.WebApi.Services;
using PolarDrive.WebApi.Services.Tsa;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Servizio per la generazione di PDF di validazione probabilistica dei gap
/// </summary>
public class GapValidationPdfService(
    PolarDriveDbContext dbContext,
    GapAnalysisService gapAnalysisService,
    PdfGenerationService pdfGenerationService,
    ITsaService tsaService)
{
    private readonly PolarDriveDbContext _db = dbContext;
    private readonly GapAnalysisService _gapAnalysisService = gapAnalysisService;
    private readonly PdfGenerationService _pdfGenerationService = pdfGenerationService;
    private readonly ITsaService _tsaService = tsaService;
    private readonly PolarDriveLogger _logger = new();

    /// <summary>
    /// Avvia la generazione di un PDF di validazione gap.
    /// Crea il record GapValidationPdf con status PROCESSING e avvia la generazione in background.
    /// </summary>
    /// <param name="pdfReportId">ID del report PDF di riferimento</param>
    /// <param name="documentType">Tipo documento: CERTIFICATION, ESCALATION, CONTRACT_BREACH</param>
    /// <param name="gapAlertId">ID dell'alert correlato (opzionale)</param>
    /// <returns>True se avviato con successo</returns>
    public async Task<bool> StartGapValidationPdfAsync(int pdfReportId, string documentType, int? gapAlertId = null)
    {
        const string source = "GapValidationPdfService.StartGapValidationPdf";

        try
        {
            await _logger.Info(source, $"Starting {documentType} PDF generation for report {pdfReportId}");

            // Verifica che il documento type sia valido
            if (documentType != GapValidationDocumentTypes.CERTIFICATION &&
                documentType != GapValidationDocumentTypes.ESCALATION &&
                documentType != GapValidationDocumentTypes.CONTRACT_BREACH)
            {
                await _logger.Error(source, $"Invalid document type: {documentType}");
                return false;
            }

            // Verifica che non esista già un PDF dello stesso tipo per questo report
            var existingPdf = await _db.GapValidationPdfs
                .FirstOrDefaultAsync(p => p.PdfReportId == pdfReportId && p.DocumentType == documentType);

            if (existingPdf != null)
            {
                await _logger.Warning(source,
                    $"A {documentType} PDF already exists for report {pdfReportId} (ID: {existingPdf.Id})");
                return false;
            }

            // Crea il record con status PROCESSING
            var gapValidationPdf = new GapValidationPdf
            {
                PdfReportId = pdfReportId,
                Status = GapAlertStatus.PROCESSING,
                DocumentType = documentType,
                GapAlertId = gapAlertId,
                CreatedAt = DateTime.Now
            };

            _db.GapValidationPdfs.Add(gapValidationPdf);
            await _db.SaveChangesAsync();

            // Avvia la generazione in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await GenerateAndSaveAsync(pdfReportId, documentType);
                }
                catch (Exception ex)
                {
                    await _logger.Error(source, $"Background generation failed for {documentType} PDF", ex.ToString());
                }
            });

            await _logger.Info(source, $"Started {documentType} PDF generation for report {pdfReportId}");
            return true;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error starting {documentType} PDF generation for report {pdfReportId}", ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Genera e salva il PDF in base al tipo documento.
    /// </summary>
    private async Task GenerateAndSaveAsync(int pdfReportId, string documentType)
    {
        switch (documentType)
        {
            case GapValidationDocumentTypes.CERTIFICATION:
                await GenerateAndSaveCertificationAsync(pdfReportId);
                break;
            case GapValidationDocumentTypes.ESCALATION:
                await GenerateAndSaveEscalationAsync(pdfReportId);
                break;
            case GapValidationDocumentTypes.CONTRACT_BREACH:
                await GenerateAndSaveContractBreachAsync(pdfReportId);
                break;
        }
    }

    /// <summary>
    /// Genera il PDF di validazione per un report
    /// </summary>
    /// <param name="pdfReportId">ID del report PDF</param>
    /// <param name="documentType">Tipo documento per il template corretto</param>
    /// <param name="notes">Note operatore (opzionale, per ESCALATION e CONTRACT_BREACH)</param>
    public async Task<GapValidationPdfResult> GenerateCertificationPdfAsync(
        int pdfReportId,
        string documentType = GapValidationDocumentTypes.CERTIFICATION,
        string? notes = null)
    {
        const string source = "GapValidationPdfService.GenerateCertificationPdf";

        try
        {
            // 1. Recupera il report e i dati correlati
            var report = await _db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == pdfReportId);

            if (report == null)
            {
                return new GapValidationPdfResult
                {
                    Success = false,
                    ErrorMessage = "Report not found"
                };
            }

            // 2. Certifica i gap e salvali nel database
            var certifications = await _gapAnalysisService.CertifyGapsForReportAsync(pdfReportId);

            if (certifications.Count == 0)
            {
                return new GapValidationPdfResult
                {
                    Success = false,
                    ErrorMessage = "No gaps to certify found in the reporting period"
                };
            }

            // 3. Genera l'HTML con template specifico per il tipo documento
            var htmlContent = await GenerateCertificationHtml(report, certifications, _db, documentType, notes);

            // 4. Prepara le opzioni PDF con header/footer coerenti con gli stili di stampa PDF attuali
            var fontStyles = GapValidationTemplate.GetFontStyles();
            var vehicleVin = report.ClientVehicle?.Vin ?? "N/A";
            var pdfOptions = new PdfConversionOptions
            {
                HeaderTemplate = $@"
                    <html>
                    <head>
                        <style>
                            {fontStyles}
                            body {{
                                margin: 0;
                                padding: 0;
                                width: 100%;
                                height: 100%;
                                display: flex;
                                align-items: center;
                                justify-content: center;
                                font-family: 'Satoshi', 'Noto Color Emoji', sans-serif;
                                letter-spacing: normal;
                                word-spacing: normal;
                            }}
                            .header-content {{
                                font-size: 10px;
                                color: #ccc;
                                text-align: center;
                                border-bottom: 1px solid #ccc;
                                padding-bottom: 5px;
                                width: 100%;
                                letter-spacing: normal;
                                word-spacing: normal;
                            }}
                        </style>
                    </head>
                    <body>
                        <div class='header-content'>Gap Validation - {vehicleVin} - {DateTime.Now:yyyy-MM-dd HH:mm}</div>
                    </body>
                    </html>",
                FooterTemplate = $@"
                    <html>
                    <head>
                        <style>
                            {fontStyles}
                            body {{
                                margin: 0;
                                padding: 0;
                                width: 100%;
                                height: 100%;
                                display: flex;
                                align-items: center;
                                justify-content: center;
                                font-family: 'Satoshi', 'Noto Color Emoji', sans-serif;
                                letter-spacing: normal;
                                word-spacing: normal;
                            }}
                            .footer-content {{
                                font-size: 10px;
                                color: #ccc;
                                text-align: center;
                                border-top: 1px solid #ccc;
                                padding-top: 5px;
                                width: 100%;
                                letter-spacing: normal;
                                word-spacing: normal;
                            }}
                        </style>
                    </head>
                    <body>
                        <div class='footer-content'>
                            Pagina <span class='pageNumber'></span> di <span class='totalPages'></span> | DataPolar Gap Validation
                        </div>
                    </body>
                    </html>"
            };

            // 5. Converti in PDF con header/footer personalizzati
            var pdfBytes = await _pdfGenerationService.ConvertHtmlToPdfAsync(htmlContent, report, pdfOptions);

            // 6. Genera hash del PDF
            var pdfHash = GenericHelpers.ComputeContentHash(pdfBytes);

            // 7. Aggiorna le certificazioni con l'hash
            foreach (var cert in certifications)
            {
                cert.ValidationHash = pdfHash;
            }
            await _db.SaveChangesAsync();

            await _logger.Info(source, $"Generated certification PDF for report {pdfReportId}",
                $"Gaps: {certifications.Count}, PDF size: {pdfBytes.Length} bytes");

            return new GapValidationPdfResult
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
            return new GapValidationPdfResult
            {
                Success = false,
                ErrorMessage = $"Error while generating: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Genera e salva il PDF di validazione nella tabella GapValidationPdfs.
    /// Questo metodo è pensato per essere eseguito in background.
    /// Una volta completato, il PDF diventa immutabile.
    /// </summary>
    public async Task GenerateAndSaveCertificationAsync(int pdfReportId)
    {
        const string source = "GapValidationPdfService.GenerateAndSaveCertification";

        try
        {
            await _logger.Info(source, $"Starting gap certification generation for report {pdfReportId}");

            // 1. Genera il PDF usando il metodo esistente (con template CERTIFICATION)
            var result = await GenerateCertificationPdfAsync(pdfReportId, GapValidationDocumentTypes.CERTIFICATION);

            // 2. Recupera il record GapValidationPdf CERTIFICATION (già creato con status PROCESSING)
            var certPdf = await _db.GapValidationPdfs
                .FirstOrDefaultAsync(c => c.PdfReportId == pdfReportId &&
                                          c.DocumentType == GapValidationDocumentTypes.CERTIFICATION);

            if (certPdf == null)
            {
                await _logger.Error(source, $"GapValidationPdf CERTIFICATION record not found for report {pdfReportId}");
                return;
            }

            if (result.Success && result.PdfContent != null)
            {
                // 3. Salva il PDF e aggiorna lo stato a COMPLETED
                certPdf.PdfContent = result.PdfContent;
                certPdf.PdfHash = result.PdfHash;
                certPdf.Status = GapAlertStatus.COMPLETED;
                certPdf.GeneratedAt = DateTime.Now;
                certPdf.GapsCertified = result.GapsCertified;
                certPdf.AverageConfidence = result.AverageConfidence;

                // ===== TSA (Timestamp Authority) - Marca Temporale =====
                if (AppConfig.TSA_ENABLED && result.PdfContent != null && result.PdfHash != null)
                {
                    try
                    {
                        var tsaResult = await _tsaService.RequestTimestampAsync(result.PdfContent, result.PdfHash);
                        if (tsaResult.Success)
                        {
                            certPdf.TsaTimestamp = tsaResult.TimestampToken;
                            certPdf.TsaServerUrl = tsaResult.ServerUrl;
                            certPdf.TsaTimestampDate = tsaResult.TimestampDate;
                            certPdf.TsaMessageImprint = tsaResult.MessageImprint;
                            certPdf.TsaVerified = true;
                            await _logger.Info(source,
                                $"TSA_SUCCESS: Marca temporale ottenuta da {_tsaService.ProviderName} per gap validation PDF {certPdf.Id}");
                        }
                        else
                        {
                            certPdf.TsaError = tsaResult.ErrorMessage;
                            await _logger.Warning(source,
                                $"TSA_FAILED: Errore TSA per gap validation PDF {certPdf.Id}: {tsaResult.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        certPdf.TsaError = ex.Message;
                        await _logger.Error(source,
                            $"TSA_EXCEPTION: Eccezione TSA per gap validation PDF {certPdf.Id}: {ex.Message}");
                    }
                }

                await _db.SaveChangesAsync();

                await _logger.Info(source, $"Gap certification COMPLETED for report {pdfReportId}",
                    $"Gaps: {result.GapsCertified}, AvgConfidence: {result.AverageConfidence:F1}%, Hash: {result.PdfHash}");
            }
            else
            {
                // 4. In caso di errore, imposta lo stato ERROR
                certPdf.Status = GapAlertStatus.ERROR;
                await _db.SaveChangesAsync();

                await _logger.Error(source, $"Gap certification FAILED for report {pdfReportId}",
                    result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Exception during gap certification for report {pdfReportId}", ex.ToString());

            // Prova a impostare lo stato ERROR
            try
            {
                var certPdf = await _db.GapValidationPdfs
                    .FirstOrDefaultAsync(c => c.PdfReportId == pdfReportId &&
                                              c.DocumentType == GapValidationDocumentTypes.CERTIFICATION);

                if (certPdf != null)
                {
                    certPdf.Status = GapAlertStatus.ERROR;
                    await _db.SaveChangesAsync();
                }
            }
            catch
            {
                // Ignora errori nel tentativo di salvare lo stato ERROR
            }
        }
    }

    /// <summary>
    /// Genera e salva il PDF di ESCALATION.
    /// Simile alla certificazione ma con titolo e disclaimer diversi.
    /// </summary>
    private async Task GenerateAndSaveEscalationAsync(int pdfReportId)
    {
        const string source = "GapValidationPdfService.GenerateAndSaveEscalation";

        try
        {
            await _logger.Info(source, $"Starting gap ESCALATION generation for report {pdfReportId}");

            // 1. Genera il PDF con template ESCALATION (arancione)
            var result = await GenerateCertificationPdfAsync(pdfReportId, GapValidationDocumentTypes.ESCALATION);

            // 2. Recupera il record GapValidationPdf ESCALATION
            var escalationPdf = await _db.GapValidationPdfs
                .FirstOrDefaultAsync(c => c.PdfReportId == pdfReportId &&
                                          c.DocumentType == GapValidationDocumentTypes.ESCALATION);

            if (escalationPdf == null)
            {
                await _logger.Error(source, $"GapValidationPdf ESCALATION record not found for report {pdfReportId}");
                return;
            }

            if (result.Success && result.PdfContent != null)
            {
                // 3. Salva il PDF - per ESCALATION status rimane ESCALATED (non COMPLETED)
                escalationPdf.PdfContent = result.PdfContent;
                escalationPdf.PdfHash = result.PdfHash;
                escalationPdf.Status = GapAlertStatus.ESCALATED;
                escalationPdf.GeneratedAt = DateTime.Now;
                escalationPdf.GapsCertified = result.GapsCertified;
                escalationPdf.AverageConfidence = result.AverageConfidence;

                // TSA Marca Temporale
                if (AppConfig.TSA_ENABLED && result.PdfContent != null && result.PdfHash != null)
                {
                    await ApplyTsaTimestampAsync(escalationPdf, result.PdfContent, result.PdfHash, source);
                }

                await _db.SaveChangesAsync();

                await _logger.Info(source, $"Gap ESCALATION COMPLETED for report {pdfReportId}",
                    $"Gaps: {result.GapsCertified}, AvgConfidence: {result.AverageConfidence:F1}%");
            }
            else
            {
                escalationPdf.Status = GapAlertStatus.ERROR;
                await _db.SaveChangesAsync();
                await _logger.Error(source, $"Gap ESCALATION FAILED for report {pdfReportId}",
                    result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Exception during gap ESCALATION for report {pdfReportId}", ex.ToString());
            await SetPdfStatusToErrorAsync(pdfReportId, GapValidationDocumentTypes.ESCALATION);
        }
    }

    /// <summary>
    /// Genera e salva il PDF di CONTRACT_BREACH.
    /// </summary>
    private async Task GenerateAndSaveContractBreachAsync(int pdfReportId)
    {
        const string source = "GapValidationPdfService.GenerateAndSaveContractBreach";

        try
        {
            await _logger.Info(source, $"Starting gap CONTRACT_BREACH generation for report {pdfReportId}");

            // 1. Genera il PDF con template CONTRACT_BREACH (rosso)
            var result = await GenerateCertificationPdfAsync(pdfReportId, GapValidationDocumentTypes.CONTRACT_BREACH);

            // 2. Recupera il record GapValidationPdf CONTRACT_BREACH
            var breachPdf = await _db.GapValidationPdfs
                .FirstOrDefaultAsync(c => c.PdfReportId == pdfReportId &&
                                          c.DocumentType == GapValidationDocumentTypes.CONTRACT_BREACH);

            if (breachPdf == null)
            {
                await _logger.Error(source, $"GapValidationPdf CONTRACT_BREACH record not found for report {pdfReportId}");
                return;
            }

            if (result.Success && result.PdfContent != null)
            {
                // 3. Salva il PDF - per CONTRACT_BREACH status è CONTRACT_BREACH (finale)
                breachPdf.PdfContent = result.PdfContent;
                breachPdf.PdfHash = result.PdfHash;
                breachPdf.Status = GapAlertStatus.CONTRACT_BREACH;
                breachPdf.GeneratedAt = DateTime.Now;
                breachPdf.GapsCertified = result.GapsCertified;
                breachPdf.AverageConfidence = result.AverageConfidence;

                // TSA Marca Temporale
                if (AppConfig.TSA_ENABLED && result.PdfContent != null && result.PdfHash != null)
                {
                    await ApplyTsaTimestampAsync(breachPdf, result.PdfContent, result.PdfHash, source);
                }

                await _db.SaveChangesAsync();

                await _logger.Info(source, $"Gap CONTRACT_BREACH COMPLETED for report {pdfReportId}",
                    $"Gaps: {result.GapsCertified}, AvgConfidence: {result.AverageConfidence:F1}%");
            }
            else
            {
                breachPdf.Status = GapAlertStatus.ERROR;
                await _db.SaveChangesAsync();
                await _logger.Error(source, $"Gap CONTRACT_BREACH FAILED for report {pdfReportId}",
                    result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Exception during gap CONTRACT_BREACH for report {pdfReportId}", ex.ToString());
            await SetPdfStatusToErrorAsync(pdfReportId, GapValidationDocumentTypes.CONTRACT_BREACH);
        }
    }

    /// <summary>
    /// Applica marca temporale TSA al PDF
    /// </summary>
    private async Task ApplyTsaTimestampAsync(GapValidationPdf pdf, byte[] pdfContent, string pdfHash, string source)
    {
        try
        {
            var tsaResult = await _tsaService.RequestTimestampAsync(pdfContent, pdfHash);
            if (tsaResult.Success)
            {
                pdf.TsaTimestamp = tsaResult.TimestampToken;
                pdf.TsaServerUrl = tsaResult.ServerUrl;
                pdf.TsaTimestampDate = tsaResult.TimestampDate;
                pdf.TsaMessageImprint = tsaResult.MessageImprint;
                pdf.TsaVerified = true;
                await _logger.Info(source,
                    $"TSA_SUCCESS: Marca temporale ottenuta da {_tsaService.ProviderName} per PDF {pdf.Id}");
            }
            else
            {
                pdf.TsaError = tsaResult.ErrorMessage;
                await _logger.Warning(source,
                    $"TSA_FAILED: Errore TSA per PDF {pdf.Id}: {tsaResult.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            pdf.TsaError = ex.Message;
            await _logger.Error(source,
                $"TSA_EXCEPTION: Eccezione TSA per PDF {pdf.Id}: {ex.Message}");
        }
    }

    /// <summary>
    /// Imposta lo status ERROR per un PDF in caso di eccezione
    /// </summary>
    private async Task SetPdfStatusToErrorAsync(int pdfReportId, string documentType)
    {
        try
        {
            var pdf = await _db.GapValidationPdfs
                .FirstOrDefaultAsync(c => c.PdfReportId == pdfReportId && c.DocumentType == documentType);

            if (pdf != null)
            {
                pdf.Status = GapAlertStatus.ERROR;
                await _db.SaveChangesAsync();
            }
        }
        catch
        {
            // Ignora errori nel tentativo di salvare lo stato ERROR
        }
    }

    /// <summary>
    /// Genera l'HTML della validazione con template specifico per tipo documento
    /// </summary>
    private static async Task<string> GenerateCertificationHtml(
        PdfReport report,
        List<GapValidation> certifications,
        PolarDriveDbContext db,
        string documentType = GapValidationDocumentTypes.CERTIFICATION,
        string? notes = null)
    {
        var company = report.ClientCompany;
        var vehicle = report.ClientVehicle;

        var sb = new StringBuilder();

        // Seleziona CSS e contenuti in base al tipo documento
        var (css, title, subtitle, badgeClass, badgeText, footerText) = documentType switch
        {
            GapValidationDocumentTypes.ESCALATION => (
                GapValidationTemplate.GetEscalationCss(),
                "SEGNALAZIONE ESCALATION GAP",
                "Documento di Attenzione - In Attesa Decisione",
                "escalation-badge",
                "⚠️ ESCALATO",
                "Documento in attesa di decisione finale"
            ),
            GapValidationDocumentTypes.CONTRACT_BREACH => (
                GapValidationTemplate.GetContractBreachCss(),
                "DICHIARAZIONE VIOLAZIONE CONTRATTUALE",
                "Contract Breach - Documento Ufficiale",
                "breach-badge",
                "⛔ CONTRACT BREACH",
                "Documento legale - Violazione contrattuale dichiarata"
            ),
            _ => (
                GapValidationTemplate.GetCertificationCss(),
                "CERTIFICAZIONE VALIDAZIONE GAP",
                "Documento di Conformità Operativa",
                "certification-badge",
                "✓ CERTIFICATO",
                "Documento certificato - Validità fiscale confermata"
            )
        };

        // Header HTML
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='it'>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset='UTF-8'>");
        sb.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        sb.AppendLine($"<title>{title} - DataPolar</title>");
        sb.AppendLine($"<style>{css}</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header documento
        sb.AppendLine("<div class='header'>");
        sb.AppendLine("<div class='logo-section'>");
        sb.AppendLine("<h1>DataPolar</h1>");
        sb.AppendLine($"<p class='subtitle'>{subtitle}</p>");
        sb.AppendLine($"<span class='{badgeClass}'>{badgeText}</span>");
        sb.AppendLine("</div>");
        sb.AppendLine($"<div class='doc-info'>");
        sb.AppendLine($"<p><strong>Data generazione:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</p>");
        sb.AppendLine($"<p><strong>Report di riferimento:</strong> #{report.Id}</p>");
        sb.AppendLine($"<p><strong>Tipo documento:</strong> {documentType}</p>");
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

        // Sezione Outages (se presenti)
        var outageStats = await CalculateOutageStatisticsAsync(certifications, db);
        if (outageStats.TotalOutages > 0)
        {
            sb.AppendLine("<div class='outage-summary-section'>");
            sb.AppendLine("<h3>Riepilogo Interruzioni di Servizio (Outages)</h3>");

            sb.AppendLine("<div class='outage-stats-grid'>");
            sb.AppendLine($"<div class='outage-stat-box'><div class='stat-value'>{outageStats.TotalOutages}</div><div class='stat-label'>Interruzioni rilevate</div></div>");
            sb.AppendLine($"<div class='outage-stat-box'><div class='stat-value'>{outageStats.TotalDowntimeDays}g {outageStats.TotalDowntimeHours % 24}h</div><div class='stat-label'>Downtime totale</div></div>");
            sb.AppendLine($"<div class='outage-stat-box'><div class='stat-value'>{outageStats.GapsAffectedCount}</div><div class='stat-label'>Gap giustificati ({outageStats.GapsAffectedPercentage:F1}%)</div></div>");
            sb.AppendLine($"<div class='outage-stat-box'><div class='stat-value'>{outageStats.AvgConfidenceWithOutage:F1}%</div><div class='stat-label'>Confidenza media (con outage)</div></div>");
            sb.AppendLine("</div>");

            // Mini-tabella dettaglio outages
            if (outageStats.OutageDetails.Count > 0)
            {
                sb.AppendLine("<table class='outage-details-table'><thead><tr>");
                sb.AppendLine("<th>Tipo</th><th>Brand/Veicolo</th><th>Inizio</th><th>Fine</th><th>Durata</th><th>Gap</th>");
                sb.AppendLine("</tr></thead><tbody>");

                foreach (var detail in outageStats.OutageDetails)
                {
                    var typeClass = detail.IsFleetApi ? "fleet-api-outage" : "vehicle-outage";
                    var typeLabel = detail.IsFleetApi ? "🔴 Fleet API" : "⚠️ Veicolo";

                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td><span class='outage-type-badge {typeClass}'>{typeLabel}</span></td>");
                    sb.AppendLine($"<td>{detail.BrandOrVehicle}</td>");
                    sb.AppendLine($"<td>{detail.OutageStart:dd/MM/yyyy HH:mm}</td>");
                    sb.AppendLine($"<td>{(detail.OutageEnd?.ToString("dd/MM/yyyy HH:mm") ?? "In corso")}</td>");
                    sb.AppendLine($"<td>{detail.DurationHours}h</td>");
                    sb.AppendLine($"<td>{detail.GapsImpacted}</td>");
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</tbody></table>");
            }

            sb.AppendLine("</div>");
        }

        // Disclaimer diversificato per tipo documento
        sb.AppendLine("<div class='disclaimer'>");

        switch (documentType)
        {
            case GapValidationDocumentTypes.ESCALATION:
                sb.AppendLine("<h3>⚠️ SEGNALAZIONE ESCALATION</h3>");
                sb.AppendLine("<p>Questo documento rappresenta una <strong>segnalazione di attenzione</strong> per anomalie rilevate nei gap del periodo analizzato. L'escalation è stata avviata per consentire una revisione approfondita prima della decisione finale.</p>");
                sb.AppendLine("<h4>STATO ATTUALE</h4>");
                sb.AppendLine("<p>Il caso è <strong>IN ATTESA DI DECISIONE</strong>. È necessario valutare se:</p>");
                sb.AppendLine("<ul>");
                sb.AppendLine("<li>Certificare il periodo analizzato (validazione positiva)</li>");
                sb.AppendLine("<li>Dichiarare una violazione contrattuale (contract breach)</li>");
                sb.AppendLine("</ul>");
                sb.AppendLine("<p class='important'>ATTENZIONE ➜ Questo documento non ha valore legale definitivo. Rappresenta uno stato intermedio in attesa di risoluzione.</p>");
                break;

            case GapValidationDocumentTypes.CONTRACT_BREACH:
                sb.AppendLine("<h3>⛔ DICHIARAZIONE DI VIOLAZIONE CONTRATTUALE</h3>");
                sb.AppendLine("<p>Con il presente documento DataPolar S.r.l. <strong>dichiara formalmente</strong> che il laboratorio mobile identificato non ha rispettato gli obblighi contrattuali relativi alla raccolta dati per il periodo indicato.</p>");
                sb.AppendLine("<h4>CONSEGUENZE CONTRATTUALI</h4>");
                sb.AppendLine("<p>La violazione contrattuale comporta:</p>");
                sb.AppendLine("<ul>");
                sb.AppendLine("<li>Impossibilità di validare i dati per il periodo indicato</li>");
                sb.AppendLine("<li>Potenziale applicazione di penali contrattuali</li>");
                sb.AppendLine("<li>Revisione degli accordi di servizio</li>");
                sb.AppendLine("<li>Registrazione della violazione nel sistema di monitoraggio</li>");
                sb.AppendLine("</ul>");
                sb.AppendLine("<p class='important'>AVVISO LEGALE ➜ Questo documento ha valore legale e può essere utilizzato come prova in sede contrattuale o giudiziaria.</p>");
                break;

            default: // CERTIFICATION
                sb.AppendLine("<h3>DICHIARAZIONE DI VALIDAZIONE PROBABILISTICA</h3>");
                sb.AppendLine("<p>DataPolar S.r.l. attesta che, sulla base dell'analisi statistica dei dati telemetrici raccolti dal laboratorio mobile identificato in questo documento, per i periodi indicati nella tabella sottostante sussiste una probabilità (espressa in percentuale) che il veicolo fosse operativo per le finalità contrattuali di raccolta dati.</p>");
                sb.AppendLine("<h4>NOTA METODOLOGICA</h4>");
                sb.AppendLine("<p>I valori di confidenza sono calcolati analizzando:</p>");
                sb.AppendLine("<ul>");
                sb.AppendLine("<li>Continuità temporale dei record adiacenti (30%)</li>");
                sb.AppendLine("<li>Progressione dei parametri operativi - batteria, stato veicolo (25%)</li>");
                sb.AppendLine("<li>Pattern storici di utilizzo del veicolo (20%)</li>");
                sb.AppendLine("<li>Durata del gap temporale (15%)</li>");
                sb.AppendLine("<li>Affidabilità storica complessiva (10%)</li>");
                sb.AppendLine("</ul>");
                sb.AppendLine("<p class='important'>CERTIFICAZIONE CONFERMATA ➜ I gap analizzati sono stati validati con esito positivo. Il laboratorio mobile risulta conforme agli obblighi contrattuali per il periodo indicato.</p>");
                break;
        }

        sb.AppendLine("</div>");

        // Sezione Note Operatore (solo per ESCALATION e CONTRACT_BREACH)
        if (!string.IsNullOrWhiteSpace(notes) &&
            (documentType == GapValidationDocumentTypes.ESCALATION ||
             documentType == GapValidationDocumentTypes.CONTRACT_BREACH))
        {
            sb.AppendLine("<div class='notes-section'>");
            sb.AppendLine("<h3>📝 Note Operatore</h3>");
            sb.AppendLine($"<div class='notes-content'>{System.Web.HttpUtility.HtmlEncode(notes)}</div>");
            sb.AppendLine("</div>");
        }

        // Tabella gap
        sb.AppendLine("<div class='gaps-section'>");
        sb.AppendLine("<h3>Dettaglio Record da Validare Validati</h3>");
        sb.AppendLine("<table class='gaps-table'>");
        sb.AppendLine("<thead>");
        sb.AppendLine("<tr>");
        sb.AppendLine("<th>Timestamp</th>");
        sb.AppendLine("<th>Confidenza</th>");
        sb.AppendLine("<th>Outage</th>");
        sb.AppendLine("<th>Giustificazione</th>");
        sb.AppendLine("</tr>");
        sb.AppendLine("</thead>");
        sb.AppendLine("<tbody>");

        foreach (var cert in certifications.OrderBy(c => c.GapTimestamp))
        {
            var confidenceClass = cert.ConfidencePercentage >= 80 ? "high" :
                                  cert.ConfidencePercentage >= 60 ? "medium" : "low";

            GapAnalysisFactors? factors = null;
            try
            {
                factors = JsonSerializer.Deserialize<GapAnalysisFactors>(cert.AnalysisFactorsJson);
            }
            catch { }

            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{cert.GapTimestamp:dd/MM/yyyy HH:mm}</td>");
            sb.AppendLine($"<td><span class='confidence {confidenceClass}'>{cert.ConfidencePercentage:F1}%</span></td>");

            // Colonna Outage
            sb.AppendLine("<td class='outage-cell'>");
            if (factors?.OutageId.HasValue == true)
            {
                var typeClass = factors.OutageType == OutageConstants.OUTAGE_FLEET_API
                    ? "fleet-api-badge" : "vehicle-badge";
                var icon = factors.OutageType == OutageConstants.OUTAGE_FLEET_API ? "🔴" : "⚠️";
                var label = factors.OutageType == OutageConstants.OUTAGE_FLEET_API ? "Fleet API" : "Vehicle";

                sb.AppendLine($"<span class='outage-badge {typeClass}'>{icon} {label}</span>");
                if (!string.IsNullOrEmpty(factors.OutageBrand))
                {
                    sb.AppendLine($"<br/><span class='outage-brand'>{factors.OutageBrand}</span>");
                }
            }
            else
            {
                sb.AppendLine("<span class='no-outage'>-</span>");
            }
            sb.AppendLine("</td>");

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
        sb.AppendLine($"<p class='footer-type'>{footerText}</p>");
        sb.AppendLine($"<p><strong>Generato dalla piattaforma PolarDrive™</strong></p>");
        sb.AppendLine($"<p>© {DateTime.Now.Year} DataPolar S.r.l. - Tutti i diritti riservati</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Genera hash del documento di validazione
    /// </summary>
    private static string GenerateCertificationDocumentHash(List<GapValidation> certifications)
    {
        var data = string.Join("|", certifications.Select(c =>
            $"{c.GapTimestamp:O}:{c.ConfidencePercentage}"));
        data += $"|{DateTime.Now:O}";

        return GenericHelpers.ComputeContentHash(data);
    }

    /// <summary>
    /// Calcola statistiche sugli outages che hanno impattato i gap certificati
    /// </summary>
    private static async Task<OutageStatistics> CalculateOutageStatisticsAsync(
        List<GapValidation> certifications,
        PolarDriveDbContext db)
    {
        var stats = new OutageStatistics();

        // Estrai outageIds unici dalle certificazioni
        var outageIds = certifications
            .Select(c =>
            {
                try
                {
                    var factors = JsonSerializer.Deserialize<GapAnalysisFactors>(c.AnalysisFactorsJson);
                    return factors?.OutageId;
                }
                catch { return null; }
            })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (outageIds.Count == 0)
            return stats;

        // Query outages completi
        var outages = await db.OutagePeriods
            .Where(o => outageIds.Contains(o.Id))
            .ToListAsync();

        stats.TotalOutages = outages.Count;

        // Calcola downtime totale
        var totalDowntime = TimeSpan.Zero;
        foreach (var outage in outages)
        {
            var end = outage.OutageEnd ?? DateTime.Now;
            totalDowntime += (end - outage.OutageStart);
        }
        stats.TotalDowntimeHours = (int)totalDowntime.TotalHours;
        stats.TotalDowntimeDays = (int)totalDowntime.TotalDays;

        // Gap affetti da outages
        var gapsWithOutage = certifications
            .Where(c =>
            {
                try
                {
                    var factors = JsonSerializer.Deserialize<GapAnalysisFactors>(c.AnalysisFactorsJson);
                    return factors?.OutageId.HasValue ?? false;
                }
                catch { return false; }
            })
            .ToList();

        stats.GapsAffectedCount = gapsWithOutage.Count;
        stats.GapsAffectedPercentage = certifications.Count > 0
            ? (gapsWithOutage.Count / (double)certifications.Count) * 100
            : 0;
        stats.AvgConfidenceWithOutage = gapsWithOutage.Any()
            ? gapsWithOutage.Average(c => c.ConfidencePercentage) : 0;

        // Dettagli outages: conta quanti gap impattati per ogni outage
        var outageGapCounts = certifications
            .Select(c =>
            {
                try
                {
                    var factors = JsonSerializer.Deserialize<GapAnalysisFactors>(c.AnalysisFactorsJson);
                    return factors?.OutageId;
                }
                catch { return null; }
            })
            .Where(id => id.HasValue)
            .GroupBy(id => id!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        stats.OutageDetails = outages
            .Select(o => new OutageDetail
            {
                IsFleetApi = o.OutageType == OutageConstants.OUTAGE_FLEET_API,
                BrandOrVehicle = o.OutageBrand ?? $"VIN {o.VehicleId}",
                OutageStart = o.OutageStart,
                OutageEnd = o.OutageEnd,
                DurationHours = (int)(o.OutageEnd.HasValue
                    ? (o.OutageEnd.Value - o.OutageStart).TotalHours
                    : (DateTime.Now - o.OutageStart).TotalHours),
                GapsImpacted = outageGapCounts.GetValueOrDefault(o.Id, 0)
            })
            .OrderByDescending(o => o.GapsImpacted)
            .Take(5)
            .ToList();

        return stats;
    }

    private class OutageStatistics
    {
        public int TotalOutages { get; set; }
        public int TotalDowntimeDays { get; set; }
        public int TotalDowntimeHours { get; set; }
        public int GapsAffectedCount { get; set; }
        public double GapsAffectedPercentage { get; set; }
        public double AvgConfidenceWithOutage { get; set; }
        public List<OutageDetail> OutageDetails { get; set; } = [];
    }

    private class OutageDetail
    {
        public bool IsFleetApi { get; set; }
        public string BrandOrVehicle { get; set; } = "";
        public DateTime OutageStart { get; set; }
        public DateTime? OutageEnd { get; set; }
        public int DurationHours { get; set; }
        public int GapsImpacted { get; set; }
    }
}

/// <summary>
/// Risultato della generazione del PDF di validazione
/// </summary>
public class GapValidationPdfResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? PdfContent { get; set; }
    public string? PdfHash { get; set; }
    public int GapsCertified { get; set; }
    public double AverageConfidence { get; set; }
}