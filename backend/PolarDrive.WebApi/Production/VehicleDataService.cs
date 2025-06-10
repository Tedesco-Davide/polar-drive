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
            var registry = _serviceProvider.GetRequiredService<VehicleApiServiceRegistry>();
            var services = registry.GetAllServices();

            var tasks = services.Select(service =>
                FetchDataForBrandAsync(service)).ToList();

            await Task.WhenAll(tasks);

            _logger.LogInformation("VehicleDataService: Completed data fetch for all brands");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VehicleDataService: Error during multi-brand data fetch");
            throw;
        }
    }


    private async Task FetchDataForBrandAsync(IVehicleDataService service)
    {
        try
        {
            _logger.LogInformation($"Fetching {service.BrandName} data");
            await service.FetchDataForAllActiveVehiclesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching {service.BrandName} data");
        }
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