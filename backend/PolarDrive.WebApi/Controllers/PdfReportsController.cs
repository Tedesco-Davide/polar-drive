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
            await _logger.Warning(source, "PDF report not found.", $"ReportId: {id}");
            return NotFound("SERVER ERROR → NOT FOUND: PDF report not found!");
        }

        await _logger.Info(source, "PDF report requested for download.", $"ReportId: {id}");

        try
        {
            var vehicleData = await db.VehiclesData
                .Where(v => v.VehicleId == report.ClientVehicleId &&
                            v.Timestamp >= report.ReportPeriodStart &&
                            v.Timestamp <= report.ReportPeriodEnd)
                .OrderBy(v => v.Timestamp)
                .ToListAsync();

            await _logger.Debug(source, "Fetched raw vehicle data.", $"Count: {vehicleData.Count}, VehicleId: {report.ClientVehicleId}");

            var rawJsonList = vehicleData.Select(v => v.RawJson).ToList();

            var aiReportGenerator = new AiReportGenerator(db);
            var aiReportContentInsights = await aiReportGenerator.GenerateSummaryFromRawJson(rawJsonList);

            await _logger.Debug(source, "AI insights generated.", $"Length: {aiReportContentInsights?.Length ?? 0}");

            if (string.IsNullOrWhiteSpace(aiReportContentInsights))
            {
                await _logger.Error(source, "AI insights generation returned null or empty string.");
                return StatusCode(500, "SERVER ERROR → AI report generation failed.");
            }

            var pdfGenerator = new PdfGenerationService(db);
            var pdfBytes = pdfGenerator.GeneratePolardriveReportPdf(report, aiReportContentInsights);

            await _logger.Info(source, "PDF generated successfully.", $"ReportId: {id}, Size: {pdfBytes.Length} bytes");

            return File(pdfBytes, "application/pdf", $"PolarDrive_Report_{id}.pdf");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Failed to generate PDF report.", ex.ToString());
            return StatusCode(500, "SERVER ERROR → PDF generation failed.");
        }
    }
}
