using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PdfReportsController : ControllerBase
{
    private readonly PolarDriveDbContext db;
    private readonly PolarDriveLogger _logger;

    public PdfReportsController(PolarDriveDbContext context)
    {
        db = context;
        _logger = new PolarDriveLogger(db);
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
                .OrderByDescending(r => r.GeneratedAt)
                .ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ✅ OTTIMIZZAZIONE: Carica tutti i count in UNA query
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
}