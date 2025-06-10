using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.AiReports;

namespace PolarDrive.WebApi.Jobs;

/// <summary>
/// Job per la generazione di report - AGGIORNATO per usare i servizi separati
/// 1. HtmlReportService per generare HTML
/// 2. PdfGenerationService per convertire in PDF
/// </summary>
public class ReportGeneratorJob(PolarDriveDbContext dbContext)
{
    private readonly PolarDriveDbContext _db = dbContext;
    private readonly PolarDriveLogger _logger = new(dbContext);

    /// <summary>
    /// Genera report per test (ultimi 5 minuti)
    /// </summary>
    public async Task RunTestAsync()
    {
        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddMinutes(-5);
        await RunForPeriodAsync(startTime, endTime, "Test-5min");
    }

    /// <summary>
    /// Genera report rapido per test (ultimi 2 minuti)
    /// </summary>
    public async Task RunQuickTestAsync()
    {
        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddMinutes(-2);
        await RunForPeriodAsync(startTime, endTime, "QuickTest-2min");
    }

    /// <summary>
    /// Genera report per un periodo specifico
    /// </summary>
    public async Task RunForPeriodAsync(DateTime startTime, DateTime endTime, string notes = "")
    {
        const string source = "ReportGeneratorJob.RunForPeriod";

        try
        {
            await _logger.Info(source, "Inizio generazione report per periodo",
                $"Periodo: {startTime:yyyy-MM-dd HH:mm} - {endTime:yyyy-MM-dd HH:mm}");

            // Trova tutti i veicoli attivi con dati nel periodo
            var activeVehicles = await _db.ClientVehicles
                .Include(v => v.ClientCompany)
                .Where(v => v.IsActiveFlag && v.IsFetchingDataFlag)
                .Where(v => _db.VehiclesData.Any(vd =>
                    vd.VehicleId == v.Id &&
                    vd.Timestamp >= startTime &&
                    vd.Timestamp <= endTime))
                .ToListAsync();

            if (!activeVehicles.Any())
            {
                await _logger.Info(source, "Nessun veicolo con dati nel periodo specificato");
                return;
            }

            await _logger.Info(source, $"Trovati {activeVehicles.Count} veicoli attivi con dati");

            // Genera report per ogni veicolo
            var successCount = 0;
            var errorCount = 0;

            foreach (var vehicle in activeVehicles)
            {
                try
                {
                    var report = await GenerateForVehicleAsync(vehicle.Id, startTime, endTime, notes);
                    if (report != null)
                    {
                        successCount++;
                        await _logger.Info(source, $"Report generato per VIN {vehicle.Vin}",
                            $"ReportId: {report.Id}");
                    }
                    else
                    {
                        errorCount++;
                        await _logger.Warning(source, $"Fallimento generazione report per VIN {vehicle.Vin}");
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    await _logger.Error(source, $"Errore generazione report per VIN {vehicle.Vin}", ex.ToString());
                }
            }

            await _logger.Info(source, "Completata generazione report per periodo",
                $"Successi: {successCount}, Errori: {errorCount}");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore generale nella generazione report", ex.ToString());
            throw;
        }
    }

    /// <summary>
    /// Genera report per un singolo veicolo
    /// </summary>
    public async Task<PdfReport?> GenerateForVehicleAsync(int vehicleId, DateTime startTime, DateTime endTime, string notes = "")
    {
        const string source = "ReportGeneratorJob.GenerateForVehicle";

        try
        {
            // Verifica che il veicolo esista
            var vehicle = await _db.ClientVehicles
                .Include(v => v.ClientCompany)
                .FirstOrDefaultAsync(v => v.Id == vehicleId);

            if (vehicle == null)
            {
                await _logger.Error(source, $"Veicolo non trovato: ID {vehicleId}");
                return null;
            }

            // Recupera i dati per il periodo
            var vehicleData = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId &&
                           vd.Timestamp >= startTime &&
                           vd.Timestamp <= endTime)
                .OrderBy(vd => vd.Timestamp)
                .ToListAsync();

            if (!vehicleData.Any())
            {
                await _logger.Warning(source, $"Nessun dato trovato per veicolo {vehicle.Vin} nel periodo");
                return null;
            }

            await _logger.Info(source, $"Trovati {vehicleData.Count} record per veicolo {vehicle.Vin}");

            // 1. Genera insights AI
            var aiGenerator = new AiReportGenerator(_db);
            var rawJsonList = vehicleData.Select(vd => vd.RawJson).ToList();
            var aiInsights = await aiGenerator.GenerateSummaryFromRawJson(rawJsonList);

            // 2. Crea record PdfReport
            var pdfReport = new PdfReport
            {
                ClientVehicleId = vehicleId,
                ClientCompanyId = vehicle.ClientCompanyId,
                ReportPeriodStart = startTime,
                ReportPeriodEnd = endTime,
                GeneratedAt = DateTime.UtcNow,
                Notes = $"{notes} - {vehicleData.Count} records processed"
            };

            _db.PdfReports.Add(pdfReport);
            await _db.SaveChangesAsync();

            await _logger.Info(source, "PdfReport record created.",
                $"ReportId: {pdfReport.Id}, VehicleId: {vehicleId}, VIN: {vehicle.Vin}");

            // 3. Genera HTML usando HtmlReportService
            var htmlService = new HtmlReportService(_db);
            var htmlOptions = new HtmlReportOptions
            {
                ShowDetailedStats = true,
                ShowRawData = false, // Per performance
                ReportType = "Vehicle Analysis",
                AdditionalCss = GetCustomReportStyles()
            };

            var htmlContent = await htmlService.GenerateHtmlReportAsync(pdfReport, aiInsights, htmlOptions);

            // 4. Converti HTML in PDF usando PdfGenerationService
            var pdfService = new PdfGenerationService(_db);
            var pdfOptions = new PdfConversionOptions
            {
                PageFormat = "A4",
                MarginTop = "2cm",
                MarginBottom = "2cm",
                MarginLeft = "1.5cm",
                MarginRight = "1.5cm",
                DisplayHeaderFooter = true,
                HeaderTemplate = $@"
                    <div style='font-size: 10px; width: 100%; text-align: center; color: #666; border-bottom: 1px solid #ccc; padding-bottom: 5px;'>
                        <span>PolarDrive Report - {vehicle.Vin} - {DateTime.UtcNow:yyyy-MM-dd}</span>
                    </div>",
                FooterTemplate = @"
                    <div style='font-size: 10px; width: 100%; text-align: center; color: #666; border-top: 1px solid #ccc; padding-top: 5px;'>
                        <span>Pagina <span class='pageNumber'></span> di <span class='totalPages'></span></span>
                    </div>"
            };

            var pdfBytes = await pdfService.ConvertHtmlToPdfAsync(htmlContent, pdfReport, pdfOptions);

            await _logger.Info(source, "Report completato con successo",
                $"ReportId: {pdfReport.Id}, PDF size: {pdfBytes.Length} bytes");

            return pdfReport;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Errore generazione report veicolo {vehicleId}", ex.ToString());
            return null;
        }
    }

    /// <summary>
    /// Genera report mensile per produzione
    /// </summary>
    public async Task RunMonthlyAsync()
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

        await RunForPeriodAsync(startOfMonth, endOfMonth, "Monthly-Production");
    }

    /// <summary>
    /// Genera report settimanale
    /// </summary>
    public async Task RunWeeklyAsync()
    {
        var now = DateTime.UtcNow;
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
        var endOfWeek = startOfWeek.AddDays(6);

        await RunForPeriodAsync(startOfWeek, endOfWeek, "Weekly");
    }

    /// <summary>
    /// Genera report giornaliero
    /// </summary>
    public async Task RunDailyAsync()
    {
        var now = DateTime.UtcNow;
        var startOfDay = now.Date;
        var endOfDay = startOfDay.AddDays(1).AddMilliseconds(-1);

        await RunForPeriodAsync(startOfDay, endOfDay, "Daily");
    }

    /// <summary>
    /// Stili CSS personalizzati per i report
    /// </summary>
    private string GetCustomReportStyles()
    {
        return @"
        /* Stili aggiuntivi per report PolarDrive */
        .ai-insights {
            background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
            border-radius: 8px;
            padding: 20px;
            margin: 20px 0;
        }

        .ai-insights h2 {
            color: #2c3e50;
            margin-top: 0;
        }

        .insights-content h3 {
            color: #34495e;
            border-left: 4px solid #3498db;
            padding-left: 10px;
            margin-top: 25px;
        }

        .insights-content h4 {
            color: #7f8c8d;
            font-size: 14px;
            margin-top: 15px;
        }

        .insights-content ul {
            list-style-type: none;
            padding-left: 0;
        }

        .insights-content li {
            padding: 5px 0;
            padding-left: 20px;
            position: relative;
        }

        .insights-content li:before {
            content: '→';
            position: absolute;
            left: 0;
            color: #3498db;
            font-weight: bold;
        }

        .stats-content table {
            background-color: white;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }

        .stats-content th {
            background-color: #004E92;
            color: white;
        }

        .report-info {
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }

        /* Responsive per stampa */
        @media print {
            .ai-insights {
                background: #f8f9fa !important;
                border: 1px solid #dee2e6;
            }
            
            .insights-content h3 {
                border-left: 4px solid #000;
            }
            
            .stats-content th {
                background-color: #004E92 !important;
                color: white !important;
            }
        }";
    }

    /// <summary>
    /// Ottiene statistiche sui report generati
    /// </summary>
    public async Task<Dictionary<string, object>> GetReportStatisticsAsync()
    {
        var stats = new Dictionary<string, object>();

        try
        {
            var totalReports = await _db.PdfReports.CountAsync();
            var reportsLast24h = await _db.PdfReports
                .Where(r => r.GeneratedAt >= DateTime.UtcNow.AddDays(-1))
                .CountAsync();

            var reportsByVehicle = await _db.PdfReports
                .Include(r => r.ClientVehicle)
                .GroupBy(r => r.ClientVehicle.Vin)
                .Select(g => new { VIN = g.Key, Count = g.Count() })
                .ToListAsync();

            var averageReportSize = await _db.VehiclesData
                .GroupBy(vd => 1)
                .Select(g => g.Average(vd => vd.RawJson.Length))
                .FirstOrDefaultAsync();

            stats["totalReports"] = totalReports;
            stats["reportsLast24Hours"] = reportsLast24h;
            stats["reportsByVehicle"] = reportsByVehicle;
            stats["averageDataSize"] = averageReportSize;
            stats["lastUpdate"] = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            await _logger.Error("ReportGeneratorJob.GetStatistics", "Errore calcolo statistiche", ex.ToString());
            stats["error"] = ex.Message;
        }

        return stats;
    }
}