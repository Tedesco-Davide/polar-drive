using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.WebApi.AiReports;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PdfReportsController(PolarDriveDbContext db) : ControllerBase
{
    private readonly PolarDriveLogger _logger = new(db);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PdfReportDTO>>> Get()
    {
        const string source = "PdfReportsController.Get";
        await _logger.Info(source, "Requested list of PDF reports.");

        var reports = await db.PdfReports
            .Include(r => r.ClientCompany)
            .Include(r => r.ClientVehicle)
            .ToListAsync();

        await _logger.Debug(source, "Fetched reports from DB.", $"Count: {reports.Count}");

        var result = reports.Select(r => new PdfReportDTO
        {
            Id = r.Id,
            ReportPeriodStart = r.ReportPeriodStart.ToString("o"),
            ReportPeriodEnd = r.ReportPeriodEnd.ToString("o"),
            GeneratedAt = r.GeneratedAt?.ToString("o"),
            CompanyVatNumber = r.ClientCompany?.VatNumber ?? "",
            CompanyName = r.ClientCompany?.Name ?? "",
            VehicleVin = r.ClientVehicle?.Vin ?? "",
            VehicleModel = r.ClientVehicle?.Model ?? "",
            Notes = r.Notes
        });

        return Ok(result);
    }

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        const string source = "PdfReportsController.PatchNotes";
        var entity = await db.PdfReports.FindAsync(id);

        if (entity == null)
        {
            await _logger.Warning(source, "PDF report not found.", $"ReportId: {id}");
            return NotFound("SERVER ERROR → NOT FOUND: PDF report not found!");
        }

        if (!body.TryGetProperty("notes", out var notesProp))
        {
            await _logger.Warning(source, "Missing 'notes' field.", $"ReportId: {id}");
            return BadRequest("SERVER ERROR → BAD REQUEST: Notes field missing!");
        }

        entity.Notes = notesProp.GetString() ?? string.Empty;
        await db.SaveChangesAsync();

        await _logger.Debug(source, "PDF report notes updated.", $"ReportId: {id}, NotesLength: {entity.Notes.Length}");
        return NoContent();
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadPdf(int id)
    {
        const string source = "PdfReportsController.DownloadPdf";

        var report = await db.PdfReports
            .Include(r => r.ClientCompany)
            .Include(r => r.ClientVehicle)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
        {
            await _logger.Warning(source, "PDF report not found in DB.", $"ReportId: {id}");
            return NotFound("SERVER ERROR → NOT FOUND: PDF report not found!");
        }

        var regenerate = HttpContext.Request.Query["regenerate"] == "true";

        var pdfPath = Path.Combine("storage", "reports", report.ReportPeriodStart.Year.ToString(),
            report.ReportPeriodStart.Month.ToString("D2"), $"PolarDrive_Report_{report.Id}.pdf");

        if (!System.IO.File.Exists(pdfPath) || regenerate)
        {
            var rawJsonList = await db.VehiclesData
                .Where(d => d.VehicleId == report.ClientVehicleId &&
                            d.Timestamp >= report.ReportPeriodStart &&
                            d.Timestamp <= report.ReportPeriodEnd)
                .OrderBy(d => d.Timestamp)
                .Select(d => d.RawJson)
                .ToListAsync();

            if (rawJsonList.Count == 0)
            {
                await _logger.Warning(source, "PDF file non trovato e nessun dato disponibile per rigenerarlo.", $"ReportId: {id}");
                return NotFound("SERVER ERROR → PDF not found and no data to regenerate.");
            }

            var aiGenerator = new AiReportGenerator(db);
            var insights = await aiGenerator.GenerateSummaryFromRawJson(rawJsonList);

            if (string.IsNullOrWhiteSpace(insights))
            {
                await _logger.Warning(source, "PDF rigenerazione fallita per mancanza di insights.", $"ReportId: {id}");
                return NotFound("SERVER ERROR → No insights to regenerate PDF.");
            }

            var generator = new PdfGenerationService(db);
            var bytes = generator.GeneratePolardriveReportPdf(report, insights);
            await System.IO.File.WriteAllBytesAsync(pdfPath, bytes);
            await _logger.Info(source, "PDF rigenerato da zero per download.", $"ReportId: {id}");
        }

        var pdfBytes = await System.IO.File.ReadAllBytesAsync(pdfPath);

        await _logger.Info(source, "PDF file inviato con successo.", $"ReportId: {id}, Size: {pdfBytes.Length}");
        return File(pdfBytes, "application/pdf", $"PolarDrive_Report_{id}.pdf");
    }
}
