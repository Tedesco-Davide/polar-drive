using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Helpers;

public static class PdfStorageHelper
{
    public static string GetReportPdfPath(PdfReport report)
    {
        return Path.Combine("storage", "reports",
            report.ReportPeriodStart.Year.ToString(),
            report.ReportPeriodStart.Month.ToString("D2"),
            $"PolarDrive_Report_{report.Id}.pdf");
    }

    public static void EnsurePdfDirectoryExists(PdfReport report)
    {
        var dir = Path.GetDirectoryName(GetReportPdfPath(report));
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
}
