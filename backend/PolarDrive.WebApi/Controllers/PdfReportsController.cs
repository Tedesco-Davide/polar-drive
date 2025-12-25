using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.WebApi.Services;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PdfReportsController(
    PolarDriveDbContext context,
    IReportGenerationService reportGenerationService) : ControllerBase
{
    private readonly PolarDriveDbContext db = context;
    private readonly PolarDriveLogger _logger = new();
    private readonly IReportGenerationService _reportGenerationService = reportGenerationService;

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<PdfReportDTO>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? search = null)
    {
        const string source = "PdfReportsController.Get";

        try
        {
            await _logger.Info(source, "Requested filtered list of PDF reports",
                $"Page: {page}, PageSize: {pageSize}");

            var baseQuery = db.PdfReports.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmedSearch = search.Trim();
                if (int.TryParse(trimmedSearch, out int searchId))
                {
                    baseQuery = baseQuery.Where(r => r.Id == searchId);
                }
                else
                {
                    var searchPattern = $"%{trimmedSearch}%";
                    baseQuery = baseQuery.Where(r => EF.Functions.Like(r.Status, searchPattern));
                }
            }

            var totalCount = await baseQuery.CountAsync();

            var reports = await baseQuery
                .OrderByDescending(r => r.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.ReportPeriodStart,
                    r.ReportPeriodEnd,
                    r.GeneratedAt,
                    r.Notes,
                    r.Status,
                    r.PdfHash,
                    HasPdf = r.PdfContent != null,
                    PdfSize = r.PdfContent != null ? (long)r.PdfContent.Length : 0L,
                    CompanyVatNumber = r.ClientCompany != null ? r.ClientCompany.VatNumber : null,
                    CompanyName = r.ClientCompany != null ? r.ClientCompany.Name : null,
                    VehicleId = r.ClientVehicle != null ? (int?)r.ClientVehicle.Id : null,
                    VehicleVin = r.ClientVehicle != null ? r.ClientVehicle.Vin : null,
                    VehicleModel = r.ClientVehicle != null ? r.ClientVehicle.Model : null,
                    VehicleBrand = r.ClientVehicle != null ? r.ClientVehicle.Brand : null
                })
                .ToListAsync();

            // Map directly to DTOs without heavy VehiclesData queries
            var result = reports.Select(report => new PdfReportDTO
            {
                Id = report.Id,
                ReportPeriodStart = report.ReportPeriodStart.ToString("o"),
                ReportPeriodEnd = report.ReportPeriodEnd.ToString("o"),
                GeneratedAt = report.GeneratedAt?.ToString("o"),
                CompanyVatNumber = report.CompanyVatNumber ?? "",
                CompanyName = report.CompanyName ?? "",
                VehicleVin = report.VehicleVin ?? "",
                VehicleModel = report.VehicleModel ?? "",
                VehicleBrand = report.VehicleBrand ?? "",
                Notes = report.Notes,
                HasPdfFile = report.HasPdf,
                HasHtmlFile = false,
                DataRecordsCount = 0, // Removed: heavy query on VehiclesData
                PdfFileSize = report.PdfSize,
                HtmlFileSize = 0,
                MonitoringDurationHours = 0, // Removed: heavy query on VehiclesData
                ReportType = "Report",
                Status = report.HasPdf ? "PDF-READY" : (!string.IsNullOrEmpty(report.Status) ? report.Status : "PROCESSING"),
                PdfHash = report.PdfHash ?? ""
            }).ToList();

            await _logger.Info(source, "Mapping DTO completato",
                $"Total: {result.Count}, Page: {page}");

            return Ok(new
            {
                Data = result,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore recupero lista report", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
    }

    private PdfReportDTO MapReportToDto(
        Data.Entities.PdfReport report,
        dynamic? stats)
    {
        var totalHistoricalRecords = stats?.TotalCount ?? 0;
        var firstRecordEver = stats?.FirstRecord ?? DateTime.Now;
        var totalMonitoringHours = (DateTime.Now - firstRecordEver).TotalHours;

        var hasPdf = report.PdfContent != null && report.PdfContent.Length > 0;
        var pdfSize = hasPdf ? report.PdfContent!.Length : 0;

        var status = hasPdf ? "PDF-READY" : (!string.IsNullOrEmpty(report.Status) ? report.Status : "PROCESSING");

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
            VehicleBrand = report.ClientVehicle?.Brand ?? "",
            Notes = report.Notes,
            HasPdfFile = hasPdf,
            HasHtmlFile = false,
            DataRecordsCount = totalHistoricalRecords,
            PdfFileSize = pdfSize,
            HtmlFileSize = 0,
            MonitoringDurationHours = Math.Max(0, Math.Floor(totalMonitoringHours)),
            ReportType = DetermineReportType(totalHistoricalRecords),
            Status = status,
            PdfHash = report.PdfHash ?? ""
        };
    }

    private static string DetermineReportType(int totalHistoricalRecords)
    {
        if (totalHistoricalRecords == 0) return "No_Data";
        return totalHistoricalRecords switch
        {
            >= MONTHLY_HOURS_THRESHOLD => "Mensile",
            >= WEEKLY_HOURS_THRESHOLD => "Settimanale",
            >= DAILY_HOURS_THRESHOLD => "Giornaliero",
            _ => "Giornaliero_Parziale"
        };
    }

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        var entity = await db.PdfReports.FindAsync(id);
        if (entity == null) return NotFound();

        if (!body.TryGetProperty("notes", out var notesProp))
            return BadRequest("Notes field missing");

        entity.Notes = notesProp.GetString() ?? string.Empty;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadPdf(int id)
    {
        try
        {
            var report = await db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null) return NotFound();

            if (report.PdfContent != null && report.PdfContent.Length > 0)
            {
                var fileName = $"PolarDrive_PolarReport_{id}_{report.ClientVehicle?.Vin}_{report.ReportPeriodStart:yyyyMMdd}.pdf";

                Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
                Response.Headers.Pragma = "no-cache";
                if (!string.IsNullOrWhiteSpace(report.PdfHash))
                    Response.Headers.ETag = $"W/\"{report.PdfHash}\"";

                return File(report.PdfContent, "application/pdf", fileName);
            }

            return NotFound("Report PDF non disponibile");
        }
        catch (Exception ex)
        {
            await _logger.Error("PdfReportsController.DownloadPdf", "Download error", ex.ToString());
            return StatusCode(500, "Download failed");
        }
    }

    [HttpPost("{id}/regenerate")]
    public async Task<IActionResult> RegenerateReport(int id)
    {
        const string source = "PdfReportsController.RegenerateReport";

        try
        {
            await _logger.Info(source, "Richiesta rigenerazione report", $"ReportId: {id}");

            var report = await db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound(new { errorCode = "REPORT_NOT_FOUND" });

            if (!string.IsNullOrWhiteSpace(report.PdfHash) &&
                report.PdfContent != null &&
                report.PdfContent.Length > 0)
            {
                return Conflict(new { errorCode = "REPORT_ALREADY_COMPLETED" });
            }

            var regenerableStatuses = new[] { "PROCESSING", "ERROR" };
            if (!string.IsNullOrWhiteSpace(report.Status) &&
                !regenerableStatuses.Contains(report.Status))
            {
                return BadRequest(new { errorCode = "REPORT_NOT_REGENERABLE", status = report.Status });
            }

            // Salva parametri
            var companyId = report.ClientCompanyId;
            var vehicleId = report.VehicleId;
            var periodStart = report.ReportPeriodStart;
            var periodEnd = report.ReportPeriodEnd;

            // Reset stato
            report.Status = "REGENERATING";
            report.GeneratedAt = null;
            await db.SaveChangesAsync();

            await _logger.Info(source, "Report impostato per rigenerazione",
                $"ReportId: {id}, NewStatus: REGENERATING");

            // ✅ FIX: Usa IServiceScopeFactory invece del service diretto!
            var scopeFactory = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();

            _ = Task.Run(async () =>
            {
                try
                {
                    // ✅ CREA UN NUOVO SCOPE INDIPENDENTE
                    using var taskScope = scopeFactory.CreateScope();
                    var taskService = taskScope.ServiceProvider.GetRequiredService<IReportGenerationService>();

                    await _logger.Info(source, "🔄 BACKGROUND: Inizio rigenerazione",
                        $"ReportId: {id}, VehicleId: {vehicleId}, CompanyId: {companyId}");

                    var success = await taskService.GenerateSingleReportAsync(
                        companyId,
                        vehicleId,
                        periodStart,
                        periodEnd,
                        isRegeneration: true,
                        existingReportId: id
                    );

                    if (success)
                    {
                        await _logger.Info(source, "✅ BACKGROUND: Rigenerazione COMPLETATA",
                            $"ReportId: {id}");
                    }
                    else
                    {
                        await _logger.Error(source, "❌ BACKGROUND: Rigenerazione FALLITA",
                            $"ReportId: {id}");
                    }
                }
                catch (Exception bgEx)
                {
                    await _logger.Error(source, "💥 BACKGROUND: Exception durante rigenerazione",
                        $"ReportId: {id}, Error: {bgEx.Message}, StackTrace: {bgEx.StackTrace}");
                }
            });

            return Accepted(new
            {
                message = "Rigenerazione avviata in background",
                reportId = id,
                status = "REGENERATING"
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore rigenerazione report",
                $"ReportId: {id}, Error: {ex.Message}");
            return StatusCode(500, new { error = "Errore interno server" });
        }
    }

    [HttpGet("{id}/can-regenerate")]
    public async Task<ActionResult<object>> CanRegenerate(int id)
    {
        try
        {
            var report = await db.PdfReports
                .Select(r => new
                {
                    r.Id,
                    r.PdfHash,
                    r.Status,
                    HasPdfContent = r.PdfContent != null && r.PdfContent.Length > 0
                })
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound(new { canRegenerate = false, reason = "Report non trovato" });

            var isImmutable = !string.IsNullOrWhiteSpace(report.PdfHash) && report.HasPdfContent;
            var isInErrorState = new[] { "PROCESSING", "ERROR" }
                .Contains(report.Status ?? "");

            var canRegenerate = !isImmutable && isInErrorState;

            return Ok(new
            {
                canRegenerate,
                isImmutable,
                pdfHash = report.PdfHash,
                status = report.Status,
                reason = isImmutable
                    ? "Report completato e certificato - immutabile per conformità fiscale"
                    : !isInErrorState
                        ? $"Report in stato '{report.Status}' non rigenerabile"
                        : "Report eleggibile per rigenerazione"
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("PdfReportsController.CanRegenerate",
                "Errore verifica rigenerabilità", ex.ToString());
            return StatusCode(500, new { canRegenerate = false, reason = "Errore interno" });
        }
    }

    /// <summary>
    /// Verifica se esistono report in stato PROCESSING.
    /// Utilizzato per bloccare nuove rigenerazioni mentre un report è in elaborazione.
    /// </summary>
    [HttpGet("has-processing")]
    public async Task<ActionResult<object>> HasProcessingReports()
    {
        const string source = "PdfReportsController.HasProcessingReports";

        try
        {
            // Controlla sia PROCESSING che REGENERATING
            var processingReport = await db.PdfReports
                .Where(r => r.Status == "PROCESSING" || r.Status == "REGENERATING")
                .Select(r => new
                {
                    r.Id,
                    r.Status,
                    CompanyName = r.ClientCompany != null ? r.ClientCompany.Name : null,
                    VehicleVin = r.ClientVehicle != null ? r.ClientVehicle.Vin : null,
                    r.GeneratedAt
                })
                .FirstOrDefaultAsync();

            if (processingReport != null)
            {
                await _logger.Info(source, "⚠️ Report in PROCESSING trovato",
                    $"ReportId: {processingReport.Id}, Company: {processingReport.CompanyName}");

                return Ok(new
                {
                    hasProcessing = true,
                    processingReportId = processingReport.Id,
                    companyName = processingReport.CompanyName,
                    vehicleVin = processingReport.VehicleVin,
                    message = "Un report è attualmente in elaborazione. Attendere il completamento prima di avviare una nuova rigenerazione."
                });
            }

            return Ok(new
            {
                hasProcessing = false,
                message = "Nessun report in elaborazione"
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore verifica report in PROCESSING", ex.ToString());
            return StatusCode(500, new { hasProcessing = false, error = "Errore interno server" });
        }
    }

    //ASD
}