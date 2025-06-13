using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.Fake;
using PolarDrive.WebApi.Production;
using PolarDrive.WebApi.PolarAiReports;

namespace PolarDrive.WebApi.Fake;

[ApiController]
[Route("api/[controller]")]
public class TeslaFakeApiController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly PolarDriveLogger _logger;
    private readonly IWebHostEnvironment _env;
    private readonly FakeProductionScheduler? _fakeScheduler;
    private readonly ProductionScheduler? _productionScheduler;

    public TeslaFakeApiController(PolarDriveDbContext db, IWebHostEnvironment env, IServiceProvider serviceProvider)
    {
        _db = db;
        _env = env;
        _logger = new PolarDriveLogger(_db);

        // ✅ NUOVO: Ottieni riferimenti agli scheduler per controllo manuale
        try
        {
            _fakeScheduler = serviceProvider.GetService<FakeProductionScheduler>();
            _productionScheduler = serviceProvider.GetService<ProductionScheduler>();
        }
        catch
        {
            // Ignora se i servizi non sono registrati
        }
    }

    /// <summary>
    /// ✅ AGGIORNATO: Forza la generazione di un report progressivo di test
    /// </summary>
    [HttpPost("GenerateProgressiveReport")]
    public async Task<IActionResult> GenerateProgressiveReport()
    {
        const string source = "TeslaFakeApiController.GenerateProgressiveReport";

        try
        {
            await _logger.Info(source, "Manual progressive report generation triggered");

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
                    // ✅ USA IL NUOVO SISTEMA PROGRESSIVO
                    await GenerateProgressiveReportForVehicle(vehicle.Id, "Manual API Generation");
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    await _logger.Error(source, $"Error generating progressive report for vehicle {vehicle.Vin}", ex.ToString());
                }
            }

            await _logger.Info(source, "Manual progressive report generation completed",
                $"Success: {successCount}, Errors: {errorCount}");

            return Ok(new
            {
                success = true,
                message = $"Progressive report generation completed for {activeVehicles.Count} vehicles",
                timestamp = DateTime.UtcNow,
                results = new { successCount, errorCount },
                note = "Reports generated using Progressive AI Analysis system"
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error in manual progressive report generation", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// ✅ NUOVO: Forza report progressivo per un singolo veicolo
    /// </summary>
    [HttpPost("GenerateVehicleProgressiveReport/{vehicleId}")]
    public async Task<IActionResult> GenerateVehicleProgressiveReport(int vehicleId, [FromBody] ProgressiveReportRequest? request = null)
    {
        const string source = "TeslaFakeApiController.GenerateVehicleProgressiveReport";

        try
        {
            var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Id == vehicleId);
            if (vehicle == null)
            {
                return NotFound($"Vehicle with ID {vehicleId} not found");
            }

            await _logger.Info(source, $"Progressive report generation triggered for VIN {vehicle.Vin}");

            var analysisLevel = request?.AnalysisLevel ?? "Manual API Generation";
            var reportId = await GenerateProgressiveReportForVehicle(vehicleId, analysisLevel);

            if (reportId.HasValue)
            {
                return Ok(new
                {
                    success = true,
                    message = $"Progressive report generated for vehicle {vehicle.Vin}",
                    reportId = reportId.Value,
                    vehicleVin = vehicle.Vin,
                    analysisLevel = analysisLevel,
                    timestamp = DateTime.UtcNow,
                    note = "Generated using Progressive AI Analysis"
                });
            }
            else
            {
                return Ok(new
                {
                    success = false,
                    message = "Progressive report could not be generated - check logs for details"
                });
            }
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error in vehicle progressive report generation", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// ✅ NUOVO: Controlla e gestisce gli scheduler
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
                case "reset_retries":
                    if (_env.IsDevelopment() && _fakeScheduler != null)
                    {
                        _fakeScheduler.ResetRetryCounters();
                        return Ok(new { success = true, message = "Fake scheduler retry counters reset" });
                    }
                    else if (!_env.IsDevelopment() && _productionScheduler != null)
                    {
                        _productionScheduler.ResetRetryCounters();
                        return Ok(new { success = true, message = "Production scheduler retry counters reset" });
                    }
                    else
                    {
                        return BadRequest("Scheduler not available");
                    }

                case "force_report":
                    if (!int.TryParse(request.VehicleId, out var vehicleId))
                    {
                        return BadRequest("Invalid vehicle ID");
                    }

                    bool result;
                    if (_env.IsDevelopment() && _fakeScheduler != null)
                    {
                        // Non ha ForceProgressiveReportAsync, usiamo il metodo diretto
                        await GenerateProgressiveReportForVehicle(vehicleId, "Forced via API");
                        result = true;
                    }
                    else if (!_env.IsDevelopment() && _productionScheduler != null)
                    {
                        result = await _productionScheduler.ForceProgressiveReportAsync(vehicleId, "Forced via API");
                    }
                    else
                    {
                        return BadRequest("Scheduler not available");
                    }

                    return Ok(new { success = result, message = result ? "Report forced successfully" : "Report forcing failed" });

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
    /// ✅ AGGIORNATO: Controlla dati con info progressive
    /// </summary>
    [HttpGet("DataStatus")]
    public async Task<IActionResult> GetDataStatus()
    {
        var vehicles = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .Where(v => v.ClientOAuthAuthorized && v.IsActiveFlag && v.IsFetchingDataFlag)
            .ToListAsync();

        var result = new List<object>();

        foreach (var vehicle in vehicles)
        {
            var dataCount = await _db.VehiclesData.CountAsync(vd => vd.VehicleId == vehicle.Id);
            var latestData = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicle.Id)
                .OrderByDescending(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            // ✅ CONTA REPORT PROGRESSIVI SPECIFICAMENTE
            var progressiveReports = await _db.PdfReports
                .Where(r => r.ClientVehicleId == vehicle.Id &&
                           (r.Notes != null && (r.Notes.Contains("[PROGRESSIVE") || r.Notes.Contains("[PRODUCTION-PROGRESSIVE"))))
                .OrderByDescending(r => r.GeneratedAt)
                .Take(3)
                .Select(r => new
                {
                    r.Id,
                    r.GeneratedAt,
                    r.Notes,
                    IsProgressive = r.Notes != null && (r.Notes.Contains("[PROGRESSIVE") || r.Notes.Contains("[PRODUCTION-PROGRESSIVE"))
                })
                .ToListAsync();

            var firstDataRecord = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicle.Id)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var monitoringDays = firstDataRecord != default ? (DateTime.UtcNow - firstDataRecord).TotalDays : 0;

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
                monitoringDays = Math.Round(monitoringDays, 1),
                progressiveReports = progressiveReports,
                progressiveReportCount = progressiveReports.Count
            });
        }

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            environment = _env.IsDevelopment() ? "Development" : "Production",
            vehicles = result,
            summary = new
            {
                totalVehicles = vehicles.Count,
                totalDataRecords = await _db.VehiclesData.CountAsync(),
                totalReports = await _db.PdfReports.CountAsync(),
                progressiveReports = await _db.PdfReports.CountAsync(r =>
                    r.Notes != null && (r.Notes.Contains("[PROGRESSIVE") || r.Notes.Contains("[PRODUCTION-PROGRESSIVE")))
            }
        });
    }

    /// <summary>
    /// ✅ AGGIORNATO: Status con info progressive
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
            notes = r.Notes,
            isProgressive = r.Notes != null && (r.Notes.Contains("[PROGRESSIVE") || r.Notes.Contains("[PRODUCTION-PROGRESSIVE")),
            pdfExists = CheckPdfExists(r)
        });

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            environment = _env.IsDevelopment() ? "Development" : "Production",
            recentReports = result,
            totalReports = await _db.PdfReports.CountAsync(),
            progressiveReports = await _db.PdfReports.CountAsync(r =>
                r.Notes != null && (r.Notes.Contains("[PROGRESSIVE") || r.Notes.Contains("[PRODUCTION-PROGRESSIVE"))),
            systemStatus = new
            {
                aiSystemAvailable = await CheckAiSystemAvailability(),
                pdfGenerationAvailable = CheckPdfGenerationAvailability(),
                schedulerAvailable = _env.IsDevelopment() ? _fakeScheduler != null : _productionScheduler != null
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

        var pdfPath = PolarDrive.WebApi.Helpers.PdfStorageHelper.GetReportPdfPath(report);

        if (System.IO.File.Exists(pdfPath))
        {
            var bytes = await System.IO.File.ReadAllBytesAsync(pdfPath);
            var fileName = $"PolarDrive_Report_{report.Id}_{report.ClientVehicle?.Vin}_{report.ReportPeriodStart:yyyyMMdd}.pdf";

            return File(bytes, "application/pdf", fileName);
        }

        // Fallback: prova con file di testo
        var textPath = pdfPath.Replace(".pdf", ".txt");
        if (System.IO.File.Exists(textPath))
        {
            var textContent = await System.IO.File.ReadAllTextAsync(textPath);
            var fileName = $"PolarDrive_Report_{report.Id}_{report.ClientVehicle?.Vin}_{report.ReportPeriodStart:yyyyMMdd}.txt";

            return File(System.Text.Encoding.UTF8.GetBytes(textContent), "text/plain", fileName);
        }

        return NotFound("Report file not found on disk");
    }

    #region Helper Methods

    /// <summary>
    /// ✅ NUOVO: Genera report progressivo per veicolo (sostituisce il vecchio ReportGeneratorJob)
    /// </summary>
    private async Task<int?> GenerateProgressiveReportForVehicle(int vehicleId, string analysisLevel)
    {
        const string source = "TeslaFakeApiController.GenerateProgressiveReportForVehicle";

        try
        {
            var vehicle = await _db.ClientVehicles.FindAsync(vehicleId);
            if (vehicle == null) return null;

            // Determina periodo progressivo
            var firstRecord = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var now = DateTime.UtcNow;
            var monitoringPeriod = firstRecord != default ? now - firstRecord : TimeSpan.FromHours(24);

            // Genera insights progressivi
            var aiGenerator = new PolarAiReportGenerator(_db);
            var progressiveInsights = await aiGenerator.GenerateProgressiveInsightsAsync(vehicleId);

            if (string.IsNullOrWhiteSpace(progressiveInsights))
            {
                await _logger.Warning(source, $"No progressive insights generated for vehicle {vehicle.Vin}");
                return null;
            }

            // Crea record del report
            var progressiveReport = new PdfReport
            {
                ClientVehicleId = vehicleId,
                ClientCompanyId = vehicle.ClientCompanyId,
                ReportPeriodStart = now.AddDays(-1),
                ReportPeriodEnd = now,
                GeneratedAt = now,
                Notes = $"[API-PROGRESSIVE-{analysisLevel.Replace(" ", "")}] MonitoringDays: {monitoringPeriod.TotalDays:F1}"
            };

            _db.PdfReports.Add(progressiveReport);
            await _db.SaveChangesAsync();

            await _logger.Info(source, $"Progressive report generated for vehicle {vehicle.Vin}: ReportId {progressiveReport.Id}");

            return progressiveReport.Id;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error generating progressive report for vehicle {vehicleId}", ex.ToString());
            throw;
        }
    }

    private bool CheckPdfExists(PdfReport report)
    {
        var pdfPath = PolarDrive.WebApi.Helpers.PdfStorageHelper.GetReportPdfPath(report);
        var textPath = pdfPath.Replace(".pdf", ".txt");
        return System.IO.File.Exists(pdfPath) || System.IO.File.Exists(textPath);
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
public class ProgressiveReportRequest
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