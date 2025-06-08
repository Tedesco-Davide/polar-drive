using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.AiReports;
using PolarDrive.WebApi.Helpers;

namespace PolarDrive.WebApi.Jobs;

/// <summary>
/// Genera report per veicoli in base a un periodo specificato
/// Può essere usato per report mensili, settimanali, o personalizzati
/// </summary>
public class ReportGeneratorJob(PolarDriveDbContext db)
{
    private readonly PolarDriveDbContext _db = db;
    private readonly PolarDriveLogger _logger = new(db);

    /// <summary>
    /// Genera report per il mese precedente (usato da scheduler automatico)
    /// </summary>
    public async Task RunMonthlyAsync()
    {
        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        await RunForPeriodAsync(periodStart, periodEnd, "Monthly");
    }

    /// <summary>
    /// Genera report per un periodo personalizzato
    /// </summary>
    public async Task RunForPeriodAsync(DateTime periodStart, DateTime periodEnd, string reportType = "Custom")
    {
        const string source = "ReportGeneratorJob.RunForPeriod";

        await _logger.Info(source, $"Starting {reportType} report generation.",
            $"Period: {periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}");

        var vehicles = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .Where(v => v.ClientOAuthAuthorized) // Solo veicoli autorizzati
            .ToListAsync();

        await _logger.Debug(source, "Fetched authorized client vehicles.", $"Count: {vehicles.Count}");

        int reportsGenerated = 0;
        int reportsSkipped = 0;

        foreach (var vehicle in vehicles)
        {
            if (vehicle.ClientCompany == null)
            {
                await _logger.Warning(source, "Skipping vehicle with missing company.", $"VehicleId: {vehicle.Id}");
                continue;
            }

            // Controlla se il report esiste già
            bool alreadyExists = await _db.PdfReports.AnyAsync(r =>
                r.ClientVehicleId == vehicle.Id &&
                r.ReportPeriodStart == periodStart &&
                r.ReportPeriodEnd == periodEnd);

            if (alreadyExists)
            {
                await _logger.Debug(source, "Report already exists, skipping.",
                    $"VehicleId: {vehicle.Id}, Company: {vehicle.ClientCompany.Name}");
                reportsSkipped++;
                continue;
            }

            // Recupera i dati grezzi per il periodo
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
                    $"VehicleId: {vehicle.Id}, VIN: {vehicle.Vin}, Period: {periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}");
                continue;
            }

            await _logger.Info(source, $"Processing {rawJsonList.Count} data records for vehicle {vehicle.Vin}");

            // Genera insights AI
            var aiGenerator = new AiReportGenerator(_db);
            var insights = await aiGenerator.GenerateSummaryFromRawJson(rawJsonList);

            if (string.IsNullOrWhiteSpace(insights))
            {
                await _logger.Warning(source, "AI insights generation returned empty.",
                    $"VehicleId: {vehicle.Id}, VIN: {vehicle.Vin}, DataCount: {rawJsonList.Count}");
                continue;
            }

            // Crea record del report
            var report = new PdfReport
            {
                ClientVehicleId = vehicle.Id,
                ClientCompanyId = vehicle.ClientCompanyId,
                ReportPeriodStart = periodStart,
                ReportPeriodEnd = periodEnd,
                GeneratedAt = DateTime.UtcNow,
                Notes = $"{reportType} report generated from {rawJsonList.Count} data points"
            };

            _db.PdfReports.Add(report);
            await _db.SaveChangesAsync();

            await _logger.Info(source, "PdfReport record created.",
                $"ReportId: {report.Id}, VehicleId: {vehicle.Id}, VIN: {vehicle.Vin}");

            // Genera e salva il PDF
            try
            {
                PdfStorageHelper.EnsurePdfDirectoryExists(report);
                var path = PdfStorageHelper.GetReportPdfPath(report);

                if (!File.Exists(path))
                {
                    var pdfGenerator = new PdfGenerationService(_db);
                    var bytes = pdfGenerator.GeneratePolardriveReportPdf(report, insights);
                    await File.WriteAllBytesAsync(path, bytes);

                    await _logger.Info(source, "PDF file generated and saved.",
                        $"Path: {path}, Size: {bytes.Length} bytes, VIN: {vehicle.Vin}");

                    reportsGenerated++;
                }
                else
                {
                    await _logger.Warning(source, "PDF file already exists on disk.",
                        $"Skipping generation. Path: {path}");
                    reportsSkipped++;
                }
            }
            catch (Exception ex)
            {
                await _logger.Error(source, $"Error generating PDF for vehicle {vehicle.Vin}", ex.ToString());
                // Rimuovi il record se la generazione PDF fallisce
                _db.PdfReports.Remove(report);
                await _db.SaveChangesAsync();
            }
        }

        await _logger.Info(source, $"{reportType} report generation completed.",
            $"Generated: {reportsGenerated}, Skipped: {reportsSkipped}, Total vehicles: {vehicles.Count}");
    }

    /// <summary>
    /// Genera report per un singolo veicolo
    /// </summary>
    public async Task<PdfReport?> GenerateForVehicleAsync(int vehicleId, DateTime periodStart, DateTime periodEnd)
    {
        const string source = "ReportGeneratorJob.GenerateForVehicle";

        var vehicle = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .FirstOrDefaultAsync(v => v.Id == vehicleId);

        if (vehicle == null)
        {
            await _logger.Warning(source, $"Vehicle not found: {vehicleId}");
            return null;
        }

        // Esegui per singolo veicolo
        await RunForPeriodAsync(periodStart, periodEnd, "On-Demand");

        // Restituisci il report generato
        return await _db.PdfReports
            .FirstOrDefaultAsync(r => r.ClientVehicleId == vehicleId &&
                                     r.ReportPeriodStart == periodStart &&
                                     r.ReportPeriodEnd == periodEnd);
    }
}