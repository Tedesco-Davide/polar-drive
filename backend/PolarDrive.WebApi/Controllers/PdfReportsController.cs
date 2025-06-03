using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PolarDrive.Data.Entities;

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

        // 📄 Generate PDF
        var pdfBytes = GeneratePolardriveReportPdf(report, rawJsonList);
        return File(pdfBytes, "application/pdf", $"PolarDrive_Report_{id}.pdf");
    }

    private byte[] GeneratePolardriveReportPdf(PdfReport report, List<string> rawJsonList)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PdfSharpCore.PageSize.A4;

        var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont("Verdana", 18, XFontStyle.Bold);
        var bodyFont = new XFont("Verdana", 12);
        var boldFont = new XFont("Verdana", 12, XFontStyle.Bold);
        var grayBrush = new XSolidBrush(XColor.FromArgb(50, 50, 50));
        var blueBrush = new XSolidBrush(XColors.DarkBlue);

        double y = 40;

        // ─── Logo proporzionato con fallback ─────────────────────────────────────
        string[] logoCandidates = [
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/logo/d54082ed-1ab7-42e7-a8cc-def69338aab7.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/logo/DataPolar_Logo_PolarDrive.png")
        ];
        string? logoPath = logoCandidates.FirstOrDefault(System.IO.File.Exists);

        if (logoPath != null)
        {
            var logo = XImage.FromFile(logoPath);

            double maxWidth = 120;
            double maxHeight = 50;
            double scale = Math.Min(maxWidth / logo.PixelWidth, maxHeight / logo.PixelHeight);

            double logoWidth = logo.PixelWidth * scale;
            double logoHeight = logo.PixelHeight * scale;

            gfx.DrawImage(logo, 40, y, logoWidth, logoHeight);
        }
        else
        {
            gfx.DrawRectangle(XBrushes.DarkBlue, 40, y, 100, 50);
            gfx.DrawString("LOGO", new XFont("Verdana", 10, XFontStyle.Bold), XBrushes.White,
                new XRect(40, y + 15, 100, 20), XStringFormats.Center);
        }

        // ─── Titolo ───────────────────────────────────────────────────────────
        gfx.DrawString("POLARDRIVE REPORT", titleFont, blueBrush,
            new XRect(0, y + 60, page.Width, 30), XStringFormats.TopCenter);
        y += 100;

        // ─── Info Box ─────────────────────────────────────────────────────────
        double boxTop = y;
        double boxHeight = 120;
        gfx.DrawRoundedRectangle(
            new XPen(XColors.LightGray, 1),
            XBrushes.WhiteSmoke,
            new XRect(40, boxTop, page.Width - 80, boxHeight),
            new XSize(15, 15));

        y += 15;
        gfx.DrawString("Azienda:", boldFont, blueBrush, 50, y);
        gfx.DrawString($"{report.ClientCompany?.Name} ({report.ClientCompany?.VatNumber})", bodyFont, grayBrush, 150, y); y += 20;

        gfx.DrawString("Veicolo:", boldFont, blueBrush, 50, y);
        gfx.DrawString($"{report.ClientVehicle?.Model} - {report.ClientVehicle?.Vin}", bodyFont, grayBrush, 150, y); y += 20;

        gfx.DrawString("Periodo:", boldFont, blueBrush, 50, y);
        gfx.DrawString($"{report.ReportPeriodStart:dd/MM/yyyy} → {report.ReportPeriodEnd:dd/MM/yyyy}", bodyFont, grayBrush, 150, y); y += 20;

        gfx.DrawString("Generato:", boldFont, blueBrush, 50, y);
        gfx.DrawString($"{DateTime.Now:dd/MM/yyyy}", bodyFont, grayBrush, 150, y); y += 40;

        // ─── Note ─────────────────────────────────────────────────────────────
        gfx.DrawEllipse(XBrushes.DarkBlue, 40, y - 10, 10, 10); // pallino stile emoji
        gfx.DrawString("NOTE:", boldFont, blueBrush, 60, y); y += 20;

        gfx.DrawString(report.Notes ?? "-", bodyFont, grayBrush,
            new XRect(40, y, page.Width - 80, 100), XStringFormats.TopLeft);
        y += 80;

        // ─── Grafico a barre simulato ─────────────────────────────────────────
        gfx.DrawEllipse(XBrushes.DarkBlue, 40, y - 10, 10, 10);
        gfx.DrawString("Attività giornaliera simulata", boldFont, XBrushes.Black, 60, y);
        y += 20;

        var barData = new[] { 3, 6, 5, 2, 7, 8, 4 };
        double barWidth = 20;
        double space = 10;
        double maxBarHeight = 60;
        double baseY = y + maxBarHeight;

        for (int i = 0; i < barData.Length; i++)
        {
            double height = barData[i] / 8.0 * maxBarHeight;
            double x = 40 + i * (barWidth + space);
            gfx.DrawRectangle(XBrushes.DarkSlateBlue, x, baseY - height, barWidth, height);
        }

        y += 100;

        // ─── Footer ───────────────────────────────────────────────────────────
        gfx.DrawString("Powered by DataPolar", bodyFont, XBrushes.LightSlateGray,
            new XRect(40, page.Height - 40, page.Width - 80, 20), XStringFormats.Center);

        // ─── Salva ─────────────────────────────────────────────────────────────
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

}