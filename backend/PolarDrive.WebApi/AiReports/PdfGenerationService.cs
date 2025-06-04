using System.Diagnostics;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.AiReports;

public static class PdfGenerationService
{
    public static byte[] GeneratePolardriveReportPdf(PdfReport report, string aiReportContentInsights)
    {
        var html = RenderHtmlFromTemplate(report, aiReportContentInsights);

        var tempDir = Path.GetTempPath();
        var htmlPath = Path.Combine(tempDir, $"PolarDrive_{report.Id}.html");
        var pdfPath = Path.Combine(tempDir, $"PolarDrive_{report.Id}.pdf");

        File.WriteAllText(htmlPath, html);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "npx",
                Arguments = $"ts-node AiReports/generateFromFile.ts \"{htmlPath}\" \"{pdfPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        process.WaitForExit();

        if (!File.Exists(pdfPath))
            throw new Exception("PDF generation failed. Check Puppeteer output.");

        var pdfBytes = File.ReadAllBytes(pdfPath);
        File.Delete(htmlPath);
        File.Delete(pdfPath);

        return pdfBytes;
    }

    private static string RenderHtmlFromTemplate(PdfReport report, string aiReportContentInsights)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "PdfGeneration");
        var templatePath = Path.Combine(basePath, "templates", "report.html");
        var cssPath = Path.Combine(basePath, "styles", "report.css");

        var htmlTemplate = File.ReadAllText(templatePath);
        var cssContent = File.ReadAllText(cssPath);

        var renderedHtml = htmlTemplate
            .Replace("{{companyName}}", report.ClientCompany?.Name ?? "")
            .Replace("{{vatNumber}}", report.ClientCompany?.VatNumber ?? "")
            .Replace("{{vehicleModel}}", report.ClientVehicle?.Model ?? "")
            .Replace("{{vehicleVin}}", report.ClientVehicle?.Vin ?? "")
            .Replace("{{periodStart}}", report.ReportPeriodStart.ToString("yyyy-MM-dd"))
            .Replace("{{periodEnd}}", report.ReportPeriodEnd.ToString("yyyy-MM-dd"))
            .Replace("{{notes}}", report.Notes ?? "")
            .Replace("{{styles}}", $"<style>{cssContent}</style>")
            .Replace("{{insights}}", aiReportContentInsights);

        return renderedHtml;
    }
}