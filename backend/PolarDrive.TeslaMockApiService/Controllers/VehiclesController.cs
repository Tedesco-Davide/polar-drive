using Microsoft.AspNetCore.Mvc;
using PolarDrive.TeslaMockApiService.Services;

namespace PolarDrive.TeslaMockApiService.Controllers;

[ApiController]
[Route("api/1")]
public class VehiclesController(VehicleStateManager vehicleStateManager, ILogger<VehiclesController> logger) : ControllerBase
{
    private readonly VehicleStateManager _vehicleStateManager = vehicleStateManager;
    private readonly ILogger<VehiclesController> _logger = logger;

    /// <summary>
    /// GET /api/1/vehicles - Lista tutti i veicoli
    /// </summary>
    [HttpGet("vehicles")]
    public IActionResult GetVehicles()
    {
        if (!IsValidToken())
            return Unauthorized(new { error = "invalid_token" });

        var allVehicles = _vehicleStateManager.GetAllVehicles();
        var vehicleStates = allVehicles.Values.ToArray();

        var vehiclesList = SmartTeslaDataGeneratorService.GenerateVehiclesList(vehicleStates);
        return Ok(vehiclesList);
    }

    /// <summary>
    /// GET /api/1/vehicles/{id} - Dettagli base di un veicolo specifico
    /// </summary>
    [HttpGet("vehicles/{id}")]
    public IActionResult GetVehicle(long id)
    {
        if (!IsValidToken())
            return Unauthorized(new { error = "invalid_token" });

        var state = GetVehicleStateById(id);
        if (state == null)
            return NotFound(new { error = "vehicle_not_found" });

        var vehicleData = new
        {
            response = new
            {
                id = id,
                vehicle_id = state.VehicleId,
                vin = state.Vin,
                color = state.Color,
                access_type = "OWNER",
                display_name = state.DisplayName,
                option_codes = "TEST0,COUS",
                granular_access = new { hide_private = false },
                tokens = new[] { "4f993c5b9e2b937b", "7a3153b1bbb48a96" },
                state = "online",
                in_service = false,
                id_s = id.ToString(),
                calendar_enabled = true,
                api_version = 54,
                backseat_token = (string?)null,
                backseat_token_updated_at = (string?)null
            }
        };

        return Ok(vehicleData);
    }

    /// <summary>
    /// GET /api/1/vehicles/{id}/vehicle_data - Tutti i dati completi del veicolo
    /// </summary>
    [HttpGet("vehicles/{id}/vehicle_data")]
    public IActionResult GetVehicleData(long id)
    {
        if (!IsValidToken())
            return Unauthorized(new { error = "invalid_token" });

        var state = GetVehicleStateById(id);
        if (state == null)
            return NotFound(new { error = "vehicle_not_found" });

        var completeVehicleData = SmartTeslaDataGeneratorService.GenerateCompleteVehicleData(state);
        return Ok(completeVehicleData);
    }

    /// <summary>
    /// POST /api/1/vehicles/{id}/wake_up - Risveglia il veicolo
    /// </summary>
    [HttpPost("vehicles/{id}/wake_up")]
    public IActionResult WakeUp(long id)
    {
        if (!IsValidToken())
            return Unauthorized(new { error = "invalid_token" });

        var state = GetVehicleStateById(id);
        if (state == null)
            return NotFound(new { error = "vehicle_not_found" });

        var wakeUpResponse = SmartTeslaDataGeneratorService.GenerateWakeUpResponse(state);
        return Ok(wakeUpResponse);
    }

    /// <summary>
    /// GET /api/1/vehicles/{id}/nearby_charging_sites - Siti di ricarica nelle vicinanze
    /// </summary>
    [HttpGet("vehicles/{id}/nearby_charging_sites")]
    public IActionResult GetNearbyChargingSites(long id)
    {
        if (!IsValidToken())
            return Unauthorized(new { error = "invalid_token" });

        var state = GetVehicleStateById(id);
        if (state == null)
            return NotFound(new { error = "vehicle_not_found" });

        var chargingSites = SmartTeslaDataGeneratorService.GenerateNearbyChargingSites(state);
        return Ok(chargingSites);
    }

    /// <summary>
    /// POST /api/1/vehicles/{id}/command/{command} - Esegue un comando sul veicolo
    /// </summary>
    [HttpPost("vehicles/{id}/command/{command}")]
    public IActionResult ExecuteCommand(long id, string command, [FromBody] object? parameters = null)
    {
        if (!IsValidToken())
            return Unauthorized(new { error = "invalid_token" });

        var state = GetVehicleStateById(id);
        if (state == null)
            return NotFound(new { error = "vehicle_not_found" });

        // Simula l'esecuzione del comando e aggiorna lo stato
        UpdateStateBasedOnCommand(state, command, parameters);

        // Salva lo stato aggiornato
        _vehicleStateManager.AddOrUpdateVehicle(state.Vin, state);

        var commandResponse = SmartTeslaDataGeneratorService.GenerateCommandResponse(command, parameters);
        return Ok(commandResponse);
    }

    /// <summary>
    /// GET /api/1/vehicles/{id}/data - Endpoint per dati RAW completi
    /// </summary>
    [HttpGet("vehicles/{id}/data")]
    public IActionResult GetRawVehicleData(long id)
    {
        if (!IsValidToken())
            return Unauthorized(new { error = "invalid_token" });

        var state = GetVehicleStateById(id);
        if (state == null)
            return NotFound(new { error = "vehicle_not_found" });

        var rawJson = SmartTeslaDataGeneratorService.GenerateRawVehicleJson(state);
        return Ok(rawJson);
    }

    #region Helper Methods

    /// <summary>
    /// Valida il token di autorizzazione
    /// </summary>
    private bool IsValidToken()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return false;

        var authHeader = Request.Headers["Authorization"].ToString();
        if (!authHeader.StartsWith("Bearer "))
            return false;

        var accessToken = authHeader.Substring("Bearer ".Length);

        return !string.IsNullOrWhiteSpace(accessToken) &&
               (accessToken.StartsWith("TESLA_ACCESS_TOKEN_") ||
                accessToken.StartsWith("TESLA_REFRESHED_ACCESS_TOKEN_"));
    }

    /// <summary>
    /// Trova lo stato del veicolo per ID
    /// </summary>
    private VehicleSimulationState? GetVehicleStateById(long id)
    {
        var allVehicles = _vehicleStateManager.GetAllVehicles();

        return allVehicles.Values.FirstOrDefault(s =>
            s.VehicleId.ToString() == id.ToString() ||
            id.ToString().EndsWith(s.VehicleId.ToString()));
    }

    /// <summary>
    /// Aggiorna lo stato del veicolo in base al comando eseguito
    /// </summary>
    private void UpdateStateBasedOnCommand(VehicleSimulationState state, string command, object? parameters)
    {
        _logger.LogInformation("Executing command {Command} for vehicle {Vin}", command, state.Vin);

        switch (command.ToLowerInvariant())
        {
            case "charge_start":
                state.IsCharging = true;
                state.ChargingState = "Charging";
                state.ChargeRate = 48;
                break;

            case "charge_stop":
                state.IsCharging = false;
                state.ChargingState = "Stopped";
                state.ChargeRate = 0;
                break;

            case "door_lock":
                state.IsLocked = true;
                break;

            case "door_unlock":
                state.IsLocked = false;
                break;

            case "auto_conditioning_start":
                state.IsClimateOn = true;
                break;

            case "auto_conditioning_stop":
                state.IsClimateOn = false;
                break;

            case "set_sentry_mode":
                // Cerca di estrarre il parametro "on" se presente
                if (parameters != null)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(parameters);
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (dict?.ContainsKey("on") == true)
                    {
                        state.SentryMode = dict["on"].ToString()?.ToLower() == "true";
                    }
                }
                break;

            case "remote_start_drive":
                state.RemoteStart = true;
                break;

            case "set_charge_limit":
                if (parameters != null)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(parameters);
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (dict?.ContainsKey("percent") == true &&
                        int.TryParse(dict["percent"].ToString(), out var percent))
                    {
                        // Il charge limit non è direttamente nello stato, ma potresti aggiungerlo
                        _logger.LogInformation("Set charge limit to {Percent}% for {Vin}", percent, state.Vin);
                    }
                }
                break;

            case "set_charging_amps":
                if (parameters != null)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(parameters);
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (dict?.ContainsKey("charging_amps") == true &&
                        int.TryParse(dict["charging_amps"].ToString(), out var amps))
                    {
                        if (state.IsCharging)
                        {
                            state.ChargeRate = amps;
                        }
                        _logger.LogInformation("Set charging amps to {Amps}A for {Vin}", amps, state.Vin);
                    }
                }
                break;

            case "set_temps":
                if (parameters != null)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(parameters);
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                    if (dict?.ContainsKey("driver_temp") == true &&
                        int.TryParse(dict["driver_temp"].ToString(), out var driverTemp))
                    {
                        state.DriverTempSetting = driverTemp;
                    }

                    if (dict?.ContainsKey("passenger_temp") == true &&
                        int.TryParse(dict["passenger_temp"].ToString(), out var passengerTemp))
                    {
                        state.PassengerTempSetting = passengerTemp;
                    }
                }
                break;

            case "flash_lights":
            case "honk_horn":
                // Questi comandi non cambiano lo stato persistente
                _logger.LogInformation("Executed {Command} for {Vin}", command, state.Vin);
                break;

            default:
                _logger.LogWarning("Unknown command {Command} for vehicle {Vin}", command, state.Vin);
                break;
        }

        // Aggiorna il timestamp
        state.LastUpdate = DateTime.UtcNow;
    }

    #endregion
}