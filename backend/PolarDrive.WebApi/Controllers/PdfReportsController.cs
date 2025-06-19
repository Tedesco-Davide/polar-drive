using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.WebApi.PolarAiReports;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PdfReportsController(PolarDriveDbContext db) : ControllerBase
{
    private readonly PolarDriveLogger _logger = new(db);

    /// <summary>
    /// Metodo Get semplificato
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PdfReportDTO>>> Get()
    {
        const string source = "PdfReportsController.Get";

        try
        {
            await _logger.Info(source, "Richiesta lista report PDF");

            var reports = await db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .OrderByDescending(r => r.GeneratedAt)
                .ToListAsync();

            await _logger.Debug(source, "Report recuperati dal database", $"Count: {reports.Count}");

            var result = new List<PdfReportDTO>();

            foreach (var r in reports)
            {
                var pdfPath = GetReportFilePath(r, "pdf");
                var htmlPath = GetReportFilePath(r, "html");

                // Fallback: Se non trova nel path principale, cerca nell'alternativo
                var pdfExists = System.IO.File.Exists(pdfPath);
                var htmlExists = System.IO.File.Exists(htmlPath);

                if (!pdfExists || !htmlExists)
                {
                    var alternatePaths = GetAlternateFilePaths(r);

                    if (!pdfExists && System.IO.File.Exists(alternatePaths.alternatePdfPath))
                    {
                        pdfPath = alternatePaths.alternatePdfPath;
                        pdfExists = true;
                    }

                    if (!htmlExists && System.IO.File.Exists(alternatePaths.alternateHtmlPath))
                    {
                        htmlPath = alternatePaths.alternateHtmlPath;
                        htmlExists = true;
                    }
                }

                var pdfSize = pdfExists ? new FileInfo(pdfPath).Length : 0;
                var htmlSize = htmlExists ? new FileInfo(htmlPath).Length : 0;

                var dataCount = await CountDataRecordsForReport(r);

                var monitoringDuration = (r.ReportPeriodEnd - r.ReportPeriodStart).TotalHours;
                var isRegenerated = r.RegenerationCount > 0;
                var lastModified = r.GeneratedAt?.ToString("o");

                var dto = new PdfReportDTO
                {
                    Id = r.Id,
                    ReportPeriodStart = r.ReportPeriodStart.ToString("o"),
                    ReportPeriodEnd = r.ReportPeriodEnd.ToString("o"),
                    GeneratedAt = r.GeneratedAt?.ToString("o"),
                    CompanyVatNumber = r.ClientCompany?.VatNumber ?? "",
                    CompanyName = r.ClientCompany?.Name ?? "",
                    VehicleVin = r.ClientVehicle?.Vin ?? "",
                    VehicleModel = r.ClientVehicle?.Model ?? "",
                    Notes = r.Notes,
                    HasPdfFile = pdfExists,
                    HasHtmlFile = htmlExists,
                    DataRecordsCount = dataCount,
                    PdfFileSize = pdfSize,
                    HtmlFileSize = htmlSize,
                    MonitoringDurationHours = Math.Round(monitoringDuration, 1),
                    LastModified = lastModified,
                    IsRegenerated = isRegenerated,
                    RegenerationCount = r.RegenerationCount,
                    ReportType = DetermineReportType(r, dataCount),
                    Status = DetermineReportStatus(pdfExists, htmlExists, dataCount),
                };

                result.Add(dto);
            }

            await _logger.Info(source, "Mapping DTO completato", $"Processed: {result.Count} reports");
            return Ok(result);
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore recupero lista report", ex.ToString());
            return StatusCode(500, "Errore interno server");
        }
    }

    /// <summary>
    /// Fallback che cerca in directory alternative
    /// </summary>
    // Aggiorna anche GetAlternateFilePaths:
    private (string alternatePdfPath, string alternateHtmlPath) GetAlternateFilePaths(Data.Entities.PdfReport report)
    {
        // Directory reports standard
        var alternateDir = Path.Combine("storage", "reports",
            report.ReportPeriodStart.Year.ToString(),
            report.ReportPeriodStart.Month.ToString("D2"));

        var alternateFileName = $"PolarDrive_Report_{report.Id}";

        return (
            alternatePdfPath: Path.Combine(alternateDir, $"{alternateFileName}.pdf"),
            alternateHtmlPath: Path.Combine(alternateDir, $"{alternateFileName}.html")
        );
    }

    /// <summary>
    /// Determina il tipo di report
    /// </summary>
    private static string DetermineReportType(Data.Entities.PdfReport report, int dataCount)
    {
        if (dataCount == 0)
            return "admin.vehicleReports.reporttypenodata";

        var duration = (report.ReportPeriodEnd - report.ReportPeriodStart).TotalHours;

        if (duration >= 24) // 1 giorno
            return "admin.vehicleReports.reporttypedaily";

        if (duration >= 168) // 7 giorni  
            return "admin.vehicleReports.reporttypeweekly";

        if (duration >= 720) // ~30 giorni
            return "admin.vehicleReports.reporttypemonthly";

        return "admin.vehicleReports.reporttypedailypartial";
    }

    /// <summary>
    /// Determina lo status del report
    /// </summary>
    private static string DetermineReportStatus(bool pdfExists, bool htmlExists, int dataCount)
    {
        if (pdfExists)
            return "PDF-READY";

        if (htmlExists)
            return "HTML-ONLY";

        if (dataCount == 0)
            return "NO-DATA";

        if (dataCount < 5)
            return "WAITING-RECORDS";

        return "GENERATE-READY";
    }

    /// <summary>
    /// Conta tutti i record del veicolo
    /// </summary>
    private async Task<int> CountDataRecordsForReport(Data.Entities.PdfReport report)
    {
        return await db.VehiclesData
            .Where(vd => vd.VehicleId == report.ClientVehicleId)
            .CountAsync();
    }

    /// <summary>
    /// Aggiorna le note di un report
    /// </summary>
    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        const string source = "PdfReportsController.PatchNotes";

        try
        {
            var entity = await db.PdfReports.FindAsync(id);

            if (entity == null)
            {
                await _logger.Warning(source, "Report PDF non trovato", $"ReportId: {id}");
                return NotFound($"Report {id} non trovato");
            }

            if (!body.TryGetProperty("notes", out var notesProp))
            {
                await _logger.Warning(source, "Campo 'notes' mancante", $"ReportId: {id}");
                return BadRequest("Campo 'notes' richiesto nel body della richiesta");
            }

            var newNotes = notesProp.GetString() ?? string.Empty;
            var oldNotes = entity.Notes ?? string.Empty;

            entity.Notes = newNotes;
            await db.SaveChangesAsync();

            await _logger.Info(source, "Note del report aggiornate",
                $"ReportId: {id}, OldLength: {oldNotes.Length}, NewLength: {newNotes.Length}");

            return Ok(new
            {
                success = true,
                message = "Note aggiornate con successo",
                reportId = id,
                notesLength = newNotes.Length
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore aggiornamento note", $"ReportId: {id}, Error: {ex}");
            return StatusCode(500, "Errore interno server");
        }
    }

    /// <summary>
    /// Download PDF con rigenerazione automatica se necessario
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadPdf(int id)
    {
        const string source = "PdfReportsController.DownloadPdf";

        try
        {
            await _logger.Info(source, "Richiesta download PDF", $"ReportId: {id}");

            // 1. Trova il report
            var report = await db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                await _logger.Warning(source, "Report non trovato in database", $"ReportId: {id}");
                return NotFound($"Report {id} non trovato");
            }

            // 2. Controlla se rigenerare
            var forceRegenerate = HttpContext.Request.Query["regenerate"].ToString().ToLower() == "true";
            var pdfPath = GetReportFilePath(report, "pdf");
            var htmlPath = GetReportFilePath(report, "html");

            // 3. Rigenera se necessario
            if (!System.IO.File.Exists(pdfPath) || forceRegenerate)
            {
                await _logger.Info(source, "Avvio rigenerazione PDF",
                    $"ReportId: {id}, Force: {forceRegenerate}, FileExists: {System.IO.File.Exists(pdfPath)}");

                var regenerationResult = await RegenerateReportFiles(report);

                if (!regenerationResult.Success)
                {
                    var errorMessage = regenerationResult.ErrorMessage ?? "Errore sconosciuto durante la rigenerazione";
                    await _logger.Error(source, "Rigenerazione fallita", $"ReportId: {id}, Error: {errorMessage}");
                    return StatusCode(500, errorMessage);
                }

                // Aggiorna il path solo se la rigenerazione ha prodotto un nuovo file
                if (!string.IsNullOrEmpty(regenerationResult.PdfPath))
                {
                    pdfPath = regenerationResult.PdfPath;
                }
            }

            // 4. Leggi e invia il file
            if (System.IO.File.Exists(pdfPath))
            {
                var pdfBytes = await System.IO.File.ReadAllBytesAsync(pdfPath);
                var fileName = $"PolarDrive_Report_{id}_{report.ClientVehicle?.Vin}_{report.ReportPeriodStart:yyyyMMdd}.pdf";

                await _logger.Info(source, "PDF inviato con successo",
                    $"ReportId: {id}, Size: {pdfBytes.Length} bytes, FileName: {fileName}");

                return File(pdfBytes, "application/pdf", fileName);
            }

            // 5. Fallback: prova HTML
            if (System.IO.File.Exists(htmlPath))
            {
                var htmlBytes = await System.IO.File.ReadAllBytesAsync(htmlPath);
                var fileName = $"PolarDrive_Report_{id}_{report.ClientVehicle?.Vin}_{report.ReportPeriodStart:yyyyMMdd}.html";

                await _logger.Info(source, "HTML inviato come fallback",
                    $"ReportId: {id}, Size: {htmlBytes.Length} bytes");

                return File(htmlBytes, "text/html", fileName);
            }

            // 6. Nessun file disponibile
            await _logger.Warning(source, "Nessun file disponibile per il download", $"ReportId: {id}");
            return NotFound("File del report non disponibile");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore generale download PDF", $"ReportId: {id}, Error: {ex}");
            return StatusCode(500, "Errore interno server durante download");
        }
    }

    /// <summary>
    /// Forza la rigenerazione di un report
    /// </summary>
    [HttpPost("{id}/regenerate")]
    public async Task<IActionResult> RegenerateReport(int id)
    {
        const string source = "PdfReportsController.RegenerateReport";

        try
        {
            await _logger.Info(source, "Richiesta rigenerazione manuale", $"ReportId: {id}");

            var report = await db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound($"Report {id} non trovato");
            }

            var result = await RegenerateReportFiles(report);

            if (result.Success)
            {
                await _logger.Info(source, "Rigenerazione completata con successo", $"ReportId: {id}");

                return Ok(new
                {
                    success = true,
                    message = "Report rigenerato con successo",
                    reportId = id,
                    pdfPath = result.PdfPath,
                    htmlPath = result.HtmlPath,
                    dataRecords = result.DataRecordsProcessed,
                    regeneratedAt = DateTime.UtcNow
                });
            }
            else
            {
                await _logger.Error(source, "Rigenerazione fallita", $"ReportId: {id}, Error: {result.ErrorMessage}");

                return BadRequest(new
                {
                    success = false,
                    message = result.ErrorMessage,
                    reportId = id
                });
            }
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore rigenerazione manuale", $"ReportId: {id}, Error: {ex}");
            return StatusCode(500, "Errore interno server");
        }
    }

    /// <summary>
    /// Ottiene le statistiche di un report
    /// </summary>
    [HttpGet("{id}/stats")]
    public async Task<IActionResult> GetReportStats(int id)
    {
        const string source = "PdfReportsController.GetReportStats";

        try
        {
            var report = await db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound($"Report {id} non trovato");
            }

            var dataCount = await db.VehiclesData
                .Where(vd => vd.VehicleId == report.ClientVehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .CountAsync();

            var firstRecord = await db.VehiclesData
                .Where(vd => vd.VehicleId == report.ClientVehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var lastRecord = await db.VehiclesData
                .Where(vd => vd.VehicleId == report.ClientVehicleId &&
                           vd.Timestamp >= report.ReportPeriodStart &&
                           vd.Timestamp <= report.ReportPeriodEnd)
                .OrderByDescending(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var pdfPath = GetReportFilePath(report, "pdf");
            var htmlPath = GetReportFilePath(report, "html");

            var stats = new
            {
                reportId = id,
                vehicleVin = report.ClientVehicle?.Vin,
                companyName = report.ClientCompany?.Name,
                periodStart = report.ReportPeriodStart,
                periodEnd = report.ReportPeriodEnd,
                generatedAt = report.GeneratedAt,
                notes = report.Notes,
                dataRecords = dataCount,
                firstRecord = firstRecord,
                lastRecord = lastRecord,
                monitoringDuration = lastRecord != default ? (lastRecord - firstRecord).TotalHours : 0,
                files = new
                {
                    pdfExists = System.IO.File.Exists(pdfPath),
                    htmlExists = System.IO.File.Exists(htmlPath),
                    pdfSize = System.IO.File.Exists(pdfPath) ? new FileInfo(pdfPath).Length : 0,
                    htmlSize = System.IO.File.Exists(htmlPath) ? new FileInfo(htmlPath).Length : 0,
                    pdfPath = System.IO.File.Exists(pdfPath) ? pdfPath : null,
                    htmlPath = System.IO.File.Exists(htmlPath) ? htmlPath : null
                }
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore recupero statistiche", $"ReportId: {id}, Error: {ex}");
            return StatusCode(500, "Errore interno server");
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Rigenerazione report con analisi AI
    /// </summary>
    private async Task<RegenerationResult> RegenerateReportFiles(Data.Entities.PdfReport report)
    {
        const string source = "PdfReportsController.RegenerateReportFiles";

        try
        {
            await _logger.Info(source, "Avvio rigenerazione con analisi AI",
                $"ReportId: {report.Id}, VehicleId: {report.ClientVehicleId}");

            // Genera insights con AI
            var aiGenerator = new PolarAiReportGenerator(db);
            var insights = await aiGenerator.GenerateInsightsAsync(report.ClientVehicleId);

            if (string.IsNullOrWhiteSpace(insights))
            {
                insights = @"
            <h2>Report Generato</h2>
            <p>Report generato automaticamente per il veicolo.</p>
            <p>Periodo: " + report.ReportPeriodStart.ToString("dd/MM/yyyy") +
                        " - " + report.ReportPeriodEnd.ToString("dd/MM/yyyy") + @"</p>
            <p>Al momento non sono disponibili dati sufficienti per un'analisi dettagliata.</p>
        ";
            }

            await _logger.Info(source, "Insights PolarAi generati",
                $"ReportId: {report.Id}, Insights length: {insights.Length} chars");

            // Genera HTML con insights AI
            var htmlService = new HtmlReportService(db);
            var htmlOptions = new HtmlReportOptions
            {
                ShowDetailedStats = true,
                ShowRawData = false,
                ReportType = "PolarAi Analysis",
                AdditionalCss = PolarAiReports.Templates.DefaultCssTemplate.Value
            };

            var htmlContent = await htmlService.GenerateHtmlReportAsync(report, insights, htmlOptions);

            // Salva HTML
            var htmlPath = GetReportFilePath(report, "html");
            var htmlDirectory = Path.GetDirectoryName(htmlPath);
            if (!string.IsNullOrEmpty(htmlDirectory))
            {
                Directory.CreateDirectory(htmlDirectory);
            }
            await System.IO.File.WriteAllTextAsync(htmlPath, htmlContent);

            // Genera PDF
            var pdfService = new PdfGenerationService(db);
            var pdfOptions = new PdfConversionOptions
            {
                PageFormat = "A4",
                MarginTop = "2cm",
                MarginBottom = "2cm",
                MarginLeft = "1.5cm",
                MarginRight = "1.5cm",
                DisplayHeaderFooter = true,
                HeaderTemplate = $@"
            <div style='font-size: 10px; width: 100%; text-align: center; color: #667eea; border-bottom: 1px solid #667eea; padding-bottom: 5px;'>
                <span>🧠 PolarAi Analysis - {report.ClientVehicle?.Vin} - Rigenerato {DateTime.UtcNow:yyyy-MM-dd HH:mm}</span>
            </div>",
                FooterTemplate = @"
            <div style='font-size: 10px; width: 100%; text-align: center; color: #666; border-top: 1px solid #ccc; padding-top: 5px;'>
                <span>Pagina <span class='pageNumber'></span> di <span class='totalPages'></span></span>
            </div>"
            };

            var pdfBytes = await pdfService.ConvertHtmlToPdfAsync(htmlContent, report, pdfOptions);

            // Salva PDF
            var pdfPath = GetReportFilePath(report, "pdf");
            var pdfDirectory = Path.GetDirectoryName(pdfPath);
            if (!string.IsNullOrEmpty(pdfDirectory))
            {
                Directory.CreateDirectory(pdfDirectory);
            }
            await System.IO.File.WriteAllBytesAsync(pdfPath, pdfBytes);

            // Aggiorna contatori
            report.RegenerationCount++;
            report.GeneratedAt = DateTime.UtcNow;
            report.Notes = $"Ultimo aggiornamento: {DateTime.UtcNow:yyyy-MM-dd HH:mm} - Rigenerazione #{report.RegenerationCount}";

            await db.SaveChangesAsync();

            await _logger.Info(source, "Report rigenerato con successo",
                $"ReportId: {report.Id}, RegenerationCount: {report.RegenerationCount}");

            return RegenerationResult.CreateSuccess(pdfPath, htmlPath, 0);
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore rigenerazione report",
                $"ReportId: {report.Id}, Error: {ex}");
            return RegenerationResult.CreateFailure($"Errore rigenerazione: {ex.Message}");
        }
    }

    /// <summary>
    /// Percorso file semplificato
    /// </summary>
    private string GetReportFilePath(Data.Entities.PdfReport report, string extension)
    {
        // Prima prova con il percorso standard dev-reports
        var standardDir = Path.Combine("storage", "dev-reports",
            report.ReportPeriodStart.Year.ToString(),
            report.ReportPeriodStart.Month.ToString("D2"));

        var standardFileName = $"PolarDrive_Report_{report.Id}.{extension}";
        var standardPath = Path.Combine(standardDir, standardFileName);

        if (System.IO.File.Exists(standardPath))
        {
            _logger.Debug("GetReportFilePath", $"Found file at standard path for report {report.Id}",
                $"Path: {standardPath}");
            return standardPath;
        }

        // Se non trova, prova nella directory reports normale
        var reportsDir = Path.Combine("storage", "reports",
            report.ReportPeriodStart.Year.ToString(),
            report.ReportPeriodStart.Month.ToString("D2"));

        var reportsPath = Path.Combine(reportsDir, standardFileName);

        if (System.IO.File.Exists(reportsPath))
        {
            _logger.Debug("GetReportFilePath", $"Found file at reports path for report {report.Id}",
                $"Path: {reportsPath}");
            return reportsPath;
        }

        // Default: ritorna il percorso standard
        _logger.Debug("GetReportFilePath", $"Using default path for report {report.Id}",
            $"Path: {standardPath}");
        return standardPath;
    }
    #endregion

    /// <summary>
    /// Diagnostica delle capacità PDF del sistema
    /// </summary>
    [HttpGet("diagnostics/pdf")]
    public async Task<IActionResult> DiagnosePdfCapabilities()
    {
        const string source = "PdfReportsController.DiagnosePdfCapabilities";

        try
        {
            await _logger.Info(source, "Avvio diagnostica capacità PDF");

            var pdfService = new PdfGenerationService(db);
            var diagnostic = await pdfService.DiagnosePdfCapabilitiesAsync();

            var result = new
            {
                timestamp = DateTime.UtcNow,
                pdfCapabilities = new
                {
                    isAvailable = diagnostic.IsAvailable,
                    errorMessage = diagnostic.ErrorMessage,
                    nodeJs = new
                    {
                        path = diagnostic.NodeJsPath,
                        exists = diagnostic.NodeJsExists,
                        version = diagnostic.NodeVersion
                    },
                    npx = new
                    {
                        path = diagnostic.NpxPath,
                        exists = diagnostic.NpxExists
                    },
                    puppeteer = new
                    {
                        testOutput = diagnostic.PuppeteerTestOutput,
                        testError = diagnostic.PuppeteerTestError
                    }
                },
                recommendations = GetPdfRecommendations(diagnostic)
            };

            await _logger.Info(source, "Diagnostica PDF completata",
                $"Available: {diagnostic.IsAvailable}");

            return Ok(result);
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore diagnostica PDF", ex.ToString());
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Fornisce raccomandazioni basate sui risultati della diagnostica
    /// </summary>
    private List<string> GetPdfRecommendations(PdfDiagnosticResult diagnostic)
    {
        var recommendations = new List<string>();

        if (!diagnostic.IsAvailable)
        {
            if (!diagnostic.NodeJsExists)
            {
                recommendations.Add("Installa Node.js da https://nodejs.org/");
                recommendations.Add($"Assicurati che sia installato in: {diagnostic.NodeJsPath}");
            }

            if (diagnostic.PuppeteerTestError?.Contains("puppeteer") == true)
            {
                recommendations.Add("Installa Puppeteer: npm install -g puppeteer");
                recommendations.Add("Oppure: npm install puppeteer nella directory del progetto");
            }

            if (diagnostic.ErrorMessage?.Contains("timeout") == true)
            {
                recommendations.Add("Il sistema è lento. Considera di aumentare i timeout.");
                recommendations.Add("Verifica che non ci siano antivirus che bloccano Node.js");
            }

            recommendations.Add("Al momento il sistema userà il fallback HTML");
        }
        else
        {
            recommendations.Add("✅ PDF generation funziona correttamente");
        }

        return recommendations;
    }
}

/// <summary>
/// Risultato dell'operazione di rigenerazione
/// </summary>
public class RegenerationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PdfPath { get; set; }
    public string? HtmlPath { get; set; }
    public int DataRecordsProcessed { get; set; }

    public static RegenerationResult CreateSuccess(string pdfPath, string htmlPath, int recordsProcessed)
    {
        return new RegenerationResult
        {
            Success = true,
            PdfPath = pdfPath,
            HtmlPath = htmlPath,
            DataRecordsProcessed = recordsProcessed
        };
    }

    public static RegenerationResult CreateFailure(string errorMessage)
    {
        return new RegenerationResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}