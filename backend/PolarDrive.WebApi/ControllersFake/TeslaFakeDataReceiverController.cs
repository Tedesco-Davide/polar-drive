using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.ControllersFake;

[ApiController]
[Route("api/[controller]")]
public class TeslaFakeDataReceiverController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly PolarDriveLogger _logger;

    public TeslaFakeDataReceiverController(PolarDriveDbContext db)
    {
        _db = db;
        _logger = new PolarDriveLogger(_db);
    }

    /// <summary>
    /// Riceve i dati dal Tesla Mock Service e li salva come RawJson
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

            // Verifica che il veicolo sia attivo e in fetching
            if (!vehicle.IsActiveFlag || !vehicle.IsFetchingDataFlag)
            {
                await _logger.Info(source, $"Vehicle {vin} is not active or not fetching data. Ignoring.");
                return Ok(new { success = true, message = "Vehicle not active/fetching, data ignored" });
            }

            // Log e salva il JSON grezzo
            var rawJsonText = data.GetRawText();
            await _logger.Info(source,
                $"Received Tesla data for VIN: {vin}",
                $"Data size: {rawJsonText.Length} chars");

            // Salva nel database
            var vehicleDataRecord = new VehicleData
            {
                VehicleId = vehicle.Id,
                Timestamp = DateTime.UtcNow,
                RawJson = rawJsonText
            };

            _db.VehiclesData.Add(vehicleDataRecord);

            // Aggiorna il timestamp di ultimo aggiornamento
            vehicle.LastDataUpdate = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _logger.Info(source,
                $"Raw JSON data saved for VIN: {vin}",
                $"Record ID: {vehicleDataRecord.Id}, Timestamp: {vehicleDataRecord.Timestamp}");

            return Ok(new
            {
                success = true,
                message = $"Data received and saved for VIN {vin}",
                recordId = vehicleDataRecord.Id,
                timestamp = vehicleDataRecord.Timestamp
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

        var latestRecord = await _db.VehiclesData
            .Where(vd => vd.VehicleId == vehicle.Id)
            .OrderByDescending(vd => vd.Timestamp)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            vin = vehicle.Vin,
            lastUpdate = vehicle.LastDataUpdate,
            latestRecordId = latestRecord?.Id,
            latestRecordTimestamp = latestRecord?.Timestamp,
            totalRecords = await _db.VehiclesData.CountAsync(vd => vd.VehicleId == vehicle.Id),
            status = vehicle.LastDataUpdate.HasValue ? "Data received" : "No data yet"
        });
    }

    /// <summary>
    /// GET endpoint per ottenere statistiche sui dati ricevuti
    /// </summary>
    [HttpGet("Stats/{vin}")]
    public async Task<IActionResult> GetDataStats(string vin)
    {
        var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Vin == vin);
        if (vehicle == null)
        {
            return NotFound($"Vehicle with VIN {vin} not found");
        }

        var totalRecords = await _db.VehiclesData.CountAsync(vd => vd.VehicleId == vehicle.Id);

        if (totalRecords == 0)
        {
            return Ok(new
            {
                vin = vehicle.Vin,
                stats = new
                {
                    TotalRecords = 0,
                    FirstRecord = (DateTime?)null,
                    LastRecord = (DateTime?)null,
                    TotalDataSize = 0
                }
            });
        }

        var stats = await _db.VehiclesData
            .Where(vd => vd.VehicleId == vehicle.Id)
            .GroupBy(vd => 1)
            .Select(g => new
            {
                TotalRecords = g.Count(),
                FirstRecord = (DateTime?)g.Min(vd => vd.Timestamp),
                LastRecord = (DateTime?)g.Max(vd => vd.Timestamp),
                TotalDataSize = g.Sum(vd => vd.RawJson.Length)
            })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            vin = vehicle.Vin,
            stats = stats
        });
    }
}