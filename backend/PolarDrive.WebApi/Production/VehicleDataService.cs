using PolarDrive.Data.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace PolarDrive.WebApi.Production;

/// <summary>
/// Servizio centrale per gestire il fetch dati di tutti i brand
/// </summary>
public class VehicleDataService(IServiceProvider serviceProvider, PolarDriveLogger logger)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly PolarDriveLogger _logger = logger;

    /// <summary>
    /// Fetch dati per tutti i brand attivi con risultati dettagliati
    /// </summary>
    public async Task<VehicleDataFetchResult> FetchDataForAllBrandsAsync()
    {
        _ = _logger.Info(
            "VehicleDataService.FetchDataForAllBrandsAsync",
            "🚗 VehicleDataService: Starting data fetch for all brands"
        );

        var result = new VehicleDataFetchResult();
        var startTime = DateTime.Now;

        try
        {
            var registry = _serviceProvider.GetRequiredService<VehicleApiServiceRegistry>();
            var services = registry.GetAllServices().ToList();

            if (!services.Any())
            {
                _ = _logger.Warning(
                    "VehicleDataService.FetchDataForAllBrandsAsync",
                    "⚠️ VehicleDataService: No vehicle API services registered"
                );
                result.OverallSuccess = false;
                result.ErrorMessage = "No vehicle API services available";
                return result;
            }

            _ = _logger.Info("📊 VehicleDataService: Found {ServiceCount} vehicle API services", services.Count.ToString());

            // Esegui fetch con risultati dettagliati
            var brandTasks = services.Select(service =>
                FetchDataForBrandWithResultAsync(service)).ToList();

            var brandResults = await Task.WhenAll(brandTasks);

            // Aggrega risultati
            result.BrandResults = brandResults.ToDictionary(br => br.BrandName, br => br);
            result.TotalBrands = services.Count;
            result.SuccessfulBrands = brandResults.Count(br => br.Success);
            result.FailedBrands = brandResults.Count(br => !br.Success);
            result.TotalVehiclesProcessed = brandResults.Sum(br => br.VehiclesProcessed);
            result.TotalVehiclesSuccess = brandResults.Sum(br => br.VehiclesSuccess);
            result.TotalVehiclesError = brandResults.Sum(br => br.VehiclesError);
            result.TotalVehiclesSkipped = brandResults.Sum(br => br.VehiclesSkipped);
            result.Duration = DateTime.Now - startTime;
            result.OverallSuccess = result.FailedBrands == 0;

            _ = _logger.Info(
                "VehicleDataService.FetchDataForAllBrandsAsync",
                $"✅ VehicleDataService: Completed data fetch for all brands - Success: {result.SuccessfulBrands}/{result.TotalBrands}, Duration: {result.Duration.TotalMilliseconds}ms"
            );

            return result;
        }
        catch (Exception ex)
        {
            result.OverallSuccess = false;
            result.ErrorMessage = ex.Message;
            result.Duration = DateTime.Now - startTime;

            _ = _logger.Error(ex.ToString(), "❌ VehicleDataService: Error during multi-brand data fetch");
            return result;
        }
    }

    /// <summary>
    /// Fetch dati per un brand specifico
    /// </summary>
    public async Task<BrandFetchResult> FetchDataForBrandAsync(string brandName)
    {
        _ = _logger.Info("🚗 VehicleDataService: Starting data fetch for brand {BrandName}", brandName);

        try
        {
            var registry = _serviceProvider.GetRequiredService<VehicleApiServiceRegistry>();
            var service = registry.GetServiceByBrand(brandName);

            if (service == null)
            {
                _ = _logger.Warning("⚠️ VehicleDataService: No service found for brand {BrandName}", brandName);
                return new BrandFetchResult
                {
                    BrandName = brandName,
                    Success = false,
                    ErrorMessage = $"No service available for brand {brandName}"
                };
            }

            return await FetchDataForBrandWithResultAsync(service);
        }
        catch (Exception ex)
        {
            _ = _logger.Error(ex.ToString(), "❌ VehicleDataService: Error fetching data for brand {BrandName}", brandName);
            return new BrandFetchResult
            {
                BrandName = brandName,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Fetch dati per un singolo veicolo con risultato dettagliato
    /// </summary>
    public async Task<VehicleFetchResult> FetchDataForVehicleAsync(string vin)
    {
        _ = _logger.Info("🚗 VehicleDataService: Starting data fetch for vehicle {VIN}", vin);

        try
        {
            // Trova il brand del veicolo
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            var vehicle = await db.ClientVehicles
                .FirstOrDefaultAsync(v => v.Vin == vin);

            if (vehicle == null)
            {
                _ = _logger.Warning("⚠️ VehicleDataService: Vehicle {VIN} not found", vin);
                return VehicleFetchResult.Error;
            }

            // Ottieni il servizio per il brand
            var registry = _serviceProvider.GetRequiredService<VehicleApiServiceRegistry>();
            var service = registry.GetServiceByBrand(vehicle.Brand);

            if (service == null)
            {
                _ = _logger.Warning("⚠️ VehicleDataService: No service found for brand {Brand}", vehicle.Brand);
                return VehicleFetchResult.Error;
            }

            var result = await service.FetchDataForVehicleAsync(vin);

            _ = _logger.Info("✅ VehicleDataService: Vehicle {VIN} fetch result: {Result}", vin, result.ToString());
            return result;
        }
        catch (Exception ex)
        {
            _ = _logger.Error(ex.ToString(), "❌ VehicleDataService: Error fetching data for vehicle {VIN}", vin);
            return VehicleFetchResult.Error;
        }
    }

    /// <summary>
    /// Statistiche dettagliate per brand con più informazioni
    /// </summary>
    public async Task<VehicleStatsByBrand> GetDetailedVehicleStatsByBrandAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

        var stats = new VehicleStatsByBrand();

        // Statistiche base per brand
        var brandStats = await db.ClientVehicles
            .GroupBy(v => v.Brand)
            .Select(g => new BrandStats
            {
                BrandName = g.Key,
                TotalVehicles = g.Count(),
                ActiveVehicles = g.Count(v => v.IsActiveFlag),
                FetchingVehicles = g.Count(v => v.IsFetchingDataFlag),
                AuthorizedVehicles = g.Count(v => v.ClientOAuthAuthorized),
                LastDataUpdate = g.Where(v => v.LastDataUpdate.HasValue)
                                  .Max(v => (DateTime?)v.LastDataUpdate)
            })
            .ToListAsync();

        stats.BrandStats = brandStats;

        // Statistiche sui dati recenti
        var recentDataStats = await db.VehiclesData
            .Where(vd => vd.Timestamp >= DateTime.Now.AddHours(-24))
            .Join(db.ClientVehicles, vd => vd.VehicleId, cv => cv.Id, (vd, cv) => new { vd, cv.Brand })
            .GroupBy(x => x.Brand)
            .Select(g => new { Brand = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Brand, x => x.Count);

        stats.RecentDataRecordsByBrand = recentDataStats;

        // Statistiche sui report
        var reportStats = await db.PdfReports
            .Join(db.ClientVehicles, r => r.VehicleId, cv => cv.Id, (r, cv) => new { r, cv.Brand })
            .GroupBy(x => x.Brand)
            .Select(g => new { Brand = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Brand, x => x.Count);

        stats.ReportsByBrand = reportStats;

        // Totali
        stats.TotalVehicles = brandStats.Sum(bs => bs.TotalVehicles);
        stats.TotalActiveVehicles = brandStats.Sum(bs => bs.ActiveVehicles);
        stats.TotalFetchingVehicles = brandStats.Sum(bs => bs.FetchingVehicles);
        stats.LastOverallUpdate = brandStats.Where(bs => bs.LastDataUpdate.HasValue)
                                           .Max(bs => bs.LastDataUpdate) ?? DateTime.MinValue;

        return stats;
    }

    /// <summary>
    /// Verifica salute di tutti i servizi con dettagli
    /// </summary>
    public async Task<ServiceHealthReport> CheckAllServicesHealthAsync()
    {
        try
        {
            var registry = _serviceProvider.GetRequiredService<VehicleApiServiceRegistry>();
            var healthStatus = await registry.CheckAllServicesHealthAsync();
            var aggregatedStats = await registry.GetAggregatedStatsAsync();

            return new ServiceHealthReport
            {
                CheckTime = DateTime.Now,
                ServiceHealthStatus = healthStatus,
                TotalServices = aggregatedStats.TotalServices,
                HealthyServices = aggregatedStats.HealthyServices,
                UnhealthyServices = aggregatedStats.TotalServices - aggregatedStats.HealthyServices,
                AggregatedStats = aggregatedStats,
                OverallHealthy = aggregatedStats.HealthyServices == aggregatedStats.TotalServices
            };
        }
        catch (Exception ex)
        {
            _ = _logger.Error(ex.ToString(), "❌ VehicleDataService: Error checking services health");
            return new ServiceHealthReport
            {
                CheckTime = DateTime.Now,
                ServiceHealthStatus = [],
                OverallHealthy = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Fetch con risultati dettagliati
    /// </summary>
    private async Task<BrandFetchResult> FetchDataForBrandWithResultAsync(IVehicleDataService service)
    {
        var result = new BrandFetchResult
        {
            BrandName = service.BrandName,
            StartTime = DateTime.Now
        };

        try
        {
            _ = _logger.Info("🚗 Fetching {BrandName} data", service.BrandName);

            // Conta veicoli prima del fetch
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            result.VehiclesProcessed = await db.ClientVehicles
                .CountAsync(v => v.Brand.Equals(service.BrandName, StringComparison.CurrentCultureIgnoreCase) &&
                               v.IsActiveFlag && v.IsFetchingDataFlag);

            if (result.VehiclesProcessed == 0)
            {
                result.Success = true;
                result.SkippedReason = "No active vehicles to process";
                _ = _logger.Info("ℹ️ {BrandName}: No active vehicles to process", service.BrandName);
                return result;
            }

            // Esegui fetch
            await service.FetchDataForAllActiveVehiclesAsync();
            result.VehiclesSuccess = result.VehiclesProcessed;
            result.Success = true;

            _ = _logger.Info("✅ {BrandName}: Successfully processed {Count} vehicles",
                service.BrandName, result.VehiclesProcessed.ToString());
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.VehiclesError = result.VehiclesProcessed;
            result.VehiclesSuccess = 0;

            _ = _logger.Error(ex.ToString(), "❌ Error fetching {BrandName} data", service.BrandName);
        }
        finally
        {
            result.EndTime = DateTime.Now;
            result.Duration = result.EndTime - result.StartTime;
        }

        return result;
    }

    /// <summary>
    /// Mantieni per compatibilità
    /// </summary>
    public async Task<Dictionary<string, int>> GetVehicleCountByBrandAsync()
    {
        var detailedStats = await GetDetailedVehicleStatsByBrandAsync();
        return detailedStats.BrandStats.ToDictionary(bs => bs.BrandName, bs => bs.ActiveVehicles);
    }
}

/// <summary>
/// Risultato del fetch dati per tutti i brand
/// </summary>
public class VehicleDataFetchResult
{
    public bool OverallSuccess { get; set; }
    public int TotalBrands { get; set; }
    public int SuccessfulBrands { get; set; }
    public int FailedBrands { get; set; }
    public int TotalVehiclesProcessed { get; set; }
    public int TotalVehiclesSuccess { get; set; }
    public int TotalVehiclesError { get; set; }
    public int TotalVehiclesSkipped { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, BrandFetchResult> BrandResults { get; set; } = new();
}

/// <summary>
/// Risultato del fetch dati per un singolo brand
/// </summary>
public class BrandFetchResult
{
    public string BrandName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public int VehiclesProcessed { get; set; }
    public int VehiclesSuccess { get; set; }
    public int VehiclesError { get; set; }
    public int VehiclesSkipped { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SkippedReason { get; set; }
}

/// <summary>
/// Statistiche dettagliate per brand
/// </summary>
public class VehicleStatsByBrand
{
    public List<BrandStats> BrandStats { get; set; } = new();
    public Dictionary<string, int> RecentDataRecordsByBrand { get; set; } = new();
    public Dictionary<string, int> ReportsByBrand { get; set; } = new();
    public int TotalVehicles { get; set; }
    public int TotalActiveVehicles { get; set; }
    public int TotalFetchingVehicles { get; set; }
    public DateTime LastOverallUpdate { get; set; }
}

/// <summary>
/// Statistiche per un singolo brand
/// </summary>
public class BrandStats
{
    public string BrandName { get; set; } = string.Empty;
    public int TotalVehicles { get; set; }
    public int ActiveVehicles { get; set; }
    public int FetchingVehicles { get; set; }
    public int AuthorizedVehicles { get; set; }
    public DateTime? LastDataUpdate { get; set; }
}

/// <summary>
/// Report sulla salute dei servizi
/// </summary>
public class ServiceHealthReport
{
    public DateTime CheckTime { get; set; }
    public Dictionary<string, bool> ServiceHealthStatus { get; set; } = new();
    public int TotalServices { get; set; }
    public int HealthyServices { get; set; }
    public int UnhealthyServices { get; set; }
    public bool OverallHealthy { get; set; }
    public VehicleApiRegistryStats? AggregatedStats { get; set; }
    public string? ErrorMessage { get; set; }
}