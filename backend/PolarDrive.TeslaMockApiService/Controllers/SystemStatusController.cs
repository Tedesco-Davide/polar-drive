using Microsoft.AspNetCore.Mvc;
using PolarDrive.TeslaMockApiService.Services;

namespace PolarDrive.TeslaMockApiService.Controllers;

[ApiController]
[Route("api/system")]
public class SystemStatusController : ControllerBase
{
    private readonly VehicleStateManager _vehicleStateManager;
    private readonly ILogger<SystemStatusController> _logger;

    public SystemStatusController(VehicleStateManager vehicleStateManager, ILogger<SystemStatusController> logger)
    {
        _vehicleStateManager = vehicleStateManager;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/system/status - Stato generale del sistema
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetSystemStatus()
    {
        var allVehicles = _vehicleStateManager.GetAllVehicles();

        var status = new
        {
            timestamp = DateTime.UtcNow,
            service_name = "Tesla Mock API Service",
            version = "1.0.0",
            uptime = DateTime.UtcNow.Subtract(System.Diagnostics.Process.GetCurrentProcess().StartTime),
            total_vehicles = allVehicles.Count,
            vehicles_summary = allVehicles.Values.Select(v => new
            {
                vin = v.Vin,
                display_name = v.DisplayName,
                battery_level = v.BatteryLevel,
                is_charging = v.IsCharging,
                is_moving = v.IsMoving,
                last_update = v.LastUpdate,
                location = new { lat = v.Latitude, lng = v.Longitude }
            }).ToArray(),
            statistics = new
            {
                charging_vehicles = allVehicles.Values.Count(v => v.IsCharging),
                moving_vehicles = allVehicles.Values.Count(v => v.IsMoving),
                locked_vehicles = allVehicles.Values.Count(v => v.IsLocked),
                sentry_mode_active = allVehicles.Values.Count(v => v.SentryMode),
                average_battery_level = allVehicles.Values.Any() ?
                    allVehicles.Values.Average(v => v.BatteryLevel) : 0
            }
        };

        return Ok(status);
    }

    /// <summary>
    /// GET /api/system/vehicles/{vin}/details - Dettagli completi di un veicolo
    /// </summary>
    [HttpGet("vehicles/{vin}/details")]
    public IActionResult GetVehicleDetails(string vin)
    {
        var vehicle = _vehicleStateManager.GetVehicle(vin);
        if (vehicle == null)
        {
            return NotFound(new { error = $"Vehicle with VIN {vin} not found" });
        }

        return Ok(new
        {
            vehicle_info = vehicle,
            last_updated_ago = DateTime.UtcNow.Subtract(vehicle.LastUpdate),
            simulated_data = SmartTeslaDataGeneratorService.GenerateCompleteVehicleData(vehicle)
        });
    }

    /// <summary>
    /// POST /api/system/vehicles/{vin}/simulate - Simula eventi per un veicolo
    /// </summary>
    [HttpPost("vehicles/{vin}/simulate")]
    public IActionResult SimulateVehicleEvent(string vin, [FromBody] SimulateEventRequest request)
    {
        var vehicle = _vehicleStateManager.GetVehicle(vin);
        if (vehicle == null)
        {
            return NotFound(new { error = $"Vehicle with VIN {vin} not found" });
        }

        try
        {
            ApplySimulationEvent(vehicle, request);
            _vehicleStateManager.AddOrUpdateVehicle(vin, vehicle);

            _logger.LogInformation("Applied simulation event {EventType} to vehicle {Vin}",
                request.EventType, vin);

            return Ok(new
            {
                success = true,
                message = $"Applied {request.EventType} to vehicle {vin}",
                updated_state = vehicle
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying simulation event {EventType} to vehicle {Vin}",
                request.EventType, vin);

            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// POST /api/system/reset - Reset di tutti i veicoli allo stato iniziale
    /// </summary>
    [HttpPost("reset")]
    public IActionResult ResetAllVehicles()
    {
        var allVehicles = _vehicleStateManager.GetAllVehicles();
        var resetCount = 0;

        foreach (var (vin, vehicle) in allVehicles)
        {
            // Reset a valori di default
            vehicle.BatteryLevel = new Random().Next(50, 95);
            vehicle.IsCharging = false;
            vehicle.ChargingState = "Complete";
            vehicle.ChargeRate = 0;
            vehicle.IsMoving = false;
            vehicle.Speed = null;
            vehicle.IsClimateOn = false;
            vehicle.IsLocked = true;
            vehicle.SentryMode = false;
            vehicle.RemoteStart = false;
            vehicle.LastUpdate = DateTime.UtcNow;

            _vehicleStateManager.AddOrUpdateVehicle(vin, vehicle);
            resetCount++;
        }

        _logger.LogInformation("Reset {Count} vehicles to default state", resetCount);

        return Ok(new
        {
            success = true,
            message = $"Reset {resetCount} vehicles to default state",
            reset_count = resetCount
        });
    }

    #region Helper Methods

    private void ApplySimulationEvent(VehicleSimulationState vehicle, SimulateEventRequest request)
    {
        switch (request.EventType.ToLowerInvariant())
        {
            case "start_charging":
                vehicle.IsCharging = true;
                vehicle.ChargingState = "Charging";
                vehicle.ChargeRate = request.Value ?? 48;
                break;

            case "stop_charging":
                vehicle.IsCharging = false;
                vehicle.ChargingState = "Complete";
                vehicle.ChargeRate = 0;
                break;

            case "set_battery":
                vehicle.BatteryLevel = Math.Max(0, Math.Min(100, request.Value ?? vehicle.BatteryLevel));
                break;

            case "start_trip":
                vehicle.IsMoving = true;
                vehicle.Speed = request.Value ?? 50;
                break;

            case "end_trip":
                vehicle.IsMoving = false;
                vehicle.Speed = null;
                break;

            case "enable_sentry":
                vehicle.SentryMode = true;
                break;

            case "disable_sentry":
                vehicle.SentryMode = false;
                break;

            case "lock_vehicle":
                vehicle.IsLocked = true;
                break;

            case "unlock_vehicle":
                vehicle.IsLocked = false;
                break;

            case "start_climate":
                vehicle.IsClimateOn = true;
                break;

            case "stop_climate":
                vehicle.IsClimateOn = false;
                break;

            default:
                throw new ArgumentException($"Unknown event type: {request.EventType}");
        }

        vehicle.LastUpdate = DateTime.UtcNow;
    }

    #endregion
}

public class SimulateEventRequest
{
    public string EventType { get; set; } = string.Empty;
    public int? Value { get; set; }
    public string? Description { get; set; }
}