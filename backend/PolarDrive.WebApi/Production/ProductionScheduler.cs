using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Jobs;

namespace PolarDrive.WebApi.Production;

/// <summary>
/// Scheduler per produzione - gestisce i task automatici
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

        _logger.LogInformation("ProductionScheduler: Starting production schedulers");

        // Avvia i task paralleli
        var dataFetchTask = RunDataFetchScheduler(stoppingToken);
        var reportTask = RunReportScheduler(stoppingToken);

        await Task.WhenAll(dataFetchTask, reportTask);
    }

    /// <summary>
    /// Scheduler per il fetch dati Tesla (ogni ora)
    /// </summary>
    private async Task RunDataFetchScheduler(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var teslaApiService = scope.ServiceProvider.GetRequiredService<TeslaApiService>();

                _logger.LogInformation("ProductionScheduler: Starting hourly Tesla data fetch");
                await teslaApiService.FetchDataForAllActiveVehiclesAsync();
                _logger.LogInformation("ProductionScheduler: Completed hourly Tesla data fetch");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProductionScheduler: Error in data fetch cycle");
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
}