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
public class PdfReportsController : ControllerBase
{
    private readonly PolarDriveDbContext db;
    private readonly PolarDriveLogger _logger;
    private readonly IReportGenerationService _reportGenerationService;

    public PdfReportsController(
        PolarDriveDbContext context,
        IReportGenerationService reportGenerationService)
    {
        db = context;
        _logger = new PolarDriveLogger(db);
        _reportGenerationService = reportGenerationService;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<PdfReportDTO>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? search = null)
    {
        const string source = "PdfReportsController.Get";

        try
        {
            await _logger.Info(source, "Richiesta lista report PDF",
                $"Page: {page}, PageSize: {pageSize}, Search: {search ?? "none"}");

            var query = db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .AsQueryable();

            // Filtro ricerca
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r =>
                    (r.ClientCompany != null && r.ClientCompany.VatNumber.Contains(search)) ||
                    (r.ClientCompany != null && r.ClientCompany.Name.Contains(search)) ||
                    (r.ClientVehicle != null && r.ClientVehicle.Vin.Contains(search)));
            }

            var totalCount = await query.CountAsync();

            var reports = await query
                .OrderByDescending(r => r.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vehicleIds = reports.Select(r => r.VehicleId).Distinct().ToList();
            var vehicleStats = await db.VehiclesData
                .Where(vd => vehicleIds.Contains(vd.VehicleId))
                .GroupBy(vd => vd.VehicleId)
                .Select(g => new
                {
                    VehicleId = g.Key,
                    TotalCount = g.Count(),
                    FirstRecord = g.Min(vd => vd.Timestamp),
                    LastRecord = g.Max(vd => vd.Timestamp)
                })
                .ToDictionaryAsync(x => x.VehicleId);

            var result = new List<PdfReportDTO>();
            foreach (var report in reports)
            {
                var stats = vehicleStats.GetValueOrDefault(report.VehicleId);
                var dto = MapReportToDto(report, stats);
                result.Add(dto);
            }

            await _logger.Info(source, "Mapping DTO completato",
                $"Total: {result.Count}, Page: {page}");

            return Ok(new PaginatedResponse<PdfReportDTO>
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

        // Status semplificato (no query aggiuntive)
        var status = hasPdf ? "PDF-READY" :
                     (!string.IsNullOrEmpty(report.Status) ? report.Status : "PROCESSING");

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
            {
                await _logger.Warning(source, "Report non trovato", $"ReportId: {id}");
                return NotFound(new { error = "Report non trovato" });
            }

            if (!string.IsNullOrWhiteSpace(report.PdfHash) &&
                report.PdfContent != null &&
                report.PdfContent.Length > 0)
            {
                await _logger.Warning(source,
                    "TENTATIVO RIGENERAZIONE REPORT IMMUTABILE BLOCCATO",
                    $"ReportId: {id}, PdfHash: {report.PdfHash}");

                return Conflict(new
                {
                    error = "Report già completato e certificato",
                    isImmutable = true
                });
            }

            var regenerableStatuses = new[] { "FILE-MISSING", "PROCESSING", "ERROR", "NO-DATA", "" };
            if (!string.IsNullOrWhiteSpace(report.Status) &&
                !regenerableStatuses.Contains(report.Status))
            {
                return BadRequest(new { error = "Report non rigenerabile" });
            }

            await _logger.Info(source, "Report eleggibile per rigenerazione",
                $"ReportId: {id}, Status: '{report.Status}', CompanyId: {report.ClientCompanyId}, VehicleId: {report.VehicleId}");

            // Salva i parametri necessari
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

            // ✅ FIX: Task.Run senza dipendenze esterne
            _ = Task.Run(async () =>
            {
                try
                {
                    // ⚠️ IMPORTANTE: Non usare 'db' da fuori - crea un nuovo scope
                    await _logger.Info(source,
                        "🔄 BACKGROUND: Inizio rigenerazione",
                        $"ReportId: {id}, VehicleId: {vehicleId}, CompanyId: {companyId}");

                    // ✅ LOG PARAMETRI PRIMA DELLA CHIAMATA
                    await _logger.Info(source,
                        "🎯 BACKGROUND: Parametri chiamata",
                        $"CompanyId={companyId}, VehicleId={vehicleId}, " +
                        $"PeriodStart={periodStart:yyyy-MM-dd}, PeriodEnd={periodEnd:yyyy-MM-dd}, " +
                        $"IsRegeneration=true, ExistingReportId={id}");

                    await _logger.Info(source,
                        "🎯 BACKGROUND: Tipo service",
                        $"Service={_reportGenerationService?.GetType().Name ?? "NULL"}");

                    if (_reportGenerationService == null)
                    {
                        await _logger.Error(source,
                            "❌ BACKGROUND: _reportGenerationService è NULL!");
                        return;
                    }

                    await _logger.Info(source,
                        "🎯 BACKGROUND: Chiamata GenerateSingleReportAsync...");

                    var success = await _reportGenerationService.GenerateSingleReportAsync(
                        companyId,
                        vehicleId,
                        periodStart,
                        periodEnd,
                        isRegeneration: true,
                        existingReportId: id
                    );

                    await _logger.Info(source, "🎯 BACKGROUND: Chiamata completata");

                    if (success)
                    {
                        await _logger.Info(source,
                            "✅ BACKGROUND: Rigenerazione COMPLETATA",
                            $"ReportId: {id}");
                    }
                    else
                    {
                        await _logger.Error(source,
                            "❌ BACKGROUND: Rigenerazione FALLITA (returned false)",
                            $"ReportId: {id}");
                    }
                }
                catch (Exception bgEx)
                {
                    await _logger.Error(source,
                        "💥 BACKGROUND: Exception durante rigenerazione",
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

    /// <summary>
    /// ✅ NUOVO ENDPOINT: Verifica se un report può essere rigenerato
    /// Ritorna true solo se il report non ha PdfHash (mai completato con successo)
    /// </summary>
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
            var isInErrorState = new[] { "FILE-MISSING", "PROCESSING", "ERROR", "NO-DATA" }
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
}