using PolarDrive.Data.Constants;
using PolarDrive.Data.DbContexts;

namespace PolarDrive.WebApi.Services;

/// <summary>
/// Background service che esegue periodicamente il monitoraggio proattivo dei gap.
/// Rileva anomalie e crea alert automatici quando le soglie configurate vengono superate.
/// </summary>
public class GapMonitoringBackgroundService(
    IServiceProvider serviceProvider,
    IWebHostEnvironment env,
    PolarDriveLogger logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IWebHostEnvironment _env = env;
    private readonly PolarDriveLogger _logger = logger;

    // Configurazione timing da app-config.json
    private TimeSpan CheckInterval => TimeSpan.FromMinutes(AppConfig.GAP_MONITORING_CHECK_INTERVAL_MINUTES);
    private TimeSpan InitialDelay => TimeSpan.FromMinutes(AppConfig.GAP_MONITORING_INITIAL_DELAY_MINUTES);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = _logger.Info(
            "GapMonitoringBackgroundService.ExecuteAsync",
            $"GapMonitoringBackgroundService started (Environment: {(_env.IsDevelopment() ? "DEV" : "PROD")})"
        );

        // Delay iniziale per permettere all'applicazione di avviarsi completamente
        _ = _logger.Info(
            "GapMonitoringBackgroundService.ExecuteAsync",
            $"Waiting {InitialDelay.TotalMinutes} minutes before first check"
        );

        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var monitoringService = scope.ServiceProvider.GetRequiredService<IGapMonitoringService>();

                _ = _logger.Info(
                    "GapMonitoringBackgroundService.ExecuteAsync",
                    "Starting gap monitoring cycle"
                );

                // Esegui monitoraggio su tutti i veicoli
                await monitoringService.CheckAllVehiclesAsync();

                // Log statistiche
                var stats = await monitoringService.GetAlertStatsAsync();
                _ = _logger.Info(
                    "GapMonitoringBackgroundService.ExecuteAsync",
                    "Gap monitoring cycle completed",
                    $"Total: {stats.TotalAlerts}, Open: {stats.OpenAlerts}, " +
                    $"Critical: {stats.CriticalAlerts}, Warning: {stats.WarningAlerts}"
                );
            }
            catch (Exception ex)
            {
                _ = _logger.Error(ex.ToString(), "Error during gap monitoring cycle");
            }

            // Aspetta prima del prossimo ciclo
            _ = _logger.Info(
                "GapMonitoringBackgroundService.ExecuteAsync",
                $"Next check in {CheckInterval.TotalMinutes} minutes"
            );

            await Task.Delay(CheckInterval, stoppingToken);
        }

        _ = _logger.Info(
            "GapMonitoringBackgroundService.ExecuteAsync",
            "GapMonitoringBackgroundService stopped"
        );
    }
}
