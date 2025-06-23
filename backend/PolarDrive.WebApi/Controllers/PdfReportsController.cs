using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.WebApi.Services;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PdfReportsController : ControllerBase
{
    private readonly PolarDriveDbContext db;
    private readonly PolarDriveLogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebHostEnvironment _env;
    private readonly IReportGenerationService _reportSchedulerService;

    public PdfReportsController(
        PolarDriveDbContext context,
        IServiceProvider serviceProvider,
        IWebHostEnvironment env,
        IReportGenerationService reportSchedulerService)
    {
        db = context;
        _logger = new PolarDriveLogger(db);
        _serviceProvider = serviceProvider;
        _env = env;
        _reportSchedulerService = reportSchedulerService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PdfReportDTO>>> Get()
    {
        const string source = "PdfReportsController.Get";

        try
        {
            await _logger.Info(source, "Richiesta lista report PDF");

            var reports = await GetReportsWithIncludes();
            var result = new List<PdfReportDTO>();

            foreach (var report in reports)
            {
                var dto = await MapReportToDto(report);
                result.Add(dto);
            }

            // ✅ Log aggregato invece di singoli dettagli
            var pdfCount = result.Count(r => r.HasPdfFile);
            var htmlCount = result.Count(r => r.HasHtmlFile);
            var processingCount = result.Count(r => r.Status == "PROCESSING");

            await _logger.Info(source, "Mapping DTO completato",
                $"Total: {result.Count}, PDF: {pdfCount}, HTML: {htmlCount}, Processing: {processingCount}");

            return Ok(result);
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore recupero lista report", ex.ToString());
            return StatusCode(500, "Errore interno server");
        }
    }

    private async Task<List<Data.Entities.PdfReport>> GetReportsWithIncludes()
    {
        return await db.PdfReports
            .Include(r => r.ClientCompany)
            .Include(r => r.ClientVehicle)
            .OrderByDescending(r => r.GeneratedAt)
            .ToListAsync();
    }

    private async Task<PdfReportDTO> MapReportToDto(Data.Entities.PdfReport report)
    {
        var (htmlPath, pdfPath) = GetReportFilePaths(report);
        var fileInfo = GetFileInfo(htmlPath, pdfPath);
        var dataCount = await CountDataRecordsForReport(report);
        var monitoringDuration = (report.ReportPeriodEnd - report.ReportPeriodStart).TotalHours;

        return new PdfReportDTO
        {
            Id = report.Id,
            ReportPeriodStart = report.ReportPeriodStart.ToString("o"),
            ReportPeriodEnd = report.ReportPeriodEnd.ToString("o"),
            GeneratedAt = report.GeneratedAt?.ToString("o"),
            CompanyVatNumber = report.ClientCompany?.VatNumber ?? "",
            CompanyName = report.ClientCompany?.Name ?? "",
            VehicleVin = report.ClientVehicle?.Vin ?? "",
            VehicleModel = report.ClientVehicle?.Model ?? "",
            Notes = report.Notes,
            HasPdfFile = fileInfo.PdfExists,
            HasHtmlFile = fileInfo.HtmlExists,
            DataRecordsCount = dataCount,
            PdfFileSize = fileInfo.PdfSize,
            HtmlFileSize = fileInfo.HtmlSize,
            MonitoringDurationHours = Math.Max(0, Math.Floor(monitoringDuration)),
            IsRegenerated = report.RegenerationCount > 0,
            RegenerationCount = report.RegenerationCount,
            LastRegenerated = report.GeneratedAt?.ToString("o"),
            ReportType = DetermineReportType(report, dataCount),
            Status = DetermineReportStatus(fileInfo.PdfExists, fileInfo.HtmlExists, dataCount, report.Status),
        };
    }

    private (string HtmlPath, string PdfPath) GetReportFilePaths(Data.Entities.PdfReport report)
    {
        var htmlPath = GetReportFilePath(report, "html");
        var pdfPath = GetReportFilePath(report, "pdf");
        return (htmlPath, pdfPath);
    }

    private static (bool PdfExists, bool HtmlExists, long PdfSize, long HtmlSize) GetFileInfo(string htmlPath, string pdfPath)
    {
        var pdfExists = System.IO.File.Exists(pdfPath);
        var htmlExists = System.IO.File.Exists(htmlPath);
        var pdfSize = pdfExists ? new FileInfo(pdfPath).Length : 0;
        var htmlSize = htmlExists ? new FileInfo(htmlPath).Length : 0;

        return (pdfExists, htmlExists, pdfSize, htmlSize);
    }

    private static string DetermineReportType(Data.Entities.PdfReport report, int dataCount)
    {
        if (dataCount == 0)
            return "No_Data";

        var duration = (report.ReportPeriodEnd - report.ReportPeriodStart).TotalHours;

        return duration switch
        {
            >= MONTHLY_HOURS_THRESHOLD => "Mensile",
            >= WEEKLY_HOURS_THRESHOLD => "Settimanale",
            >= DAILY_HOURS_THRESHOLD => "Giornaliero",
            _ => "Giornaliero_Parizale"
        };
    }

    private static string DetermineReportStatus(bool pdfExists, bool htmlExists, int dataCount, string? dbStatus = null)
    {
        if (!string.IsNullOrEmpty(dbStatus))
        {
            return dbStatus switch
            {
                "PROCESSING" => "PROCESSING",
                "ERROR" => "ERROR",
                _ => DetermineFileBasedStatus(pdfExists, htmlExists, dataCount)
            };
        }
        return DetermineFileBasedStatus(pdfExists, htmlExists, dataCount);
    }

    private static string DetermineFileBasedStatus(bool pdfExists, bool htmlExists, int dataCount)
    {
        if (dataCount == 0)
            return "NO-DATA";

        return (pdfExists, htmlExists) switch
        {
            (true, _) => "PDF-READY",
            (false, true) => "HTML-ONLY",
            (false, false) when dataCount < MIN_RECORDS_FOR_GENERATION => "WAITING-RECORDS",
            _ => "GENERATE-READY"
        };
    }

    private async Task<int> CountDataRecordsForReport(Data.Entities.PdfReport report)
    {
        return await db.VehiclesData
            .Where(vd => vd.VehicleId == report.ClientVehicleId &&
                         vd.Timestamp >= report.ReportPeriodStart &&
                         vd.Timestamp <= report.ReportPeriodEnd)
            .CountAsync();
    }

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        var entity = await db.PdfReports.FindAsync(id);
        if (entity == null)
        {
            await _logger.Warning("PdfReportsController.PatchNotes", "Report not found.", $"ReportId: {id}");
            return NotFound();
        }

        if (!body.TryGetProperty("notes", out var notesProp))
        {
            await _logger.Warning("PdfReportsController.PatchNotes", "Missing 'notes' field in PATCH body.", $"ReportId: {id}");
            return BadRequest("SERVER ERROR → BAD REQUEST: Notes field missing!");
        }

        entity.Notes = notesProp.GetString() ?? string.Empty;
        await db.SaveChangesAsync();

        await _logger.Debug("PdfReportsController.PatchNotes", "Report notes updated.", $"ReportId: {id}");

        return NoContent();
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadPdf(int id)
    {
        try
        {
            await _logger.Info("PdfReportsController.DownloadPdf", "PDF download requested.", $"ReportId: {id}");

            var report = await db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                await _logger.Warning("PdfReportsController.DownloadPdf", "Report not found.", $"ReportId: {id}");
                return NotFound();
            }

            var htmlPath = GetReportFilePath(report, "html");
            var pdfPath = GetReportFilePath(report, "pdf");

            if (System.IO.File.Exists(pdfPath))
            {
                var pdfBytes = await System.IO.File.ReadAllBytesAsync(pdfPath);
                var fileName = $"PolarDrive_Report_{id}_{report.ClientVehicle?.Vin}_{report.ReportPeriodStart:yyyyMMdd}.pdf";

                await _logger.Info("PdfReportsController.DownloadPdf", "PDF downloaded successfully.",
                    $"ReportId: {id}, Size: {pdfBytes.Length} bytes, FileName: {fileName}");

                return File(pdfBytes, "application/pdf", fileName);
            }
            else if (System.IO.File.Exists(htmlPath))
            {
                var htmlBytes = await System.IO.File.ReadAllBytesAsync(htmlPath);
                var fileName = $"PolarDrive_Report_{id}_{report.ClientVehicle?.Vin}_{report.ReportPeriodStart:yyyyMMdd}.html";

                await _logger.Info("PdfReportsController.DownloadPdf", "HTML downloaded as fallback.",
                    $"ReportId: {id}, Size: {htmlBytes.Length} bytes");

                return File(htmlBytes, "text/html", fileName);
            }

            await _logger.Warning("PdfReportsController.DownloadPdf", "No file available for download.", $"ReportId: {id}");
            return NotFound("SERVER ERROR → NOT FOUND: Report file not available!");
        }
        catch (Exception ex)
        {
            await _logger.Error("PdfReportsController.DownloadPdf", "Download error occurred.", $"ReportId: {id}, Error: {ex}");
            return StatusCode(500, "SERVER ERROR → INTERNAL ERROR: Download failed!");
        }
    }

    [HttpPost("{id}/regenerate")]
    public async Task<IActionResult> RegenerateReport(int id)
    {
        try
        {
            await _logger.Info("PdfReportsController.RegenerateReport", "Report regeneration requested.", $"ReportId: {id}");

            var report = await db.PdfReports
                .Include(r => r.ClientVehicle)
                .Include(r => r.ClientCompany)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                await _logger.Warning("PdfReportsController.RegenerateReport", "Report not found.", $"ReportId: {id}");
                return NotFound();
            }

            var vehicleId = report.ClientVehicleId;
            var vehicle = await db.ClientVehicles
                .Include(v => v.ClientCompany)
                .FirstOrDefaultAsync(v => v.Id == vehicleId);

            if (vehicle == null)
            {
                await _logger.Warning("PdfReportsController.RegenerateReport", "Vehicle not found.", $"VehicleId: {vehicleId}");
                return BadRequest(new { success = false, message = "SERVER ERROR → INTERNAL ERROR: Vehicle not found" });
            }

            report.RegenerationCount++;
            report.Status = "PROCESSING";
            report.Notes = $"Ultima rigenerazione: {DateTime.UtcNow:yyyy-MM-dd HH:mm} - numero rigenerazione #{report.RegenerationCount}";
            await db.SaveChangesAsync();

            BackgroundJob.Enqueue(() => ProcessReportRegenerationAsync(report.Id));

            await db.SaveChangesAsync();

            await _logger.Info("PdfReportsController.RegenerateReport", "Report regenerated successfully.",
                $"ReportId: {report.Id}, RegenerationCount: {report.RegenerationCount}");

            return Ok(new
            {
                success = true,
                message = "Report regenerated successfully",
                regenerationCount = report.RegenerationCount
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("PdfReportsController.RegenerateReport", "Regeneration error occurred.", $"ReportId: {id}, Error: {ex}");
            return StatusCode(500, new { success = false, message = "SERVER ERROR → INTERNAL ERROR: Regeneration failed!" });
        }
    }

    public async Task ProcessReportRegenerationAsync(int reportId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();
        var logger = new PolarDriveLogger(db);

        try
        {
            await logger.Info("ProcessReportRegenerationAsync", "Starting regeneration", $"ReportId: {reportId}");

            var report = await db.PdfReports
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report != null)
            {
                // Aggiorna status prima della rigenerazione
                report.Status = "PROCESSING";
                await db.SaveChangesAsync();

                var reportService = scope.ServiceProvider.GetRequiredService<IReportGenerationService>();
                await reportService.ForceRegenerateFilesAsync(reportId);

                // ✅ Aggiorna status e timestamp DOPO la rigenerazione
                var updatedReport = await db.PdfReports.FindAsync(reportId);
                if (updatedReport != null)
                {
                    updatedReport.Status = "COMPLETED";
                    updatedReport.GeneratedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }

                await logger.Info("ProcessReportRegenerationAsync", "Regeneration completed", $"ReportId: {reportId}");
            }
        }
        catch (Exception ex)
        {
            await logger.Error("ProcessReportRegenerationAsync", "Regeneration failed", $"ReportId: {reportId}, Error: {ex}");

            var report = await db.PdfReports.FindAsync(reportId);
            if (report != null)
            {
                report.Status = "ERROR";
                report.Notes = $"Regeneration error: {ex.Message}";
                await db.SaveChangesAsync();
            }
        }
    }

    private string GetReportFilePath(Data.Entities.PdfReport report, string extension)
    {
        var folder = extension == "html" && _env.IsDevelopment() ? "dev-reports" : "reports";
        var generationDate = report.GeneratedAt ?? DateTime.UtcNow;

        var storageDir = Path.Combine("storage", folder,
            generationDate.Year.ToString(),
            generationDate.Month.ToString("D2"));

        var storageFileName = $"PolarDrive_Report_{report.Id}.{extension}";
        var storagePath = Path.Combine(storageDir, storageFileName);

        return storagePath;
    }
}