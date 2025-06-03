using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System.Text;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PdfReportsController(PolarDriveDbContext db) : ControllerBase
{
    private readonly PolarDriveLogger _logger = new(db);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PdfReportDTO>>> Get()
    {
        await _logger.Info("PdfReportsController.Get", "Requested list of PDF reports.");

        var reports = await db.PdfReports
            .Include(r => r.ClientCompany)
            .Include(r => r.ClientVehicle)
            .ToListAsync();

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
        var entity = await db.PdfReports.FindAsync(id);
        if (entity == null)
        {
            await _logger.Warning("PdfReportsController.PatchNotes", "PDF report not found.", $"ReportId: {id}");
            return NotFound("SERVER ERROR → NOT FOUND: PDF report not found!");
        }

        if (!body.TryGetProperty("notes", out var notesProp))
        {
            await _logger.Warning("PdfReportsController.PatchNotes", "Missing 'notes' field.", $"ReportId: {id}");
            return BadRequest("SERVER ERROR → BAD REQUEST: Notes field missing!");
        }

        entity.Notes = notesProp.GetString() ?? string.Empty;
        await db.SaveChangesAsync();

        await _logger.Debug("PdfReportsController.PatchNotes", "PDF report notes updated.", $"ReportId: {id}");
        return NoContent();
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadPdf(int id)
    {
        var report = await db.PdfReports
            .Include(r => r.ClientCompany)
            .Include(r => r.ClientVehicle)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
        {
            await _logger.Warning("PdfReportsController.DownloadPdf", "PDF report not found.", $"ReportId: {id}");
            return NotFound("SERVER ERROR → NOT FOUND: PDF report not found!");
        }

        await _logger.Info("PdfReportsController.DownloadPdf", "PDF report requested for download.", $"ReportId: {id}");

        // Recupero dati grezzi dal DB
        var vehicleData = await db.VehiclesData
            .Where(v => v.VehicleId == report.ClientVehicleId && v.Timestamp >= report.ReportPeriodStart && v.Timestamp <= report.ReportPeriodEnd)
            .OrderBy(v => v.Timestamp)
            .ToListAsync();
        var rawJsonList = vehicleData.Select(v => v.RawJson).ToList();

        // Chiamata AI Effettiva ad AI in locale
        // var aiReportContent = await AiReportGenerator.GenerateSummaryFromRawJson(rawJsonList);

        // 🔧 Prepare dynamic content
        var sb = new StringBuilder();
        sb.AppendLine("🧾 POLARDRIVE REPORT");
        sb.AppendLine("-----------------------------");
        sb.AppendLine($"Azienda: {report.ClientCompany?.Name} ({report.ClientCompany?.VatNumber})");
        sb.AppendLine($"Veicolo: {report.ClientVehicle?.Model} - {report.ClientVehicle?.Vin}");
        sb.AppendLine($"Periodo: {report.ReportPeriodStart:dd/MM/yyyy} → {report.ReportPeriodEnd:dd/MM/yyyy}");
        sb.AppendLine($"Generato: {DateTime.Now:dd/MM/yyyy}");
        sb.AppendLine();
        sb.AppendLine("📌 NOTE:");
        sb.AppendLine(report.Notes ?? "-");

        // 📄 Generate PDF
        using var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        var font = new XFont("Verdana", 12, XFontStyle.Regular);
        gfx.DrawString(sb.ToString(), font, XBrushes.Black, new XRect(40, 40, page.Width - 80, page.Height - 80), XStringFormats.TopLeft);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Seek(0, SeekOrigin.Begin);

        await _logger.Info("PdfReportsController.DownloadPdf", "PDF report successfully generated.", $"ReportId: {id}");

        return File(stream.ToArray(), "application/pdf", $"PolarDrive_Report_{id}.pdf");
    }
}