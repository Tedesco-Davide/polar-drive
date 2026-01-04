using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Diagnostics;
using PolarDrive.WebApi.Helpers;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.PolarAiReports;

// Aggregatore intelligente che processa 720h di dati JSON Tesla, per ottimizzare l'uso di token con PolarAi™
public class IntelligentDataAggregator(
    PolarDriveDbContext dbContext,
    int maxRetryAttempts = 3,
    int baseDelayMs = 1000)
{
    private readonly PolarDriveDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly PolarDriveLogger _logger = new();
    private readonly PolarDriveLoggerFileSpecific _loggerFileSpecific = new("IntelligentDataAggregator");
    private readonly int _maxRetryAttempts = maxRetryAttempts;
    private readonly TimeSpan _baseDelay = TimeSpan.FromMilliseconds(baseDelayMs);
    private readonly Random _jitterRandom = new();
    private readonly Stopwatch _totalStopwatch = new();
    private readonly HashSet<string> _processedRecords = [];
    private readonly List<ProcessingError> _processingErrors = [];
    private readonly Dictionary<string, int> _processingStats = [];
    private readonly Dictionary<string, TimeSpan> _processingTimes = [];
    private const int MaxListSize = 200;

    // Reservoir Sampling: limita liste a MaxListSize elementi mantenendo rappresentatività statistica.
    // Primi 200 valori → aggiunti direttamente. Successivi → probabilità decrescente di sostituire uno esistente.
    // Risultato: campione uniforme di tutti i record processati, RAM costante indipendentemente dal volume dati.
    private static void AddWithLimit<T>(List<T> list, T value)
    {
        if (list.Count < MaxListSize)
            list.Add(value);
        else if (Random.Shared.Next(list.Count) < MaxListSize)
            list[Random.Shared.Next(MaxListSize)] = value;
    }

    public async Task<(string text, GoogleAdsTeslaDataAggregation data)> GenerateGoogleAdsAggregatedInsights(List<string> rawJsonAnonymizedList, int vehicleId)
    {
        var source = "IntelligentDataAggregator.GenerateAggregatedInsights";
        _totalStopwatch.Start();

        await _logger.Info(source, $"Processando {rawJsonAnonymizedList.Count} record per aggregazione completa Google Ads", $"VehicleId: {vehicleId}");
        LogProcessingStep("Initialize", $"Inizializzato processamento per {rawJsonAnonymizedList.Count} JSON");

        var aggregation = new GoogleAdsTeslaDataAggregation();
        var processedRecords = 0;

        // Processa ogni JSON e aggrega tutti i dati
        foreach (var rawJsonAnonymous in rawJsonAnonymizedList)
        {
            var recordStopwatch = Stopwatch.StartNew();
            try
            {
                if (string.IsNullOrWhiteSpace(rawJsonAnonymous))
                {
                    LogProcessingStep("SkipEmpty", "JSON vuoto saltato");
                    continue;
                }

                // Scarta righe non-JSON (evita lavoro inutile alla sanitizzazione)
                var span = rawJsonAnonymous.AsSpan().TrimStart();
                if (!(span.StartsWith("{") || span.StartsWith("[")))
                {
                    LogProcessingStep("SkipNonJson", "Record non JSON");
                    continue;
                }

                var fixes = new List<string>();
                var sanitizedJson = SanitizeJsonAggressive(rawJsonAnonymous, msg => fixes.Add(msg));

                var jsonHash = GenericHelpers.ComputeContentHash(sanitizedJson);

                if (_processedRecords.Contains(jsonHash))
                {
                    LogProcessingStep("SkipDuplicate", $"JSON duplicato saltato (hash: {jsonHash[..8]})");
                    continue;
                }

                _processedRecords.Add(jsonHash);

                using var doc = JsonDocument.Parse(
                    sanitizedJson,
                    new JsonDocumentOptions { AllowTrailingCommas = true }
                );
                var root = doc.RootElement;

                if (root.TryGetProperty("response", out var response) &&
                    response.TryGetProperty("data", out var dataArray) &&
                    dataArray.ValueKind == JsonValueKind.Array)
                {
                    var itemsProcessed = 0;
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        if (!item.TryGetProperty("type", out var typeProp) ||
                            !item.TryGetProperty("content", out var content))
                            continue;

                        var type = typeProp.GetString();
                        var itemStopwatch = Stopwatch.StartNew();

                        switch (type)
                        {
                            case "vehicle_endpoints":
                                // Prendiamo in considerazione solo endpoints necessari per campagne Google Ads
                                ProcessVehicleEndpointsGoogleAds(content, aggregation);
                                LogProcessingStep("VehicleEndpoints", $"Processati vehicle endpoints per Google Ads");
                                break;
                            case "user_profile":
                                // Ignoriamo esplicitamente per rispettare Tesla Fleet Api
                                break;
                                // Ignoriamo questi tipi per Google Ads
                            case "charging_history":
                            case "energy_endpoints":
                            case "partner_public_key":
                            case "vehicle_commands":
                                break;
                            default:
                                if (!"user_profile".Equals(type, StringComparison.OrdinalIgnoreCase))
                                    LogProcessingStep("UnknownType", $"Tipo sconosciuto: {type}");
                                break;
                        }

                        itemStopwatch.Stop();
                        if (itemStopwatch.ElapsedMilliseconds > 100) // Log solo operazioni lente
                        {
                            await _logger.Warning(source, $"Operazione lenta: {type}",
                                $"Tempo: {itemStopwatch.ElapsedMilliseconds}ms");
                        }

                        itemsProcessed++;
                    }

                    LogProcessingStep("JsonCompleted", $"JSON processato con {itemsProcessed} items");
                }

                recordStopwatch.Stop();
                processedRecords++;

                // Log progresso ogni 100 record
                if (processedRecords % 100 == 0)
                {
                    LogProcessingStep("Progress",
                        $"Processati {processedRecords}/{rawJsonAnonymizedList.Count} JSON " +
                        $"(Velocità media: {processedRecords / _totalStopwatch.Elapsed.TotalSeconds:F1} JSON/sec)");
                    GC.Collect(0, GCCollectionMode.Optimized, false);
                }
            }
            catch (JsonException ex)
            {
                await HandleException(ex, $"Processing JSON record {processedRecords}", "JsonDocument.Parse");
            }
            catch (ArgumentException ex)
            {
                await HandleException(ex, $"Processing JSON record {processedRecords}", "Data validation");
            }
            catch (InvalidOperationException ex)
            {
                await HandleException(ex, $"Processing JSON record {processedRecords}", "Business logic");
            }
            catch (Exception ex)
            {
                await HandleException(ex, $"Processing JSON record {processedRecords}", "Unknown operation");
            }
            finally
            {
                recordStopwatch?.Stop();
            }
        }

        _processedRecords.Clear();

        // Aggiungi dati SMS Adaptive Profile
        await AddAdaptiveProfileDataComplete(vehicleId, aggregation);

        // Genera output ottimizzato
        var result = await GenerateGoogleAdsOptimizedPromptDataAsync(aggregation, processedRecords, vehicleId);

        // Log riduzione token
        var totalChars = rawJsonAnonymizedList.Sum(j => j?.Length ?? 0);
        var saved = Math.Max(0, totalChars - (result?.Length ?? 0));
        var reduction = totalChars > 0 ? (saved * 100.0 / totalChars) : 0.0;

        _totalStopwatch.Stop();

        // Log statistiche dettagliate
        await LogProcessingPhase("TotalProcessing", processedRecords, _totalStopwatch.Elapsed);

        if (_processingStats.Count != 0)
        {
            var topOperations = _processingStats.OrderByDescending(kvp => kvp.Value).Take(5);
            await _logger.Info(source, "Top operazioni eseguite:",
                string.Join(", ", topOperations.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
        }

        await _logger.Info(source,
            $"Aggregazione Google Ads completata: {processedRecords} record → {(result?.Length ?? 0)} caratteri",
            $"Tempo totale: {_totalStopwatch.Elapsed.TotalSeconds:F2}s, " +
            $"Velocità: {processedRecords / Math.Max(0.001, _totalStopwatch.Elapsed.TotalSeconds):F1} record/sec");

        return (result ?? string.Empty, aggregation);
    }

    #region PROCESSAMENTO DATI

    private static void ProcessVehicleEndpointsGoogleAds(JsonElement content, GoogleAdsTeslaDataAggregation aggregation)
    {
        if (content.ValueKind != JsonValueKind.Object) return;

        if (content.TryGetProperty("vehicle_data", out var vehicleData) &&
            vehicleData.TryGetProperty("response", out var vdResponse))
        {
            ProcessVehicleDataGoogleAds(vdResponse, aggregation);
        }
    }

    private static void ProcessVehicleDataGoogleAds(JsonElement vdResponse, GoogleAdsTeslaDataAggregation aggregation)
    {
        // DRIVE STATE - solo essenziali
        if (vdResponse.TryGetProperty("drive_state", out var driveState))
        {
            var speed = GetSafeIntValue(driveState, "speed");
            if (speed > 0 && DataValidator.IsValidSpeed(speed))
                AddWithLimit(aggregation.Speeds, speed);

            var odometer = GetSafeDecimalValue(driveState, "odometer");
                if (odometer > 0)
                {
                    if (odometer < aggregation.OdometerMin) aggregation.OdometerMin = odometer;
                    if (odometer > aggregation.OdometerMax) aggregation.OdometerMax = odometer;
                }

            var heading = GetSafeIntValue(driveState, "heading");
            if (heading >= 0 && heading <= 360)
                AddWithLimit(aggregation.Headings, heading);

            var shiftState = GetSafeStringValue(driveState, "shift_state");
            if (!string.IsNullOrEmpty(shiftState))
            {
                var normalized = DataNormalizer.NormalizeText(shiftState);
                aggregation.ShiftStates[normalized] = aggregation.ShiftStates.GetValueOrDefault(normalized) + 1;
            }

            var latitude = GetSafeDecimalValue(driveState, "latitude");
            var longitude = GetSafeDecimalValue(driveState, "longitude");

            if (latitude != 0 && longitude != 0)
            {
                var zone = GetGeographicZone(latitude);
                aggregation.GeographicZones[zone] = aggregation.GeographicZones.GetValueOrDefault(zone) + 1;
            }
        }

        // CHARGE STATE - solo core metrics
        if (vdResponse.TryGetProperty("charge_state", out var chargeState))
        {
            var batteryLevel = GetSafeIntValue(chargeState, "battery_level");
            if (batteryLevel > 0 && DataValidator.IsValidBatteryLevel(batteryLevel))
                AddWithLimit(aggregation.BatteryLevels, batteryLevel);

            var batteryRange = GetSafeDecimalValue(chargeState, "battery_range");
            if (batteryRange > 0)
                AddWithLimit(aggregation.BatteryRanges, DataNormalizer.NormalizeDistance(batteryRange));

            var chargingState = GetSafeStringValue(chargeState, "charging_state");
            if (!string.IsNullOrEmpty(chargingState))
            {
                var normalized = DataNormalizer.NormalizeText(chargingState);
                aggregation.ChargingStates[normalized] = aggregation.ChargingStates.GetValueOrDefault(normalized) + 1;
            }

            var chargeRate = GetSafeDecimalValue(chargeState, "charge_rate");
            if (chargeRate >= 0 && chargeRate <= 250)
                AddWithLimit(aggregation.ChargeRates, chargeRate);

            var chargeLimit = GetSafeIntValue(chargeState, "charge_limit_soc");
            if (chargeLimit > 0 && DataValidator.IsValidBatteryLevel(chargeLimit))
                AddWithLimit(aggregation.ChargeLimits, chargeLimit);

            if (GetSafeBooleanValue(chargeState, "fast_charger_present"))
                aggregation.FastChargerUsageCount++;
        }

        // CLIMATE STATE - solo comfort essentials
        if (vdResponse.TryGetProperty("climate_state", out var climateState))
        {
            var insideTemp = GetSafeDecimalValue(climateState, "inside_temp");
            if (insideTemp != 0)
            {
                var normalized = DataNormalizer.NormalizeTemperature(insideTemp);
                if (DataValidator.IsValidTemperature(normalized))
                    AddWithLimit(aggregation.InsideTemperatures, normalized);
            }

            var outsideTemp = GetSafeDecimalValue(climateState, "outside_temp");
            if (outsideTemp != 0)
            {
                var normalized = DataNormalizer.NormalizeTemperature(outsideTemp);
                if (DataValidator.IsValidTemperature(normalized))
                    AddWithLimit(aggregation.OutsideTemperatures, normalized);
            }

            var isClimateOn = GetSafeBooleanValue(climateState, "is_climate_on");
            aggregation.ClimateUsage[isClimateOn] = aggregation.ClimateUsage.GetValueOrDefault(isClimateOn) + 1;
        }

        // VEHICLE STATE - security & TPMS
        if (vdResponse.TryGetProperty("vehicle_state", out var vehicleState))
        {
            var locked = GetSafeBooleanValue(vehicleState, "locked");
            var sentryMode = GetSafeBooleanValue(vehicleState, "sentry_mode");

            aggregation.SecurityStates["locked"] = aggregation.SecurityStates.GetValueOrDefault("locked") + (locked ? 1 : 0);
            aggregation.SecurityStates["sentry"] = aggregation.SecurityStates.GetValueOrDefault("sentry") + (sentryMode ? 1 : 0);

            var tpmsFL = GetSafeDecimalValue(vehicleState, "tpms_pressure_fl");
            var tpmsFR = GetSafeDecimalValue(vehicleState, "tpms_pressure_fr");
            var tpmsRL = GetSafeDecimalValue(vehicleState, "tpms_pressure_rl");
            var tpmsRR = GetSafeDecimalValue(vehicleState, "tpms_pressure_rr");

            foreach (var pressure in new[] { tpmsFL, tpmsFR, tpmsRL, tpmsRR })
            {
                if (pressure > 0 && DataValidator.IsValidTirePressure(pressure))
                    AddWithLimit(aggregation.TirePressures, pressure);
            }
        }

        // VEHICLE CONFIG - segmentation
        if (vdResponse.TryGetProperty("vehicle_config", out var vehicleConfig))
        {
            aggregation.VehicleModel = GetSafeStringValue(vehicleConfig, "car_type");
            aggregation.ExteriorColor = GetSafeStringValue(vehicleConfig, "exterior_color");
            aggregation.EfficiencyPackage = GetSafeStringValue(vehicleConfig, "efficiency_package");

            var driverAssist = GetSafeStringValue(vehicleConfig, "driver_assist");
            if (!string.IsNullOrEmpty(driverAssist))
            {
                aggregation.AdasFeatures[driverAssist] = aggregation.AdasFeatures.GetValueOrDefault(driverAssist) + 1;
            }
        }
    }

    private static string GetGeographicZone(decimal lat)
    {
        // Dividi Italia in zone molto ampie (>50 km di differenza tra zone)
        return lat switch
        {
            > 45.0m => "Nord Italia",
            > 43.0m => "Centro-Nord Italia",
            > 41.0m => "Centro Italia",
            _ => "Sud Italia"
        };
    }

    #endregion

    #region ADAPTIVE PROFILE SMS

    private async Task AddAdaptiveProfileDataComplete(int vehicleId, GoogleAdsTeslaDataAggregation aggregation)
    {
        try
        {
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var adaptiveSessions = await ExecuteWithRetry(() =>
                _dbContext.SmsAdaptiveProfile
                    .Where(e => e.VehicleId == vehicleId && e.ReceivedAt >= thirtyDaysAgo)
                    .OrderByDescending(e => e.ReceivedAt)
                    .ToListAsync());

            aggregation.AdaptiveSessionsCount = adaptiveSessions.Count(s => s.ParsedCommand == SmsCommand.ADAPTIVE_PROFILE_ON);
            var offSessions = adaptiveSessions.Count(s => s.ParsedCommand == SmsCommand.ADAPTIVE_PROFILE_OFF);
            aggregation.AdaptiveSessionsStoppedManually = offSessions;
            aggregation.AdaptiveSessionsStoppedAutomatically = Math.Max(0, aggregation.AdaptiveSessionsCount - offSessions);

            aggregation.HasActiveAdaptiveSession = await ExecuteWithRetry(() => GetActiveAdaptiveSession(vehicleId)) != null;

            if (adaptiveSessions.Count != 0)
            {
                // Analisi pattern orari
                var sessionsByHour = adaptiveSessions
                    .Where(s => s.ParsedCommand == SmsCommand.ADAPTIVE_PROFILE_ON)
                    .GroupBy(s => s.ReceivedAt.Hour)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();

                if (sessionsByHour != null)
                {
                    aggregation.MostActiveAdaptiveHour = sessionsByHour.Key;
                }

                // Frequenza di utilizzo
                var totalDays = Math.Max(1, (DateTime.Now - adaptiveSessions.Min(e => e.ReceivedAt)).TotalDays);
                var frequency = aggregation.AdaptiveSessionsCount / totalDays;

                aggregation.AdaptiveFrequencyAnalysis = frequency switch
                {
                    >= 1 => "Uso quotidiano",
                    >= 0.5 => "Uso frequente",
                    >= 0.2 => "Uso regolare",
                    _ => "Uso occasionale"
                };

                aggregation.AdaptiveFrequencyValue = frequency;
            }

            // Conteggio dati raccolti durante sessioni adaptive
            aggregation.AdaptiveDataRecordsCount =
                    await ExecuteWithRetry(() => _dbContext.VehiclesData
                    .Where(d => d.VehicleId == vehicleId && d.IsSmsAdaptiveProfile)
                    .CountAsync());

            var sessionsByDay = adaptiveSessions
            .Where(s => s.ParsedCommand == "ADAPTIVE_PROFILE_ON")
            .GroupBy(s => s.ReceivedAt.DayOfWeek.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

            foreach (var kvp in sessionsByDay)
            {
                aggregation.AdaptiveSessionsByDay[kvp.Key] = kvp.Value;
            }
        }
        catch (Exception)
        {
            // Gestione errore silente
        }
    }

    private async Task<SmsAdaptiveProfile?> GetActiveAdaptiveSession(int vehicleId)
    {
        var fourHoursAgo = DateTime.Now.AddHours(-4);

        return await ExecuteWithRetry(() =>
            _dbContext.SmsAdaptiveProfile
                .Where(e => e.VehicleId == vehicleId
                        && e.ParsedCommand == SmsCommand.ADAPTIVE_PROFILE_ON
                        && e.ReceivedAt >= fourHoursAgo)
                .OrderByDescending(e => e.ReceivedAt)
                .FirstOrDefaultAsync());
    }

    #endregion

    #region GENERAZIONE OUTPUT OTTIMIZZATO

    private async Task<string> GenerateGoogleAdsOptimizedPromptDataAsync(GoogleAdsTeslaDataAggregation aggregation, int processedRecords, int vehicleId)
    {
        var sb = new StringBuilder();
        var (firstUtc, lastUtc, totalRecords, realDensity) = await GetLightStatsAsync(vehicleId);

        sb.AppendLine("# TESLA SIGNALS - GOOGLE ADS OPTIMIZED");
        sb.AppendLine($"**Records**: {processedRecords} processed");
        sb.AppendLine($"**Period**: {firstUtc:yyyy-MM-dd HH:mm} → {lastUtc:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        // DRIVING
        if (aggregation.Speeds.Count != 0)
        {
            sb.AppendLine("### 🚗 DRIVING");
            sb.AppendLine($"- Avg Speed: {aggregation.AvgSpeed:F1} km/h");
            
            if (aggregation.OdometerMax > aggregation.OdometerMin)
            {
                aggregation.TotalKilometers = aggregation.OdometerMax - aggregation.OdometerMin;
                sb.AppendLine($"- Total Distance: {aggregation.TotalKilometers:F0} km");
                sb.AppendLine($"- Odometer Range: {aggregation.OdometerMin:F0}-{aggregation.OdometerMax:F0} km");
            }
            
            if (aggregation.GeographicZones.Count != 0)
            {
                var topZone = aggregation.GeographicZones.OrderByDescending(kvp => kvp.Value).First();
                sb.AppendLine($"- Primary Zone: {topZone.Key}");
            }
        }

        // BATTERY
        if (aggregation.BatteryLevels.Count != 0)
        {
            sb.AppendLine("### 🔋 BATTERY");
            sb.AppendLine($"- Avg Level: {aggregation.BatteryLevelAvg:F1}%");
            sb.AppendLine($"- Avg Range: {aggregation.BatteryRanges.Average():F1} km");
            sb.AppendLine($"- Fast Charger Usage: {aggregation.FastChargerUsageCount} times");
        }

        // CLIMATE
        if (aggregation.InsideTemperatures.Count != 0)
        {
            sb.AppendLine("### 🌡️ CLIMATE");
            sb.AppendLine($"- Climate Active: {aggregation.ClimateUsagePercent:F1}% of time");
            sb.AppendLine($"- Avg Inside: {aggregation.InsideTemperatures.Average():F1}°C");
        }

        // SECURITY
        if (aggregation.SecurityStates.Count != 0)
        {
            sb.AppendLine("### 🔒 SECURITY");
            sb.AppendLine($"- Locked: {aggregation.SecurityUsagePercent:F1}% of time");
        }

        // CONFIG
        if (!string.IsNullOrEmpty(aggregation.VehicleModel))
        {
            sb.AppendLine("### 🚙 CONFIG");
            sb.AppendLine($"- Model: {aggregation.VehicleModel}");
            sb.AppendLine($"- Color: {aggregation.ExteriorColor}");
        }

        // ADAPTIVE
        if (aggregation.AdaptiveSessionsCount > 0 || aggregation.HasActiveAdaptiveSession)
        {
            sb.AppendLine("### 📊 ADAPTIVE PROFILE");
            sb.AppendLine($"- Sessions: {aggregation.AdaptiveSessionsCount} activations");
            sb.AppendLine($"- Stopped: {aggregation.AdaptiveSessionsStoppedManually} manual, {aggregation.AdaptiveSessionsStoppedAutomatically} auto");
            sb.AppendLine($"- Active: {(aggregation.HasActiveAdaptiveSession ? "Yes" : "No")}");

            if (aggregation.MostActiveAdaptiveHour.HasValue)
                sb.AppendLine($"- Peak Hour: {aggregation.MostActiveAdaptiveHour:00}:xx");

            if (aggregation.AdaptiveSessionsByDay.Count != 0)
            {
                var topDays = aggregation.AdaptiveSessionsByDay.OrderByDescending(kvp => kvp.Value).Take(2);
                sb.AppendLine($"- Top Days: {string.Join(", ", topDays.Select(kvp => $"{kvp.Key} ({kvp.Value})"))}");
            }

            if (aggregation.AdaptiveFrequencyValue > 0)
                sb.AppendLine($"- Frequency: {aggregation.AdaptiveFrequencyAnalysis} ({aggregation.AdaptiveFrequencyValue:F2}/day)");

            if (aggregation.AdaptiveDataRecordsCount > 0)
                sb.AppendLine($"- Data Collected: {aggregation.AdaptiveDataRecordsCount:N0} records");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= _maxRetryAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (DbUpdateException ex) when (attempt < _maxRetryAttempts)
            {
                lastException = ex;
                await DelayWithJitter(attempt);
                await HandleException(ex, "Database retry", $"Attempt {attempt + 1}");
            }
            catch (TimeoutException ex) when (attempt < _maxRetryAttempts)
            {
                lastException = ex;
                await DelayWithJitter(attempt);
                await HandleException(ex, "Timeout retry", $"Attempt {attempt + 1}");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Database") && attempt < _maxRetryAttempts)
            {
                lastException = ex;
                await DelayWithJitter(attempt);
                await HandleException(ex, "Database operation retry", $"Attempt {attempt + 1}");
            }
            catch (Exception ex)
            {
                await HandleException(ex, "Non-retryable error", "Final attempt");
                throw;
            }
        }

        if (lastException != null)
            await HandleException(lastException, "All retries failed", "Final failure");

        throw lastException ?? new InvalidOperationException("Operation failed after all retry attempts");
    }

    private async Task DelayWithJitter(int attempt)
    {
        var delay = TimeSpan.FromMilliseconds(
            _baseDelay.TotalMilliseconds * Math.Pow(2, attempt) +
            _jitterRandom.Next(0, 1000)
        );

        await Task.Delay(delay);
    }

    private async Task<(DateTime firstUtc, DateTime lastUtc, int totalRecords, double realDensity)> GetLightStatsAsync(int vehicleId)
    {
        try
        {
            var firstRecord = await ExecuteWithRetry(() =>
                _dbContext.VehiclesData
                    .Where(v => v.VehicleId == vehicleId)
                    .OrderBy(v => v.Timestamp)
                    .Select(v => v.Timestamp)
                    .FirstOrDefaultAsync());

            var lastRecord = await ExecuteWithRetry(() =>
                _dbContext.VehiclesData
                    .Where(v => v.VehicleId == vehicleId)
                    .OrderByDescending(v => v.Timestamp)
                    .Select(v => v.Timestamp)
                    .FirstOrDefaultAsync());

            var total = await ExecuteWithRetry(() =>
                _dbContext.VehiclesData
                    .Where(v => v.VehicleId == vehicleId)
                    .CountAsync());

            if (firstRecord == default || lastRecord == default || total == 0)
                return (default, default, total, 0);

            var totalHoursReal = Math.Max(1.0, (lastRecord - firstRecord).TotalHours);
            var density = total / totalHoursReal;
            return (firstRecord, lastRecord, total, density);
        }
        catch
        {
            return (default, default, 0, 0);
        }
    }

    #endregion

    #region CLASSI DI VALIDAZIONE

    public static class DataValidator
    {
        public static bool IsValidBatteryLevel(int level) => level >= 0 && level <= 100;

        public static bool IsValidSpeed(int speed) => speed >= 0 && speed <= 300; // km/h realistici

        public static bool IsValidTemperature(decimal temp) => temp >= -50 && temp <= 70; // Celsius

        public static bool IsValidTirePressure(decimal pressure) => pressure >= 1.0m && pressure <= 5.0m; // bar
    }

    public static class DataNormalizer
    {
        // Normalizzazione temperature (Fahrenheit → Celsius se necessario)
        public static decimal NormalizeTemperature(decimal temp)
        {
            // Se temperatura sembra essere in Fahrenheit (>50°), converti
            if (temp > 50)
            {
                return Math.Round((temp - 32) * 5 / 9, 1);
            }
            return Math.Round(temp, 1);
        }

        // Normalizzazione distanze (miglia → km se necessario)
        public static decimal NormalizeDistance(decimal distance)
        {
            // Tesla API può restituire miglia o km dipende dal mercato
            if (distance > 0 && distance < 1000) // Probabilmente miglia
            {
                return Math.Round(distance * 1.609344m, 1);
            }
            return Math.Round(distance, 1);
        }

        public static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            return text.Trim()
                      .Replace('\u00A0', ' ') // Non-breaking space
                      .Replace('\t', ' ')
                      .Replace('\r', ' ')
                      .Replace('\n', ' ')
                      .ToUpperInvariant();
        }

    }

    #endregion

    #region METODI DI SUPPORTO COMPLETI

    private static string SanitizeJsonAggressive(string input, Action<string>? onFix = null)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var sb = new StringBuilder(input.Length + 128);
        var stack = new Stack<char>(); // '{' o '[' per capire dove siamo
        bool inString = false, escape = false;
        bool lastTokenWasValue = false;     // ha appena chiuso un valore (num/true/false/null/string/]/})
        bool lastNonSpaceWasCommaOrOpen = true; // l'ultimo significativo era ',' o '{'/'['
        int i = 0;

        // helper: logga fix
        void Fix(string msg) { onFix?.Invoke(msg); }

        // helper: salta whitespace ma non lo scrive subito
        int PeekNextNonSpace(int from)
        {
            int j = from;
            while (j < input.Length && char.IsWhiteSpace(input[j])) j++;
            return j;
        }

        // helper: quando chiudiamo valore in un oggetto, se dopo non c'è , } ] -> metti la virgola
        void MaybeInsertComma(int lookFrom)
        {
            if (stack.Count == 0 || stack.Peek() != '{') return; // serve solo dentro oggetto
            if (!lastTokenWasValue) return;
            int j = PeekNextNonSpace(lookFrom);
            if (j >= input.Length) return;
            char nx = input[j];
            if (nx == ',' || nx == '}') return; // ok
            // se arriva una chiave/valore senza virgola (", {, [, lettera o numero) metti la virgola
            if (nx == '"' || nx == '{' || nx == '[' || char.IsLetter(nx) || nx == '-' || char.IsDigit(nx))
            {
                sb.Append(',');
                Fix("Inserita virgola mancante dopo un valore in oggetto.");
                lastNonSpaceWasCommaOrOpen = true;
            }
        }

        // helper: pulizia token numerico grezzo
        string CleanNumber(string raw)
        {
            string n = raw.Trim();

            // gestisci simboli strani in coda al decimale: "123.*", "123.x", "123."
            // -> forza almeno una cifra dopo il punto
            if (System.Text.RegularExpressions.Regex.IsMatch(n, @"^\-?\d+\.(?:\D.*|)$"))
            {
                // se non ci sono cifre dopo il punto, metti ".0"
                n = System.Text.RegularExpressions.Regex.Replace(n, @"^(\-?\d+)\.(?!\d)", "$1.0");
                // se c'è ".*" o ".qualcosa-non-digit", tronca a ".0"
                n = System.Text.RegularExpressions.Regex.Replace(n, @"^(\-?\d+)\.[^\d].*$", "$1.0");
            }

            // multipli punti: "123..", "123...5" -> prima occorrenza valida, poi ".0"
            n = System.Text.RegularExpressions.Regex.Replace(n, @"^(\-?\d+)\.{2,}\d*$", "$1.0");

            // notazione scientifica malformata tipo "1.2e*" -> "1.2e0"
            n = System.Text.RegularExpressions.Regex.Replace(n, @"^(\-?\d+(?:\.\d+)?[eE])[^\+\-\d].*$", "${1}0");

            // decimali con virgola: ": 123,45" -> ": 123.45"
            // SOLO se non esistono altri punti e non c'è 'e/E'
            if (System.Text.RegularExpressions.Regex.IsMatch(n, @"^\-?\d+,\d+$"))
            {
                n = n.Replace(',', '.');
                Fix("Convertita virgola decimale in punto.");
            }

            // doppio segno negativo: "--123" -> "-123"
            n = System.Text.RegularExpressions.Regex.Replace(n, @"^\-\-(\d+)$", "-$1");

            // validazione finale: se ancora contiene caratteri non-numerici ammessi, fallback a "0"
            if (!System.Text.RegularExpressions.Regex.IsMatch(n, @"^\-?\d+(?:\.\d+)?(?:[eE][\+\-]?\d+)?$"))
            {
                Fix($"Numero malformato '{raw}' -> impostato a 0.");
                n = "0";
            }

            return n;
        }

        // rimozione BOM
        if (input.Length > 0 && input[0] == '\uFEFF') input = input.Substring(1);

        while (i < input.Length)
        {
            char c = input[i];

            if (inString)
            {
                sb.Append(c);
                if (escape) { escape = false; i++; continue; }
                if (c == '\\') { escape = true; i++; continue; }
                if (c == '"') { inString = false; lastTokenWasValue = true; lastNonSpaceWasCommaOrOpen = false; MaybeInsertComma(i + 1); }
                i++;
                continue;
            }

            // fuori da stringa
            if (char.IsWhiteSpace(c)) { sb.Append(c); i++; continue; }

            if (c == '"')
            {
                inString = true;
                sb.Append(c);
                lastTokenWasValue = false;
                lastNonSpaceWasCommaOrOpen = false;
                i++;
                continue;
            }

            if (c == '{' || c == '[')
            {
                stack.Push(c);
                sb.Append(c);
                lastTokenWasValue = false;
                lastNonSpaceWasCommaOrOpen = true; // appena aperto
                i++;
                continue;
            }

            if (c == '}' || c == ']')
            {
                // elimina eventuale virgola finale prima della chiusura
                int k = sb.Length - 1;
                while (k >= 0 && char.IsWhiteSpace(sb[k])) k--;
                if (k >= 0 && sb[k] == ',')
                {
                    sb.Remove(k, 1);
                    Fix("Rimossa virgola finale prima di '}' o ']'.");
                }

                if (stack.Count > 0) stack.Pop();
                sb.Append(c);
                lastTokenWasValue = true;
                lastNonSpaceWasCommaOrOpen = false;
                // dopo chiusura valore in oggetto, magari serve virgola
                MaybeInsertComma(i + 1);
                i++;
                continue;
            }

            if (c == ',')
            {
                sb.Append(c);
                lastTokenWasValue = false;
                lastNonSpaceWasCommaOrOpen = true;
                i++;
                continue;
            }

            if (c == ':')
            {
                sb.Append(c);
                lastTokenWasValue = false;
                lastNonSpaceWasCommaOrOpen = false;
                i++;
                continue;
            }

            // true / false / null
            if (char.IsLetter(c))
            {
                int j = i;
                while (j < input.Length && char.IsLetter(input[j])) j++;
                string word = input.Substring(i, j - i);

                if (word is "true" or "false" or "null")
                {
                    sb.Append(word);
                    lastTokenWasValue = true;
                    lastNonSpaceWasCommaOrOpen = false;
                    MaybeInsertComma(j);
                    i = j;
                    continue;
                }

                // se entra qui, è una lettera dove ci si aspettava altro: prova a inserire virgola
                if (stack.Count > 0 && stack.Peek() == '{' && !lastNonSpaceWasCommaOrOpen && lastTokenWasValue)
                {
                    sb.Append(',');
                    Fix($"Inserita virgola prima di '{word}'.");
                }

                sb.Append(word);
                i = j;
                continue;
            }

            // NUMERI (o numeri malformati)
            if (c == '-' || char.IsDigit(c))
            {
                int j = i;
                // cattura una "run" di numero + caratteri associati finché non vediamo un terminatore JSON
                while (j < input.Length)
                {
                    char cj = input[j];
                    if (char.IsDigit(cj) || cj == '-' || cj == '+' || cj == '.' || cj == 'e' || cj == 'E') { j++; continue; }
                    // fermati prima di delimitatori o virgolette
                    if (cj == ',' || cj == '}' || cj == ']' || cj == ':' || cj == ' ' || cj == '\t' || cj == '\r' || cj == '\n' || cj == '"'
                        || cj == '{' || cj == '[')
                        break;
                    // include caratteri “strani” nella run per poterli ripulire
                    j++;
                }

                string rawNum = input.Substring(i, j - i);
                string clean = CleanNumber(rawNum);
                if (!rawNum.Equals(clean, StringComparison.Ordinal))
                    Fix($"Numero riparato: '{rawNum}' -> '{clean}'.");

                sb.Append(clean);
                lastTokenWasValue = true;
                lastNonSpaceWasCommaOrOpen = false;

                // se il prossimo token non è , } ] inserisci virgola (solo dentro oggetto)
                MaybeInsertComma(j);

                i = j;
                continue;
            }

            // qualunque altro simbolo “strano”: scrivi com’è (non lo tocchiamo)
            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static string? GetSafeStringValue(JsonElement element, string propertyName, string? defaultValue = "")
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? (prop.GetString() ?? defaultValue)
            : defaultValue;
    }

    private static int GetSafeIntValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.Number ? prop.GetInt32() : 0;
    }

    private static decimal GetSafeDecimalValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.Number ? prop.GetDecimal() : 0m;
    }

    private static bool GetSafeBooleanValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.True;
    }

    #endregion

    #region CODICE PER SCRITTURA SU FILE LOG

    public class ProcessingError
    {
        public ErrorType Type { get; set; }
        public string Message { get; set; } = "";
        public string Details { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public enum ErrorType
    {
        DatabaseError,
        JsonParsingError,
        ValidationError,
        SystemError,
        UnknownError
    }

    private async Task LogProcessingPhase(string phase, int count, TimeSpan elapsed)
    {
        await _logger.Info($"IntelligentDataAggregator.{phase}",
            $"Processati {count} elementi in {elapsed.TotalMilliseconds:F0}ms",
            $"Velocità: {count / Math.Max(0.001, elapsed.TotalSeconds):F1} elementi/sec");
        _processingTimes[phase] = elapsed;
    }

    private void LogProcessingStep(string step, string details)
    {
        // Scrivi su file dedicato (non DB)
        _loggerFileSpecific.Info($"{step}: {details}");

        // Aggiorna statistiche in memoria
        _processingStats[step] = _processingStats.GetValueOrDefault(step) + 1;
    }

    private async Task HandleException(Exception ex, string context, string operation)
    {
        var error = new ProcessingError
        {
            Type = ClassifyException(ex),
            Message = ex.Message,
            Details = $"Context: {context}, Operation: {operation}",
            Timestamp = DateTime.Now
        };

        _processingErrors.Add(error);

        var logLevel = error.Type switch
        {
            ErrorType.DatabaseError => "Error",
            ErrorType.JsonParsingError => "Warning",
            ErrorType.ValidationError => "Debug",
            ErrorType.SystemError => "Error",
            _ => "Warning"
        };

        await _logger.LogByLevel("IntelligentDataAggregator.HandleException",
            $"{error.Type}: {error.Message}", error.Details, logLevel);
    }

    private static ErrorType ClassifyException(Exception ex)
    {
        return ex switch
        {
            TimeoutException or InvalidOperationException when ex.Message.Contains("Database") => ErrorType.DatabaseError,
            DbUpdateException => ErrorType.DatabaseError,
            System.Data.Common.DbException => ErrorType.DatabaseError,
            JsonException or FormatException => ErrorType.JsonParsingError,
            ArgumentException or ArgumentOutOfRangeException => ErrorType.ValidationError,
            OutOfMemoryException or StackOverflowException => ErrorType.SystemError,
            _ => ErrorType.UnknownError
        };
    }

    #endregion
}

#region CLASSI DI AGGREGAZIONE PER COMPETENZA

public class GoogleAdsTeslaDataAggregation
{
    // DRIVING (essenziale Google Ads)
    public List<int> Speeds { get; set; } = [];
    public decimal OdometerMin { get; set; } = decimal.MaxValue;
    public decimal OdometerMax { get; set; } = decimal.MinValue;
    public decimal TotalKilometers { get; set; }
    public List<int> Headings { get; set; } = [];
    public Dictionary<string, int> ShiftStates { get; set; } = [];

    // LOCATION (privacy-safe)
    public Dictionary<string, int> GeographicZones { get; set; } = [];

    // BATTERY & CHARGING (core marketing)
    public List<int> BatteryLevels { get; set; } = [];
    public List<decimal> BatteryRanges { get; set; } = [];
    public Dictionary<string, int> ChargingStates { get; set; } = [];
    public List<decimal> ChargeRates { get; set; } = [];
    public List<int> ChargeLimits { get; set; } = [];
    public int FastChargerUsageCount { get; set; }

    // CLIMATE (comfort insights)
    public List<decimal> InsideTemperatures { get; set; } = [];
    public List<decimal> OutsideTemperatures { get; set; } = [];
    public Dictionary<bool, int> ClimateUsage { get; set; } = [];

    // VEHICLE STATE (security & usage)
    public Dictionary<string, int> SecurityStates { get; set; } = [];
    public List<decimal> TirePressures { get; set; } = [];

    // VEHICLE CONFIG (segmentation)
    public string? VehicleModel { get; set; }
    public string? ExteriorColor { get; set; }
    public string? EfficiencyPackage { get; set; }

    // SAFETY/ADAS (adoption insights)
    public Dictionary<string, int> AdasFeatures { get; set; } = [];

    // STATS
    public decimal BatteryLevelAvg => BatteryLevels.Any() ? BatteryLevels.Average(x => (decimal)x) : 0;
    public decimal AvgSpeed => Speeds.Count != 0 ? Speeds.Average(x => (decimal)x) : 0;
    public decimal ClimateUsagePercent => ClimateUsage.Count != 0
        ? (ClimateUsage.GetValueOrDefault(true) * 100.0m / Math.Max(1, ClimateUsage.Values.Sum())) : 0;
    public decimal SecurityUsagePercent => SecurityStates.TryGetValue("locked", out int value) && SecurityStates.Values.Sum() > 0
        ? (value * 100.0m / SecurityStates.Values.Sum()) : 0;

    // ADAPTIVE PROFILE
    public int AdaptiveSessionsCount { get; set; }
    public int AdaptiveSessionsStoppedManually { get; set; }
    public int AdaptiveSessionsStoppedAutomatically { get; set; }
    public bool HasActiveAdaptiveSession { get; set; }
    public int? MostActiveAdaptiveHour { get; set; }
    public string AdaptiveFrequencyAnalysis { get; set; } = "";
    public double AdaptiveFrequencyValue { get; set; }
    public int AdaptiveDataRecordsCount { get; set; }
    public Dictionary<string, int> AdaptiveSessionsByDay { get; set; } = [];
}

#endregion

#region CLASSE PER LOGGER

public static class LoggerExtensions
{
    public static async Task LogByLevel(this PolarDriveLogger logger, string source, string message, string details, string level)
    {
        switch (level)
        {
            case "Error": await logger.Error(source, message, details); break;
            case "Warning": await logger.Warning(source, message, details); break;
            case "Debug": await logger.Debug(source, message, details); break;
            default: await logger.Info(source, message, details); break;
        }
    }
}

#endregion
