namespace PolarDrive.WebApi.Production;

/// <summary>
/// Interfaccia standard per tutti i servizi API dei veicoli
/// </summary>
public interface IVehicleDataService
{
    /// <summary>
    /// Nome del brand gestito dal servizio
    /// </summary>
    string BrandName { get; }

    /// <summary>
    /// Fetch dati per tutti i veicoli attivi del brand
    /// </summary>
    Task FetchDataForAllActiveVehiclesAsync();

    /// <summary>
    /// Fetch dati per un veicolo specifico tramite VIN
    /// </summary>
    Task<VehicleFetchResult> FetchDataForVehicleAsync(string vin);

    /// <summary>
    /// Verifica se il servizio è disponibile/configurato
    /// </summary>
    Task<bool> IsServiceAvailableAsync();

    /// <summary>
    /// Ottieni statistiche di utilizzo del servizio
    /// </summary>
    Task<IVehicleServiceUsageStats> GetUsageStatsAsync();

    /// <summary>
    /// Ottieni status dei token OAuth (se applicabile)
    /// </summary>
    Task<TokenStatus> GetTokenStatusAsync();

    /// <summary>
    /// Refresh token scaduti (se applicabile)
    /// </summary>
    Task<int> RefreshExpiredTokensAsync();
}

/// <summary>
/// Interfaccia generica per statistiche di utilizzo
/// </summary>
public interface IVehicleServiceUsageStats
{
    string BrandName { get; set; }
    int ActiveVehicles { get; set; }
    int FetchingVehicles { get; set; }
    DateTime LastFetch { get; set; }
    long TotalDataRecords { get; set; }
    long RecentDataRecords { get; set; }
    bool IsHealthy { get; set; }
    string? LastError { get; set; }
    TokenStatus TokenStatus { get; set; }
}

/// <summary>
/// Statistiche di utilizzo del servizio con più dettagli
/// </summary>
public class ServiceUsageStats : IVehicleServiceUsageStats
{
    public string BrandName { get; set; } = string.Empty;
    public int ActiveVehicles { get; set; }
    public int FetchingVehicles { get; set; }
    public DateTime LastFetch { get; set; }
    public long TotalDataRecords { get; set; }
    public long RecentDataRecords { get; set; }
    public bool IsHealthy { get; set; }
    public string? LastError { get; set; }
    public TokenStatus TokenStatus { get; set; } = new();
}

/// <summary>
/// Classe TokenStatus condivisa
/// </summary>
public class TokenStatus
{
    public int ValidTokens { get; set; }
    public int ExpiredTokens { get; set; }
    public DateTime? NextExpiration { get; set; }
}

/// <summary>
/// Registro dei servizi API disponibili
/// </summary>
public class VehicleApiServiceRegistry(IServiceProvider serviceProvider, ILogger<VehicleApiServiceRegistry> logger)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<VehicleApiServiceRegistry> _logger = logger;

    /// <summary>
    /// Ottieni tutti i servizi API registrati
    /// </summary>
    public IEnumerable<IVehicleDataService> GetAllServices()
    {
        var services = new List<IVehicleDataService>();

        try
        {
            // Tesla (sempre disponibile)
            var teslaService = _serviceProvider.GetService<TeslaApiService>();
            if (teslaService != null)
            {
                services.Add(new TeslaApiServiceAdapter(teslaService));
                _logger.LogDebug("Tesla API service registered successfully");
            }
            else
            {
                _logger.LogWarning("Tesla API service not available in service provider");
            }

            _logger.LogInformation("Vehicle API service registry initialized with {ServiceCount} services", services.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle API services");
        }

        return services;
    }

    /// <summary>
    /// Ottieni servizio per brand specifico
    /// </summary>
    public IVehicleDataService? GetServiceByBrand(string brandName)
    {
        try
        {
            return GetAllServices().FirstOrDefault(s =>
                s.BrandName.Equals(brandName, StringComparison.CurrentCultureIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving service for brand {BrandName}", brandName);
            return null;
        }
    }

    /// <summary>
    /// Verifica salute di tutti i servizi
    /// </summary>
    public async Task<Dictionary<string, bool>> CheckAllServicesHealthAsync()
    {
        var healthStatus = new Dictionary<string, bool>();

        foreach (var service in GetAllServices())
        {
            try
            {
                var isHealthy = await service.IsServiceAvailableAsync();
                healthStatus[service.BrandName] = isHealthy;
                _logger.LogDebug("Health check for {BrandName}: {Status}", service.BrandName, isHealthy ? "Healthy" : "Unhealthy");
            }
            catch (Exception ex)
            {
                healthStatus[service.BrandName] = false;
                _logger.LogError(ex, "Health check failed for {BrandName}", service.BrandName);
            }
        }

        return healthStatus;
    }

    /// <summary>
    /// Ottieni statistiche aggregate di tutti i servizi
    /// </summary>
    public async Task<VehicleApiRegistryStats> GetAggregatedStatsAsync()
    {
        var stats = new VehicleApiRegistryStats();
        var services = GetAllServices();

        foreach (var service in services)
        {
            try
            {
                var serviceStats = await service.GetUsageStatsAsync();
                stats.TotalActiveVehicles += serviceStats.ActiveVehicles;
                stats.TotalFetchingVehicles += serviceStats.FetchingVehicles;
                stats.TotalDataRecords += serviceStats.TotalDataRecords;
                stats.TotalRecentDataRecords += serviceStats.RecentDataRecords;

                if (serviceStats.LastFetch > stats.LastOverallFetch)
                {
                    stats.LastOverallFetch = serviceStats.LastFetch;
                }

                stats.ServiceHealthStatus[service.BrandName] = serviceStats.IsHealthy;
                stats.ServiceStats[service.BrandName] = serviceStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stats for {BrandName}", service.BrandName);
                stats.ServiceHealthStatus[service.BrandName] = false;
            }
        }

        stats.TotalServices = services.Count();
        stats.HealthyServices = stats.ServiceHealthStatus.Count(kvp => kvp.Value);

        return stats;
    }
}

/// <summary>
/// Adapter per TeslaApiService completamente allineato
/// </summary>
public class TeslaApiServiceAdapter(TeslaApiService teslaService) : IVehicleDataService
{
    private readonly TeslaApiService _teslaService = teslaService;

    public string BrandName => "Tesla";

    public async Task FetchDataForAllActiveVehiclesAsync()
    {
        await _teslaService.FetchDataForAllActiveVehiclesAsync();
    }

    /// <summary>
    /// Restituisce VehicleFetchResult
    /// </summary>
    public async Task<VehicleFetchResult> FetchDataForVehicleAsync(string vin)
    {
        return await _teslaService.FetchDataForVehicleAsync(vin);
    }

    public async Task<bool> IsServiceAvailableAsync()
    {
        return await _teslaService.IsServiceAvailableAsync();
    }

    /// <summary>
    /// Converte TeslaServiceUsageStats a interfaccia generica
    /// </summary>
    public async Task<IVehicleServiceUsageStats> GetUsageStatsAsync()
    {
        var teslaStats = await _teslaService.GetUsageStatsAsync();

        // Converte TeslaServiceUsageStats a ServiceUsageStats
        return new ServiceUsageStats
        {
            BrandName = teslaStats.BrandName,
            ActiveVehicles = teslaStats.ActiveVehicles,
            FetchingVehicles = teslaStats.FetchingVehicles,
            LastFetch = teslaStats.LastFetch,
            TotalDataRecords = teslaStats.TotalDataRecords,
            RecentDataRecords = teslaStats.RecentDataRecords,
            IsHealthy = teslaStats.IsHealthy,
            LastError = teslaStats.LastError,
            TokenStatus = teslaStats.TokenStatus
        };
    }

    /// <summary>
    /// Delega al servizio Tesla
    /// </summary>
    public async Task<TokenStatus> GetTokenStatusAsync()
    {
        return await _teslaService.GetTokenStatusAsync();
    }

    /// <summary>
    /// Delega al servizio Tesla
    /// </summary>
    public async Task<int> RefreshExpiredTokensAsync()
    {
        return await _teslaService.RefreshExpiredTokensAsync();
    }
}

/// <summary>
/// Statistiche aggregate del registry
/// </summary>
public class VehicleApiRegistryStats
{
    public int TotalServices { get; set; }
    public int HealthyServices { get; set; }
    public int TotalActiveVehicles { get; set; }
    public int TotalFetchingVehicles { get; set; }
    public long TotalDataRecords { get; set; }
    public long TotalRecentDataRecords { get; set; }
    public DateTime LastOverallFetch { get; set; } = DateTime.MinValue;
    public Dictionary<string, bool> ServiceHealthStatus { get; set; } = new();
    public Dictionary<string, IVehicleServiceUsageStats> ServiceStats { get; set; } = new();
}

/// <summary>
/// ENUM condiviso (se non già definito altrove)
/// </summary>
public enum VehicleFetchResult
{
    Success,    // Dati recuperati e salvati con successo
    Skipped,    // Operazione saltata (veicolo offline, token mancante, etc.)
    Error       // Errore durante l'operazione
}

public class TeslaVehiclesResponse
{
    public List<TeslaVehicleInfo> Response { get; set; } = [];
}

public class TeslaVehicleInfo
{
    public long Id { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}