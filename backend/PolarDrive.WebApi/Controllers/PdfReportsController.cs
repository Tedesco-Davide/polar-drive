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
    /// Ottiene la lista di tutti i report PDF
    /// </summary>
    [HttpGet]
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
                // ✅ USA IL METODO MIGLIORATO
                var pdfPath = GetReportFilePath(r, "pdf");
                var htmlPath = GetReportFilePath(r, "html");

                // ✅ FALLBACK: Se non trova nel path calcolato, cerca in entrambe le directory
                var pdfExists = System.IO.File.Exists(pdfPath);
                var htmlExists = System.IO.File.Exists(htmlPath);

                if (!pdfExists || !htmlExists)
                {
                    // ✅ FALLBACK: Prova entrambi i percorsi possibili
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

                // ✅ CONTA I RECORD - MIGLIORATO
                var dataCount = await CountDataRecordsForReport(r);

                // ✅ CALCOLA ALTRI CAMPI
                var monitoringDuration = (r.ReportPeriodEnd - r.ReportPeriodStart).TotalHours;
                var isRegenerated = r.RegenerationCount > 0;
                var lastModified = r.GeneratedAt?.ToString("o");
                var isProgressive = IsProgressiveReport(r);

                // ✅ CREA IL DTO CON TUTTI I CAMPI
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
                    ReportType = DetermineReportType(r, isRegenerated, dataCount, isProgressive),
                    Status = DetermineReportStatus(r, pdfExists, htmlExists, dataCount),
                    CanRegenerate = dataCount > 0 || isProgressive, // ✅ I progressivi si possono sempre rigenerare
                    AvailableFormats = GetAvailableFormats(pdfExists, htmlExists),
                    IsProgressive = isProgressive // ✅ NUOVO CAMPO
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
    /// ✅ NUOVO: Ottiene percorsi alternativi per fallback
    /// </summary>
    private (string alternatePdfPath, string alternateHtmlPath) GetAlternateFilePaths(Data.Entities.PdfReport report)
    {
        var isCurrentlyProgressive = IsProgressiveReport(report);

        string alternateDir;
        string alternateFileName;

        if (isCurrentlyProgressive)
        {
            // Attualmente progressivo, prova path tradizionale
            alternateDir = Path.Combine("storage", "reports",
                report.ReportPeriodStart.Year.ToString(),
                report.ReportPeriodStart.Month.ToString("D2"));
            alternateFileName = $"PolarDrive_Report_{report.Id}";
        }
        else
        {
            // Attualmente tradizionale, prova path progressivo
            alternateDir = Path.Combine("storage", "dev-progressive-reports",
                report.ReportPeriodStart.Year.ToString(),
                report.ReportPeriodStart.Month.ToString("D2"));
            alternateFileName = $"PolarDrive_Progressive_Dev_{report.Id}";
        }

        return (
            alternatePdfPath: Path.Combine(alternateDir, $"{alternateFileName}.pdf"),
            alternateHtmlPath: Path.Combine(alternateDir, $"{alternateFileName}.html")
        );
    }

    // ✅ METODO HELPER PER DETERMINARE IL TIPO DI REPORT
    private static string DetermineReportType(Data.Entities.PdfReport report, bool isRegenerated, int dataCount)
    {
        if (isRegenerated)
            return "Rigenerato";

        if (dataCount == 0)
            return "Nessun Dato";

        var duration = (report.ReportPeriodEnd - report.ReportPeriodStart).TotalHours;

        // ✅ LOGICA PIÙ CHIARA E COERENTE
        if (duration >= 720) // ~30 giorni
            return "Mensile";

        if (duration >= 168) // 7 giorni  
            return "Settimanale";

        if (duration >= 24) // 1 giorno
        {
            if (dataCount >= 100)
                return "Giornaliero Completo";
            else if (dataCount >= 20)
                return "Giornaliero Parziale";
            else
                return "Giornaliero Limitato";
        }

        // ✅ CASI BREVI
        if (duration < 1) // Meno di 1 ora
            return "Test Rapido";

        if (dataCount < 5)
            return "Dati Insufficienti";

        if (dataCount >= 50)
            return "Completo";

        return "Standard";
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
    /// Ora usa SEMPRE l'analisi progressiva
    /// </summary>
    private async Task<RegenerationResult> RegenerateReportFiles(Data.Entities.PdfReport report)
    {
        const string source = "PdfReportsController.RegenerateReportFiles";

        try
        {
            await _logger.Info(source, "Avvio rigenerazione con analisi progressiva",
                $"ReportId: {report.Id}, VehicleId: {report.ClientVehicleId}");

            // ✅ USA SEMPRE L'ANALISI PROGRESSIVA
            var aiGenerator = new PolarAiReportGenerator(db);
            var insights = await aiGenerator.GenerateProgressiveInsightsAsync(report.ClientVehicleId);

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

            await _logger.Info(source, "Insights progressivi generati",
                $"ReportId: {report.Id}, Insights length: {insights.Length} chars");

            // 2. Genera HTML con insights progressivi
            var htmlService = new HtmlReportService(db);
            var htmlOptions = new HtmlReportOptions
            {
                ShowDetailedStats = true,
                ShowRawData = false,
                ReportType = "Progressive AI Analysis", // ✅ Indica che è progressivo
                AdditionalCss = GetProgressiveRegenerationStyles() // ✅ Stili dedicati
            };

            var htmlContent = await htmlService.GenerateHtmlReportAsync(report, insights, htmlOptions);

            // 3. Salva HTML
            var htmlPath = GetReportFilePath(report, "html");
            var htmlDirectory = Path.GetDirectoryName(htmlPath);
            if (!string.IsNullOrEmpty(htmlDirectory))
            {
                Directory.CreateDirectory(htmlDirectory);
            }
            await System.IO.File.WriteAllTextAsync(htmlPath, htmlContent);

            // 4. Genera PDF con header progressivo
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
                    <span>🧠 PolarDrive Progressive Analysis - {report.ClientVehicle?.Vin} - Rigenerato {DateTime.UtcNow:yyyy-MM-dd HH:mm}</span>
                </div>",
                FooterTemplate = @"
                <div style='font-size: 10px; width: 100%; text-align: center; color: #666; border-top: 1px solid #ccc; padding-top: 5px;'>
                    <span>Pagina <span class='pageNumber'></span> di <span class='totalPages'></span></span>
                </div>"
            };

            var pdfBytes = await pdfService.ConvertHtmlToPdfAsync(htmlContent, report, pdfOptions);

            // 5. Salva PDF
            var pdfPath = GetReportFilePath(report, "pdf");
            var pdfDirectory = Path.GetDirectoryName(pdfPath);
            if (!string.IsNullOrEmpty(pdfDirectory))
            {
                Directory.CreateDirectory(pdfDirectory);
            }
            await System.IO.File.WriteAllBytesAsync(pdfPath, pdfBytes);

            // Incrementa il contatore di rigenerazioni
            report.RegenerationCount++;

            // Aggiorna anche il timestamp
            report.GeneratedAt = DateTime.UtcNow;

            // 6. Aggiorna le note del report per indicare che è progressivo
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
    /// ✅ AGGIUNGI questo metodo per stili dedicati ai report progressivi rigenerati
    /// </summary>
    private string GetProgressiveRegenerationStyles()
    {
        return @"
        .progressive-regenerated-badge {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 8px 16px;
            border-radius: 25px;
            font-size: 12px;
            font-weight: 500; /* ✅ Ridotto da bold a 500 */
            display: inline-block;
            margin: 10px 15px 10px 0;
            box-shadow: 0 4px 8px rgba(0,0,0,0.2);
        }
        
        .report-info::after {
            content: ' 🧠 ANALISI PROGRESSIVA';
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 4px 8px;
            border-radius: 12px;
            font-size: 10px;
            font-weight: 500; /* ✅ Ridotto */
            margin-left: 10px;
        }
        
        .ai-insights {
            border-left: 5px solid #667eea;
            background: linear-gradient(135deg, rgba(102, 126, 234, 0.1) 0%, rgba(118, 75, 162, 0.1) 100%);
            padding: 20px;
            border-radius: 0 12px 12px 0;
        }
        
        .ai-insights::before {
            content: '🧠 Analisi Progressiva AI • ';
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
            font-weight: 500; /* ✅ Ridotto da bold */
            font-size: 14px;
        }
        
        /* ✅ OVERRIDE GLOBALE per tutti gli elementi nell'AI insights */
        .ai-insights * {
            font-weight: normal !important;
        }
        
        .ai-insights h1, .ai-insights h2, .ai-insights h3, .ai-insights h4 {
            font-weight: 500 !important; /* ✅ Headers più leggeri */
        }
        
        .ai-insights strong, .ai-insights b {
            font-weight: 500 !important; /* ✅ Grassetto controllato */
            color: #667eea;
        }
        
        .progressive-evolution {
            border: 2px dashed #667eea;
            padding: 15px;
            margin: 20px 0;
            background: rgba(102, 126, 234, 0.05);
            border-radius: 8px;
        }
        
        .progressive-evolution::before {
            content: '📈 Evoluzione nel Tempo • ';
            color: #667eea;
            font-weight: 500; /* ✅ Ridotto */
        }";
    }

    /// <summary>
    /// Percorso file sempre progressivo
    /// </summary>
    private string GetReportFilePath(Data.Entities.PdfReport report, string extension)
    {
        // Nessuna logica complessa
        var outputDir = Path.Combine("storage", "dev-progressive-reports",
            report.ReportPeriodStart.Year.ToString(),
            report.ReportPeriodStart.Month.ToString("D2"));

        var fileName = $"PolarDrive_Progressive_Dev_{report.Id}.{extension}";
        var fullPath = Path.Combine(outputDir, fileName);

        _logger.Debug("GetReportFilePath", $"Path calculated for report {report.Id}",
            $"Path: {fullPath}");

        return fullPath;
    }

    /// <summary>
    /// Tutti i report sono sempre progressivi
    /// </summary>
    private static bool IsProgressiveReport(Data.Entities.PdfReport report)
    {
        return true;
    }

    /// <summary>
    /// Stili CSS personalizzati per report rigenerati
    /// </summary>
    private string GetCustomRegenerationStyles()
    {
        return @"
            .regenerated-badge {
                background: linear-gradient(135deg, #ffc107 0%, #ff8f00 100%);
                color: #000;
                padding: 6px 12px;
                border-radius: 20px;
                font-size: 11px;
                font-weight: bold;
                display: inline-block;
                margin-left: 15px;
                box-shadow: 0 2px 4px rgba(0,0,0,0.2);
            }
            
            .report-info::after {
                content: ' 🔄 RIGENERATO';
                background-color: #ffc107;
                color: #000;
                padding: 2px 6px;
                border-radius: 4px;
                font-size: 10px;
                font-weight: bold;
                margin-left: 10px;
            }
            
            .ai-insights {
                border-left: 5px solid #ffc107;
            }
            
            .ai-insights::before {
                content: '🔄 Report Rigenerato • ';
                color: #ffc107;
                font-weight: bold;
                font-size: 12px;
            }";
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

    // ✅ ENDPOINT PER TESTARE PDF VELOCEMENTE
    /// <summary>
    /// Test rapido di generazione PDF
    /// </summary>
    [HttpPost("test/pdf")]
    public async Task<IActionResult> TestPdfGeneration()
    {
        const string source = "PdfReportsController.TestPdfGeneration";

        try
        {
            await _logger.Info(source, "Test rapido generazione PDF");

            var testHtml = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>PDF Test</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; }
        h1 { color: #004E92; }
        .test-content { background: #f8f9fa; padding: 20px; border-radius: 5px; }
    </style>
</head>
<body>
    <h1>🧪 PDF Generation Test</h1>
    <div class='test-content'>
        <p><strong>Timestamp:</strong> " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + @" UTC</p>
        <p><strong>System:</strong> PolarDrive PDF Service</p>
        <p><strong>Status:</strong> Test in corso...</p>
        <p>Se vedi questo file, la generazione PDF funziona correttamente! ✅</p>
    </div>
</body>
</html>";

            var pdfService = new PdfGenerationService(db);
            var options = new PdfConversionOptions
            {
                PageFormat = "A4",
                MarginTop = "2cm",
                MarginBottom = "2cm"
            };

            var startTime = DateTime.UtcNow;
            var pdfBytes = await pdfService.GeneratePdfFromHtmlAsync(testHtml, "test.pdf", options);
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;

            await _logger.Info(source, "Test PDF completato con successo",
                $"Size: {pdfBytes.Length} bytes, Duration: {duration:F1}s");

            return File(pdfBytes, "application/pdf", $"PolarDrive_PDFTest_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Test PDF fallito", ex.ToString());

            // Fallback HTML
            var fallbackHtml = $@"
        <html><body>
            <h1>PDF Test Fallito</h1>
            <p>Errore: {ex.Message}</p>
            <p>Timestamp: {DateTime.UtcNow}</p>
        </body></html>";

            return File(System.Text.Encoding.UTF8.GetBytes(fallbackHtml), "text/html",
                $"PolarDrive_PDFTest_FALLBACK_{DateTime.UtcNow:yyyyMMddHHmmss}.html");
        }
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

    // ✅ RINOMINA I METODI STATICI per evitare conflitto con la proprietà
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