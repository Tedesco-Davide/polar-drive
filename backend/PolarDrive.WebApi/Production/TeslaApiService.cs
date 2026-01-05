using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.Data.Constants;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Production;

/// <summary>
/// Service che chiama le API Tesla reali e salva i dati
/// Usato in PRODUZIONE (non in development/mock)
/// </summary>
public class TeslaApiService(PolarDriveDbContext db, IWebHostEnvironment env, HttpClient httpClient, IConfiguration cfg)
{
    private readonly PolarDriveDbContext _db = db;
    private readonly PolarDriveLogger _logger = new();
    private readonly IWebHostEnvironment _env = env;
    private readonly HttpClient _httpClient = httpClient;
    private readonly IConfiguration _cfg = cfg;

    /// <summary>
    /// Metodo principale - raccoglie dati per tutti i veicoli in fetching (include grace period)
    /// </summary>
    public async Task FetchDataForAllActiveVehiclesAsync()
    {
        const string source = "TeslaApiService.FetchDataForAllActiveVehicles";

        if (_env.IsDevelopment())
        {
            await _logger.Info(source, "Skipping Tesla API calls in Development mode - using mock service instead");
            return;
        }

        await _logger.Info(source, "Starting Tesla API data fetch for all vehicles");

        // ✅ CORREZIONE: Include veicoli in grace period (solo IsFetchingDataFlag)
        var fetchingVehicles = await _db.ClientVehicles
            .Where(v => v.ClientOAuthAuthorized && v.IsFetchingDataFlag && v.Brand.ToLower() == VehicleConstants.VehicleBrand.TESLA)  // ← Rimosso IsActiveFlag
            .Include(v => v.ClientCompany)
            .ToListAsync();

        // Separa per tipo contratto per logging
        var activeContractVehicles = fetchingVehicles.Count(v => v.IsActiveFlag);
        var gracePeriodVehicles = fetchingVehicles.Count(v => !v.IsActiveFlag);

        await _logger.Info(source,
            $"Found {fetchingVehicles.Count} Tesla vehicles for data fetching (Active: {activeContractVehicles}, Grace Period: {gracePeriodVehicles})");

        // Warning se ci sono veicoli in grace period
        if (gracePeriodVehicles > 0)
        {
            await _logger.Warning(source,
                $"Grace Period Alert: Fetching data for {gracePeriodVehicles} vehicles with terminated contracts - awaiting token revocation");
        }

        int successCount = 0;
        int errorCount = 0;
        int skippedCount = 0;

        foreach (var vehicle in fetchingVehicles)
        {
            try
            {
                // Log contract status per ogni veicolo in grace period
                if (!vehicle.IsActiveFlag)
                {
                    await _logger.Info(source,
                        $"Fetching data for {vehicle.Vin} in grace period (Company: {vehicle.ClientCompany?.Name}) - awaiting client token revocation");
                }

                var result = await FetchDataForSingleVehicleAsync(vehicle);
                switch (result)
                {
                    case VehicleFetchResult.Success:
                        successCount++;
                        break;
                    case VehicleFetchResult.Skipped:
                        skippedCount++;
                        break;
                    case VehicleFetchResult.Error:
                        errorCount++;
                        break;
                }

                // Pausa tra veicoli per evitare rate limiting
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                errorCount++;
                await _logger.Error(source, $"Unexpected error fetching data for vehicle {vehicle.Vin}", ex.ToString());
            }
        }

        await _logger.Info(source, "Completed Tesla API data fetch cycle",
            $"Success: {successCount}, Skipped: {skippedCount}, Errors: {errorCount} (Grace Period vehicles: {gracePeriodVehicles})");
    }

    /// <summary>
    /// Fetch dati per un singolo veicolo tramite VIN (supporta grace period)
    /// </summary>
    public async Task<VehicleFetchResult> FetchDataForVehicleAsync(string vin)
    {
        const string source = "TeslaApiService.FetchDataForVehicle";

        // ✅ CORREZIONE: Cerca veicolo solo per VIN e brand, non per IsActiveFlag
        var vehicle = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .FirstOrDefaultAsync(v => v.Vin == vin && v.Brand.Equals(VehicleConstants.VehicleBrand.TESLA, StringComparison.CurrentCultureIgnoreCase));

        if (vehicle == null)
        {
            await _logger.Warning(source, $"Vehicle with VIN {vin} not found");
            return VehicleFetchResult.Error;
        }

        // Controlla se deve fetchare dati
        if (!vehicle.IsFetchingDataFlag)
        {
            await _logger.Info(source, $"Vehicle {vin} is not fetching data, skipping");
            return VehicleFetchResult.Skipped;
        }

        // Log se è in grace period
        if (!vehicle.IsActiveFlag)
        {
            await _logger.Info(source,
                $"Fetching data for {vin} in grace period (Company: {vehicle.ClientCompany?.Name}) - awaiting client token revocation");
        }

        return await FetchDataForSingleVehicleAsync(vehicle);
    }

    /// <summary>
    /// ✅ MIGLIORATO: Verifica disponibilità servizio con timeout e retry
    /// </summary>
    public async Task<bool> IsServiceAvailableAsync()
    {
        const string source = "TeslaApiService.IsServiceAvailable";

        try
        {
            // Controlla se abbiamo almeno un token valido
            var hasValidTokens = await _db.ClientTokens
                .AnyAsync(t => t.AccessTokenExpiresAt > DateTime.Now);

            if (!hasValidTokens)
            {
                await _logger.Debug(source, "No valid Tesla tokens found");
                return false;
            }

            // Test di connettività con retry
            using var testClient = new HttpClient();
            testClient.Timeout = TimeSpan.FromSeconds(15);

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    var response = await testClient.GetAsync("https://owner-api.teslamotors.com/api/1/status");
                    var isAvailable = response.IsSuccessStatusCode;

                    await _logger.Debug(source, $"Tesla API availability check (attempt {attempt}): {isAvailable}");

                    if (isAvailable) return true;
                }
                catch (Exception ex) when (attempt == 1)
                {
                    await _logger.Debug(source, $"Tesla API availability check attempt {attempt} failed: {ex.Message}");
                    await Task.Delay(5000);
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            await _logger.Warning(source, "Tesla API availability check failed", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Statistiche di utilizzo con separazione grace period
    /// </summary>
    public async Task<ServiceUsageStats> GetUsageStatsAsync()
    {
        const string source = "TeslaApiService.GetUsageStats";

        try
        {
            // Statistiche separate per grace period
            var activeVehicles = await _db.ClientVehicles
                .CountAsync(v => v.Brand.Equals(VehicleConstants.VehicleBrand.TESLA, StringComparison.CurrentCultureIgnoreCase) &&
                               v.IsActiveFlag && v.IsFetchingDataFlag);

            var fetchingVehicles = await _db.ClientVehicles
                .CountAsync(v => v.Brand.Equals(VehicleConstants.VehicleBrand.TESLA, StringComparison.CurrentCultureIgnoreCase) &&
                               v.IsFetchingDataFlag);

            var gracePeriodVehicles = await _db.ClientVehicles
                .CountAsync(v => v.Brand.Equals(VehicleConstants.VehicleBrand.TESLA, StringComparison.CurrentCultureIgnoreCase) &&
                               !v.IsActiveFlag && v.IsFetchingDataFlag);

            var lastFetch = await _db.ClientVehicles
                .Where(v => v.Brand.Equals(VehicleConstants.VehicleBrand.TESLA, StringComparison.CurrentCultureIgnoreCase) &&
                           v.IsFetchingDataFlag && v.LastDataUpdate.HasValue)
                .MaxAsync(v => (DateTime?)v.LastDataUpdate) ?? DateTime.MinValue;

            var totalDataRecords = await _db.VehiclesData
                .CountAsync(vd => _db.ClientVehicles.Any(cv => cv.Id == vd.VehicleId &&
                    cv.Brand.Equals(VehicleConstants.VehicleBrand.TESLA, StringComparison.CurrentCultureIgnoreCase)));

            var recentDataRecords = await _db.VehiclesData
                .CountAsync(vd => vd.Timestamp >= DateTime.Now.AddHours(-24) &&
                                 _db.ClientVehicles.Any(cv => cv.Id == vd.VehicleId &&
                                    cv.Brand.Equals(VehicleConstants.VehicleBrand.TESLA, StringComparison.CurrentCultureIgnoreCase)));

            var isHealthy = await IsServiceAvailableAsync();

            // Ottieni info sui token
            var tokenInfo = await GetTokenStatusAsync();

            // Log grace period nelle statistiche
            if (gracePeriodVehicles > 0)
            {
                await _logger.Debug(source, $"Statistics include {gracePeriodVehicles} vehicles in grace period");
            }

            return new ServiceUsageStats
            {
                BrandName = "Tesla",
                ActiveVehicles = activeVehicles,
                FetchingVehicles = fetchingVehicles,
                LastFetch = lastFetch,
                TotalDataRecords = totalDataRecords,
                RecentDataRecords = recentDataRecords,
                IsHealthy = isHealthy,
                LastError = null, // TODO: Potresti salvare l'ultimo errore nel DB
                TokenStatus = tokenInfo
            };
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error getting Tesla usage stats", ex.ToString());

            return new ServiceUsageStats
            {
                BrandName = "Tesla",
                ActiveVehicles = 0,
                FetchingVehicles = 0,
                LastFetch = DateTime.MinValue,
                TotalDataRecords = 0,
                RecentDataRecords = 0,
                IsHealthy = false,
                LastError = ex.Message,
                TokenStatus = new TokenStatus { ValidTokens = 0, ExpiredTokens = 0 }
            };
        }
    }

    /// <summary>
    /// Ottieni status dei token OAuth
    /// </summary>
    public async Task<TokenStatus> GetTokenStatusAsync()
    {
        try
        {
            var now = DateTime.Now;

            var validTokens = await _db.ClientTokens
                .CountAsync(t => t.AccessTokenExpiresAt > now);

            var expiredTokens = await _db.ClientTokens
                .CountAsync(t => t.AccessTokenExpiresAt <= now);

            var nextExpiration = await _db.ClientTokens
                .Where(t => t.AccessTokenExpiresAt > now)
                .MinAsync(t => (DateTime?)t.AccessTokenExpiresAt);

            return new TokenStatus
            {
                ValidTokens = validTokens,
                ExpiredTokens = expiredTokens,
                NextExpiration = nextExpiration
            };
        }
        catch (Exception ex)
        {
            await _logger.Warning("TeslaApiService.GetTokenStatus", "Error getting token status", ex.Message);
            return new TokenStatus { ValidTokens = 0, ExpiredTokens = 0 };
        }
    }

    /// <summary>
    /// Metodo per refresh di tutti i token scaduti
    /// </summary>
    public async Task<int> RefreshExpiredTokensAsync()
    {
        const string source = "TeslaApiService.RefreshExpiredTokens";

        try
        {
            var expiredTokens = await _db.ClientTokens
                .Where(t => t.AccessTokenExpiresAt <= DateTime.Now.AddMinutes(20)) // Refresh 5 minuti prima della scadenza
                .ToListAsync();

            var refreshedCount = 0;

            foreach (var token in expiredTokens)
            {
                try
                {
                    var refreshed = await RefreshTokenAsync(token);
                    if (refreshed)
                    {
                        refreshedCount++;
                        await _logger.Debug(source, $"Successfully refreshed token for vehicle ID {token.VehicleId}");
                    }
                    else
                    {
                        await _logger.Warning(source, $"Failed to refresh token for vehicle ID {token.VehicleId}");
                    }
                }
                catch (Exception ex)
                {
                    await _logger.Error(source, $"Error refreshing token for vehicle ID {token.VehicleId}", ex.ToString());
                }

                // Pausa tra i refresh per evitare rate limiting
                await Task.Delay(5000);
            }

            await _logger.Info(source, $"Token refresh completed: {refreshedCount}/{expiredTokens.Count} tokens refreshed");
            return refreshedCount;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error in bulk token refresh", ex.ToString());
            return 0;
        }
    }

    /// <summary>
    /// Chiama Tesla API per un singolo veicolo (con logging grace period)
    /// </summary>
    private async Task<VehicleFetchResult> FetchDataForSingleVehicleAsync(ClientVehicle vehicle)
    {
        const string source = "TeslaApiService.FetchDataForSingleVehicle";

        // Log contract status all'inizio del fetch
        var contractStatus = GetContractStatus(vehicle);
        if (!vehicle.IsActiveFlag)
        {
            await _logger.Debug(source, $"Processing vehicle {vehicle.Vin} with status: {contractStatus}");
        }

        // Recupera il token OAuth per questo veicolo
        var token = await _db.ClientTokens.FirstOrDefaultAsync(t => t.VehicleId == vehicle.Id);
        if (token == null)
        {
            await _logger.Warning(source, $"No OAuth token found for vehicle {vehicle.Vin} ({contractStatus})");
            return VehicleFetchResult.Skipped;
        }

        // Controlla se il token è scaduto
        if (token.AccessTokenExpiresAt <= DateTime.Now)
        {
            await _logger.Info(source, $"Token expired for vehicle {vehicle.Vin} ({contractStatus}), attempting refresh");
            var refreshed = await RefreshTokenAsync(token);
            if (!refreshed)
            {
                await _logger.Error(source, $"Failed to refresh token for vehicle {vehicle.Vin} ({contractStatus})");
                await LogFetchFailureAsync(vehicle.Id, FetchFailureReason.TOKEN_REFRESH_FAILED, $"Token refresh failed for vehicle {vehicle.Vin}");
                return VehicleFetchResult.Error;
            }
        }

        try
        {
            // 1. Ottieni lista veicoli Tesla
            var vehicles = await GetTeslaVehiclesAsync(token.AccessToken);
            var teslaVehicle = vehicles.FirstOrDefault(v => v.Vin == vehicle.Vin);

            if (teslaVehicle == null)
            {
                await _logger.Warning(source, $"Vehicle {vehicle.Vin} ({contractStatus}) not found in Tesla account");
                return VehicleFetchResult.Skipped;
            }

            await _logger.Debug(source, $"Vehicle {vehicle.Vin} ({contractStatus}) current state: {teslaVehicle.State}");

            // 2. ✅ GESTIONE INTELLIGENTE DELLO STATO (invariata)
            switch (teslaVehicle.State.ToLower())
            {
                case "offline":
                    await _logger.Info(source, $"Vehicle {vehicle.Vin} ({contractStatus}) is offline, OUTAGE PERIOD detected");
                    await SaveStatusRecord(vehicle.Vin, "OFFLINE", "Vehicle is offline, certified OUTAGE PERIOD");
                    // Log per Validazione Probabilistica Gap - veicolo offline è un problema tecnico documentato
                    await LogFetchFailureAsync(vehicle.Id, FetchFailureReason.TESLA_VEHICLE_OFFLINE, "Vehicle is offline - no connectivity to Tesla servers");
                    return VehicleFetchResult.Success;

                case "asleep":
                    await _logger.Info(source, $"Vehicle {vehicle.Vin} ({contractStatus}) is asleep, attempting to wake up");
                    var wakeUpSuccess = await WakeUpVehicleAsync(teslaVehicle.Id, token.AccessToken);
                    if (!wakeUpSuccess)
                    {
                        await _logger.Warning(source, $"Failed to wake up vehicle {vehicle.Vin} ({contractStatus}), vehicle in sleep mode");
                        await SaveStatusRecord(vehicle.Vin, "ASLEEP", "Vehicle in sleep mode");
                        // Log per Validazione Probabilistica Gap - veicolo in sleep è un problema tecnico documentato
                        await LogFetchFailureAsync(vehicle.Id, FetchFailureReason.TESLA_VEHICLE_ASLEEP, "Vehicle in sleep mode and could not be woken up");
                        return VehicleFetchResult.Success;
                    }
                    // Aspetta più tempo per veicoli che erano dormienti
                    await Task.Delay(20000);
                    break;

                case "online":
                    await _logger.Debug(source, $"Vehicle {vehicle.Vin} ({contractStatus}) is already online, proceeding with data fetch");
                    // Nessun wake-up necessario, procedi direttamente
                    break;

                default:
                    await _logger.Warning(source, $"Vehicle {vehicle.Vin} ({contractStatus}) has unknown state '{teslaVehicle.State}', attempting data fetch anyway");
                    break;
            }

            // 3. Ottieni dati completi del veicolo
            var vehicleData = await GetVehicleDataAsync(teslaVehicle.Id, token.AccessToken);

            // 4. Salva nel database
            await SaveVehicleDataAsync(vehicle.Vin, vehicleData);

            await _logger.Info(source, $"Successfully fetched and saved data for vehicle {vehicle.Vin} ({contractStatus})");
            return VehicleFetchResult.Success;
        }
        catch (HttpRequestException httpEx)
        {
            await _logger.Error(source, $"HTTP error fetching data for vehicle {vehicle.Vin} ({contractStatus})", $"Status: {httpEx.Message}");
            await SaveStatusRecord(vehicle.Vin, "ERROR_HTTP", $"HTTP error: {httpEx.Message}");

            // Log per Validazione Probabilistica Gap
            var failureReason = httpEx.StatusCode switch
            {
                System.Net.HttpStatusCode.TooManyRequests => FetchFailureReason.TESLA_API_RATE_LIMIT,
                System.Net.HttpStatusCode.Unauthorized => FetchFailureReason.UNAUTHORIZED,
                System.Net.HttpStatusCode.ServiceUnavailable => FetchFailureReason.TESLA_API_UNAVAILABLE,
                System.Net.HttpStatusCode.GatewayTimeout => FetchFailureReason.TIMEOUT,
                _ => FetchFailureReason.NETWORK_ERROR
            };
            await LogFetchFailureAsync(vehicle.Id, failureReason, httpEx.Message, (int?)httpEx.StatusCode, "https://owner-api.teslamotors.com");

            return VehicleFetchResult.Success;
        }
        catch (TaskCanceledException tcEx)
        {
            await _logger.Error(source, $"Timeout fetching data for vehicle {vehicle.Vin} ({contractStatus})", tcEx.Message);
            await SaveStatusRecord(vehicle.Vin, "ERROR_TIMEOUT", $"Request timeout: {tcEx.Message}");
            await LogFetchFailureAsync(vehicle.Id, FetchFailureReason.TIMEOUT, tcEx.Message);
            return VehicleFetchResult.Success;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error in Tesla API call for vehicle {vehicle.Vin} ({contractStatus})", ex.ToString());
            await SaveStatusRecord(vehicle.Vin, "ERROR_SYSTEM", $"System error: {ex.Message}");
            await LogFetchFailureAsync(vehicle.Id, FetchFailureReason.UNKNOWN, ex.ToString());
            return VehicleFetchResult.Success;
        }
    }

    private async Task SaveStatusRecord(string vin, string status, string reason)
    {
        var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Vin == vin);
        if (vehicle != null)
        {
            var statusJson = JsonSerializer.Serialize(new {
                polar_drive_status = status,
                reason = reason,
                timestamp = DateTime.Now
            });

            var hasActiveAdaptiveProfile = await _db.SmsAdaptiveProfile.AnyAsync(p => p.VehicleId == vehicle.Id && p.ConsentAccepted && p.ExpiresAt > DateTime.Now);

            var record = new VehicleData
            {
                VehicleId = vehicle.Id,
                Timestamp = DateTime.Now,
                RawJsonAnonymized = statusJson,
                IsSmsAdaptiveProfile = hasActiveAdaptiveProfile
            };

            _db.VehiclesData.Add(record);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Salva un log di fallimento fetch per la certificazione probabilistica dei gap
    /// </summary>
    private async Task LogFetchFailureAsync(int vehicleId, string failureReason, string errorDetails, int? httpStatusCode = null, string? requestUrl = null, long? responseTimeMs = null)
    {
        const string source = "TeslaApiService.LogFetchFailure";

        try
        {
            var failureLog = new FetchFailureLog
            {
                VehicleId = vehicleId,
                AttemptedAt = DateTime.Now,
                FailureReason = failureReason,
                ErrorDetails = errorDetails.Length > 4000 ? errorDetails[..4000] : errorDetails,
                HttpStatusCode = httpStatusCode,
                RequestUrl = requestUrl?.Length > 500 ? requestUrl[..500] : requestUrl,
                ResponseTimeMs = responseTimeMs
            };

            _db.FetchFailureLogs.Add(failureLog);
            await _db.SaveChangesAsync();

            await _logger.Debug(source, $"Logged fetch failure for vehicle ID {vehicleId}: {failureReason}");
        }
        catch (Exception ex)
        {
            // Non bloccare il flusso principale se il logging fallisce
            await _logger.Warning(source, $"Failed to log fetch failure for vehicle ID {vehicleId}", ex.Message);
        }
    }

    /// <summary>
    /// Helper per determinare stato contrattuale (come negli altri servizi)
    /// </summary>
    private string GetContractStatus(ClientVehicle vehicle)
    {
        return (vehicle.IsActiveFlag, vehicle.IsFetchingDataFlag) switch
        {
            (true, true) => "Active Contract",
            (true, false) => "Contract Active - Paused",
            (false, true) => "Grace Period",
            (false, false) => "Contract Terminated"
        };
    }

    private async Task<List<TeslaVehicleInfo>> GetTeslaVehiclesAsync(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.GetAsync("https://owner-api.teslamotors.com/api/1/vehicles");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<TeslaVehiclesResponse>(json);

        return data?.Response ?? [];
    }

    private async Task<bool> WakeUpVehicleAsync(long vehicleId, string accessToken)
    {
        const string source = "TeslaApiService.WakeUpVehicle";

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var wakeUpResponse = await _httpClient.PostAsync($"https://owner-api.teslamotors.com/api/1/vehicles/{vehicleId}/wake_up", null);

            if (wakeUpResponse.IsSuccessStatusCode)
            {
                await _logger.Debug(source, $"Wake-up command sent successfully for vehicle ID {vehicleId}");

                // Aspetta un po' che si svegli
                await Task.Delay(15000);

                // ✅ VERIFICA SE SI È EFFETTIVAMENTE SVEGLIATO
                var vehicles = await GetTeslaVehiclesAsync(accessToken);
                var vehicle = vehicles.FirstOrDefault(v => v.Id == vehicleId);

                if (vehicle != null && vehicle.State.Equals("online", StringComparison.CurrentCultureIgnoreCase))
                {
                    await _logger.Info(source, $"Vehicle ID {vehicleId} successfully woken up");
                    return true;
                }
                else
                {
                    await _logger.Warning(source, $"Vehicle ID {vehicleId} wake-up command sent but vehicle state is still: {vehicle?.State ?? "unknown"}");
                    return false;
                }
            }
            else
            {
                await _logger.Warning(source, $"Wake-up command failed for vehicle ID {vehicleId}: {wakeUpResponse.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error waking up vehicle ID {vehicleId}", ex.ToString());
            return false;
        }
    }

    private async Task<JsonElement> GetVehicleDataAsync(long vehicleId, string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.GetAsync($"https://owner-api.teslamotors.com/api/1/vehicles/{vehicleId}/vehicle_data");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private async Task SaveVehicleDataAsync(string vin, JsonElement data)
    {
        const string source = "TeslaApiService.SaveVehicleData";

        try
        {
            var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Vin == vin);
            if (vehicle == null)
            {
                await _logger.Warning(source, $"Vehicle with VIN {vin} not found for data saving");
                return;
            }

            // Anonimizza i dati Tesla prima del salvataggio
            var rawJsonText = data.GetRawText();
            var anonymizedJson = TeslaDataAnonymizerHelper.AnonymizeVehicleData(rawJsonText);

            var hasActiveAdaptiveProfile = await _db.SmsAdaptiveProfile.AnyAsync(p => p.VehicleId == vehicle.Id && p.ConsentAccepted && p.ExpiresAt > DateTime.Now);

            var vehicleDataRecord = new VehicleData
            {
                VehicleId = vehicle.Id,
                Timestamp = DateTime.Now,
                RawJsonAnonymized = anonymizedJson,
                IsSmsAdaptiveProfile = hasActiveAdaptiveProfile
            };

            _db.VehiclesData.Add(vehicleDataRecord);
            vehicle.LastDataUpdate = DateTime.Now;

            await _db.SaveChangesAsync();

            await _logger.Debug(source, $"Saved vehicle data for VIN {vin}",
                $"Record ID: {vehicleDataRecord.Id}, Data size: {vehicleDataRecord.RawJsonAnonymized.Length} chars");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error saving vehicle data for VIN {vin}", ex.ToString());
            throw;
        }
    }

    private async Task<bool> RefreshTokenAsync(ClientToken token)
    {
        try
        {
            var newToken = await Controllers.VehicleOAuthController.TeslaOAuthService.RefreshAccessToken(token.RefreshToken, _cfg, _env);

            token.AccessToken = newToken;
            token.AccessTokenExpiresAt = DateTime.Now.AddHours(8);
            token.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            await _logger.Error("TeslaApiService.RefreshToken", "Failed to refresh access token", ex.ToString());
            return false;
        }
    }
}