using System.IO;
using PolarDrive.Data.Entities;
using PolarDrive.Data.DbContexts;

namespace PolarDrive.WebApi.Helpers;

public static class PdfStorageHelper
{
    private static readonly PolarDriveLogger Logger = new(new PolarDriveDbContext(null!));

    public static string GetReportPdfPath(PdfReport report)
    {
        var path = Path.Combine("storage", "reports",
            report.ReportPeriodStart.Year.ToString(),
            report.ReportPeriodStart.Month.ToString("D2"),
            $"PolarDrive_Report_{report.Id}.pdf");

        Logger.Debug("PdfStorageHelper.GetReportPdfPath", "Calculated PDF path.", $"Path: {path}").Wait();
        return path;
    }

    public static void EnsurePdfDirectoryExists(PdfReport report)
    {
        var dir = Path.GetDirectoryName(GetReportPdfPath(report));
        if (dir == null) return;

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            Logger.Info("PdfStorageHelper.EnsurePdfDirectoryExists", "Created missing PDF directory.", $"Path: {dir}").Wait();
        }
        else
        {
            Logger.Debug("PdfStorageHelper.EnsurePdfDirectoryExists", "PDF directory already exists.", $"Path: {dir}").Wait();
        }
    }
}