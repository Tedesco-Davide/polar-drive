using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.Data.Constants;
using PolarDrive.WebApi.Services;
using System.Text.Json;
using System.Net;

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

    // Environment-aware thresholds ragionevoli
    private TimeSpan _vehicleInactivityThreshold => _env.IsDevelopment()
        ? TimeSpan.FromMinutes(10)    // DEVELOPMENT => fetch ogni minuto | threshold 10 minuti
        : TimeSpan.FromHours(6);      // PRODUCTION  => fetch ogni ora | threshold 6 ore

    private TimeSpan _gracePeriod => _env.IsDevelopment() 
        ? TimeSpan.FromMinutes(5)     // DEVELOPMENT => grace di 5 minuti
        : TimeSpan.FromHours(2);      // PRODUCTION  => grace di 2 ore

    private readonly TimeSpan _fleetApiTimeout = TimeSpan.FromSeconds(60);

    private readonly int _maxRetries = 3;

    private string GetVehicleListEndpoint()
    {
        return _env.IsDevelopment()
            ? "http://localhost:5071/api/1/vehicles"
            : "https://fleet-api.tesla.com/api/1/vehicles";
    }

    private string GetVehicleDataEndpoint(string vehicleId)
    {
        return _env.IsDevelopment()
            ? $"http://localhost:5071/api/1/vehicles/{vehicleId}/vehicle_data"
            : $"https://fleet-api.tesla.com/api/1/vehicles/{vehicleId}/vehicle_data";
    }

    private async Task<string?> GetVehicleIdFromVin(string vin)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = _fleetApiTimeout;

        try
        {
            var endpoint = GetVehicleListEndpoint();
            var response = await httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                await _logger.Warning("OutageDetectionService",
                    $"Vehicle list API returned {response.StatusCode} for VIN {vin}");
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonContent);

            // Tesla Fleet API format
            if (doc.RootElement.TryGetProperty("response", out var responseArray))
            {
                foreach (var vehicle in responseArray.EnumerateArray())
                {
                    if (vehicle.TryGetProperty("vin", out var vinProperty) &&
                        vehicle.TryGetProperty("id", out var idProperty))
                    {
                        var vehicleVin = vinProperty.GetString();
                        if (string.Equals(vehicleVin, vin, StringComparison.OrdinalIgnoreCase))
                        {
                            var vehicleId = idProperty.GetInt64().ToString();
                            await _logger.Debug("OutageDetectionService",
                                $"Found vehicle ID {vehicleId} for VIN {vin}");
                            return vehicleId;
                        }
                    }
                }
            }

            await _logger.Warning("OutageDetectionService",
                $"VIN {vin} not found in vehicle list response");
            return null;
        }
        catch (JsonException ex)
        {
            await _logger.Error("OutageDetectionService",
                $"Failed to parse vehicle list JSON for VIN {vin}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            await _logger.Warning("OutageDetectionService",
                $"Failed to get vehicle ID for VIN {vin}: {ex.Message}");
            return null;
        }
    }

    private async Task<bool> IsVehicleInSleepMode(ClientVehicle vehicle)
    {
        // Prima ottieni il Vehicle ID dal VIN
        var vehicleId = await GetVehicleIdFromVin(vehicle.Vin);
        if (string.IsNullOrEmpty(vehicleId))
        {
            await _logger.Warning("OutageDetectionService",
                $"Could not find vehicle ID for VIN {vehicle.Vin} - assuming not in sleep mode");
            return false; // Non considerare sleep mode se non troviamo l'ID
        }
        
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30); // Timeout più breve per sleep check
        
        try 
        {
            var endpoint = GetVehicleDataEndpoint(vehicleId);
            var response = await httpClient.GetAsync(endpoint);
            
            if (response.StatusCode == HttpStatusCode.RequestTimeout ||
                response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                await _logger.Debug("OutageDetectionService",
                    $"Vehicle {vehicle.Vin} - Timeout/Unavailable, likely in sleep mode");
                return true; // Probabilmente in sleep mode
            }
            
            if (response.IsSuccessStatusCode)
            {
                // Se la chiamata riesce, il veicolo è sveglio
                await _logger.Debug("OutageDetectionService",
                    $"Vehicle {vehicle.Vin} - Vehicle is awake and responding");
                return false;
            }
            
            // Controlla il contenuto della risposta di errore
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            try
            {
                using var doc = JsonDocument.Parse(jsonContent);
                
                // Tesla API format
                if (doc.RootElement.TryGetProperty("error", out var errorProperty))
                {
                    var errorMsg = errorProperty.GetString()?.ToLowerInvariant() ?? "";
                    
                    if (errorMsg.Contains("vehicle unavailable") || 
                        errorMsg.Contains("asleep") ||
                        errorMsg.Contains("offline") ||
                        errorMsg.Contains("sleeping"))
                    {
                        await _logger.Debug("OutageDetectionService",
                            $"Vehicle {vehicle.Vin} - API says vehicle is sleeping: {errorMsg}");
                        return true;
                    }
                }
            }
            catch (JsonException)
            {
                // Se non riusciamo a parsare il JSON, controlliamo il testo grezzo
                if (jsonContent.ToLowerInvariant().Contains("vehicle unavailable") || 
                    jsonContent.ToLowerInvariant().Contains("asleep") ||
                    jsonContent.ToLowerInvariant().Contains("offline"))
                {
                    await _logger.Debug("OutageDetectionService",
                        $"Vehicle {vehicle.Vin} - Raw response indicates sleep mode");
                    return true;
                }
            }
            
            await _logger.Debug("OutageDetectionService",
                $"Vehicle {vehicle.Vin} - Unknown response, assuming awake");
            return false; // Se non riconosciamo il messaggio, assumiamo sia sveglio
        }
        catch (TaskCanceledException)
        {
            await _logger.Debug("OutageDetectionService",
                $"Vehicle {vehicle.Vin} - Sleep check timeout, assuming sleep mode");
            return true; // Timeout = probabilmente sleep
        }
        catch (Exception ex)
        {
            await _logger.Warning("OutageDetectionService",
                $"Vehicle {vehicle.Vin} - Sleep check failed: {ex.Message}");
            return true; // Errore = assume sleep mode per sicurezza
        }
    }

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
            .Where(v => v.IsFetchingDataFlag)
            .Include(v => v.ClientCompany)
            .ToListAsync();

        await _logger.Info("OutageDetectionService", $"Found {outageVehicles.Count} vehicles to check for outages");

        foreach (var vehicle in outageVehicles)
        {
            try
            {
                await _logger.Debug("OutageDetectionService", $"Checking vehicle {vehicle.Vin} (ID: {vehicle.Id})");

                var isVehicleDown = await IsVehicleDownAsync(vehicle);

                await _logger.Debug("OutageDetectionService", $"Vehicle {vehicle.Vin} is down: {isVehicleDown}");

                if (isVehicleDown)
                {
                    await HandleVehicleOutageAsync(vehicle);
                }
                else
                {
                    // FIX: Risolvi immediatamente se il veicolo è online
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
            .ThenInclude(v => v.ClientCompany) // Include anche la company
            .ToListAsync();

        await _logger.Info("OutageDetectionService", $"Found {ongoingOutages.Count} ongoing outages to check for resolution");

        foreach (var outage in ongoingOutages)
        {
            await _logger.Debug("OutageDetectionService", $"Checking outage {outage.Id} ({outage.OutageType} - {outage.OutageBrand})");

            try
            {
                bool shouldResolve = false;

                if (outage.OutageType == "Outage Fleet Api")
                {
                    // Controlla se l'API è tornata online
                    shouldResolve = !await IsFleetApiDownAsync(outage.OutageBrand);
                    
                    if (shouldResolve)
                    {
                        await _logger.Info("OutageDetectionService", $"Fleet API {outage.OutageBrand} is back online");
                    }
                }
                else if (outage.OutageType == "Outage Vehicle" && outage.ClientVehicle != null)
                {
                    // FIX: Rileggi sempre il veicolo con i dati più aggiornati
                    var freshVehicle = await _db.ClientVehicles
                        .Include(v => v.ClientCompany)
                        .FirstOrDefaultAsync(v => v.Id == outage.ClientVehicle.Id);

                    if (freshVehicle != null)
                    {
                        // FIX: Verifica più accurata dello stato del veicolo
                        shouldResolve = await IsVehicleBackOnlineAsync(freshVehicle);

                        await _logger.Debug("OutageDetectionService", 
                            $"Vehicle {freshVehicle.Vin} - Back online check result: {shouldResolve}");
                    }
                    else
                    {
                        await _logger.Warning("OutageDetectionService", 
                            $"Vehicle with ID {outage.ClientVehicle.Id} not found in database");
                    }
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
                ? "http://localhost:5071/api/tesla/health"
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
        // RILEGGI sempre lo stato più recente dal database
        var currentVehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Id == vehicle.Id);

        if (currentVehicle == null || !currentVehicle.IsActiveFlag)
        {
            await _logger.Debug("OutageDetectionService",
                $"Vehicle {vehicle.Vin} - Considered DOWN due to IsActive={currentVehicle?.IsActiveFlag}");
            return true;
        }

        // Verifica se il veicolo è in fetching - se sì, non considerarlo in outage
        if (!currentVehicle.IsFetchingDataFlag)
        {
            await _logger.Debug("OutageDetectionService",
                $"Vehicle {vehicle.Vin} - Not fetching data, considered DOWN");
            return true;
        }

        var lastDataReceived = await _db.VehiclesData
            .Where(vd => vd.VehicleId == vehicle.Id)
            .OrderByDescending(vd => vd.Timestamp)
            .FirstOrDefaultAsync();

        if (lastDataReceived == null)
        {
            // Se il veicolo è stato attivato di recente, non considerarlo DOWN
            if (currentVehicle.FirstActivationAt.HasValue)
            {
                var timeSinceActivation = DateTime.Now - currentVehicle.FirstActivationAt.Value;

                if (timeSinceActivation < _gracePeriod)
                {
                    await _logger.Debug("OutageDetectionService",
                        $"Vehicle {vehicle.Vin} - Grace period active ({timeSinceActivation.TotalMinutes:F1} min since activation)");
                    return false; // Non considerarlo DOWN
                }
                else
                {
                    await _logger.Warning("OutageDetectionService",
                        $"Vehicle {vehicle.Vin} - No data received after grace period ({timeSinceActivation.TotalMinutes:F1} min since activation)");
                }
            }
            else
            {
                await _logger.Warning("OutageDetectionService",
                    $"Vehicle {vehicle.Vin} - No activation date and no data ever received");
            }

            return true; // Nessun dato mai ricevuto dopo grace period
        }

        var timeSinceLastData = DateTime.Now - lastDataReceived.Timestamp;
        bool isDataTooOld = timeSinceLastData > _vehicleInactivityThreshold;

        if (isDataTooOld)
        {
            // Prima di dichiarare outage, controlla se è in sleep mode
            bool isInSleepMode = await IsVehicleInSleepMode(vehicle);
            
            if (isInSleepMode)
            {
                await _logger.Debug("OutageDetectionService",
                    $"Vehicle {vehicle.Vin} - Data old but vehicle in sleep mode, not considered DOWN");
                return false; // Non è un outage se è solo dormendo
            }
            else
            {
                await _logger.Warning("OutageDetectionService",
                    $"Vehicle {vehicle.Vin} - Data too old AND not sleeping: {timeSinceLastData.TotalMinutes:F1} min (threshold: {_vehicleInactivityThreshold.TotalMinutes} min)");
                return true; // Vero outage
            }
        }
        else
        {
            await _logger.Debug("OutageDetectionService",
                $"Vehicle {vehicle.Vin} - Data is fresh: {timeSinceLastData.TotalMinutes:F1} min ago");
            return false;
        }
    }

    // Nuovo metodo specifico per verificare se un veicolo è tornato online
    private async Task<bool> IsVehicleBackOnlineAsync(ClientVehicle vehicle)
    {
        // Controlla lo stato attuale del veicolo
        if (!vehicle.IsActiveFlag || !vehicle.IsFetchingDataFlag)
        {
            await _logger.Debug("OutageDetectionService",
                $"Vehicle {vehicle.Vin} - Still offline (Active: {vehicle.IsActiveFlag}, Fetching: {vehicle.IsFetchingDataFlag})");
            return false;
        }

        // Controlla se ci sono dati recenti - usa un periodo più breve del threshold per "back online"
        var recentDataWindow = _env.IsDevelopment() 
            ? TimeSpan.FromMinutes(5)    // Ultimi 5 minuti in development
            : TimeSpan.FromHours(2);     // Ultimi 2 ore in produzione (più recente del threshold di 6 ore)
            
        var recentData = await _db.VehiclesData
            .Where(vd => vd.VehicleId == vehicle.Id)
            .Where(vd => vd.Timestamp > DateTime.Now.Subtract(recentDataWindow))
            .OrderByDescending(vd => vd.Timestamp)
            .FirstOrDefaultAsync();

        if (recentData != null)
        {
            var dataAge = DateTime.Now - recentData.Timestamp;
            await _logger.Info("OutageDetectionService",
                $"Vehicle {vehicle.Vin} - Found recent data from {dataAge.TotalMinutes:F1} minutes ago - BACK ONLINE");
            return true;
        }

        await _logger.Debug("OutageDetectionService",
            $"Vehicle {vehicle.Vin} - No recent data found, still offline");
        return false;
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
                Notes = $"Auto-detected: vehicle not responding for over {_vehicleInactivityThreshold.TotalMinutes} minutes"
            };

            _db.OutagePeriods.Add(newOutage);
            await _db.SaveChangesAsync();

            await _logger.Info("OutageDetectionService",
                $"Detected new vehicle outage for VIN {vehicle.Vin}");
        }
        else
        {
            await _logger.Debug("OutageDetectionService",
                $"Vehicle outage for VIN {vehicle.Vin} already exists (ID: {existingOutage.Id})");
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
                $"Resolved vehicle outage for VIN {vehicle.Vin} (ID: {ongoingOutage.Id})");
        }
    }

    #endregion
}