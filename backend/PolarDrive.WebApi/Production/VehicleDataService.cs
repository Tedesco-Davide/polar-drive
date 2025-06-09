using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace PolarDrive.WebApi.Production;

/// <summary>
/// Servizio centrale per gestire il fetch dati di tutti i brand
/// </summary>
public class VehicleDataService(IServiceProvider serviceProvider, ILogger<VehicleDataService> logger)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<VehicleDataService> _logger = logger;

    /// <summary>
    /// Fetch dati per tutti i brand attivi
    /// </summary>
    public async Task FetchDataForAllBrandsAsync()
    {
        _logger.LogInformation("VehicleDataService: Starting data fetch for all brands");

        try
        {
            // Fetch in parallelo per tutti i brand
            var tasks = new List<Task>
            {
                FetchTeslaDataAsync(),
                FetchPolestarDataAsync(),
                FetchPorscheDataAsync()
                // Aggiungi altri brand qui quando necessario
            };

            await Task.WhenAll(tasks);

            _logger.LogInformation("VehicleDataService: Completed data fetch for all brands");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VehicleDataService: Error during multi-brand data fetch");
            throw;
        }
    }

    /// <summary>
    /// Fetch dati Tesla
    /// </summary>
    private async Task FetchTeslaDataAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var teslaApiService = scope.ServiceProvider.GetRequiredService<TeslaApiService>();

            _logger.LogInformation("VehicleDataService: Fetching Tesla data");
            await teslaApiService.FetchDataForAllActiveVehiclesAsync();
            _logger.LogInformation("VehicleDataService: Tesla data fetch completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VehicleDataService: Error fetching Tesla data");
            // Non rilanciare l'eccezione per non bloccare gli altri brand
        }
    }

    /// <summary>
    /// Fetch dati Polestar
    /// </summary>
    private async Task FetchPolestarDataAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();

            // TODO: Implementare quando PolestarApiService sarà disponibile
            // var polestarApiService = scope.ServiceProvider.GetRequiredService<PolestarApiService>();
            // await polestarApiService.FetchDataForAllActiveVehiclesAsync();

            _logger.LogInformation("VehicleDataService: Polestar service not yet implemented - skipping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VehicleDataService: Error fetching Polestar data");
        }
    }

    /// <summary>
    /// Fetch dati Porsche
    /// </summary>
    private async Task FetchPorscheDataAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();

            // TODO: Implementare quando PorscheApiService sarà disponibile
            // var porscheApiService = scope.ServiceProvider.GetRequiredService<PorscheApiService>();
            // await porscheApiService.FetchDataForAllActiveVehiclesAsync();

            _logger.LogInformation("VehicleDataService: Porsche service not yet implemented - skipping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VehicleDataService: Error fetching Porsche data");
        }
    }

    /// <summary>
    /// Ottieni veicoli attivi per brand specifico
    /// </summary>
    private async Task<List<ClientVehicle>> GetActiveVehiclesByBrandAsync(string brand)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

        return await db.ClientVehicles
            .Where(v => v.Brand.Equals(brand, StringComparison.CurrentCultureIgnoreCase) && v.IsActiveFlag)
            .ToListAsync();
    }

    /// <summary>
    /// Ottieni statistiche sui veicoli per brand
    /// </summary>
    public async Task<Dictionary<string, int>> GetVehicleCountByBrandAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

        return await db.ClientVehicles
            .Where(v => v.IsActiveFlag)
            .GroupBy(v => v.Brand)
            .Select(g => new { Brand = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Brand, x => x.Count);
    }
}