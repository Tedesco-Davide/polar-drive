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
            .Where(v => v.ClientOAuthAuthorized && v.IsActiveFlag && v.IsFetchingDataFlag)
            .Include(v => v.ClientCompany)
            .ToListAsync();

        await _logger.Debug(source, $"Found {activeVehicles.Count} active vehicles for data fetching");

        foreach (var vehicle in activeVehicles)
        {
            try
            {
                await FetchDataForSingleVehicleAsync(vehicle);
            }
            catch (Exception ex)
            {
                await _logger.Error(source, $"Error fetching data for vehicle {vehicle.Vin}", ex.ToString());
            }
        }

        await _logger.Info(source, "Completed Tesla API data fetch cycle");
    }

    /// <summary>
    /// Chiama Tesla API per un singolo veicolo
    /// </summary>
    public async Task FetchDataForSingleVehicleAsync(ClientVehicle vehicle)
    {
        const string source = "TeslaApiService.FetchDataForSingleVehicle";

        // Recupera il token OAuth per questo veicolo
        var token = await _db.ClientTokens.FirstOrDefaultAsync(t => t.VehicleId == vehicle.Id);
        if (token == null)
        {
            await _logger.Warning(source, $"No OAuth token found for vehicle {vehicle.Vin}");
            return;
        }

        // Controlla se il token è scaduto
        if (token.AccessTokenExpiresAt <= DateTime.UtcNow)
        {
            await _logger.Info(source, $"Token expired for vehicle {vehicle.Vin}, attempting refresh");
            var refreshed = await RefreshTokenAsync(token);
            if (!refreshed)
            {
                await _logger.Error(source, $"Failed to refresh token for vehicle {vehicle.Vin}");
                return;
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
                return;
            }

            // 2. Sveglia il veicolo se dormiente
            await WakeUpVehicleAsync(teslaVehicle.Id, token.AccessToken);

            // 3. Ottieni dati completi del veicolo
            var vehicleData = await GetVehicleDataAsync(teslaVehicle.Id, token.AccessToken);

            // 4. Salva nel database via TeslaDataReceiverController
            await SaveVehicleDataAsync(vehicle.Vin, vehicleData);

            await _logger.Info(source, $"Successfully fetched and saved data for vehicle {vehicle.Vin}");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error in Tesla API call for vehicle {vehicle.Vin}", ex.ToString());
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

    private async Task WakeUpVehicleAsync(long vehicleId, string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Prova a svegliare il veicolo
        var wakeUpResponse = await _httpClient.PostAsync($"https://owner-api.teslamotors.com/api/1/vehicles/{vehicleId}/wake_up", null);

        // Aspetta un po' che si svegli
        await Task.Delay(5000);
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
        // Chiama il nostro TeslaDataReceiverController interno
        var receiverController = new Controllers.TeslaDataReceiverController(_db);
        await receiverController.ReceiveVehicleData(vin, data);
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
        catch
        {
            return false;
        }
    }
}

// DTOs per Tesla API
public class TeslaVehiclesResponse
{
    public List<TeslaVehicleInfo> Response { get; set; } = new();
}

public class TeslaVehicleInfo
{
    public long Id { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}