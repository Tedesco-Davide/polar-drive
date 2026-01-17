using PolarDrive.Data.Constants;
using PolarDrive.Data.DbContexts;

namespace PolarDrive.WebApi.Services;

/// <summary>
/// Background service che esegue periodicamente il controllo degli outage
/// </summary>
public class OutageDetectionBackgroundService(
    IServiceProvider serviceProvider,
    IWebHostEnvironment env,
    PolarDriveLogger logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IWebHostEnvironment _env = env;
    private readonly PolarDriveLogger _logger = logger;

    // Configurazione timing - DEV: 1 minuto, PROD: 5 minuti (da app-config.json)
    private TimeSpan CheckInterval => _env.IsDevelopment()
        ? TimeSpan.FromMinutes(AppConfig.DEV_OUTAGE_CHECK_INTERVAL_MINUTES)
        : TimeSpan.FromMinutes(AppConfig.PROD_OUTAGE_CHECK_INTERVAL_MINUTES);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = _logger.Info(
            "OutageDetectionBackgroundService.ExecuteAsync",
            "OutageDetectionBackgroundService started"
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var outageService = scope.ServiceProvider.GetRequiredService<IOutageDetectionService>();

                _ = _logger.Info(
                    "OutageDetectionBackgroundService.ExecuteAsync",
                    "Starting outage detection cycle"
                );

                // 1. Controlla Fleet API outages
                await outageService.CheckFleetApiOutagesAsync();

                // 2. Controlla Vehicle outages
                await outageService.CheckVehicleOutagesAsync();

                // 3. Risolvi outages automaticamente
                await outageService.ResolveOutagesAsync();

                _ = _logger.Info(
                    "OutageDetectionBackgroundService.ExecuteAsync",
                    "Outage detection cycle completed successfully"
                );
            }
            catch (Exception ex)
            {
                _ = _logger.Error(ex.ToString(), "Error during outage detection cycle");
            }

            // Aspetta prima del prossimo ciclo
            await Task.Delay(CheckInterval, stoppingToken);
        }

        _ = _logger.Info(
            "OutageDetectionBackgroundService.ExecuteAsync",
            "OutageDetectionBackgroundService stopped"
        );
    }
}