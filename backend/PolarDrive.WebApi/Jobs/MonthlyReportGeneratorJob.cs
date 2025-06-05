using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.AiReports;
using PolarDrive.WebApi.Helpers;

namespace PolarDrive.WebApi.Jobs;

public class MonthlyReportGeneratorJob(PolarDriveDbContext db)
{
    private readonly PolarDriveDbContext _db = db;
    private readonly PolarDriveLogger _logger = new(db);

    public async Task RunAsync()
    {
        const string source = "MonthlyReportGeneratorJob.RunAsync";
        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        await _logger.Info(source, "Starting monthly report generation.",
            $"Target period: {periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}");

        var vehicles = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .ToListAsync();

        await _logger.Debug(source, "Fetched client vehicles.", $"Count: {vehicles.Count}");

        foreach (var vehicle in vehicles)
        {
            if (vehicle.ClientCompany == null)
            {
                await _logger.Warning(source, "Skipping vehicle with missing company.", $"VehicleId: {vehicle.Id}");
                continue;
            }

            bool alreadyExists = await _db.PdfReports.AnyAsync(r =>
                r.ClientVehicleId == vehicle.Id &&
                r.ReportPeriodStart == periodStart &&
                r.ReportPeriodEnd == periodEnd);

            if (alreadyExists)
            {
                await _logger.Debug(source, "Report already exists, skipping.",
                    $"VehicleId: {vehicle.Id}, Company: {vehicle.ClientCompany.Name}");
                continue;
            }

            var rawJsonList = await _db.VehiclesData
                .Where(d => d.VehicleId == vehicle.Id &&
                            d.Timestamp >= periodStart &&
                            d.Timestamp <= periodEnd)
                .OrderBy(d => d.Timestamp)
                .Select(d => d.RawJson)
                .ToListAsync();

            if (rawJsonList.Count == 0)
            {
                await _logger.Debug(source, "No data found for report period.",
                    $"VehicleId: {vehicle.Id}, PeriodStart: {periodStart:yyyy-MM-dd}");
                continue;
            }

            var aiGenerator = new AiReportGenerator(_db);
            var insights = await aiGenerator.GenerateSummaryFromRawJson(rawJsonList);

            if (string.IsNullOrWhiteSpace(insights))
            {
                await _logger.Warning(source, "AI insights generation returned empty.",
                    $"VehicleId: {vehicle.Id}, PeriodStart: {periodStart:yyyy-MM-dd}");
                continue;
            }

            var report = new PdfReport
            {
                ClientVehicleId = vehicle.Id,
                ClientCompanyId = vehicle.ClientCompanyId,
                ReportPeriodStart = periodStart,
                ReportPeriodEnd = periodEnd,
                GeneratedAt = DateTime.UtcNow,
                Notes = ""
            };

            _db.PdfReports.Add(report);
            await _db.SaveChangesAsync();

            await _logger.Info(source, "PdfReport record created.",
                $"ReportId: {report.Id}, VehicleId: {vehicle.Id}");

            PdfStorageHelper.EnsurePdfDirectoryExists(report);
            var path = PdfStorageHelper.GetReportPdfPath(report);

            if (!File.Exists(path))
            {
                var pdfGenerator = new PdfGenerationService(_db);
                var bytes = pdfGenerator.GeneratePolardriveReportPdf(report, insights);
                await File.WriteAllBytesAsync(path, bytes);

                await _logger.Info(source, "PDF file generated and saved.",
                    $"Path: {path}, Size: {bytes.Length} bytes");
            }
            else
            {
                await _logger.Warning(source, "PDF file already exists on disk.",
                    $"Skipping generation. Path: {path}");
            }
        }

        await _logger.Info(source, "Monthly report generation completed.");
    }
}