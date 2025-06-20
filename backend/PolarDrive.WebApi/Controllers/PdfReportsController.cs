using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.WebApi.Scheduler;
using PolarDrive.WebApi.Services;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PdfReportsController : ControllerBase
{
    private const int DAILY_HOURS_THRESHOLD = 24;
    private const int WEEKLY_HOURS_THRESHOLD = 168;
    private const int MONTHLY_HOURS_THRESHOLD = 720;
    private const int MIN_RECORDS_FOR_GENERATION = 5;

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

            await _logger.Info(source, "Mapping DTO completato", $"Processed: {result.Count} reports");
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
            MonitoringDurationHours = Math.Floor(monitoringDuration),
            IsRegenerated = report.RegenerationCount > 0,
            RegenerationCount = report.RegenerationCount,
            LastRegenerated = report.GeneratedAt?.ToString("o"),
            ReportType = DetermineReportType(report, dataCount),
            Status = DetermineReportStatus(fileInfo.PdfExists, fileInfo.HtmlExists, dataCount),
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
            return "admin.vehicleReports.reporttypenodata";

        var duration = (report.ReportPeriodEnd - report.ReportPeriodStart).TotalHours;

        return duration switch
        {
            >= MONTHLY_HOURS_THRESHOLD => "admin.vehicleReports.reporttypemonthly",
            >= WEEKLY_HOURS_THRESHOLD => "admin.vehicleReports.reporttypeweekly",
            >= DAILY_HOURS_THRESHOLD => "admin.vehicleReports.reporttypedaily",
            _ => "admin.vehicleReports.reporttypedailypartial"
        };
    }

    private static string DetermineReportStatus(bool pdfExists, bool htmlExists, int dataCount)
    {
        return (pdfExists, htmlExists, dataCount) switch
        {
            (true, _, _) => "PDF-READY",
            (false, true, _) => "HTML-ONLY",
            (false, false, 0) => "NO-DATA",
            (false, false, < MIN_RECORDS_FOR_GENERATION) => "WAITING-RECORDS",
            _ => "GENERATE-READY"
        };
    }

    private async Task<int> CountDataRecordsForReport(Data.Entities.PdfReport report)
    {
        return await db.VehiclesData
            .Where(vd => vd.VehicleId == report.ClientVehicleId)
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

            // Usa il metodo pubblico ForceGenerateReport che è più appropriato per generazioni manuali
            await _reportSchedulerService.ForceRegenerateFilesAsync(report.Id);

            // Aggiorna il conteggio di rigenerazione
            report.RegenerationCount++;
            report.Notes = $"Ultima rigenerazione: {DateTime.UtcNow:yyyy-MM-dd HH:mm} - numero rigenerazione #{report.RegenerationCount}";

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

    private string GetReportFilePath(Data.Entities.PdfReport report, string extension)
    {
        var folder = extension == "html" && _env.IsDevelopment() ? "dev-reports" : "reports";
        var storageDir = Path.Combine("storage", folder,
            report.ReportPeriodStart.Year.ToString(),
            report.ReportPeriodStart.Month.ToString("D2"));
        var storageFileName = $"PolarDrive_Report_{report.Id}.{extension}";
        var storagePath = Path.Combine(storageDir, storageFileName);

        if (System.IO.File.Exists(storagePath))
        {
            _logger.Debug("PdfReportsController.GetReportFilePath", "File found at standard path.",
                $"ReportId: {report.Id}, Path: {storagePath}");
        }
        else
        {
            _logger.Error("PdfReportsController.GetReportFilePath", "File not found at standard path.",
                $"ReportId: {report.Id}, Path: {storagePath}");
        }

        return storagePath;
    }
}