using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.Production;

namespace PolarDrive.WebApi.Scheduler;

[ApiController]
[Route("api/[controller]")]
public class TeslaFakeDataReceiverController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly PolarDriveLogger _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IServiceProvider _serviceProvider;

    public TeslaFakeDataReceiverController(PolarDriveDbContext db, IWebHostEnvironment env, IServiceProvider serviceProvider)
    {
        _db = db;
        _env = env;
        _serviceProvider = serviceProvider;
        _logger = new PolarDriveLogger(_db);
    }

    /// <summary>
    /// Riceve i dati dal Tesla Mock Service con validazione avanzata
    /// </summary>
    [HttpPost("ReceiveVehicleData/{vin}")]
    public async Task<IActionResult> ReceiveVehicleData(string vin, [FromBody] JsonElement data)
    {
        const string source = "TeslaFakeDataReceiverController.ReceiveVehicleData";

        // Controllo ambiente
        if (!_env.IsDevelopment())
        {
            await _logger.Warning(source, "Fake data receiver called in non-development environment");
            return BadRequest("This endpoint is only available in development mode");
        }

        try
        {
            // Validazione VIN
            if (string.IsNullOrWhiteSpace(vin) || vin.Length != 17)
            {
                await _logger.Warning(source, $"Invalid VIN format received: {vin}");
                return BadRequest("Invalid VIN format. VIN must be 17 characters long.");
            }

            // Verifica che il VIN esista nel database
            var vehicle = await _db.ClientVehicles
                .Include(v => v.ClientCompany)
                .FirstOrDefaultAsync(v => v.Vin == vin);

            if (vehicle == null)
            {
                await _logger.Warning(source, $"Received data for unknown VIN: {vin}");
                return NotFound($"Vehicle with VIN {vin} not found");
            }

            // Verifica brand Tesla
            if (!vehicle.Brand.Equals("tesla", StringComparison.CurrentCultureIgnoreCase))
            {
                await _logger.Warning(source, $"Received Tesla data for non-Tesla vehicle: {vin} (Brand: {vehicle.Brand})");
                return BadRequest($"Vehicle {vin} is not a Tesla vehicle (Brand: {vehicle.Brand})");
            }

            if (!vehicle.IsFetchingDataFlag)
            {
                await _logger.Info(source, $"Vehicle {vin} is not fetching data. Ignoring.");
                return Ok(new
                {
                    success = true,
                    message = "Vehicle not fetching data, data ignored",
                    contractStatus = vehicle.IsActiveFlag ? "Contract active" : "Contract terminated - data collection ending soon"
                });
            }

            // Aggiungi warning se contratto scaduto ma dati ancora attivi
            if (!vehicle.IsActiveFlag && vehicle.IsFetchingDataFlag)
            {
                await _logger.Warning(source,
                    $"Collecting data for vehicle {vin} with terminated contract - awaiting client token revocation");
            }

            // Validazione contenuto JSON
            var validationResult = ValidateVehicleData(data);
            if (!validationResult.IsValid)
            {
                await _logger.Warning(source, $"Invalid vehicle data received for VIN {vin}: {validationResult.ErrorMessage}");
                return BadRequest($"Invalid vehicle data: {validationResult.ErrorMessage}");
            }

            // Anonimizza i dati prima del salvataggio
            var rawJsonText = data.GetRawText();
            var anonymizedJson = TeslaDataAnonymizerHelper.AnonymizeVehicleData(rawJsonText);
            await _logger.Info(source,
                $"Received Tesla mock anonymized data for VIN: {vin}",
                $"Data size: {rawJsonText.Length} chars, Company: {vehicle.ClientCompany?.Name}");

            // Controlla limite dati per veicolo (evita spam)
            var recentDataCount = await _db.VehiclesData
                .CountAsync(vd => vd.VehicleId == vehicle.Id &&
                                 vd.Timestamp >= DateTime.UtcNow.AddMinutes(-5));

            if (recentDataCount >= 50) // Max 50 record ogni 5 minuti in dev
            {
                await _logger.Warning(source, $"Rate limit exceeded for vehicle {vin}: {recentDataCount} records in last 5 minutes");
                return StatusCode(429, new
                {
                    success = false,
                    error = "Rate limit exceeded. Too many data points in short time.",
                    recentCount = recentDataCount
                });
            }

            // Salva nel database
            var vehicleDataRecord = new VehicleData
            {
                VehicleId = vehicle.Id,
                Timestamp = DateTime.UtcNow,
                RawJsonAnonymized = anonymizedJson  // ✅ Dati anonimizzati
            };

            _db.VehiclesData.Add(vehicleDataRecord);

            // Aggiorna il timestamp di ultimo aggiornamento
            vehicle.LastDataUpdate = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _logger.Info(source,
                $"Mock Tesla data saved for VIN: {vin}",
                $"Record ID: {vehicleDataRecord.Id}, Timestamp: {vehicleDataRecord.Timestamp}");

            // Statistiche immediate per development
            var totalRecords = await _db.VehiclesData.CountAsync(vd => vd.VehicleId == vehicle.Id);
            var monitoringDuration = await GetMonitoringDuration(vehicle.Id);

            return Ok(new
            {
                success = true,
                message = $"Mock Tesla data received and saved for VIN {vin}",
                recordId = vehicleDataRecord.Id,
                timestamp = vehicleDataRecord.Timestamp,
                vehicleInfo = new
                {
                    vin = vehicle.Vin,
                    model = vehicle.Model,
                    company = vehicle.ClientCompany?.Name,
                    totalRecords = totalRecords,
                    monitoringDuration = $"{monitoringDuration.TotalDays:F1} days"
                },
                developmentInfo = new
                {
                    environment = "Development",
                    mockData = true,
                    dataSize = rawJsonText.Length
                }
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error processing mock Tesla data for VIN {vin}", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error processing vehicle data",
                environment = "Development"
            });
        }
    }

    /// <summary>
    /// Verifica ultimo dato con più dettagli per development
    /// </summary>
    [HttpGet("LastData/{vin}")]
    public async Task<IActionResult> GetLastData(string vin)
    {
        try
        {
            var vehicle = await _db.ClientVehicles
                .Include(v => v.ClientCompany)
                .FirstOrDefaultAsync(v => v.Vin == vin);

            if (vehicle == null)
            {
                return NotFound($"Vehicle with VIN {vin} not found");
            }

            var latestRecord = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicle.Id)
                .OrderByDescending(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var totalRecords = await _db.VehiclesData.CountAsync(vd => vd.VehicleId == vehicle.Id);
            var recentRecords = await _db.VehiclesData
                .CountAsync(vd => vd.VehicleId == vehicle.Id &&
                                 vd.Timestamp >= DateTime.UtcNow.AddHours(-1));

            // Info sui report
            var reports = await _db.PdfReports.CountAsync(r => r.VehicleId == vehicle.Id);
            var monitoringDuration = await GetMonitoringDuration(vehicle.Id);

            return Ok(new
            {
                vin = vehicle.Vin,
                vehicleInfo = new
                {
                    model = vehicle.Model,
                    brand = vehicle.Brand,
                    company = vehicle.ClientCompany?.Name,
                    isActive = vehicle.IsActiveFlag,
                    isFetching = vehicle.IsFetchingDataFlag,
                    isAuthorized = vehicle.ClientOAuthAuthorized
                },
                dataInfo = new
                {
                    lastUpdate = vehicle.LastDataUpdate,
                    latestRecordId = latestRecord?.Id,
                    latestRecordTimestamp = latestRecord?.Timestamp,
                    totalRecords = totalRecords,
                    recentRecords = recentRecords,
                    monitoringDuration = $"{monitoringDuration.TotalDays:F1} days",
                    status = vehicle.LastDataUpdate.HasValue ? "Data received" : "No data yet"
                },
                info = new
                {
                    reports = reports,
                    analysisLevel = DetermineAnalysisLevel(monitoringDuration)
                },
                environment = _env.EnvironmentName
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("TeslaFakeDataReceiverController.GetLastData", $"Error getting last data for VIN {vin}", ex.ToString());
            return StatusCode(500, new { success = false, error = "Error retrieving vehicle data" });
        }
    }

    /// <summary>
    /// Statistiche avanzate per development
    /// </summary>
    [HttpGet("Stats/{vin}")]
    public async Task<IActionResult> GetDataStats(string vin)
    {
        try
        {
            var vehicle = await _db.ClientVehicles
                .Include(v => v.ClientCompany)
                .FirstOrDefaultAsync(v => v.Vin == vin);

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
                    vehicleInfo = new
                    {
                        model = vehicle.Model,
                        company = vehicle.ClientCompany?.Name
                    },
                    stats = new
                    {
                        TotalRecords = 0,
                        FirstRecord = (DateTime?)null,
                        LastRecord = (DateTime?)null,
                        TotalDataSize = 0,
                        MonitoringDuration = "0 days"
                    },
                    environment = _env.EnvironmentName
                });
            }

            // Statistiche più dettagliate
            var stats = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicle.Id)
                .GroupBy(vd => 1)
                .Select(g => new
                {
                    TotalRecords = g.Count(),
                    FirstRecord = (DateTime?)g.Min(vd => vd.Timestamp),
                    LastRecord = (DateTime?)g.Max(vd => vd.Timestamp),
                    TotalDataSize = g.Sum(vd => vd.RawJsonAnonymized.Length),
                    AvgDataSize = g.Average(vd => vd.RawJsonAnonymized.Length)
                })
                .FirstOrDefaultAsync();

            // Statistiche temporali
            var hourlyDistribution = await GetHourlyDataDistribution(vehicle.Id);
            var recentStats = await GetRecentDataStats(vehicle.Id);
            var reportStats = await GetReportStats(vehicle.Id);

            var monitoringDuration = stats?.FirstRecord.HasValue == true
                ? DateTime.UtcNow - stats.FirstRecord.Value
                : TimeSpan.Zero;

            return Ok(new
            {
                vin = vehicle.Vin,
                vehicleInfo = new
                {
                    model = vehicle.Model,
                    brand = vehicle.Brand,
                    company = vehicle.ClientCompany?.Name,
                    isActive = vehicle.IsActiveFlag,
                    isFetching = vehicle.IsFetchingDataFlag
                },
                stats = new
                {
                    TotalRecords = stats?.TotalRecords ?? 0,
                    FirstRecord = stats?.FirstRecord,
                    LastRecord = stats?.LastRecord,
                    TotalDataSize = stats?.TotalDataSize ?? 0,
                    AvgDataSize = Math.Round(stats?.AvgDataSize ?? 0, 2),
                    MonitoringDuration = $"{monitoringDuration.TotalDays:F1} days",
                    DataRate = CalculateDataRate(stats?.TotalRecords ?? 0, monitoringDuration)
                },
                recentActivity = recentStats,
                hourlyDistribution = hourlyDistribution,
                analysis = new
                {
                    reports = reportStats,
                    currentLevel = DetermineAnalysisLevel(monitoringDuration),
                    nextLevelIn = GetTimeToNextLevel(monitoringDuration)
                },
                environment = _env.EnvironmentName
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("TeslaFakeDataReceiverController.GetDataStats", $"Error getting stats for VIN {vin}", ex.ToString());
            return StatusCode(500, new { success = false, error = "Error retrieving vehicle statistics" });
        }
    }

    /// <summary>
    /// Endpoint per statistiche globali development
    /// </summary>
    [HttpGet("GlobalStats")]
    public async Task<IActionResult> GetGlobalStats()
    {
        try
        {
            if (!_env.IsDevelopment())
            {
                return BadRequest("This endpoint is only available in development mode");
            }

            var totalVehicles = await _db.ClientVehicles.CountAsync();
            var activeVehicles = await _db.ClientVehicles.CountAsync(v => v.IsActiveFlag);
            var fetchingVehicles = await _db.ClientVehicles.CountAsync(v => v.IsFetchingDataFlag);
            var teslaVehicles = await _db.ClientVehicles.CountAsync(v => v.Brand.ToLower() == "tesla");

            var totalDataRecords = await _db.VehiclesData.CountAsync();
            var recentDataRecords = await _db.VehiclesData
                .CountAsync(vd => vd.Timestamp >= DateTime.UtcNow.AddHours(-1));

            var reports = await _db.PdfReports.CountAsync();
            var serviceStatus = await CheckDevelopmentServices();

            return Ok(new
            {
                timestamp = DateTime.UtcNow,
                environment = _env.EnvironmentName,
                vehicles = new
                {
                    total = totalVehicles,
                    active = activeVehicles,
                    fetching = fetchingVehicles,
                    tesla = teslaVehicles
                },
                data = new
                {
                    totalRecords = totalDataRecords,
                    recentRecords = recentDataRecords,
                    avgRecordsPerVehicle = fetchingVehicles > 0 ? totalDataRecords / fetchingVehicles : 0
                },
                reports = reports,
                services = serviceStatus
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("TeslaFakeDataReceiverController.GetGlobalStats", "Error getting global stats", ex.ToString());
            return StatusCode(500, new { success = false, error = "Error retrieving global statistics" });
        }
    }

    #region Helper Methods

    /// <summary>
    /// Validazione dati veicolo
    /// </summary>
    private DataValidationResult ValidateVehicleData(JsonElement data)
    {
        try
        {
            var rawText = data.GetRawText();

            if (string.IsNullOrWhiteSpace(rawText))
                return new DataValidationResult { IsValid = false, ErrorMessage = "Empty data received" };

            if (rawText.Length > 1_000_000) // 1MB limit per sicurezza
                return new DataValidationResult { IsValid = false, ErrorMessage = "Data size exceeds limit" };

            if (rawText.Length < 50) // Minimo ragionevole per dati Tesla
                return new DataValidationResult { IsValid = false, ErrorMessage = "Data too small to be valid Tesla data" };

            // Verifica che sia JSON valido
            if (data.ValueKind == JsonValueKind.Null || data.ValueKind == JsonValueKind.Undefined)
                return new DataValidationResult { IsValid = false, ErrorMessage = "Invalid JSON format" };

            return new DataValidationResult { IsValid = true };
        }
        catch (Exception ex)
        {
            return new DataValidationResult { IsValid = false, ErrorMessage = $"Validation error: {ex.Message}" };
        }
    }

    /// <summary>
    /// Calcola durata monitoraggio
    /// </summary>
    private async Task<TimeSpan> GetMonitoringDuration(int vehicleId)
    {
        var firstRecord = await _db.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId)
            .MinAsync(vd => (DateTime?)vd.Timestamp);

        return firstRecord.HasValue ? DateTime.UtcNow - firstRecord.Value : TimeSpan.Zero;
    }

    /// <summary>
    /// Determina livello di analisi
    /// </summary>
    private string DetermineAnalysisLevel(TimeSpan monitoringDuration)
    {
        return monitoringDuration.TotalMinutes switch
        {
            < 5 => "Valutazione Iniziale",
            < 15 => "Analisi Rapida",
            < 30 => "Pattern Recognition",
            < 60 => "Behavioral Analysis",
            _ => "Deep Dive Analysis"
        };
    }

    /// <summary>
    /// Calcola tempo al prossimo livello
    /// </summary>
    private string GetTimeToNextLevel(TimeSpan monitoringDuration)
    {
        var nextThreshold = monitoringDuration.TotalMinutes switch
        {
            < 5 => TimeSpan.FromMinutes(5),
            < 15 => TimeSpan.FromMinutes(15),
            < 30 => TimeSpan.FromMinutes(30),
            < 60 => TimeSpan.FromHours(1),
            _ => TimeSpan.Zero
        };

        if (nextThreshold == TimeSpan.Zero)
            return "Max level reached";

        var timeToNext = nextThreshold - monitoringDuration;
        return timeToNext.TotalMinutes > 0 ? $"{timeToNext.TotalMinutes:F0} minutes" : "Ready for next level";
    }

    /// <summary>
    /// Statistiche distribuzione oraria
    /// </summary>
    private async Task<object> GetHourlyDataDistribution(int vehicleId)
    {
        var last24Hours = await _db.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= DateTime.UtcNow.AddHours(-24))
            .GroupBy(vd => vd.Timestamp.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .OrderBy(x => x.Hour)
            .ToListAsync();

        return last24Hours;
    }

    /// <summary>
    /// Statistiche dati recenti
    /// </summary>
    private async Task<object> GetRecentDataStats(int vehicleId)
    {
        var now = DateTime.UtcNow;
        return new
        {
            Last5Minutes = await _db.VehiclesData.CountAsync(vd => vd.VehicleId == vehicleId && vd.Timestamp >= now.AddMinutes(-5)),
            Last15Minutes = await _db.VehiclesData.CountAsync(vd => vd.VehicleId == vehicleId && vd.Timestamp >= now.AddMinutes(-15)),
            LastHour = await _db.VehiclesData.CountAsync(vd => vd.VehicleId == vehicleId && vd.Timestamp >= now.AddHours(-1)),
            Last24Hours = await _db.VehiclesData.CountAsync(vd => vd.VehicleId == vehicleId && vd.Timestamp >= now.AddHours(-24))
        };
    }

    /// <summary>
    /// Statistiche report
    /// </summary>
    private async Task<object> GetReportStats(int vehicleId)
    {
        var totalReports = await _db.PdfReports.CountAsync(r => r.VehicleId == vehicleId);

        return new
        {
            Total = totalReports
        };
    }

    /// <summary>
    /// Calcola rate di dati
    /// </summary>
    private string CalculateDataRate(int totalRecords, TimeSpan duration)
    {
        if (duration.TotalMinutes < 1) return "N/A";

        var recordsPerHour = totalRecords / duration.TotalHours;
        return $"{recordsPerHour:F1} records/hour";
    }

    /// <summary>
    /// Verifica servizi development
    /// </summary>
    private async Task<object> CheckDevelopmentServices()
    {
        try
        {
            // Check VehicleDataService
            var vehicleDataService = _serviceProvider.GetService<VehicleDataService>();

            // Check FakeProductionScheduler
            var fakeScheduler = _serviceProvider.GetService<Scheduler.PolarDriveScheduler>();

            // Check AI service
            var aiAvailable = false;
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(2);
                var response = await client.GetAsync("http://localhost:11434/api/tags");
                aiAvailable = response.IsSuccessStatusCode;
            }
            catch { }

            return new
            {
                VehicleDataService = vehicleDataService != null,
                FakeScheduler = fakeScheduler != null,
                AiService = aiAvailable,
                Database = true // Se arriviamo qui, il DB funziona
            };
        }
        catch (Exception ex)
        {
            await _logger.Warning("TeslaFakeDataReceiverController.CheckDevelopmentServices", "Error checking services", ex.Message);
            return new { Error = "Unable to check services" };
        }
    }

    #endregion
}

/// <summary>
/// Risultato validazione dati
/// </summary>
public class DataValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}