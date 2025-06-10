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
    /// Fetch dati per un veicolo specifico
    /// ✅ CAMBIA QUESTA RIGA:
    /// </summary>
    Task<VehicleFetchResult> FetchDataForVehicleAsync(string vehicleId);  // ← Era Task, ora Task<VehicleFetchResult>

    /// <summary>
    /// Verifica se il servizio è disponibile/configurato
    /// </summary>
    Task<bool> IsServiceAvailableAsync();

    /// <summary>
    /// Ottieni statistiche di utilizzo del servizio
    /// </summary>
    Task<ServiceUsageStats> GetUsageStatsAsync();
}

/// <summary>
/// Statistiche di utilizzo del servizio
/// </summary>
public class ServiceUsageStats
{
    public string BrandName { get; set; } = string.Empty;
    public int ActiveVehicles { get; set; }
    public DateTime LastFetch { get; set; }
    public bool IsHealthy { get; set; }
    public string? LastError { get; set; }
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
    /// ✅ CORRETTO: Ora restituisce IVehicleDataService
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
            }

            // Altri servizi quando saranno implementati
            // var polestarService = _serviceProvider.GetService<PolestarApiService>();
            // if (polestarService != null) services.Add(polestarService);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle API services");
        }

        return services;
    }
}

/// <summary>
/// Adapter per TeslaApiService per conformarsi all'interfaccia standard
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
    /// ✅ CAMBIA QUESTO METODO:
    /// </summary>
    public async Task<VehicleFetchResult> FetchDataForVehicleAsync(string vehicleId)
    {
        // Ora ritorna il risultato invece di ignorarlo
        return await _teslaService.FetchDataForVehicleAsync(vehicleId);
    }

    public async Task<bool> IsServiceAvailableAsync()
    {
        return await _teslaService.IsServiceAvailableAsync();
    }

    public async Task<ServiceUsageStats> GetUsageStatsAsync()
    {
        return await _teslaService.GetUsageStatsAsync();
    }
}