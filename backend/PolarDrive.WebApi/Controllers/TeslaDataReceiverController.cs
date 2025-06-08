using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeslaDataReceiverController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly PolarDriveLogger _logger;

    public TeslaDataReceiverController(PolarDriveDbContext db)
    {
        _db = db;
        _logger = new PolarDriveLogger(_db);
    }

    /// <summary>
    /// Riceve i dati dal Tesla Mock Service ogni ora
    /// </summary>
    [HttpPost("ReceiveVehicleData/{vin}")]
    public async Task<IActionResult> ReceiveVehicleData(string vin, [FromBody] JsonElement data)
    {
        const string source = "TeslaDataReceiverController.ReceiveVehicleData";

        try
        {
            // Verifica che il VIN esista nel database
            var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Vin == vin);
            if (vehicle == null)
            {
                await _logger.Warning(source, $"Received data for unknown VIN: {vin}");
                return NotFound($"Vehicle with VIN {vin} not found");
            }

            // Log dei dati ricevuti per debug
            await _logger.Info(source,
                $"Received Tesla mock data for VIN: {vin}",
                $"Data size: {data.GetRawText().Length} chars");

            // Estrai informazioni chiave dai dati
            var extractedData = ExtractKeyVehicleData(data);

            if (extractedData != null)
            {
                await _logger.Debug(source,
                    $"Extracted vehicle data for {vin}",
                    $"Battery: {extractedData.BatteryLevel}%, Charging: {extractedData.IsCharging}, Location: {extractedData.Latitude},{extractedData.Longitude}");

                // Qui potresti salvare i dati estratti nel database
                // o processarli come necessario per la tua applicazione
                await ProcessVehicleData(vehicle, extractedData);
            }

            return Ok(new
            {
                success = true,
                message = $"Data received for VIN {vin}",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error processing data for VIN {vin}", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error processing vehicle data"
            });
        }
    }

    /// <summary>
    /// Estrae i dati chiave dal JSON complesso del Mock Service
    /// </summary>
    private VehicleDataExtract? ExtractKeyVehicleData(JsonElement data)
    {
        try
        {
            // Il Mock Service invia dati in formato response.data[]
            if (!data.TryGetProperty("response", out var response) ||
                !response.TryGetProperty("data", out var dataArray))
            {
                return null;
            }

            var extract = new VehicleDataExtract();

            // Cerca vehicle_endpoints per i dati del veicolo
            foreach (var item in dataArray.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var type) &&
                    type.GetString() == "vehicle_endpoints" &&
                    item.TryGetProperty("content", out var content))
                {
                    // Estrai dati dalla vehicle_data
                    if (content.TryGetProperty("vehicle_data", out var vehicleData) &&
                        vehicleData.TryGetProperty("response", out var vehicleResponse))
                    {
                        // Battery/Charging info
                        if (vehicleResponse.TryGetProperty("charge_state", out var chargeState))
                        {
                            if (chargeState.TryGetProperty("battery_level", out var battery))
                                extract.BatteryLevel = battery.GetInt32();

                            if (chargeState.TryGetProperty("charging_state", out var charging))
                                extract.ChargingState = charging.GetString();

                            extract.IsCharging = extract.ChargingState == "Charging";
                        }

                        // Location info
                        if (vehicleResponse.TryGetProperty("drive_state", out var driveState))
                        {
                            if (driveState.TryGetProperty("latitude", out var lat))
                                extract.Latitude = lat.GetDecimal();

                            if (driveState.TryGetProperty("longitude", out var lng))
                                extract.Longitude = lng.GetDecimal();

                            if (driveState.TryGetProperty("speed", out var speed))
                                extract.Speed = speed.ValueKind != JsonValueKind.Null ? speed.GetInt32() : null;
                        }

                        // Climate info
                        if (vehicleResponse.TryGetProperty("climate_state", out var climateState))
                        {
                            if (climateState.TryGetProperty("inside_temp", out var insideTemp))
                                extract.InsideTemp = insideTemp.GetDecimal();

                            if (climateState.TryGetProperty("outside_temp", out var outsideTemp))
                                extract.OutsideTemp = outsideTemp.GetDecimal();
                        }

                        // Vehicle state
                        if (vehicleResponse.TryGetProperty("vehicle_state", out var vehicleState))
                        {
                            if (vehicleState.TryGetProperty("locked", out var locked))
                                extract.IsLocked = locked.GetBoolean();

                            if (vehicleState.TryGetProperty("sentry_mode", out var sentry))
                                extract.SentryMode = sentry.GetBoolean();
                        }
                    }
                    break;
                }
            }

            extract.LastUpdated = DateTime.UtcNow;
            return extract;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Processa i dati estratti (aggiorna database, triggerera notifiche, etc.)
    /// </summary>
    private async Task ProcessVehicleData(ClientVehicle vehicle, VehicleDataExtract data)
    {
        // Qui potresti:
        // 1. Aggiornare record del veicolo nel database
        // 2. Salvare telemetria in una tabella separata
        // 3. Triggere notifiche se necessario
        // 4. Calcolare statistiche

        // Esempio: aggiorna ultimo aggiornamento
        vehicle.LastDataUpdate = data.LastUpdated;
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// GET endpoint per verificare l'ultimo dato ricevuto per un VIN
    /// </summary>
    [HttpGet("LastData/{vin}")]
    public async Task<IActionResult> GetLastData(string vin)
    {
        var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Vin == vin);
        if (vehicle == null)
        {
            return NotFound($"Vehicle with VIN {vin} not found");
        }

        return Ok(new
        {
            vin = vehicle.Vin,
            last_update = vehicle.LastDataUpdate,
            status = vehicle.LastDataUpdate.HasValue ? "Data received" : "No data yet"
        });
    }
}

/// <summary>
/// Classe per i dati estratti dai payload complessi del Mock Service
/// </summary>
public class VehicleDataExtract
{
    public int BatteryLevel { get; set; }
    public string? ChargingState { get; set; }
    public bool IsCharging { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int? Speed { get; set; }
    public decimal InsideTemp { get; set; }
    public decimal OutsideTemp { get; set; }
    public bool IsLocked { get; set; }
    public bool SentryMode { get; set; }
    public DateTime LastUpdated { get; set; }
}