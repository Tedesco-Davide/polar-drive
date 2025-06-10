using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Jobs;

namespace PolarDrive.WebApi.Fake;

/// <summary>
/// Scheduler FAKE per development - genera report molto frequentemente per testing
/// </summary>
public class FakeProductionScheduler(IServiceProvider serviceProvider, ILogger<FakeProductionScheduler> logger, IWebHostEnvironment env) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<FakeProductionScheduler> _logger = logger;
    private readonly IWebHostEnvironment _env = env;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // FUNZIONA SOLO in Development
        if (!_env.IsDevelopment())
        {
            _logger.LogInformation("FakeProductionScheduler: Only runs in Development mode");
            return;
        }

        _logger.LogInformation("🚀 FakeProductionScheduler: Starting DEVELOPMENT report scheduler");
        _logger.LogInformation("📋 Report generation frequency: Every 5 minutes for testing");

        // Avvia solo il report scheduler (non il data fetch, lo fa già TeslaMockApiService)
        await RunFakeReportScheduler(stoppingToken);
    }

    /// <summary>
    /// Scheduler FAKE per report - genera report ogni 5 minuti per testing rapido
    /// </summary>
    private async Task RunFakeReportScheduler(CancellationToken stoppingToken)
    {
        // Aspetta 2 minuti all'avvio per permettere accumulo dati
        _logger.LogInformation("⏳ Waiting 2 minutes for data accumulation before first report...");
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();
                var reportJob = new ReportGeneratorJob(db);

                _logger.LogInformation("📊 FakeProductionScheduler: Starting quick test report generation");

                // Genera report per gli ultimi 5 minuti (dovrebbe catturare 4-5 records)
                await reportJob.RunTestAsync();

                _logger.LogInformation("✅ FakeProductionScheduler: Completed quick test report generation");

                // Log statistiche sui report generati
                await LogReportStatistics(db);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ FakeProductionScheduler: Error in fake report generation");
            }

            // ⚡ FREQUENZA VELOCE: Genera report ogni 5 minuti
            _logger.LogInformation("⏰ Next report generation in 5 minutes...");
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    /// <summary>
    /// Log statistiche sui report per il development
    /// </summary>
    private async Task LogReportStatistics(PolarDriveDbContext db)
    {
        try
        {
            var totalReports = await db.PdfReports.CountAsync();
            var recentReports = await db.PdfReports
                .Where(r => r.GeneratedAt >= DateTime.UtcNow.AddHours(-1))
                .CountAsync();

            var totalVehicleData = await db.VehiclesData.CountAsync();
            var recentData = await db.VehiclesData
                .Where(d => d.Timestamp >= DateTime.UtcNow.AddMinutes(-10))
                .CountAsync();

            _logger.LogInformation("📈 FakeProductionScheduler Statistics:");
            _logger.LogInformation($"   Total Reports: {totalReports} (Last hour: {recentReports})");
            _logger.LogInformation($"   Total Vehicle Data: {totalVehicleData} (Last 10min: {recentData})");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ FakeProductionScheduler: Failed to log statistics");
        }
    }
}