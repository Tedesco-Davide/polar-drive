using System.Text;
using System.Text.Json;
using PolarDrive.TeslaMockApiService.Models;
using PolarDrive.TeslaMockApiService.Services;

namespace PolarDrive.TeslaMockApiService.Services;

public class TeslaDataPusherService : BackgroundService
{
    private readonly ILogger<TeslaDataPusherService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly Random _random = new();

    public TeslaDataPusherService(
        ILogger<TeslaDataPusherService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var webApiBaseUrl = _configuration["WebAPI:BaseUrl"] ?? "http://localhost:5000";
        var pushIntervalMinutes = _configuration.GetValue<int>("TeslaDataPusher:IntervalMinutes", 60); // Default: ogni ora
        var enablePusher = _configuration.GetValue<bool>("TeslaDataPusher:Enabled", true);

        if (!enablePusher)
        {
            _logger.LogInformation("Tesla Data Pusher is disabled via configuration");
            return;
        }

        _logger.LogInformation("Tesla Data Pusher started. Pushing data every {IntervalMinutes} minutes to {WebApiUrl}",
            pushIntervalMinutes, webApiBaseUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PushTeslaDataToWebAPI(webApiBaseUrl, stoppingToken);

                _logger.LogInformation("Successfully pushed Tesla mock data to WebAPI at {Time}", DateTime.UtcNow);

                // Aspetta il prossimo ciclo
                await Task.Delay(TimeSpan.FromMinutes(pushIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Tesla Data Pusher was cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while pushing Tesla data to WebAPI");

                // In caso di errore, aspetta meno tempo prima di riprovare
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task PushTeslaDataToWebAPI(string webApiBaseUrl, CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();

        // Lista di VIN fittizi da simulare (potresti prenderli da config o DB)
        var vehicleVins = GetSimulatedVehicleVins();

        foreach (var vin in vehicleVins)
        {
            try
            {
                // 1. Genera i dati complessi usando il tuo FakeTeslaJsonDataFetch
                var mockData = GenerateMockTeslaData(vin);

                // 2. Converti in JSON
                var jsonContent = JsonSerializer.Serialize(mockData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // 3. Spara alla tua WebAPI
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(
                    $"{webApiBaseUrl}/api/TeslaDataReceiver/ReceiveVehicleData/{vin}",
                    content,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Successfully sent data for VIN: {Vin}", vin);
                }
                else
                {
                    _logger.LogWarning("Failed to send data for VIN: {Vin}. Status: {StatusCode}",
                        vin, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending data for VIN: {Vin}", vin);
            }
        }
    }

    private TeslaCompleteDataDto GenerateMockTeslaData(string vin)
    {
        var timestamp = DateTime.UtcNow;

        // Usa il tuo FakeTeslaJsonDataFetch per generare dati realistici
        var rawJson = FakeTeslaJsonDataFetch.GenerateRawVehicleJson(timestamp, _random);

        // Deserializza nel tuo DTO complesso
        var mockData = JsonSerializer.Deserialize<TeslaCompleteDataDto>(rawJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Personalizza con il VIN specifico
        CustomizeDataForVin(mockData, vin);

        return mockData;
    }

    private void CustomizeDataForVin(TeslaCompleteDataDto mockData, string vin)
    {
        // Personalizza i dati per questo VIN specifico
        // Ad esempio, aggiorna VIN nei vari oggetti
        foreach (var dataItem in mockData.Response.Data)
        {
            if (dataItem.Type == "charging_history" && dataItem.Content is ChargingHistoryDto chargingHistory)
            {
                chargingHistory.Vin = vin;
            }

            // Aggiungi altre personalizzazioni per VIN...
        }
    }

    private List<string> GetSimulatedVehicleVins()
    {
        // Potresti prenderli da:
        // 1. Configuration
        // 2. Database (se accessibile)
        // 3. File JSON
        // 4. Lista hardcoded per testing

        var vins = _configuration.GetSection("TeslaDataPusher:SimulatedVins").Get<List<string>>();

        return vins ?? new List<string>
        {
            "5YJ3000000NEXUS01",
            "5YJ3000000NEXUS02",
            "5YJ3000000NEXUS03"
        };
    }
}

// Extension method per registrazione semplice
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTeslaDataPusher(this IServiceCollection services)
    {
        services.AddHostedService<TeslaDataPusherService>();
        services.AddHttpClient(); // Per le chiamate HTTP
        return services;
    }
}