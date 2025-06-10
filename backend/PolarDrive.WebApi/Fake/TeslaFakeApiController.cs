using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.Jobs;

namespace PolarDrive.WebApi.Fake;

[ApiController]
[Route("api/[controller]")]
public class TeslaFakeApiController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly PolarDriveLogger _logger;

    public TeslaFakeApiController(PolarDriveDbContext db)
    {
        _db = db;
        _logger = new PolarDriveLogger(_db);
    }

    /// <summary>
    /// Forza la generazione di un report di test (ultimi 5 minuti - 4-5 records)
    /// Aggiornato per usare ReportGeneratorJob
    /// </summary>
    [HttpPost("GenerateTestReport")]
    public async Task<IActionResult> GenerateTestReport()
    {
        const string source = "TestController.GenerateTestReport";

        try
        {
            await _logger.Info(source, "Manual test report generation triggered (5 min period)");

            var reportJob = new ReportGeneratorJob(_db);
            await reportJob.RunTestAsync();

            await _logger.Info(source, "Manual test report generation completed");

            return Ok(new
            {
                success = true,
                message = "Test report generation completed (last 5 minutes)",
                timestamp = DateTime.UtcNow,
                period = "5 minutes",
                note = "Reports generated using improved AI system with local fallback"
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error in manual test report generation", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Forza la generazione di un report rapido (ultimi 2 minuti - 1-2 records)
    /// </summary>
    [HttpPost("GenerateQuickReport")]
    public async Task<IActionResult> GenerateQuickReport()
    {
        const string source = "TestController.GenerateQuickReport";

        try
        {
            await _logger.Info(source, "Manual quick report generation triggered (2 min period)");

            var reportJob = new ReportGeneratorJob(_db);
            await reportJob.RunQuickTestAsync();

            await _logger.Info(source, "Manual quick report generation completed");

            return Ok(new
            {
                success = true,
                message = "Quick report generation completed (last 2 minutes)",
                timestamp = DateTime.UtcNow,
                period = "2 minutes",
                note = "Reports generated using improved AI system with local fallback"
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error in manual quick report generation", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Genera report per un periodo personalizzato
    /// </summary>
    [HttpPost("GenerateCustomReport")]
    public async Task<IActionResult> GenerateCustomReport([FromBody] CustomReportRequest request)
    {
        const string source = "TestController.GenerateCustomReport";

        try
        {
            if (!DateTime.TryParse(request.PeriodStart, out var startDate) ||
                !DateTime.TryParse(request.PeriodEnd, out var endDate))
            {
                return BadRequest("Invalid date format. Use yyyy-MM-dd HH:mm");
            }

            if (startDate >= endDate)
            {
                return BadRequest("Start date must be before end date");
            }

            await _logger.Info(source, "Manual custom report generation triggered",
                $"Period: {startDate:yyyy-MM-dd HH:mm} to {endDate:yyyy-MM-dd HH:mm}");

            var reportJob = new ReportGeneratorJob(_db);
            await reportJob.RunForPeriodAsync(startDate, endDate, "Custom-API");

            await _logger.Info(source, "Manual custom report generation completed");

            return Ok(new
            {
                success = true,
                message = $"Custom report generation completed for period {startDate:yyyy-MM-dd HH:mm} to {endDate:yyyy-MM-dd HH:mm}",
                timestamp = DateTime.UtcNow,
                periodStart = startDate,
                periodEnd = endDate
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error in manual custom report generation", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Genera report per un singolo veicolo
    /// </summary>
    [HttpPost("GenerateVehicleReport/{vehicleId}")]
    public async Task<IActionResult> GenerateVehicleReport(int vehicleId, [FromBody] CustomReportRequest request)
    {
        const string source = "TestController.GenerateVehicleReport";

        try
        {
            var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Id == vehicleId);
            if (vehicle == null)
            {
                return NotFound($"Vehicle with ID {vehicleId} not found");
            }

            if (!DateTime.TryParse(request.PeriodStart, out var startDate) ||
                !DateTime.TryParse(request.PeriodEnd, out var endDate))
            {
                return BadRequest("Invalid date format. Use yyyy-MM-dd HH:mm");
            }

            await _logger.Info(source, $"Vehicle report generation triggered for VIN {vehicle.Vin}");

            var reportJob = new ReportGeneratorJob(_db);
            var report = await reportJob.GenerateForVehicleAsync(vehicleId, startDate, endDate);

            if (report != null)
            {
                return Ok(new
                {
                    success = true,
                    message = $"Report generated for vehicle {vehicle.Vin}",
                    reportId = report.Id,
                    vehicleVin = vehicle.Vin,
                    timestamp = DateTime.UtcNow
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
    /// Controlla quanti dati ci sono per ogni veicolo
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

            var recentReports = await _db.PdfReports
                .Where(r => r.ClientVehicleId == vehicle.Id)
                .OrderByDescending(r => r.GeneratedAt)
                .Take(3)
                .Select(r => new { r.Id, r.GeneratedAt, r.Notes })
                .ToListAsync();

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
                recentReports = recentReports
            });
        }

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            vehicles = result,
            summary = new
            {
                totalVehicles = vehicles.Count,
                totalDataRecords = await _db.VehiclesData.CountAsync(),
                totalReports = await _db.PdfReports.CountAsync()
            }
        });
    }

    /// <summary>
    /// Controlla lo stato dei report generati
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
            pdfExists = CheckPdfExists(r)
        });

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            recentReports = result,
            totalReports = await _db.PdfReports.CountAsync(),
            systemStatus = new
            {
                aiSystemAvailable = await CheckAiSystemAvailability(),
                pdfGenerationAvailable = CheckPdfGenerationAvailability()
            }
        });
    }

    /// <summary>
    /// Download PDF report
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
}

// Request model for custom reports
public class CustomReportRequest
{
    public string PeriodStart { get; set; } = string.Empty;
    public string PeriodEnd { get; set; } = string.Empty;
}