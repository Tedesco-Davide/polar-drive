using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Jobs;

namespace PolarDrive.WebApi.Production;

/// <summary>
/// Scheduler per produzione - gestisce i task automatici multi-brand
/// </summary>
public class ProductionScheduler(IServiceProvider serviceProvider, ILogger<ProductionScheduler> logger, IWebHostEnvironment env) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ProductionScheduler> _logger = logger;
    private readonly IWebHostEnvironment _env = env;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_env.IsDevelopment())
        {
            _logger.LogInformation("ProductionScheduler: Skipping in Development mode");
            return;
        }

        _logger.LogInformation("ProductionScheduler: Starting multi-brand production schedulers");

        // Avvia i task paralleli
        var dataFetchTask = RunDataFetchScheduler(stoppingToken);
        var reportTask = RunReportScheduler(stoppingToken);

        await Task.WhenAll(dataFetchTask, reportTask);
    }

    /// <summary>
    /// Scheduler per il fetch dati multi-brand (ogni ora)
    /// </summary>
    private async Task RunDataFetchScheduler(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var vehicleDataService = scope.ServiceProvider.GetRequiredService<VehicleDataService>();

                _logger.LogInformation("ProductionScheduler: Starting hourly multi-brand data fetch");

                // Fetch dati per tutti i brand
                await vehicleDataService.FetchDataForAllBrandsAsync();

                // Log statistiche
                await LogVehicleStatistics(vehicleDataService);

                _logger.LogInformation("ProductionScheduler: Completed hourly multi-brand data fetch");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProductionScheduler: Error in multi-brand data fetch cycle");
            }

            // Aspetta 1 ora
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    /// <summary>
    /// Scheduler per i report mensili (controlla ogni giorno)
    /// </summary>
    private async Task RunReportScheduler(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Genera report solo il primo giorno del mese
                if (now.Day == 1)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();
                    var reportJob = new ReportGeneratorJob(db);

                    _logger.LogInformation("ProductionScheduler: Starting monthly report generation");
                    await reportJob.RunMonthlyAsync();
                    _logger.LogInformation("ProductionScheduler: Completed monthly report generation");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProductionScheduler: Error in report generation");
            }

            // Aspetta 24 ore
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    /// <summary>
    /// Log statistiche veicoli per brand
    /// </summary>
    private async Task LogVehicleStatistics(VehicleDataService vehicleDataService)
    {
        try
        {
            var stats = await vehicleDataService.GetVehicleCountByBrandAsync();

            _logger.LogInformation("ProductionScheduler: Vehicle statistics by brand:");
            foreach (var (brand, count) in stats)
            {
                _logger.LogInformation($"  - {brand}: {count} active vehicles");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ProductionScheduler: Failed to log vehicle statistics");
        }
    }
}