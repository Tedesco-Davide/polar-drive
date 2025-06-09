using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Production;

/// <summary>
/// Service che chiama le API Tesla reali e salva i dati
/// Usato in PRODUZIONE (non in development/mock)
/// </summary>
public class TeslaApiService
{
    private readonly PolarDriveDbContext _db;
    private readonly PolarDriveLogger _logger;
    private readonly IWebHostEnvironment _env;
    private readonly HttpClient _httpClient;

    public TeslaApiService(PolarDriveDbContext db, IWebHostEnvironment env, HttpClient httpClient)
    {
        _db = db;
        _env = env;
        _httpClient = httpClient;
        _logger = new PolarDriveLogger(_db);
    }

    /// <summary>
    /// Metodo principale - chiama Tesla API per tutti i veicoli attivi
    /// </summary>
    public async Task FetchDataForAllActiveVehiclesAsync()
    {
        const string source = "TeslaApiService.FetchDataForAllActiveVehicles";

        if (_env.IsDevelopment())
        {
            await _logger.Info(source, "Skipping Tesla API calls in Development mode - using mock service instead");
            return;
        }

        await _logger.Info(source, "Starting Tesla API data fetch for all active vehicles");

        var activeVehicles = await _db.ClientVehicles
            .Where(v => v.ClientOAuthAuthorized && v.IsActiveFlag && v.IsFetchingDataFlag && v.Brand.ToLower() == "tesla")
            .Include(v => v.ClientCompany)
            .ToListAsync();

        await _logger.Debug(source, $"Found {activeVehicles.Count} active Tesla vehicles for data fetching");

        int successCount = 0;
        int errorCount = 0;
        int skippedCount = 0;

        foreach (var vehicle in activeVehicles)
        {
            try
            {
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
            }
            catch (Exception ex)
            {
                errorCount++;
                await _logger.Error(source, $"Unexpected error fetching data for vehicle {vehicle.Vin}", ex.ToString());
            }
        }

        await _logger.Info(source, "Completed Tesla API data fetch cycle",
            $"Success: {successCount}, Skipped: {skippedCount}, Errors: {errorCount}");
    }

    /// <summary>
    /// Chiama Tesla API per un singolo veicolo
    /// </summary>
    public async Task<VehicleFetchResult> FetchDataForSingleVehicleAsync(ClientVehicle vehicle)
    {
        const string source = "TeslaApiService.FetchDataForSingleVehicle";

        // Recupera il token OAuth per questo veicolo
        var token = await _db.ClientTokens.FirstOrDefaultAsync(t => t.VehicleId == vehicle.Id);
        if (token == null)
        {
            await _logger.Warning(source, $"No OAuth token found for vehicle {vehicle.Vin}");
            return VehicleFetchResult.Skipped;
        }

        // Controlla se il token è scaduto
        if (token.AccessTokenExpiresAt <= DateTime.UtcNow)
        {
            await _logger.Info(source, $"Token expired for vehicle {vehicle.Vin}, attempting refresh");
            var refreshed = await RefreshTokenAsync(token);
            if (!refreshed)
            {
                await _logger.Error(source, $"Failed to refresh token for vehicle {vehicle.Vin}");
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
                await _logger.Warning(source, $"Vehicle {vehicle.Vin} not found in Tesla account");
                return VehicleFetchResult.Skipped;
            }

            await _logger.Debug(source, $"Vehicle {vehicle.Vin} current state: {teslaVehicle.State}");

            // 2. ✅ GESTIONE INTELLIGENTE DELLO STATO
            switch (teslaVehicle.State.ToLower())
            {
                case "offline":
                    await _logger.Info(source, $"Vehicle {vehicle.Vin} is offline, skipping data fetch to save API quota");
                    return VehicleFetchResult.Skipped;

                case "asleep":
                    await _logger.Info(source, $"Vehicle {vehicle.Vin} is asleep, attempting to wake up");
                    var wakeUpSuccess = await WakeUpVehicleAsync(teslaVehicle.Id, token.AccessToken);
                    if (!wakeUpSuccess)
                    {
                        await _logger.Warning(source, $"Failed to wake up vehicle {vehicle.Vin}, skipping data fetch");
                        return VehicleFetchResult.Skipped;
                    }
                    // Aspetta più tempo per veicoli che erano dormienti
                    await Task.Delay(10000);
                    break;

                case "online":
                    await _logger.Debug(source, $"Vehicle {vehicle.Vin} is already online, proceeding with data fetch");
                    // Nessun wake-up necessario, procedi direttamente
                    break;

                default:
                    await _logger.Warning(source, $"Vehicle {vehicle.Vin} has unknown state '{teslaVehicle.State}', attempting data fetch anyway");
                    break;
            }

            // 3. Ottieni dati completi del veicolo
            var vehicleData = await GetVehicleDataAsync(teslaVehicle.Id, token.AccessToken);

            // 4. Salva nel database
            await SaveVehicleDataAsync(vehicle.Vin, vehicleData);

            await _logger.Info(source, $"Successfully fetched and saved data for vehicle {vehicle.Vin}");
            return VehicleFetchResult.Success;
        }
        catch (HttpRequestException httpEx)
        {
            await _logger.Error(source, $"HTTP error fetching data for vehicle {vehicle.Vin}",
                $"Status: {httpEx.Message}");
            return VehicleFetchResult.Error;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error in Tesla API call for vehicle {vehicle.Vin}", ex.ToString());
            return VehicleFetchResult.Error;
        }
    }

    private async Task<List<TeslaVehicleInfo>> GetTeslaVehiclesAsync(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.GetAsync("https://owner-api.teslamotors.com/api/1/vehicles");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<TeslaVehiclesResponse>(json);

        return data?.Response ?? new List<TeslaVehicleInfo>();
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
                await Task.Delay(5000);

                // ✅ VERIFICA SE SI È EFFETTIVAMENTE SVEGLIATO
                var vehicles = await GetTeslaVehiclesAsync(accessToken);
                var vehicle = vehicles.FirstOrDefault(v => v.Id == vehicleId);

                if (vehicle != null && vehicle.State.ToLower() == "online")
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
        // ✅ SALVA DIRETTAMENTE NEL DATABASE (più efficiente che simulare chiamata HTTP)
        const string source = "TeslaApiService.SaveVehicleData";

        try
        {
            var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Vin == vin);
            if (vehicle == null)
            {
                await _logger.Warning(source, $"Vehicle with VIN {vin} not found for data saving");
                return;
            }

            var vehicleDataRecord = new VehicleData
            {
                VehicleId = vehicle.Id,
                Timestamp = DateTime.UtcNow,
                RawJson = data.GetRawText()
            };

            _db.VehiclesData.Add(vehicleDataRecord);
            vehicle.LastDataUpdate = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _logger.Debug(source, $"Saved vehicle data for VIN {vin}",
                $"Record ID: {vehicleDataRecord.Id}, Data size: {vehicleDataRecord.RawJson.Length} chars");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error saving vehicle data for VIN {vin}", ex.ToString());
            throw; // Re-throw per far sapere al chiamante che il salvataggio è fallito
        }
    }

    private async Task<bool> RefreshTokenAsync(ClientToken token)
    {
        try
        {
            var newToken = await Controllers.VehicleOAuthController.TeslaOAuthService.RefreshAccessToken(token.RefreshToken, _env);

            token.AccessToken = newToken;
            token.AccessTokenExpiresAt = DateTime.UtcNow.AddHours(8);
            token.UpdatedAt = DateTime.UtcNow;

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

// ✅ ENUM PER RISULTATI PIÙ CHIARI
public enum VehicleFetchResult
{
    Success,    // Dati recuperati e salvati con successo
    Skipped,    // Operazione saltata (veicolo offline, token mancante, etc.)
    Error       // Errore durante l'operazione
}

// DTOs per Tesla API
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