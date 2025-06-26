using PolarDrive.Services;

namespace PolarDrive.WebApi.Services;

/// <summary>
/// Servizio background che esegue periodicamente il controllo degli outages
/// </summary>
public class OutageBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutageBackgroundService> _logger;

    // Intervalli di controllo
    private readonly TimeSpan _fleetApiCheckInterval = TimeSpan.FromMinutes(5);  // Controlla API ogni 5 minuti
    private readonly TimeSpan _vehicleCheckInterval = TimeSpan.FromMinutes(15);  // Controlla veicoli ogni 15 minuti
    private readonly TimeSpan _resolutionCheckInterval = TimeSpan.FromMinutes(10); // Controlla risoluzioni ogni 10 minuti

    public OutageBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OutageBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutageBackgroundService started");

        // Avvia i task paralleli per i diversi tipi di controllo
        var tasks = new[]
        {
            FleetApiCheckLoop(stoppingToken),
            VehicleCheckLoop(stoppingToken),
            ResolutionCheckLoop(stoppingToken)
        };

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OutageBackgroundService stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OutageBackgroundService encountered an error");
            throw;
        }
    }

    /// <summary>
    /// Loop per il controllo delle Fleet API
    /// </summary>
    private async Task FleetApiCheckLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var outageDetectionService = scope.ServiceProvider.GetRequiredService<IOutageDetectionService>();

                await outageDetectionService.CheckFleetApiOutagesAsync();
                _logger.LogDebug("Fleet API outage check completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Fleet API outage check");
            }

            try
            {
                await Task.Delay(_fleetApiCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Loop per il controllo dei veicoli
    /// </summary>
    private async Task VehicleCheckLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var outageDetectionService = scope.ServiceProvider.GetRequiredService<IOutageDetectionService>();

                await outageDetectionService.CheckVehicleOutagesAsync();
                _logger.LogDebug("Vehicle outage check completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during vehicle outage check");
            }

            try
            {
                await Task.Delay(_vehicleCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Loop per la risoluzione automatica degli outages
    /// </summary>
    private async Task ResolutionCheckLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var outageDetectionService = scope.ServiceProvider.GetRequiredService<IOutageDetectionService>();

                await outageDetectionService.ResolveOutagesAsync();
                _logger.LogDebug("Outage resolution check completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during outage resolution check");
            }

            try
            {
                await Task.Delay(_resolutionCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutageBackgroundService is stopping");
        await base.StopAsync(stoppingToken);
    }
}