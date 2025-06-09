using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.AiReports;
using PolarDrive.WebApi.Helpers;

namespace PolarDrive.WebApi.Jobs;

/// <summary>
/// Genera report per veicoli in base a un periodo specificato
/// Aggiornato per usare AiReportGenerator con fallback locale
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
    /// Test rapido - genera report per gli ultimi 5 minuti (per testing con dati ogni 1 minuto)
    /// </summary>
    public async Task RunTestAsync()
    {
        var now = DateTime.UtcNow;
        var periodStart = now.AddMinutes(-5); // Ultimi 5 minuti per catturare 4-5 records
        var periodEnd = now;

        await RunForPeriodAsync(periodStart, periodEnd, "Test");
    }

    /// <summary>
    /// Test ultra-rapido - genera report per gli ultimi 2 minuti (minimo per avere dati)
    /// </summary>
    public async Task RunQuickTestAsync()
    {
        var now = DateTime.UtcNow;
        var periodStart = now.AddMinutes(-2); // Ultimi 2 minuti per 1-2 records
        var periodEnd = now;

        await RunForPeriodAsync(periodStart, periodEnd, "QuickTest");
    }

    /// <summary>
    /// Genera report per un periodo personalizzato
    /// </summary>
    public async Task RunForPeriodAsync(DateTime periodStart, DateTime periodEnd, string reportType = "Custom")
    {
        const string source = "ReportGeneratorJob.RunForPeriod";

        await _logger.Info(source, $"Starting {reportType} report generation.",
            $"Period: {periodStart:yyyy-MM-dd HH:mm} to {periodEnd:yyyy-MM-dd HH:mm}");

        var vehicles = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .Where(v => v.ClientOAuthAuthorized) // Solo veicoli autorizzati
            .ToListAsync();

        await _logger.Debug(source, "Fetched authorized client vehicles.", $"Count: {vehicles.Count}");

        int reportsGenerated = 0;
        int reportsSkipped = 0;
        int reportsFailed = 0;

        foreach (var vehicle in vehicles)
        {
            try
            {
                await ProcessVehicleReport(vehicle, periodStart, periodEnd, reportType, source);
                reportsGenerated++;
            }
            catch (Exception ex)
            {
                await _logger.Error(source, $"Failed to process vehicle {vehicle.Vin}", ex.ToString());
                reportsFailed++;
            }
        }

        await _logger.Info(source, $"{reportType} report generation completed.",
            $"Generated: {reportsGenerated}, Skipped: {reportsSkipped}, Failed: {reportsFailed}, Total vehicles: {vehicles.Count}");
    }

    private async Task ProcessVehicleReport(ClientVehicle vehicle, DateTime periodStart, DateTime periodEnd, string reportType, string source)
    {
        if (vehicle.ClientCompany == null)
        {
            await _logger.Warning(source, "Skipping vehicle with missing company.", $"VehicleId: {vehicle.Id}");
            return;
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
            return;
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
                $"VehicleId: {vehicle.Id}, VIN: {vehicle.Vin}, Period: {periodStart:yyyy-MM-dd HH:mm} to {periodEnd:yyyy-MM-dd HH:mm}");

            // Crea comunque un report vuoto per documentare il periodo
            await CreateEmptyReport(vehicle, periodStart, periodEnd, reportType, source);
            return;
        }

        await _logger.Info(source, $"Processing {rawJsonList.Count} data records for vehicle {vehicle.Vin}");

        // Genera insights AI con il nuovo generatore
        var aiGenerator = new AiReportGenerator(_db);
        var insights = await aiGenerator.GenerateSummaryFromRawJson(rawJsonList);

        if (string.IsNullOrWhiteSpace(insights))
        {
            await _logger.Warning(source, "AI insights generation returned empty.",
                $"VehicleId: {vehicle.Id}, VIN: {vehicle.Vin}, DataCount: {rawJsonList.Count}");

            // Usa un report di fallback
            insights = GenerateFallbackReport(vehicle, rawJsonList, periodStart, periodEnd);
        }

        // Crea e salva il report
        await CreateAndSaveReport(vehicle, periodStart, periodEnd, reportType, insights, rawJsonList.Count, source);
    }

    private async Task CreateEmptyReport(ClientVehicle vehicle, DateTime periodStart, DateTime periodEnd, string reportType, string source)
    {
        var insights = $@"# REPORT VEICOLO TESLA - NESSUN DATO

## PERIODO ANALIZZATO
- **Veicolo**: {vehicle.Model} - {vehicle.Vin}
- **Periodo**: {periodStart:yyyy-MM-dd HH:mm} - {periodEnd:yyyy-MM-dd HH:mm}
- **Tipo Report**: {reportType}

## STATO DATI
Nessun dato è stato raccolto durante il periodo specificato. Questo può essere dovuto a:
- Veicolo spento o non connesso
- Problemi di connettività
- Servizio di raccolta dati non attivo

## RACCOMANDAZIONI
- Verificare la connettività del veicolo
- Controllare le impostazioni di raccolta dati
- Riprovare la generazione del report in un periodo con dati disponibili

*Report generato il {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC*";

        await CreateAndSaveReport(vehicle, periodStart, periodEnd, reportType, insights, 0, source);
    }

    private string GenerateFallbackReport(ClientVehicle vehicle, List<string> rawJsonList, DateTime periodStart, DateTime periodEnd)
    {
        return $@"# REPORT VEICOLO TESLA - MODALITÀ FALLBACK

## INFORMAZIONI GENERALI
- **Veicolo**: {vehicle.Model} - {vehicle.Vin}
- **Periodo**: {periodStart:yyyy-MM-dd HH:mm} - {periodEnd:yyyy-MM-dd HH:mm}
- **Campioni raccolti**: {rawJsonList.Count}

## RIEPILOGO DATI
Durante il periodo analizzato sono stati raccolti {rawJsonList.Count} campioni di dati dal veicolo.
I dati sono stati memorizzati correttamente nel sistema e sono disponibili per analisi future.

## ANALISI TECNICA
- **Frequenza campionamento**: {rawJsonList.Count} campioni nel periodo
- **Dimensione media dati**: {rawJsonList.Average(j => j.Length):F0} caratteri per campione
- **Stato raccolta**: Operativa

## RACCOMANDAZIONI
- Verificare la configurazione del servizio AI per analisi più dettagliate
- Controllare la connettività con il servizio Mistral AI
- I dati sono pronti per analisi future quando il servizio AI sarà disponibile

*Report generato in modalità fallback il {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC*";
    }

    private async Task CreateAndSaveReport(ClientVehicle vehicle, DateTime periodStart, DateTime periodEnd,
        string reportType, string insights, int dataCount, string source)
    {
        // Crea record del report
        var report = new PdfReport
        {
            ClientVehicleId = vehicle.Id,
            ClientCompanyId = vehicle.ClientCompanyId,
            ReportPeriodStart = periodStart,
            ReportPeriodEnd = periodEnd,
            GeneratedAt = DateTime.UtcNow,
            Notes = $"{reportType} report generated from {dataCount} data points"
        };

        _db.PdfReports.Add(report);
        await _db.SaveChangesAsync();

        await _logger.Info(source, "PdfReport record created.",
            $"ReportId: {report.Id}, VehicleId: {vehicle.Id}, VIN: {vehicle.Vin}");

        // Genera e salva il PDF
        try
        {
            await GenerateAndSavePdf(report, insights, source);
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error generating PDF for vehicle {vehicle.Vin}", ex.ToString());

            // Rimuovi il record se la generazione PDF fallisce
            _db.PdfReports.Remove(report);
            await _db.SaveChangesAsync();
            throw; // Rilancia l'eccezione per il conteggio dei fallimenti
        }
    }

    private async Task GenerateAndSavePdf(PdfReport report, string insights, string source)
    {
        PdfStorageHelper.EnsurePdfDirectoryExists(report);
        var path = PdfStorageHelper.GetReportPdfPath(report);

        if (File.Exists(path))
        {
            await _logger.Warning(source, "PDF file already exists on disk.",
                $"Skipping generation. Path: {path}");
            return;
        }

        try
        {
            var pdfGenerator = new PdfGenerationService(_db);
            var bytes = pdfGenerator.GeneratePolardriveReportPdf(report, insights);
            await File.WriteAllBytesAsync(path, bytes);

            await _logger.Info(source, "PDF file generated and saved.",
                $"Path: {path}, Size: {bytes.Length} bytes, VIN: {report.ClientVehicle?.Vin}");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "PDF generation failed, creating fallback text file", ex.Message);

            // Fallback: salva come file di testo se PDF fallisce
            var textPath = path.Replace(".pdf", ".txt");
            await File.WriteAllTextAsync(textPath, insights);

            await _logger.Info(source, "Fallback text file created", $"Path: {textPath}");
        }
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