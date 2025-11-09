using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.Scheduler;
using PolarDrive.WebApi.Production;
using PolarDrive.WebApi.Services;

namespace PolarDrive.WebApi.Scheduler;

[ApiController]
[Route("api/[controller]")]
public class TeslaFakeApiController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly PolarDriveLogger _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IReportGenerationService _reportService;
    private readonly PolarDriveScheduler? _fakeScheduler;

    public TeslaFakeApiController(
        PolarDriveDbContext db,
        IWebHostEnvironment env,
        IServiceProvider serviceProvider,
        IReportGenerationService reportService)
    {
        if (!env.IsDevelopment())
        {
            throw new InvalidOperationException("TeslaFakeApiController is only available in Development environment");
        }

        _db = db;
        _env = env;
        _reportService = reportService;
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
    /// Forza la generazione di un report di test per tutti i veicoli attivi
    /// ✅ DELEGATO al ReportGenerationService
    /// </summary>
    [HttpPost("GenerateReport")]
    public async Task<IActionResult> GenerateReport()
    {
        const string source = "TeslaFakeApiController.GenerateReport";

        try
        {
            await _logger.Info(source, "Manual batch report generation triggered via API");

            var result = await _reportService.GenerateReportForAllActiveVehiclesAsync();

            if (result.Success)
            {
                await _logger.Info(source, "Manual batch report generation completed", result.ErrorMessage ?? "Success");

                return Ok(new
                {
                    success = true,
                    message = result.ErrorMessage ?? "Batch report generation completed successfully",
                    timestamp = result.Timestamp,
                    note = "Reports generated via ReportGenerationService"
                });
            }
            else
            {
                return Ok(new
                {
                    success = false,
                    message = result.ErrorMessage ?? "Batch report generation failed",
                    timestamp = result.Timestamp
                });
            }
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error in manual batch report generation", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Forza report per un singolo veicolo
    /// ✅ DELEGATO al ReportGenerationService
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

            await _logger.Info(source, $"Manual report generation triggered for VIN {vehicle.Vin} via API");

            var analysisLevel = request?.AnalysisLevel ?? "Manual API Generation";
            var result = await _reportService.GenerateReportForVehicleAsync(vehicleId, analysisLevel);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    message = $"Report generated for vehicle {result.VehicleVin}",
                    reportId = result.ReportId,
                    vehicleVin = result.VehicleVin,
                    analysisLevel = result.AnalysisLevel,
                    timestamp = result.Timestamp,
                    filesGenerated = result.FileStatus,
                    note = "Generated via ReportGenerationService"
                });
            }
            else
            {
                return Ok(new
                {
                    success = false,
                    message = result.ErrorMessage ?? "Report generation failed",
                    vehicleVin = result.VehicleVin,
                    timestamp = result.Timestamp
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
    /// Controlla e gestisce gli scheduler
    /// ✅ DELEGATO al ReportGenerationService per force_report
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

                    var result = await _reportService.GenerateReportForVehicleAsync(vehicleId, "Forced via API");

                    return Ok(new
                    {
                        success = result.Success,
                        message = result.Success ? "Report forced successfully via ReportGenerationService" : "Report forcing failed",
                        reportId = result.ReportId,
                        errorMessage = result.ErrorMessage
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
    /// ✅ MANTENUTO: Questo è specifico per debug/monitoring, non tocca la generazione report
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
                .Where(r => r.VehicleId == vehicle.Id)
                .OrderByDescending(r => r.GeneratedAt)
                .Take(3)
                .Select(r => new
                {
                    r.Id,
                    r.GeneratedAt,
                    r.Notes,
                    r.Status,
                    FilesExist = GetFilesStatusSync(r) // ✅ Versione sincrona per query
                })
                .ToListAsync();

            var firstDataRecord = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicle.Id)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var monitoringDays = firstDataRecord != default ? (DateTime.Now - firstDataRecord).TotalDays : 0;
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
            timestamp = DateTime.Now,
            environment = "Development",
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
    /// ✅ MANTENUTO: Questo è specifico per monitoring, non tocca la generazione report
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
            filesStatus = GetFilesStatusSync(r) // ✅ Versione sincrona per query
        });

        return Ok(new
        {
            timestamp = DateTime.Now,
            environment = "Development",
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
    /// Download PDF report
    /// ✅ MANTENUTO: Questo è specifico per file serving, non tocca la generazione report
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

        var pdfPath = GetFilePath(report, "pdf", "dev-reports");

        if (System.IO.File.Exists(pdfPath))
        {
            var bytes = await System.IO.File.ReadAllBytesAsync(pdfPath);
            var fileName = $"PolarDrive_PolarReport_{report.Id}_{report.ClientVehicle?.Vin}_{report.ReportPeriodStart:yyyyMMdd}.pdf";

            return File(bytes, "application/pdf", fileName);
        }

        // Fallback: prova con file HTML (per development)
        var htmlPath = GetFilePath(report, "html", "dev-reports");
        if (System.IO.File.Exists(htmlPath))
        {
            var htmlContent = await System.IO.File.ReadAllTextAsync(htmlPath);
            var fileName = $"PolarDrive_PolarReport_{report.Id}_{report.ClientVehicle?.Vin}_{report.ReportPeriodStart:yyyyMMdd}.html";

            return File(System.Text.Encoding.UTF8.GetBytes(htmlContent), "text/html", fileName);
        }

        return NotFound("Report file not found on disk");
    }

    #region ✅ HELPER METHODS - Solo per monitoring/utility, NESSUNA logica di generazione report

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

    /// <summary>
    /// Versione sincrona di GetFilesStatus per uso in query LINQ
    /// </summary>
    private object GetFilesStatusSync(PdfReport report)
    {
        var pdfPath = GetFilePath(report, "pdf", "dev-reports");
        var htmlPath = GetFilePath(report, "html", "dev-reports");

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
    /// Path file (mantenuto solo per monitoring e download)
    /// </summary>
    private static string GetFilePath(PdfReport report, string ext, string folder)
    {
        var generationDate = report.GeneratedAt ?? DateTime.Now;

        return Path.Combine("storage", folder,
            generationDate.Year.ToString(),
            generationDate.Month.ToString("D2"),
            $"PolarDrive_PolarReport_{report.Id}.{ext}");
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

public class ReportRequest
{
    public string AnalysisLevel { get; set; } = "Manual Generation";
}

public class SchedulerControlRequest
{
    public string Action { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
}