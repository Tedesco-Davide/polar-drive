using System.Diagnostics;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.AiReports;

public class PdfGenerationService(PolarDriveDbContext dbContext)
{
    private readonly PolarDriveLogger _logger = new(dbContext);

    public byte[] GeneratePolardriveReportPdf(PdfReport report, string aiReportContentInsights)
    {
        var source = "PdfGenerationService.GeneratePolardriveReportPdf";
        var html = RenderHtmlFromTemplate(report, aiReportContentInsights);

        var tempDir = Path.GetTempPath();
        var htmlPath = Path.Combine(tempDir, $"PolarDrive_{report.Id}.html");
        var pdfPath = Path.Combine("storage", "reports", report.ReportPeriodStart.Year.ToString(),
            report.ReportPeriodStart.Month.ToString("D2"), $"PolarDrive_Report_{report.Id}.pdf");

        File.WriteAllText(htmlPath, html);
        _logger.Debug(source, "HTML template scritto su disco temporaneo.", $"Path: {htmlPath}");

        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        var npxPath = Path.Combine(programFiles, "nodejs", "npx.cmd");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = npxPath,
                Arguments = $"ts-node AiReports/generateFromFile.ts \"{htmlPath}\" \"{pdfPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _logger.Info(source, "Avvio generazione PDF con Puppeteer.", $"Html: {htmlPath}, Output: {pdfPath}");

        process.Start();
        process.WaitForExit();

        if (!File.Exists(pdfPath))
        {
            var error = process.StandardError.ReadToEnd();
            _logger.Error(source, "Generazione PDF fallita.", $"Errore Puppeteer: {error}");
            throw new Exception("PDF generation failed. Check Puppeteer output.");
        }

        var pdfBytes = File.ReadAllBytes(pdfPath);
        _logger.Info(source, "PDF generato con successo.", $"Dimensione: {pdfBytes.Length} bytes, ReportId: {report.Id}");

        // Cleanup
        File.Delete(htmlPath);
        _logger.Debug(source, "File temporanei eliminati.", $"HTML: {htmlPath}, PDF: {pdfPath}");

        return pdfBytes;
    }

    private static string RenderHtmlFromTemplate(PdfReport report, string aiReportContentInsights)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "AiReports");
        var templatePath = Path.Combine(basePath, "templates", "report.html");
        var cssPath = Path.Combine(basePath, "styles", "report.css");

        var htmlTemplate = File.ReadAllText(templatePath);
        var cssContent = File.ReadAllText(cssPath);

        return htmlTemplate
            .Replace("{{companyName}}", report.ClientCompany?.Name ?? "")
            .Replace("{{vatNumber}}", report.ClientCompany?.VatNumber ?? "")
            .Replace("{{vehicleModel}}", report.ClientVehicle?.Model ?? "")
            .Replace("{{vehicleVin}}", report.ClientVehicle?.Vin ?? "")
            .Replace("{{periodStart}}", report.ReportPeriodStart.ToString("yyyy-MM-dd"))
            .Replace("{{periodEnd}}", report.ReportPeriodEnd.ToString("yyyy-MM-dd"))
            .Replace("{{notes}}", report.Notes ?? "")
            .Replace("{{styles}}", $"<style>{cssContent}</style>")
            .Replace("{{insights}}", aiReportContentInsights);
    }
}