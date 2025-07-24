using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.Scheduler;
using PolarDrive.WebApi.Production;
using PolarDrive.WebApi.PolarAiReports;

namespace PolarDrive.WebApi.Scheduler;

[ApiController]
[Route("api/[controller]")]
public class TeslaFakeApiController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly PolarDriveLogger _logger;
    private readonly IWebHostEnvironment _env;
    private readonly PolarDriveScheduler? _fakeScheduler;

    public TeslaFakeApiController(PolarDriveDbContext db, IWebHostEnvironment env, IServiceProvider serviceProvider)
    {
        _db = db;
        _env = env;
        _logger = new PolarDriveLogger(_db);

        // Ottieni riferimenti agli scheduler per controllo manuale
        try
        {
            _fakeScheduler = serviceProvider.GetService<PolarDriveScheduler>();
        }
        catch
        {
            // Ignora se i servizi non sono registrati
        }
    }

    /// <summary>
    ///  Forza la generazione di un report di test
    /// </summary>
    [HttpPost("GenerateReport")]
    public async Task<IActionResult> GenerateReport()
    {
        const string source = "TeslaFakeApiController.GenerateReport";

        try
        {
            await _logger.Info(source, "Manual report generation triggered");

            var activeVehicles = await _db.ClientVehicles
                .Where(v => v.IsActiveFlag && v.IsFetchingDataFlag)
                .ToListAsync();

            if (!activeVehicles.Any())
            {
                return Ok(new
                {
                    success = false,
                    message = "No active vehicles found for report generation",
                    timestamp = DateTime.UtcNow
                });
            }

            var successCount = 0;
            var errorCount = 0;

            foreach (var vehicle in activeVehicles)
            {
                try
                {
                    var reportId = await GenerateReportForVehicleComplete(vehicle.Id, "Manual API Generation");
                    if (reportId.HasValue)
                        successCount++;
                    else
                        errorCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    await _logger.Error(source, $"Error generating report for vehicle {vehicle.Vin}", ex.ToString());
                }
            }

            await _logger.Info(source, "Manual report generation completed",
                $"Success: {successCount}, Errors: {errorCount}");

            return Ok(new
            {
                success = true,
                message = $"Report generation completed for {activeVehicles.Count} vehicles",
                timestamp = DateTime.UtcNow,
                results = new { successCount, errorCount },
                note = "Reports generated with FULL logic (HTML+PDF files)"
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error in manual report generation", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Forza report per un singolo veicolo - LOGICA COMPLETA
    /// </summary>
    [HttpPost("GenerateVehicleReport/{vehicleId}")]
    public async Task<IActionResult> GenerateVehicleReport(int vehicleId, [FromBody] ReportRequest? request = null)
    {
        const string source = "TeslaFakeApiController.GenerateVehicleReport";

        try
        {
            var vehicle = await _db.ClientVehicles
                .Include(v => v.ClientCompany)
                .FirstOrDefaultAsync(v => v.Id == vehicleId);

            if (vehicle == null)
            {
                return NotFound($"Vehicle with ID {vehicleId} not found");
            }

            await _logger.Info(source, $"COMPLETE report generation triggered for VIN {vehicle.Vin}");

            var analysisLevel = request?.AnalysisLevel ?? "Manual API Generation";
            var reportId = await GenerateReportForVehicleComplete(vehicleId, analysisLevel);

            if (reportId.HasValue)
            {
                // Verifica se i file sono stati creati
                var report = await _db.PdfReports.FindAsync(reportId.Value);
                var filesStatus = GetFilesStatus(report!);

                return Ok(new
                {
                    success = true,
                    message = $"COMPLETE report generated for vehicle {vehicle.Vin}",
                    reportId = reportId.Value,
                    vehicleVin = vehicle.Vin,
                    analysisLevel = analysisLevel,
                    timestamp = DateTime.UtcNow,
                    filesGenerated = filesStatus,
                    note = "Generated with FULL ReportGenerationService logic"
                });
            }
            else
            {
                return Ok(new
                {
                    success = false,
                    message = "Report could not be generated - check logs for details"
                });
            }
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error in vehicle report generation", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// ✅ NUOVO: Forza rigenerazione file per report esistente
    /// </summary>
    [HttpPost("RegenerateFiles/{reportId}")]
    public async Task<IActionResult> RegenerateFiles(int reportId)
    {
        const string source = "TeslaFakeApiController.RegenerateFiles";

        try
        {
            await _logger.Info(source, $"File regeneration triggered for report {reportId}");

            await ForceRegenerateFilesAsync(reportId);

            var report = await _db.PdfReports
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            var filesStatus = GetFilesStatus(report!);

            return Ok(new
            {
                success = true,
                message = $"Files regenerated for report {reportId}",
                reportId = reportId,
                vehicleVin = report?.ClientVehicle?.Vin,
                timestamp = DateTime.UtcNow,
                filesGenerated = filesStatus,
                note = "Files regenerated with latest data and AI analysis"
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error regenerating files for report {reportId}", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                reportId = reportId
            });
        }
    }

    /// <summary>
    /// Controlla e gestisce gli scheduler
    /// </summary>
    [HttpPost("ControlScheduler")]
    public async Task<IActionResult> ControlScheduler([FromBody] SchedulerControlRequest request)
    {
        const string source = "TeslaFakeApiController.ControlScheduler";

        try
        {
            await _logger.Info(source, $"Scheduler control action: {request.Action}");

            switch (request.Action.ToLower())
            {
                case "force_report":
                    if (!int.TryParse(request.VehicleId, out var vehicleId))
                    {
                        return BadRequest("Invalid vehicle ID");
                    }

                    var reportId = await GenerateReportForVehicleComplete(vehicleId, "Forced via API");
                    var result = reportId.HasValue;

                    return Ok(new
                    {
                        success = result,
                        message = result ? "Complete report forced successfully" : "Report forcing failed",
                        reportId = reportId
                    });

                default:
                    return BadRequest($"Unknown action: {request.Action}");
            }
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error in scheduler control", ex.ToString());
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Controlla dati con supporto grace period
    /// </summary>
    [HttpGet("DataStatus")]
    public async Task<IActionResult> GetDataStatus()
    {
        // ✅ CORREZIONE: Include veicoli in grace period (solo IsFetchingDataFlag)
        var vehicles = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .Where(v => v.ClientOAuthAuthorized && v.IsFetchingDataFlag)  // ← Rimosso IsActiveFlag
            .ToListAsync();

        var result = new List<object>();

        foreach (var vehicle in vehicles)
        {
            var dataCount = await _db.VehiclesData.CountAsync(vd => vd.VehicleId == vehicle.Id);
            var latestData = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicle.Id)
                .OrderByDescending(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var reports = await _db.PdfReports
                .Where(r => r.ClientVehicleId == vehicle.Id)
                .OrderByDescending(r => r.GeneratedAt)
                .Take(3)
                .Select(r => new
                {
                    r.Id,
                    r.GeneratedAt,
                    r.Notes,
                    r.Status,
                    FilesExist = GetFilesStatus(r)
                })
                .ToListAsync();

            var firstDataRecord = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicle.Id)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var monitoringDays = firstDataRecord != default ? (DateTime.UtcNow - firstDataRecord).TotalDays : 0;
            var contractStatus = GetContractStatus(vehicle);

            result.Add(new
            {
                vehicleId = vehicle.Id,
                vin = vehicle.Vin,
                model = vehicle.Model,
                companyName = vehicle.ClientCompany?.Name,
                dataRecords = dataCount,
                latestData = latestData?.Timestamp,
                lastUpdate = vehicle.LastDataUpdate,
                isActive = vehicle.IsActiveFlag,
                isFetching = vehicle.IsFetchingDataFlag,
                contractStatus = contractStatus,
                isGracePeriod = !vehicle.IsActiveFlag && vehicle.IsFetchingDataFlag,
                monitoringDays = Math.Round(monitoringDays, 1),
                reports = reports,
                reportCount = reports.Count
            });
        }

        var totalVehicles = vehicles.Count;
        var activeContractVehicles = vehicles.Count(v => v.IsActiveFlag && v.IsFetchingDataFlag);
        var gracePeriodVehicles = vehicles.Count(v => !v.IsActiveFlag && v.IsFetchingDataFlag);

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            environment = _env.IsDevelopment() ? "Development" : "Production",
            vehicles = result,
            summary = new
            {
                totalVehicles = totalVehicles,
                activeContracts = activeContractVehicles,
                gracePeriodVehicles = gracePeriodVehicles,
                totalDataRecords = await _db.VehiclesData.CountAsync(),
                totalReports = await _db.PdfReports.CountAsync(),
            }
        });
    }

    /// <summary>
    /// Status con info
    /// </summary>
    [HttpGet("ReportStatus")]
    public async Task<IActionResult> GetReportStatus()
    {
        var reports = await _db.PdfReports
            .Include(r => r.ClientVehicle)
            .Include(r => r.ClientCompany)
            .OrderByDescending(r => r.GeneratedAt)
            .Take(20)
            .ToListAsync();

        var result = reports.Select(r => new
        {
            reportId = r.Id,
            vin = r.ClientVehicle?.Vin,
            vehicleModel = r.ClientVehicle?.Model,
            companyName = r.ClientCompany?.Name,
            companyVat = r.ClientCompany?.VatNumber,
            periodStart = r.ReportPeriodStart,
            periodEnd = r.ReportPeriodEnd,
            generatedAt = r.GeneratedAt,
            status = r.Status,
            notes = r.Notes,
            filesStatus = GetFilesStatus(r)
        });

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            environment = _env.IsDevelopment() ? "Development" : "Production",
            recentReports = result,
            totalReports = await _db.PdfReports.CountAsync(),
            systemStatus = new
            {
                aiSystemAvailable = await CheckAiSystemAvailability(),
                pdfGenerationAvailable = CheckPdfGenerationAvailability(),
                schedulerAvailable = _env.IsDevelopment()
            }
        });
    }

    /// <summary>
    /// ✅ MANTENUTO: Download PDF report (invariato)
    /// </summary>
    [HttpGet("DownloadReport/{reportId}")]
    public async Task<IActionResult> DownloadReport(int reportId)
    {
        var report = await _db.PdfReports
            .Include(r => r.ClientVehicle)
            .Include(r => r.ClientCompany)
            .FirstOrDefaultAsync(r => r.Id == reportId);

        if (report == null)
        {
            return NotFound("Report not found");
        }

        var pdfPath = GetFilePath(report, "pdf", "reports");

        if (System.IO.File.Exists(pdfPath))
        {
            var bytes = await System.IO.File.ReadAllBytesAsync(pdfPath);
            var fileName = $"PolarDrive_Report_{report.Id}_{report.ClientVehicle?.Vin}_{report.ReportPeriodStart:yyyyMMdd}.pdf";

            return File(bytes, "application/pdf", fileName);
        }

        // Fallback: prova con file HTML (per development)
        var htmlPath = GetFilePath(report, "html", _env.IsDevelopment() ? "dev-reports" : "reports");
        if (System.IO.File.Exists(htmlPath))
        {
            var htmlContent = await System.IO.File.ReadAllTextAsync(htmlPath);
            var fileName = $"PolarDrive_Report_{report.Id}_{report.ClientVehicle?.Vin}_{report.ReportPeriodStart:yyyyMMdd}.html";

            return File(System.Text.Encoding.UTF8.GetBytes(htmlContent), "text/html", fileName);
        }

        return NotFound("Report file not found on disk");
    }

    #region Helper Methods - LOGICA COMPLETA COME ReportGenerationService

    /// <summary>
    /// ✅ ENHANCED: Genera report completo con TUTTA la logica di ReportGenerationService
    /// </summary>
    private async Task<int?> GenerateReportForVehicleComplete(int vehicleId, string analysisLevel)
    {
        const string source = "TeslaFakeApiController.GenerateReportForVehicleComplete";

        try
        {
            var vehicle = await _db.ClientVehicles
                .Include(v => v.ClientCompany)
                .FirstOrDefaultAsync(v => v.Id == vehicleId);

            if (vehicle == null)
            {
                await _logger.Warning(source, $"Vehicle {vehicleId} not found");
                return null;
            }

            // ✅ GRACE PERIOD WARNING
            if (!vehicle.IsActiveFlag && vehicle.IsFetchingDataFlag)
                await _logger.Warning(source, $"⏳ Grace Period generation for {vehicle.Vin}");

            // ✅ CALCOLO PERIODO COME ReportGenerationService
            var now = DateTime.UtcNow;
            var reportCount = await _db.PdfReports.CountAsync(r => r.ClientVehicleId == vehicleId);

            // 1) Prendo la fine dell'ultimo report (se esiste)
            var lastReportEnd = await _db.PdfReports
                .Where(r => r.ClientVehicleId == vehicleId)
                .OrderByDescending(r => r.ReportPeriodEnd)
                .Select(r => (DateTime?)r.ReportPeriodEnd)
                .FirstOrDefaultAsync();

            DateTime start;
            DateTime end = now;

            // 2) Logica periodo IDENTICA a ReportGenerationService
            if (reportCount == 0 && vehicle.FirstActivationAt.HasValue)
            {
                // Primo report: dall'attivazione
                start = vehicle.FirstActivationAt.Value;
                await _logger.Info(source, $"🔧 First report from activation: {start} to {end}");
            }
            else
            {
                // Report successivi: dalla fine dell'ultimo report o 30 giorni fa
                start = lastReportEnd ?? now.AddHours(-720); // 30 giorni default
            }

            var period = new ReportPeriodInfo
            {
                Start = start,
                End = end,
                DataHours = (int)(end - start).TotalHours,
                AnalysisLevel = reportCount == 0 ? "Valutazione Iniziale" : analysisLevel,
                MonitoringDays = (end - start).TotalDays
            };

            // ✅ CONTROLLO DATI COME ReportGenerationService
            var dataCount = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId &&
                             vd.Timestamp >= period.Start &&
                             vd.Timestamp <= period.End)
                .CountAsync();

            await _logger.Info(source,
                $"🧠 Generating {period.AnalysisLevel} for {vehicle.Vin} | " +
                $"{period.Start:yyyy-MM-dd HH:mm} to {period.End:yyyy-MM-dd HH:mm} | " +
                $"Report #{reportCount + 1} | DataRecords: {dataCount}");

            // ✅ CREA SEMPRE RECORD REPORT
            var report = new PdfReport
            {
                ClientVehicleId = vehicleId,
                ClientCompanyId = vehicle.ClientCompanyId,
                ReportPeriodStart = period.Start,
                ReportPeriodEnd = period.End,
                GeneratedAt = now,
                Notes = $"[API-{period.AnalysisLevel}] DataHours: {period.DataHours}, Monitoring: {period.MonitoringDays:F1}d"
            };

            // ✅ SE NON CI SONO DATI: status NO-DATA e NIENTE FILE
            if (dataCount == 0)
            {
                report.Status = "NO-DATA";
                report.Notes += " - Nessun dato disponibile per il periodo";

                _db.PdfReports.Add(report);
                await _db.SaveChangesAsync();

                await _logger.Warning(source,
                    $"⚠️ Report {report.Id} created but NO FILES generated for {vehicle.Vin} - No data available for period");
                return report.Id; // ← RITORNA ID ANCHE SENZA FILE
            }

            // ✅ SE CI SONO DATI: GENERA TUTTO
            _db.PdfReports.Add(report);
            await _db.SaveChangesAsync();

            // ✅ GENERA INSIGHTS CON PolarAI
            var aiGen = new PolarAiReportGenerator(_db);
            var insights = await aiGen.GeneratePolarAiInsightsAsync(vehicleId);

            if (string.IsNullOrWhiteSpace(insights))
            {
                await _logger.Error(source, $"No insights generated for {vehicle.Vin}");
                throw new InvalidOperationException($"No insights for {vehicle.Vin}");
            }

            // ✅ GENERA FILE HTML + PDF
            await GenerateReportFiles(report, insights, period, vehicle);

            await _logger.Info(source,
                $"✅ Report {report.Id} COMPLETE for {vehicle.Vin} | " +
                $"Period: {period.DataHours}h | Type: {period.AnalysisLevel} | Files: HTML+PDF");

            return report.Id;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error generating complete report for vehicle {vehicleId}", ex.ToString());
            throw;
        }
    }

    /// <summary>
    /// ✅ NUOVO: Genera file HTML + PDF (logica da ReportGenerationService)
    /// </summary>
    private async Task GenerateReportFiles(PdfReport report, string insights, ReportPeriodInfo period, ClientVehicle vehicle)
    {
        const string source = "TeslaFakeApiController.GenerateReportFiles";

        try
        {
            // ✅ HTML GENERATION
            var htmlSvc = new HtmlReportService(_db);
            var htmlOpt = new HtmlReportOptions
            {
                ShowDetailedStats = true,
                ShowRawData = false,
                ReportType = $"🧠 {period.AnalysisLevel}",
                AdditionalCss = PolarAiReports.Templates.DefaultCssTemplate.Value
            };
            var html = await htmlSvc.GenerateHtmlReportAsync(report, insights, htmlOpt);

            // ✅ SALVA HTML (sempre in dev, opzionale in prod)
            if (_env.IsDevelopment())
            {
                var htmlPath = GetFilePath(report, "html", "dev-reports");
                await SaveFile(htmlPath, html);
                await _logger.Info(source, $"HTML saved: {htmlPath}");
            }

            // ✅ PDF GENERATION
            var pdfSvc = new PdfGenerationService(_db);
            var pdfOpt = new PdfConversionOptions
            {
                PageFormat = "A4",
                MarginTop = "2cm",
                MarginBottom = "2cm",
                MarginLeft = "1.5cm",
                MarginRight = "1.5cm",
                DisplayHeaderFooter = true,
                HeaderTemplate = $"<div style='font-size:10px;text-align:center;color:#004E92;border-bottom:1px solid #004E92;padding-bottom:5px;'>{vehicle.Vin} - {DateTime.UtcNow:yyyy-MM-dd HH:mm}</div>",
                FooterTemplate = "<div style='font-size:10px;text-align:center;color:#666;border-top:1px solid #ccc;padding-top:5px;'>Pagina <span class='pageNumber'></span> di <span class='totalPages'></span> | DataPolar Analytics</div>"
            };
            var pdfBytes = await pdfSvc.ConvertHtmlToPdfAsync(html, report, pdfOpt);

            // ✅ SALVA PDF
            var pdfPath = GetFilePath(report, "pdf", "reports");
            await SaveFile(pdfPath, pdfBytes);
            await _logger.Info(source, $"PDF saved: {pdfPath}");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error generating files for report {report.Id}", ex.ToString());
            throw;
        }
    }

    /// <summary>
    /// ✅ NUOVO: Rigenerazione file (da ReportGenerationService)
    /// </summary>
    private async Task ForceRegenerateFilesAsync(int reportId)
    {
        const string source = "TeslaFakeApiController.ForceRegenerateFilesAsync";

        await _logger.Info(source, $"🔄 Rigenerazione file per report {reportId}");

        // 1) Carica il report e il veicolo associato
        var report = await _db.PdfReports
                             .Include(r => r.ClientVehicle!)
                             .ThenInclude(v => v!.ClientCompany)
                             .FirstOrDefaultAsync(r => r.Id == reportId)
                     ?? throw new InvalidOperationException($"Report {reportId} non trovato");

        var vehicle = report.ClientVehicle;

        // 2) Controlla se ci sono dati nel periodo del report
        var dataCount = await _db.VehiclesData
            .Where(vd => vd.VehicleId == vehicle!.Id &&
                         vd.Timestamp >= report.ReportPeriodStart &&
                         vd.Timestamp <= report.ReportPeriodEnd)
            .CountAsync();

        await _logger.Info(source,
            $"🔍 Report {reportId} regeneration check: VIN={vehicle!.Vin}, " +
            $"Period={report.ReportPeriodStart} to {report.ReportPeriodEnd}, DataCount={dataCount}");

        // ✅ Se non ci sono dati, aggiorna status e NON rigenerare file
        if (dataCount == 0)
        {
            report.Status = "NO-DATA";
            report.Notes = $"Ultima rigenerazione: {DateTime.UtcNow:yyyy-MM-dd HH:mm} - " +
                          $"numero rigenerazione #{report.RegenerationCount} - Nessun dato disponibile per il periodo";
            report.RegenerationCount++;

            // Elimina eventuali file esistenti (cleanup)
            await DeleteExistingFiles(report);

            await _db.SaveChangesAsync();

            await _logger.Warning(source,
                $"⚠️ Report {reportId} rigenerazione saltata per {vehicle.Vin} - " +
                $"Nessun dato disponibile nel periodo specificato");
            return; // ← ESCI SENZA CREARE NUOVI FILE
        }

        // ✅ Se ci sono dati, procedi con la rigenerazione normale
        await _logger.Info(source, $"✅ Report {reportId} ha {dataCount} record di dati - Procedendo con rigenerazione file");

        // 3) Ricalcola gli insights
        var aiGen = new PolarAiReportGenerator(_db);
        var insights = await aiGen.GeneratePolarAiInsightsAsync(vehicle.Id);
        if (string.IsNullOrWhiteSpace(insights))
            throw new InvalidOperationException($"Nessun insight generato per {vehicle.Vin}");

        // 4) Crea un ReportPeriodInfo a partire dal report esistente
        var period = new ReportPeriodInfo
        {
            Start = report.ReportPeriodStart,
            End = report.ReportPeriodEnd,
            DataHours = (int)(report.ReportPeriodEnd - report.ReportPeriodStart).TotalHours,
            AnalysisLevel = $"Rigenerazione #{report.RegenerationCount + 1}",
            MonitoringDays = (report.ReportPeriodEnd - report.ReportPeriodStart).TotalDays
        };

        // 5) Elimina i vecchi file prima di rigenerare
        await DeleteExistingFiles(report);

        // 6) Rigenera i file
        await GenerateReportFiles(report, insights, period, vehicle);

        // 7) Aggiorna lo status del report
        report.Status = "COMPLETED"; // o null per far usare la logica file-based
        report.RegenerationCount++;
        report.Notes = $"Rigenerato: {DateTime.UtcNow:yyyy-MM-dd HH:mm} - " +
                      $"#{report.RegenerationCount} - DataRecords: {dataCount}";
        await _db.SaveChangesAsync();

        await _logger.Info(source,
            $"✅ File rigenerati con successo per report {reportId} - VIN: {vehicle.Vin}, DataRecords: {dataCount}");
    }

    /// <summary>
    /// ✅ NUOVO: Elimina file esistenti
    /// </summary>
    private async Task DeleteExistingFiles(PdfReport report)
    {
        const string source = "TeslaFakeApiController.DeleteExistingFiles";

        try
        {
            var htmlPath = GetFilePath(report, "html", _env.IsDevelopment() ? "dev-reports" : "reports");
            var pdfPath = GetFilePath(report, "pdf", "reports");

            if (System.IO.File.Exists(htmlPath))
            {
                System.IO.File.Delete(htmlPath);
                await _logger.Debug(source, "File HTML eliminato", htmlPath);
            }

            if (System.IO.File.Exists(pdfPath))
            {
                System.IO.File.Delete(pdfPath);
                await _logger.Debug(source, "File PDF eliminato", pdfPath);
            }
        }
        catch (Exception ex)
        {
            await _logger.Warning(source, $"Errore durante eliminazione file esistenti per report {report.Id}", ex.Message);
        }
    }

    /// <summary>
    /// ✅ NUOVO: Path file (da ReportGenerationService)
    /// </summary>
    private static string GetFilePath(PdfReport report, string ext, string folder)
    {
        // ✅ USA LA DATA DI GENERAZIONE, NON IL PERIODO DEI DATI
        var generationDate = report.GeneratedAt ?? DateTime.UtcNow;

        return Path.Combine("storage", folder,
            generationDate.Year.ToString(),
            generationDate.Month.ToString("D2"),
            $"PolarDrive_Report_{report.Id}.{ext}");
    }

    /// <summary>
    /// ✅ NUOVO: Salva file
    /// </summary>
    private async Task SaveFile(string path, object content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        switch (content)
        {
            case string txt:
                await System.IO.File.WriteAllTextAsync(path, txt);
                break;
            case byte[] data:
                await System.IO.File.WriteAllBytesAsync(path, data);
                break;
        }
    }

    /// <summary>
    /// ✅ NUOVO: Status file esistenti
    /// </summary>
    private object GetFilesStatus(PdfReport report)
    {
        var pdfPath = GetFilePath(report, "pdf", "reports");
        var htmlPath = GetFilePath(report, "html", _env.IsDevelopment() ? "dev-reports" : "reports");

        return new
        {
            pdf = new
            {
                exists = System.IO.File.Exists(pdfPath),
                path = pdfPath,
                size = System.IO.File.Exists(pdfPath) ? new FileInfo(pdfPath).Length : 0
            },
            html = new
            {
                exists = System.IO.File.Exists(htmlPath),
                path = htmlPath,
                size = System.IO.File.Exists(htmlPath) ? new FileInfo(htmlPath).Length : 0
            }
        };
    }

    /// <summary>
    /// Helper per determinare stato contrattuale
    /// </summary>
    private string GetContractStatus(ClientVehicle vehicle)
    {
        return (vehicle.IsActiveFlag, vehicle.IsFetchingDataFlag) switch
        {
            (true, true) => "Active Contract - Data Collection Active",
            (true, false) => "Active Contract - Data Collection Paused",
            (false, true) => "Contract Terminated - Grace Period Active",
            (false, false) => "Contract Terminated - Data Collection Stopped"
        };
    }

    private async Task<bool> CheckAiSystemAvailability()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            var response = await client.GetAsync("http://localhost:11434/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private bool CheckPdfGenerationAvailability()
    {
        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        var npxPath = Path.Combine(programFiles, "nodejs", "npx.cmd");
        return System.IO.File.Exists(npxPath);
    }

    #endregion
}

// ✅ NUOVI REQUEST MODELS
public class ReportRequest
{
    public string AnalysisLevel { get; set; } = "Manual Generation";
}

public class SchedulerControlRequest
{
    public string Action { get; set; } = string.Empty;  // "reset_retries", "force_report"
    public string VehicleId { get; set; } = string.Empty;  // Solo per "force_report"
}

// ✅ LEGACY REQUEST MODEL (mantenuto per compatibilità)
public class CustomReportRequest
{
    public string PeriodStart { get; set; } = string.Empty;
    public string PeriodEnd { get; set; } = string.Empty;
}

// ✅ NUOVO: Helper classes (da ReportGenerationService)