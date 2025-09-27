using System.Text;

namespace PolarDrive.TeslaMockApiService.Services;

public class TeslaDataPusherService : BackgroundService
{
    private readonly ILogger<TeslaDataPusherService> _logger;
    private readonly IConfiguration _configuration;
    private readonly VehicleStateManager _vehicleStateManager;
    private readonly HttpClient _httpClient;
    private readonly Random _random = new();

    public TeslaDataPusherService(
        ILogger<TeslaDataPusherService> logger,
        IConfiguration configuration,
        VehicleStateManager vehicleStateManager)
    {
        _logger = logger;
        _configuration = configuration;
        _vehicleStateManager = vehicleStateManager;

        // ✅ CONFIGURA HTTPCLIENT PER IGNORARE CERTIFICATI SSL
        var handler = new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var webApiBaseUrl = _configuration["WebAPI:BaseUrl"] ?? "http://localhost:5000";
        var pushIntervalMinutes = _configuration.GetValue<int>("TeslaDataPusher:IntervalMinutes", 60);
        var enablePusher = _configuration.GetValue<bool>("TeslaDataPusher:Enabled", true);

        if (!enablePusher)
        {
            _logger.LogInformation("Tesla Data Pusher is disabled via configuration");
            return;
        }

        _logger.LogInformation("Tesla Data Pusher started. Pushing data every {IntervalMinutes} minutes to {WebApiUrl}",
            pushIntervalMinutes, webApiBaseUrl);

        // Inizializza i veicoli al primo avvio
        await InitializeVehicleStates();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1. Aggiorna gli stati dei veicoli (simulazione dinamica)
                await UpdateVehicleStates();

                // 2. Invia i dati aggiornati alla WebAPI
                await PushTeslaDataToWebAPI(webApiBaseUrl, stoppingToken);

                _logger.LogInformation("Successfully pushed Tesla mock data to WebAPI at {Time}", DateTime.Now);

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
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private Task InitializeVehicleStates()
    {
        var vins = GetSimulatedVehicleVins();

        foreach (var vin in vins)
        {
            if (!_vehicleStateManager.HasVehicle(vin))
            {
                var initialState = CreateInitialVehicleState(vin);
                _vehicleStateManager.AddOrUpdateVehicle(vin, initialState);
                _logger.LogInformation("Initialized vehicle state for VIN: {Vin}", vin);
            }
        }

        return Task.CompletedTask;
    }

    private async Task UpdateVehicleStates()
    {
        var allVehicles = _vehicleStateManager.GetAllVehicles();

        foreach (var (vin, state) in allVehicles)
        {
            // Simula cambiamenti dinamici ogni ora
            await SimulateVehicleChanges(state);
            _vehicleStateManager.AddOrUpdateVehicle(vin, state);
        }
    }

    private Task SimulateVehicleChanges(VehicleSimulationState state)
    {
        // Simula consumo batteria nel tempo
        if (!state.IsCharging && state.BatteryLevel > 5)
        {
            // Consuma 1-3% di batteria ogni ora (dipende se è in movimento)
            var consumption = state.IsMoving ? _random.Next(2, 4) : _random.Next(1, 2);
            state.BatteryLevel = Math.Max(5, state.BatteryLevel - consumption);
        }

        // Simula ricarica automatica se batteria bassa
        if (state.BatteryLevel <= 20 && !state.IsCharging)
        {
            state.IsCharging = true;
            state.ChargingState = "Charging";
            state.ChargeRate = _random.Next(35, 50);
            _logger.LogInformation("Auto-started charging for VIN {Vin} - Battery at {Level}%",
                state.Vin, state.BatteryLevel);
        }

        // Simula fine ricarica
        if (state.IsCharging && state.BatteryLevel >= 90)
        {
            state.IsCharging = false;
            state.ChargingState = "Complete";
            state.ChargeRate = 0;
            _logger.LogInformation("Completed charging for VIN {Vin} - Battery at {Level}%",
                state.Vin, state.BatteryLevel);
        }

        // Simula carica durante ricarica
        if (state.IsCharging && state.BatteryLevel < 95)
        {
            var chargeAdded = _random.Next(5, 15); // 5-15% ogni ora
            state.BatteryLevel = Math.Min(100, state.BatteryLevel + chargeAdded);
            state.ChargeEnergyAdded += chargeAdded * 0.75m; // ~0.75 kWh per %
        }

        // Simula movimenti casuali
        if (_random.Next(1, 5) == 1) // 25% probabilità di muoversi
        {
            state.IsMoving = true;
            state.Speed = _random.Next(30, 80);
            // Movimento casuale piccolo (max 0.01 gradi = ~1km)
            state.Latitude += (decimal)(_random.NextDouble() - 0.5) * 0.01m;
            state.Longitude += (decimal)(_random.NextDouble() - 0.5) * 0.01m;
            state.Heading = _random.Next(0, 360);
            state.Odometer += _random.Next(10, 50); // 10-50 miglia
        }
        else
        {
            state.IsMoving = false;
            state.Speed = null;
        }

        // Simula cambiamenti climatici
        if (_random.Next(1, 4) == 1) // 33% probabilità
        {
            state.OutsideTemp += (decimal)(_random.NextDouble() - 0.5) * 4; // +/- 2°C
            state.OutsideTemp = Math.Max(-10, Math.Min(40, state.OutsideTemp)); // Limiti realistici

            if (Math.Abs(state.InsideTemp - state.OutsideTemp) > 10)
            {
                state.IsClimateOn = true;
            }
        }

        // Aggiorna timestamp
        state.LastUpdate = DateTime.Now;

        return Task.CompletedTask;
    }

    private VehicleSimulationState CreateInitialVehicleState(string vin)
    {
        // ✅ FIX: Genera VehicleId sicuro da VIN + hash
        var vinHashCode = Math.Abs(vin.GetHashCode());
        var vehicleId = (vinHashCode % 900000) + 100000; // Numero tra 100000-999999

        // ✅ Determina il modello dal VIN in modo più robusto  
        var modelType = DetermineModelFromVin(vin);

        return new VehicleSimulationState
        {
            Vin = vin, // ✅ IMPORTANTE: Imposta esplicitamente il VIN
            VehicleId = vehicleId,
            DisplayName = $"Model {modelType} - {vin[^2..]}",
            Color = GetRandomColor(),
            BatteryLevel = _random.Next(20, 95),
            IsCharging = _random.Next(1, 4) == 1, // 25% probabilità
            ChargingState = _random.Next(1, 4) == 1 ? "Charging" : "Complete",
            ChargeRate = _random.Next(1, 4) == 1 ? _random.Next(30, 50) : 0,
            Latitude = 41.9028m + (decimal)(_random.NextDouble() - 0.5) * 0.1m, // Roma area
            Longitude = 12.4964m + (decimal)(_random.NextDouble() - 0.5) * 0.1m,
            OutsideTemp = _random.Next(10, 30),
            InsideTemp = _random.Next(18, 25),
            IsLocked = _random.Next(1, 3) == 1, // 50% probabilità
            SentryMode = _random.Next(1, 5) == 1, // 20% probabilità
            Odometer = _random.Next(5000, 50000),
            LastUpdate = DateTime.UtcNow
        };
    }

    private string DetermineModelFromVin(string vin)
    {
        // Determina il modello dal VIN in modo più sicuro
        if (vin.Contains("01")) return "3";
        if (vin.Contains("02")) return "Y";
        if (vin.Contains("03")) return "S";
        if (vin.Contains("04")) return "X";
        if (vin.Contains("05")) return "Roadster";

        // Default basato sulla posizione nel VIN
        var vinNumber = Math.Abs(vin.GetHashCode()) % 5;
        return vinNumber switch
        {
            0 => "3",
            1 => "Y",
            2 => "S",
            3 => "X",
            _ => "Cybertruck"
        };
    }

    private string GetRandomColor()
    {
        var colors = new[]
        {
            "Pearl White Multi-Coat",
            "Midnight Silver Metallic",
            "Deep Blue Metallic",
            "Solid Black",
            "Red Multi-Coat"
        };
        return colors[_random.Next(colors.Length)];
    }

    private async Task PushTeslaDataToWebAPI(string webApiBaseUrl, CancellationToken cancellationToken)
    {
        var allVehicles = _vehicleStateManager.GetAllVehicles();

        foreach (var (vin, state) in allVehicles)
        {
            try
            {
                // Genera i dati completi usando SmartTeslaDataGeneratorService
                var rawJson = SmartTeslaDataGeneratorService.GenerateRawVehicleJson(state);

                var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

                // ✅ USA L'HTTPCLIENT CONFIGURATO CON SSL BYPASS
                var response = await _httpClient.PostAsync(
                    $"{webApiBaseUrl}/api/TeslaFakeDataReceiver/ReceiveVehicleData/{vin}",
                    content,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Successfully sent data for VIN: {Vin} - Battery: {Battery}%, Charging: {Charging}",
                        vin, state.BatteryLevel, state.IsCharging);
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to send data for VIN: {Vin}. Status: {StatusCode}, Response: {Response}",
                        vin, response.StatusCode, responseContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending data for VIN: {Vin}", vin);
            }

            // Piccola pausa tra le chiamate per non sovraccaricare
            await Task.Delay(100, cancellationToken);
        }
    }

    private List<string> GetSimulatedVehicleVins()
    {
        var vins = _configuration.GetSection("TeslaDataPusher:SimulatedVins").Get<List<string>>();
        return vins ??
        [
            "5YJ3000000NEXUS01"
        ];
    }

    // ✅ IMPORTANTE: Dispose dell'HttpClient
    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}