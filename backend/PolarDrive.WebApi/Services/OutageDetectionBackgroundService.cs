using PolarDrive.Services;

namespace PolarDrive.WebApi.Services;

/// <summary>
/// Background service che esegue periodicamente il controllo degli outage
/// </summary>
public class OutageDetectionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutageDetectionBackgroundService> _logger;

    // Configurazione timing
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Controlla ogni minuto

    public OutageDetectionBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OutageDetectionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutageDetectionBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var outageService = scope.ServiceProvider.GetRequiredService<IOutageDetectionService>();

                _logger.LogInformation("Starting outage detection cycle");

                // 1. Controlla Fleet API outages
                await outageService.CheckFleetApiOutagesAsync();

                // 2. Controlla Vehicle outages
                await outageService.CheckVehicleOutagesAsync();

                // 3. Risolvi outages automaticamente
                await outageService.ResolveOutagesAsync();

                _logger.LogInformation("Outage detection cycle completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during outage detection cycle");
            }

            // Aspetta prima del prossimo ciclo
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("OutageDetectionBackgroundService stopped");
    }
}