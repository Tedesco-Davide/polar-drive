using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.Data.Constants;
using PolarDrive.WebApi.Services;

namespace PolarDrive.Services;

public class OutageDetectionService(
    PolarDriveDbContext db,
    IHttpClientFactory httpClientFactory,
    IWebHostEnvironment env) : IOutageDetectionService
{
    private readonly PolarDriveDbContext _db = db;
    private readonly PolarDriveLogger _logger = new PolarDriveLogger(db);
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IWebHostEnvironment _env = env;

    // Configurazioni per timeout e soglie
    private readonly TimeSpan _vehicleInactivityThreshold = TimeSpan.FromMinutes(55);
    private readonly TimeSpan _fleetApiTimeout = TimeSpan.FromSeconds(60);
    private readonly int _maxRetries = 3;

    public async Task CheckFleetApiOutagesAsync()
    {
        await _logger.Info("OutageDetectionService", "Starting Fleet API outage detection");

        foreach (var brand in VehicleConstants.ValidBrands)
        {
            try
            {
                var isApiDown = await IsFleetApiDownAsync(brand);

                if (isApiDown)
                {
                    await HandleFleetApiOutageAsync(brand);
                }
                else
                {
                    await ResolveFleetApiOutageAsync(brand);
                }
            }
            catch (Exception ex)
            {
                await _logger.Error("OutageDetectionService",
                    $"Error checking Fleet API for brand {brand}", ex.ToString());
            }
        }
    }

    public async Task CheckVehicleOutagesAsync()
    {
        await _logger.Info("OutageDetectionService", "Starting vehicle outage detection");

        var outageVehicles = await _db.ClientVehicles
            .Where(v => !v.IsActiveFlag && v.IsFetchingDataFlag)
            .Include(v => v.ClientCompany)
            .ToListAsync();

        foreach (var vehicle in outageVehicles)
        {
            try
            {
                var isVehicleDown = await IsVehicleDownAsync(vehicle);

                if (isVehicleDown)
                {
                    await HandleVehicleOutageAsync(vehicle);
                }
                else
                {
                    await ResolveVehicleOutageAsync(vehicle);
                }
            }
            catch (Exception ex)
            {
                await _logger.Error("OutageDetectionService",
                    $"Error checking vehicle {vehicle.Vin}", ex.ToString());
            }
        }
    }

    public async Task ResolveOutagesAsync()
    {
        await _logger.Info("OutageDetectionService", "Starting automatic outage resolution");

        var ongoingOutages = await _db.OutagePeriods
            .Where(o => o.OutageEnd == null)
            .Include(o => o.ClientVehicle)
            .ToListAsync();

        foreach (var outage in ongoingOutages)
        {
            try
            {
                bool shouldResolve = false;

                if (outage.OutageType == "Outage Fleet Api")
                {
                    // ✅ Fix: controlla se l'API è tornata online
                    shouldResolve = !await IsFleetApiDownAsync(outage.OutageBrand);
                }
                else if (outage.OutageType == "Outage Vehicle" && outage.ClientVehicle != null)
                {
                    shouldResolve = !await IsVehicleDownAsync(outage.ClientVehicle);
                }

                if (shouldResolve)
                {
                    outage.OutageEnd = DateTime.Now;
                    await _db.SaveChangesAsync();

                    await _logger.Info("OutageDetectionService",
                        $"Auto-resolved outage {outage.Id} ({outage.OutageType} - {outage.OutageBrand})");
                }
            }
            catch (Exception ex)
            {
                await _logger.Error("OutageDetectionService",
                    $"Error resolving outage {outage.Id}", ex.ToString());
            }
        }
    }

    #region Private Methods - Fleet API Detection

    private async Task<bool> IsFleetApiDownAsync(string brand)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = _fleetApiTimeout;

        for (int retry = 0; retry < _maxRetries; retry++)
        {
            try
            {
                var endpoint = GetFleetApiEndpoint(brand);
                await _logger.Info("OutageDetectionService", $"Testing endpoint: {endpoint}");

                var response = await httpClient.GetAsync(endpoint);

                if (response.IsSuccessStatusCode)
                {
                    await _logger.Info("OutageDetectionService", $"Fleet API {brand} is UP (status: {response.StatusCode})");
                    return false; // API funziona
                }

                await _logger.Warning("OutageDetectionService",
                    $"Fleet API {brand} returned {response.StatusCode} on attempt {retry + 1}");
            }
            catch (TaskCanceledException)
            {
                await _logger.Warning("OutageDetectionService",
                    $"Fleet API {brand} timeout on attempt {retry + 1}");
            }
            catch (HttpRequestException ex)
            {
                await _logger.Warning("OutageDetectionService",
                    $"Fleet API {brand} connection error on attempt {retry + 1}: {ex.Message}");
            }
            catch (Exception ex)
            {
                await _logger.Warning("OutageDetectionService",
                    $"Fleet API {brand} error on attempt {retry + 1}: {ex.Message}");
            }

            if (retry < _maxRetries - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry))); // Exponential backoff
            }
        }

        await _logger.Error("OutageDetectionService", $"Fleet API {brand} is DOWN after {_maxRetries} attempts");
        return true; // Considerato down dopo tutti i tentativi
    }

    private string GetFleetApiEndpoint(string brand)
    {
        return brand.ToLower() switch
        {
            "tesla" => _env.IsDevelopment()
                ? "http://localhost:5071/api/tesla/health" // ✅ Verifica che questo endpoint esista nel tuo mock
                : "https://fleet-api.tesla.com/api/1/health",

            _ => throw new ArgumentException($"Unknown brand: {brand}")
        };
    }

    private async Task HandleFleetApiOutageAsync(string brand)
    {
        // Controlla se esiste già un outage ongoing per questo brand
        var existingOutage = await _db.OutagePeriods
            .FirstOrDefaultAsync(o =>
                o.OutageType == "Outage Fleet Api" &&
                o.OutageBrand == brand &&
                o.OutageEnd == null);

        if (existingOutage == null)
        {
            // Crea nuovo outage
            var newOutage = new OutagePeriod
            {
                AutoDetected = true,
                OutageType = "Outage Fleet Api",
                OutageBrand = brand,
                CreatedAt = DateTime.Now,
                OutageStart = DateTime.Now,
                OutageEnd = null,
                VehicleId = null,
                ClientCompanyId = null,
                Notes = $"Auto-detected Fleet API outage for {brand}"
            };

            _db.OutagePeriods.Add(newOutage);
            await _db.SaveChangesAsync();

            await _logger.Info("OutageDetectionService",
                $"Detected new Fleet API outage for {brand}");
        }
        else
        {
            await _logger.Info("OutageDetectionService",
                $"Fleet API outage for {brand} already exists (ID: {existingOutage.Id})");
        }
    }

    private async Task ResolveFleetApiOutageAsync(string brand)
    {
        var ongoingOutage = await _db.OutagePeriods
            .FirstOrDefaultAsync(o =>
                o.OutageType == "Outage Fleet Api" &&
                o.OutageBrand == brand &&
                o.OutageEnd == null);

        if (ongoingOutage != null)
        {
            ongoingOutage.OutageEnd = DateTime.Now;
            await _db.SaveChangesAsync();

            await _logger.Info("OutageDetectionService",
                $"Resolved Fleet API outage for {brand} (ID: {ongoingOutage.Id})");
        }
    }

    #endregion

    #region Private Methods - Vehicle Detection

    private async Task<bool> IsVehicleDownAsync(ClientVehicle vehicle)
    {
        if (!vehicle.IsActiveFlag)
        {
            return true;
        }

        // Controlla l'ultima volta che abbiamo ricevuto dati
        var lastDataReceived = await _db.VehiclesData
            .Where(vd => vd.VehicleId == vehicle.Id)
            .OrderByDescending(vd => vd.Timestamp)
            .FirstOrDefaultAsync();

        if (lastDataReceived == null)
        {
            return true; // Nessun dato mai ricevuto
        }

        var timeSinceLastData = DateTime.Now - lastDataReceived.Timestamp;
        if (timeSinceLastData > _vehicleInactivityThreshold)
        {
            return true; // Troppo tempo senza dati
        }

        return false; // ✅ Semplificato: se ha dati recenti, è considerato UP
    }

    private async Task HandleVehicleOutageAsync(ClientVehicle vehicle)
    {
        var existingOutage = await _db.OutagePeriods
            .FirstOrDefaultAsync(o =>
                o.OutageType == "Outage Vehicle" &&
                o.VehicleId == vehicle.Id &&
                o.OutageEnd == null);

        if (existingOutage == null)
        {
            var newOutage = new OutagePeriod
            {
                AutoDetected = true,
                OutageType = "Outage Vehicle",
                OutageBrand = vehicle.Brand,
                CreatedAt = DateTime.Now,
                OutageStart = DateTime.Now,
                OutageEnd = null,
                VehicleId = vehicle.Id,
                ClientCompanyId = vehicle.ClientCompanyId,
                Notes = "Auto-detected: vehicle not responding for over 8 hours"
            };

            _db.OutagePeriods.Add(newOutage);
            await _db.SaveChangesAsync();

            await _logger.Info("OutageDetectionService",
                $"Detected new vehicle outage for VIN {vehicle.Vin}");
        }
    }

    private async Task ResolveVehicleOutageAsync(ClientVehicle vehicle)
    {
        var ongoingOutage = await _db.OutagePeriods
            .FirstOrDefaultAsync(o =>
                o.OutageType == "Outage Vehicle" &&
                o.VehicleId == vehicle.Id &&
                o.OutageEnd == null);

        if (ongoingOutage != null)
        {
            ongoingOutage.OutageEnd = DateTime.Now;
            await _db.SaveChangesAsync();

            await _logger.Info("OutageDetectionService",
                $"Resolved vehicle outage for VIN {vehicle.Vin}");
        }
    }

    #endregion
}