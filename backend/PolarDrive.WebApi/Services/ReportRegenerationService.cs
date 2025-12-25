using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Services
{
    /// <summary>
    /// Background service che rigenera automaticamente i report in stato ERROR.
    /// Processa un report alla volta per evitare sovraccarichi e conflitti.
    /// </summary>
    public class ReportRegenerationService(
        IServiceProvider serviceProvider,
        PolarDriveLogger logger,
        IWebHostEnvironment env) : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly PolarDriveLogger _logger = logger;
        private readonly IWebHostEnvironment _env = env;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            const string source = "ReportRegenerationService.ExecuteAsync";

            await _logger.Info(source,
                $"🔄 Starting ReportRegenerationService in {(_env.IsDevelopment() ? "DEV" : "PROD")} mode");

            // Delay iniziale per permettere agli altri servizi di avviarsi
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessFailedReportsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    await _logger.Error(source,
                        "❌ Error in regeneration loop",
                        ex.ToString());
                }

                // Attesa tra un ciclo e l'altro
                var delay = _env.IsDevelopment()
                    ? TimeSpan.FromMinutes(DEV_RETRY_FAILED_PDF_REPEAT_MINUTES)
                    : TimeSpan.FromHours(PROD_RETRY_FAILED_PDF_REPEAT_HOURS);

                await _logger.Debug(source,
                    $"⏳ Next check in {delay.TotalMinutes} minutes");

                await Task.Delay(delay, stoppingToken);
            }
        }

        /// <summary>
        /// Trova e rigenera i report in stato ERROR, uno alla volta.
        /// </summary>
        private async Task ProcessFailedReportsAsync(CancellationToken stoppingToken)
        {
            const string source = "ReportRegenerationService.ProcessFailedReportsAsync";

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();
            var reportService = scope.ServiceProvider.GetRequiredService<IReportGenerationService>();

            // 🔍 Trova tutti i report in stato ERROR
            var failedReports = await db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .Where(r => r.Status == "ERROR")
                .OrderBy(r => r.CreatedAt) // Processa prima i più vecchi
                .ToListAsync(stoppingToken);

            if (!failedReports.Any())
            {
                await _logger.Debug(source, "✅ No failed reports to regenerate");
                return;
            }

            await _logger.Info(source,
                $"🔍 Found {failedReports.Count} reports in ERROR status to regenerate");

            int successCount = 0;
            int errorCount = 0;

            // 🔄 Processa UN REPORT ALLA VOLTA
            foreach (var report in failedReports)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    await _logger.Warning(source, "⚠️ Cancellation requested, stopping regeneration loop");
                    break;
                }

                // 🔒 Verifica che non ci siano già report in PROCESSING o REGENERATING
                var hasProcessing = await db.PdfReports
                    .AnyAsync(r => r.Status == "PROCESSING" || r.Status == "REGENERATING", stoppingToken);

                if (hasProcessing)
                {
                    await _logger.Warning(source,
                        "⏸️ Another report is being processed, skipping this cycle",
                        $"ReportId: {report.Id}");
                    break; // Esce dal loop, riproverà al prossimo ciclo
                }

                // 🔒 Verifica che il report non sia già stato rigenerato con successo (immutabile)
                if (!string.IsNullOrWhiteSpace(report.PdfHash) && report.PdfContent?.Length > 0)
                {
                    await _logger.Warning(source,
                        "⏭️ Report is immutable (already has PDF), skipping",
                        $"ReportId: {report.Id}, PdfHash: {report.PdfHash}");
                    continue;
                }

                await _logger.Info(source,
                    $"🔄 Starting regeneration for report",
                    $"ReportId: {report.Id}, Company: {report.ClientCompany?.Name ?? "N/A"}, VIN: {report.ClientVehicle?.Vin ?? "N/A"}");

                try
                {
                    // ✅ Usa la stessa logica di rigenerazione del controller
                    var success = await reportService.GenerateSingleReportAsync(
                        companyId: report.ClientCompanyId,
                        vehicleId: report.VehicleId,
                        periodStart: report.ReportPeriodStart,
                        periodEnd: report.ReportPeriodEnd,
                        isRegeneration: true,
                        existingReportId: report.Id
                    );

                    if (success)
                    {
                        successCount++;
                        await _logger.Info(source,
                            $"✅ Report regenerated successfully",
                            $"ReportId: {report.Id}");
                    }
                    else
                    {
                        errorCount++;
                        await _logger.Warning(source,
                            $"⚠️ Report regeneration returned false",
                            $"ReportId: {report.Id}");
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    await _logger.Error(source,
                        $"❌ Error regenerating report",
                        $"ReportId: {report.Id}, Error: {ex.Message}");
                }

                // ⏳ Pausa tra un report e l'altro per evitare sovraccarico
                if (!stoppingToken.IsCancellationRequested)
                {
                    var vehicleDelay = _env.IsDevelopment()
                        ? TimeSpan.FromSeconds(30)
                        : TimeSpan.FromMinutes(VEHICLE_DELAY_MINUTES);

                    await _logger.Debug(source,
                        $"⏳ Waiting {vehicleDelay.TotalSeconds}s before next report");

                    await Task.Delay(vehicleDelay, stoppingToken);
                }
            }

            // 📊 Log finale del ciclo
            if (successCount > 0 || errorCount > 0)
            {
                await _logger.Info(source,
                    $"📊 Regeneration cycle completed",
                    $"Success: {successCount}, Failed: {errorCount}, Total: {failedReports.Count}");
            }
        }
    }
}
