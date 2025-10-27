using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Diagnostics;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Aggregatore intelligente COMPLETO che processa 720h di dati JSON Tesla,
/// per ottimizzare l'uso di token con PolarAi™
/// </summary>
public class IntelligentDataAggregator
{
    private readonly PolarDriveDbContext _dbContext;
    private readonly PolarDriveLogger _logger;
    private readonly PolarDriveLoggerFileSpecific _loggerFileSpecific;
    private readonly int _maxRetryAttempts;
    private readonly TimeSpan _baseDelay;
    private readonly Random _jitterRandom;
    private readonly Stopwatch _totalStopwatch;
    private readonly HashSet<string> _processedRecords;
    private readonly HashSet<string> _processedSessions;
    private readonly HashSet<string> _processedCommands;
    private readonly List<ProcessingError> _processingErrors;
    private readonly Dictionary<string, int> _processingStats;
    private readonly Dictionary<string, TimeSpan> _processingTimes;

    public IntelligentDataAggregator(
        PolarDriveDbContext dbContext,
        int maxRetryAttempts = 3,
        int baseDelayMs = 1000)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = new PolarDriveLogger(_dbContext);
        _loggerFileSpecific = new PolarDriveLoggerFileSpecific("IntelligentDataAggregator");
        _maxRetryAttempts = maxRetryAttempts;
        _baseDelay = TimeSpan.FromMilliseconds(baseDelayMs);
        _jitterRandom = new Random();
        _totalStopwatch = new Stopwatch();
        _processedRecords = [];
        _processedSessions = [];
        _processedCommands = [];
        _processingErrors = [];
        _processingStats = [];
        _processingTimes = [];
    }

    public async Task<string> GenerateAggregatedInsights(List<string> rawJsonAnonymizedList, int vehicleId)
    {
        var source = "IntelligentDataAggregator.GenerateAggregatedInsights";
        _totalStopwatch.Start();

        await _logger.Info(source, $"Processando {rawJsonAnonymizedList.Count} record per aggregazione completa", $"VehicleId: {vehicleId}");
        LogProcessingStep("Initialize", $"Inizializzato processamento per {rawJsonAnonymizedList.Count} JSON");

        var aggregation = new CompleteTeslaDataAggregation();
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

                var jsonHash = ComputeHash(sanitizedJson);
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
                            case "charging_history":
                                ProcessChargingHistoryComplete(content, aggregation);
                                LogProcessingStep("ChargingHistory", $"Processata charging history");
                                break;
                            case "vehicle_endpoints":
                                ProcessVehicleEndpointsComplete(content, aggregation);
                                LogProcessingStep("VehicleEndpoints", $"Processati vehicle endpoints");
                                break;
                            case "vehicle_commands":
                                ProcessVehicleCommandsComplete(content, aggregation);
                                LogProcessingStep("VehicleCommands", $"Processati vehicle commands");
                                break;
                            case "energy_endpoints":
                                ProcessEnergyEndpointsComplete(content, aggregation);
                                LogProcessingStep("EnergyEndpoints", $"Processati energy endpoints");
                                break;
                            case "partner_public_key":
                                ProcessPartnerPublicKeyComplete(content, aggregation);
                                LogProcessingStep("PartnerKey", $"Processata partner key");
                                break;
                            case "user_profile":
                                // Ignoriamo esplicitamente per rispettare Tesla Fleet Api
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
                        $"(Velocità media: {(processedRecords / _totalStopwatch.Elapsed.TotalSeconds):F1} JSON/sec)");
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

        // Aggiungi dati SMS Adaptive Profile
        await AddAdaptiveProfileDataComplete(vehicleId, aggregation);

        // Genera output ottimizzato ma COMPLETO
        var result = await GenerateCompleteOptimizedPromptDataAsync(aggregation, processedRecords, vehicleId);

        // Log riduzione token
        var totalChars = rawJsonAnonymizedList.Sum(j => j?.Length ?? 0);
        var saved = Math.Max(0, totalChars - (result?.Length ?? 0));
        var reduction = totalChars > 0 ? (saved * 100.0 / totalChars) : 0.0;

        _totalStopwatch.Stop();

        // Log statistiche dettagliate
        await LogProcessingPhase("TotalProcessing", processedRecords, _totalStopwatch.Elapsed);

        if (_processingStats.Any())
        {
            var topOperations = _processingStats.OrderByDescending(kvp => kvp.Value).Take(5);
            await _logger.Info(source, "Top operazioni eseguite:",
                string.Join(", ", topOperations.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
        }

        await _logger.Info(source,
            $"Aggregazione COMPLETA completata: {processedRecords} record → {(result?.Length ?? 0)} caratteri",
            $"Riduzione: {reduction:F1}% (da {totalChars} char) mantenendo tutta la logica. " +
            $"Tempo totale: {_totalStopwatch.Elapsed.TotalSeconds:F2}s, " +
            $"Velocità: {(processedRecords / Math.Max(0.001, _totalStopwatch.Elapsed.TotalSeconds)):F1} record/sec");

        return result ?? string.Empty;
    }

    #region PROCESSAMENTO COMPLETO DEI DATI

    private static string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes)[..16];
    }

    private void ProcessChargingHistoryComplete(JsonElement content, CompleteTeslaDataAggregation aggregation)
    {
        if (content.ValueKind != JsonValueKind.Object) return;

        // Estrazione valori grezzi
        var sessionId = GetSafeIntValue(content, "sessionId");
        var startDateTime = GetSafeStringValue(content, "chargeStartDateTime");
        var stopDateTime = GetSafeStringValue(content, "chargeStopDateTime");
        var site = GetSafeStringValue(content, "siteLocationName");
        var unlatch = GetSafeStringValue(content, "unlatchDateTime");
        var country = GetSafeStringValue(content, "countryCode");
        var billingType = GetSafeStringValue(content, "billingType");
        var vehicleType = GetSafeStringValue(content, "vehicleMakeType");

        // DEDUPLICAZIONE SESSIONI con dati normalizzati
        var normalizedStart = string.IsNullOrEmpty(startDateTime) ? null : DataNormalizer.NormalizeDateTime(startDateTime);
        if (!normalizedStart.HasValue) return; // Salta se data non valida

        var sessionKey = $"{sessionId}_{normalizedStart.Value:yyyy-MM-ddTHH:mm:ssZ}";
        if (_processedSessions.Contains(sessionKey))
            return; // Sessione già processata
        _processedSessions.Add(sessionKey);

        // NORMALIZZAZIONE DATE
        var normalizedStop = string.IsNullOrEmpty(stopDateTime) ? null : DataNormalizer.NormalizeDateTime(stopDateTime);
        var normalizedUnlatch = string.IsNullOrEmpty(unlatch) ? null : DataNormalizer.NormalizeDateTime(unlatch);

        // Processa solo se ha date valide
        if (normalizedStart.HasValue && normalizedStop.HasValue)
        {
            var duration = (normalizedStop.Value - normalizedStart.Value).TotalMinutes;

            // Validazione durata realistica
            if (duration <= 0 || duration > 1440) // Max 24 ore
                return;

            var session = new ChargingSessionComplete
            {
                SessionId = sessionId,
                Duration = duration,
                StartTime = normalizedStart.Value,
                StopTime = normalizedStop.Value,

                // NORMALIZZAZIONE TESTI
                Site = AnonymizeChargingSite(site),
                Country = DataNormalizer.NormalizeText(country!),
                BillingType = DataNormalizer.NormalizeText(billingType!),
                VehicleType = DataNormalizer.NormalizeText(vehicleType!)
            };

            // Analisi intelligente della sessione con durata normalizzata
            session.SessionType = duration switch
            {
                < 15 => "Ricarica veloce (top-up)",
                < 60 => "Ricarica breve",
                < 180 => "Ricarica standard",
                _ => "Ricarica completa"
            };

            // Gestione unlatch normalizzata
            if (normalizedUnlatch.HasValue)
            {
                var disconnectDelay = (normalizedUnlatch.Value - normalizedStop.Value).TotalMinutes;
                const double MaxDisconnectDelayMinutes = 12 * 60; // 12 ore
                if (disconnectDelay >= 0 && disconnectDelay <= MaxDisconnectDelayMinutes)
                {
                    session.DisconnectDelay = disconnectDelay;
                }
            }

            aggregation.ChargingSessions.Add(session);

            // Analisi dettagliata delle fees con normalizzazione
            if (content.TryGetProperty("fees", out var feesArray) && feesArray.ValueKind == JsonValueKind.Array)
            {
                ProcessChargingFeesNormalized(feesArray, aggregation, session);
            }

            // Gestione invoices con normalizzazione
            if (content.TryGetProperty("invoices", out var invoicesArray) && invoicesArray.ValueKind == JsonValueKind.Array)
            {
                session.InvoiceCount = invoicesArray.GetArrayLength();
                foreach (var invoice in invoicesArray.EnumerateArray())
                {
                    var invoiceType = GetSafeStringValue(invoice, "invoiceType");
                    var normalizedInvoiceType = DataNormalizer.NormalizeText(invoiceType!);
                    aggregation.InvoiceTypes[normalizedInvoiceType] = aggregation.InvoiceTypes.GetValueOrDefault(normalizedInvoiceType) + 1;
                }
            }

            // Log sessione processata (solo se logging dettagliato attivo)
            LogProcessingStep("ChargingSession", $"Sessione {sessionId}: {session.Site}, {duration:F0}min, {session.SessionType}");
        }

        // Aggregazione per paese e sito con testi normalizzati
        var normalizedCountry = DataNormalizer.NormalizeText(country!);
        var normalizedSite = AnonymizeChargingSite(site);

        if (!string.IsNullOrEmpty(normalizedCountry))
            aggregation.ChargingByCountry[normalizedCountry] = aggregation.ChargingByCountry.GetValueOrDefault(normalizedCountry) + 1;

        if (!string.IsNullOrEmpty(normalizedSite))
            aggregation.ChargingBySite[normalizedSite] = aggregation.ChargingBySite.GetValueOrDefault(normalizedSite) + 1;
    }

    private void ProcessChargingFeesNormalized(JsonElement feesArray, CompleteTeslaDataAggregation aggregation, ChargingSessionComplete session)
    {
        var totalCost = 0m;
        var totalEnergy = 0m;
        var currency = "EUR";

        foreach (var fee in feesArray.EnumerateArray())
        {
            var feeType = DataNormalizer.NormalizeText(GetSafeStringValue(fee, "feeType")!);
            var totalDue = GetSafeDecimalValue(fee, "totalDue");
            var isPaid = GetSafeBooleanValue(fee, "isPaid");
            var rawCurrency = GetSafeStringValue(fee, "currencyCode");
            var pricingType = DataNormalizer.NormalizeText(GetSafeStringValue(fee, "pricingType")!);
            var status = DataNormalizer.NormalizeText(GetSafeStringValue(fee, "status")!);

            // Normalizzazione currency
            currency = DataNormalizer.NormalizeText(rawCurrency ?? currency);

            if (feeType == "CHARGING" && totalDue > 0)
            {
                var usageBase = GetSafeDecimalValue(fee, "usageBase");
                var usageTier2 = GetSafeDecimalValue(fee, "usageTier2");
                var energy = usageBase + usageTier2;

                // Normalizzazione valori monetari
                var normalizedCost = DataNormalizer.NormalizeCurrency(totalDue, currency);
                totalCost += normalizedCost;
                totalEnergy += energy;

                // Analisi pricing tiers con valori normalizzati
                var rateBase = GetSafeDecimalValue(fee, "rateBase");
                if (rateBase > 0)
                {
                    aggregation.PricingTiers.Add(new PricingTierData
                    {
                        Rate = DataNormalizer.NormalizeCurrency(rateBase, currency),
                        Usage = Math.Round(usageBase, 3),
                        Currency = currency,
                        Type = "Base"
                    });
                }

                var rateTier2 = GetSafeDecimalValue(fee, "rateTier2");
                if (rateTier2 > 0 && usageTier2 > 0)
                {
                    aggregation.PricingTiers.Add(new PricingTierData
                    {
                        Rate = DataNormalizer.NormalizeCurrency(rateTier2, currency),
                        Usage = Math.Round(usageTier2, 3),
                        Currency = currency,
                        Type = "Tier2"
                    });
                }
            }

            // Statistiche payment status normalizzate
            var paymentKey = $"{(isPaid ? "PAID" : "UNPAID")}_{status}";
            aggregation.PaymentStatus[paymentKey] = aggregation.PaymentStatus.GetValueOrDefault(paymentKey) + 1;
        }

        // Assegna valori normalizzati alla sessione
        if (totalCost > 0 && totalEnergy > 0)
        {
            session.TotalCost = totalCost;
            session.TotalEnergy = Math.Round(totalEnergy, 3);
            session.CostPerKwh = Math.Round(totalCost / totalEnergy, 4);
            session.Currency = currency;

            // Analisi costi con valori normalizzati
            session.CostAnalysis = session.CostPerKwh switch
            {
                < 0.30m => "Tariffa conveniente",
                < 0.50m => "Tariffa media",
                < 0.70m => "Tariffa elevata",
                _ => "Tariffa molto cara"
            };

            aggregation.ChargingCosts.Add(totalCost);
            aggregation.EnergyConsumed.Add(totalEnergy);
            aggregation.CostPerKwhValues.Add(session.CostPerKwh);
        }
    }

    private void ProcessVehicleEndpointsComplete(JsonElement content, CompleteTeslaDataAggregation aggregation)
    {
        if (content.ValueKind != JsonValueKind.Object) return;

        // Vehicle Data
        if (content.TryGetProperty("vehicle_data", out var vehicleData) &&
            vehicleData.TryGetProperty("response", out var vdResponse))
        {
            ProcessVehicleDataComplete(vdResponse, aggregation);
        }

        // Vehicle List
        if (content.TryGetProperty("list", out var list) &&
            list.TryGetProperty("response", out var listResponse))
        {
            aggregation.AssociatedVehiclesCount = GetSafeIntValue(list, "count");
            ProcessVehicleList(listResponse, aggregation);
        }

        // Altri endpoint come nel codice originale...
        ProcessOtherVehicleEndpoints(content, aggregation);
    }

    private void ProcessVehicleDataComplete(JsonElement vdResponse, CompleteTeslaDataAggregation aggregation)
    {
        // Informazioni base
        var state = DataNormalizer.NormalizeText(GetSafeStringValue(vdResponse, "state") ?? "");
        if (!string.IsNullOrEmpty(state))
            aggregation.VehicleStates[state] = aggregation.VehicleStates.GetValueOrDefault(state) + 1;

        // Charge State - analisi completa
        if (vdResponse.TryGetProperty("charge_state", out var chargeState))
        {
            ProcessChargeStateComplete(chargeState, aggregation);
        }

        // Climate State - analisi completa
        if (vdResponse.TryGetProperty("climate_state", out var climateState))
        {
            ProcessClimateStateComplete(climateState, aggregation);
        }

        // Drive State - con tutti i metodi helper
        if (vdResponse.TryGetProperty("drive_state", out var driveState))
        {
            ProcessDriveStateComplete(driveState, aggregation);
        }

        // Vehicle State - con TPMS dettagliato
        if (vdResponse.TryGetProperty("vehicle_state", out var vehicleState))
        {
            ProcessVehicleStateComplete(vehicleState, aggregation);
        }
    }

    private void ProcessChargeStateComplete(JsonElement chargeState, CompleteTeslaDataAggregation aggregation)
    {
        var validBatteryLevels = 0;
        var invalidBatteryLevels = 0;
        var batteryLevel = GetSafeIntValue(chargeState, "battery_level");
        var batteryRange = GetSafeDecimalValue(chargeState, "battery_range");
        var chargingState = GetSafeStringValue(chargeState, "charging_state");
        var chargeLimit = GetSafeIntValue(chargeState, "charge_limit_soc");
        var chargeRate = GetSafeDecimalValue(chargeState, "charge_rate");
        var minutesToFull = GetSafeIntValue(chargeState, "minutes_to_full_charge");

        if (batteryRange > 0)
        {
            var normalizedRange = DataNormalizer.NormalizeDistance(batteryRange);
            if (normalizedRange <= 1000) // Range realistico in km
                aggregation.BatteryRanges.Add(normalizedRange);
        }

        if (!string.IsNullOrEmpty(chargingState))
        {
            var normalizedState = DataNormalizer.NormalizeText(chargingState);
            if (IsValidChargingState(normalizedState))
                aggregation.ChargingStates[normalizedState] = aggregation.ChargingStates.GetValueOrDefault(normalizedState) + 1;
        }

        if (batteryLevel > 0 && DataValidator.IsValidBatteryLevel(batteryLevel))
        {
            aggregation.BatteryLevels.Add(batteryLevel);
            validBatteryLevels++;

            // Analisi dello stato batteria
            var batteryAnalysis = batteryLevel switch
            {
                < 20 => "Batteria scarica - ricarica consigliata",
                < 50 => "Livello medio - valutare ricarica",
                < 80 => "Buon livello di carica",
                _ => "Batteria ben carica"
            };
            aggregation.BatteryAnalyses[batteryAnalysis] = aggregation.BatteryAnalyses.GetValueOrDefault(batteryAnalysis) + 1;
        }
        else if (batteryLevel > 0)
        {
            invalidBatteryLevels++;
        }

        if (chargeLimit > 0 && DataValidator.IsValidBatteryLevel(chargeLimit))
            aggregation.ChargeLimits.Add(chargeLimit);

        if (chargeRate >= 0 && chargeRate <= 250) // kW realistici
            aggregation.ChargeRates.Add(chargeRate);

        if (minutesToFull >= 0 && minutesToFull <= 1440) // Max 24 ore
            aggregation.MinutesToFullReadings.Add(minutesToFull);

        if (validBatteryLevels > 0 || invalidBatteryLevels > 0)
        {
            _ = LogValidationResult("BatteryLevels", validBatteryLevels, invalidBatteryLevels);
        }
    }

    private static bool IsValidChargingState(string state) =>
        new[] { "CHARGING", "COMPLETE", "DISCONNECTED", "STOPPED", "NOPOWER" }
            .Contains(state?.ToUpperInvariant());

    private void ProcessClimateStateComplete(JsonElement climateState, CompleteTeslaDataAggregation aggregation)
    {
        var insideTemp = GetSafeDecimalValue(climateState, "inside_temp");
        var outsideTemp = GetSafeDecimalValue(climateState, "outside_temp");
        var driverTemp = GetSafeDecimalValue(climateState, "driver_temp_setting");
        var passengerTemp = GetSafeDecimalValue(climateState, "passenger_temp_setting");
        var isClimateOn = GetSafeBooleanValue(climateState, "is_climate_on");
        var cabinOverheat = GetSafeStringValue(climateState, "cabin_overheat_protection");

        // NORMALIZZAZIONE TEMPERATURE
        if (insideTemp != 0)
        {
            var normalizedTemp = DataNormalizer.NormalizeTemperature(insideTemp);
            if (DataValidator.IsValidTemperature(normalizedTemp))
                aggregation.InsideTemperatures.Add(normalizedTemp);
        }

        if (outsideTemp != 0)
        {
            var normalizedTemp = DataNormalizer.NormalizeTemperature(outsideTemp);
            if (DataValidator.IsValidTemperature(normalizedTemp))
                aggregation.OutsideTemperatures.Add(normalizedTemp);
        }

        if (driverTemp != 0)
        {
            var normalizedTemp = DataNormalizer.NormalizeTemperature(driverTemp);
            if (DataValidator.IsValidTemperature(normalizedTemp))
                aggregation.DriverTempSettings.Add(normalizedTemp);
        }

        aggregation.ClimateUsage[isClimateOn] = aggregation.ClimateUsage.GetValueOrDefault(isClimateOn) + 1;

        var normalizedOverheat = DataNormalizer.NormalizeText(cabinOverheat ?? "");
        if (!string.IsNullOrEmpty(normalizedOverheat))
            aggregation.CabinOverheatSettings[normalizedOverheat] = aggregation.CabinOverheatSettings.GetValueOrDefault(normalizedOverheat) + 1;

        // Analisi intelligente del clima
        if (insideTemp != 0 && outsideTemp != 0)
        {
            var tempDifference = Math.Abs(insideTemp - outsideTemp);
            var climateAnalysis = tempDifference > 10 ?
                "Differenza significativa - sistema climatico probabilmente attivo" :
                "Temperature equilibrate";
            aggregation.ClimateAnalyses[climateAnalysis] = aggregation.ClimateAnalyses.GetValueOrDefault(climateAnalysis) + 1;
        }
    }

    private void ProcessDriveStateComplete(JsonElement driveState, CompleteTeslaDataAggregation aggregation)
    {
        var latitude = GetSafeDecimalValue(driveState, "latitude");
        var longitude = GetSafeDecimalValue(driveState, "longitude");
        var heading = GetSafeIntValue(driveState, "heading");
        var speed = GetSafeIntValue(driveState, "speed");
        var shiftState = GetSafeStringValue(driveState, "shift_state");
        var normalizedSpeed = DataNormalizer.NormalizeSpeed(speed);

        if (latitude != 0) latitude = AnonymizeDecimal(latitude, precision: 1);
        if (longitude != 0) longitude = AnonymizeDecimal(longitude, precision: 1);

        // NORMALIZZAZIONE APPLICATA
        if (speed > 0)
        {
            if (DataValidator.IsValidSpeed(normalizedSpeed))
                aggregation.Speeds.Add(normalizedSpeed);
        }

        if (latitude != 0 && longitude != 0)
        {
            // Aggrega per ZONA GEOGRAFICA invece di coordinate precise
            var zone = GetGeographicZone(latitude, longitude);
            aggregation.GeographicZones[zone] = aggregation.GeographicZones.GetValueOrDefault(zone) + 1;
            
            // Aggrega direzione separatamente (senza collegare alle coordinate)
            if (heading >= 0 && heading <= 360)
            {
                var direction = GetCompassDirection(heading);
                aggregation.HeadingDistribution[direction] = aggregation.HeadingDistribution.GetValueOrDefault(direction) + 1;
            }
        }

        if (!string.IsNullOrEmpty(shiftState))
        {
            var normalizedShift = DataNormalizer.NormalizeText(shiftState);
            var translatedShift = TranslateShiftState(normalizedShift);
            aggregation.ShiftStates[translatedShift] = aggregation.ShiftStates.GetValueOrDefault(translatedShift) + 1;
        }
    }

    private static string GetGeographicZone(decimal lat, decimal lon)
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

    private void ProcessVehicleStateComplete(JsonElement vehicleState, CompleteTeslaDataAggregation aggregation)
    {
        var locked = GetSafeBooleanValue(vehicleState, "locked");
        var sentryMode = GetSafeBooleanValue(vehicleState, "sentry_mode");
        var valetMode = GetSafeBooleanValue(vehicleState, "valet_mode");
        var odometer = GetSafeDecimalValue(vehicleState, "odometer");
        var vehicleName = GetSafeStringValue(vehicleState, "vehicle_name");

        if (!string.IsNullOrEmpty(vehicleName)) aggregation.VehicleName = vehicleName;
        if (odometer > 0) aggregation.OdometerReadings.Add(odometer);

        aggregation.SecurityStates["locked"] = aggregation.SecurityStates.GetValueOrDefault("locked") + (locked ? 1 : 0);
        aggregation.SecurityStates["sentry"] = aggregation.SecurityStates.GetValueOrDefault("sentry") + (sentryMode ? 1 : 0);
        aggregation.SecurityStates["valet"] = aggregation.SecurityStates.GetValueOrDefault("valet") + (valetMode ? 1 : 0);

        // TPMS con analisi avanzata e validazione
        var tpmsFL = GetSafeDecimalValue(vehicleState, "tpms_pressure_fl");
        var tpmsFR = GetSafeDecimalValue(vehicleState, "tpms_pressure_fr");
        var tpmsRL = GetSafeDecimalValue(vehicleState, "tpms_pressure_rl");
        var tpmsRR = GetSafeDecimalValue(vehicleState, "tpms_pressure_rr");

        var pressures = new[] { tpmsFL, tpmsFR, tpmsRL, tpmsRR }
            .Where(p => p > 0 && DataValidator.IsValidTirePressure(p))
            .ToArray();

        if (pressures.Any())
        {
            foreach (var pressure in pressures)
            {
                aggregation.TirePressures.Add(pressure);
            }

            // Analisi pressioni
            var avgPressure = pressures.Average();
            var maxDifference = pressures.Max() - pressures.Min();

            var pressureAnalysis = maxDifference > 0.3m ?
                "Differenze significative tra pneumatici" :
                avgPressure < 2.5m ? "Pressioni generalmente basse" :
                avgPressure > 3.5m ? "Pressioni generalmente alte" :
                "Pressioni nella norma";

            aggregation.TirePressureAnalyses[pressureAnalysis] = aggregation.TirePressureAnalyses.GetValueOrDefault(pressureAnalysis) + 1;
        }
    }

    private void ProcessOtherVehicleEndpoints(JsonElement content, CompleteTeslaDataAggregation aggregation)
    {
        // Implementa tutti gli altri endpoint
        // Drivers, Fleet Status, Nearby Charging Sites, ecc.

        if (content.TryGetProperty("drivers", out var drivers) &&
            drivers.TryGetProperty("response", out var driversResponse))
        {
            aggregation.AuthorizedDriversCount = GetSafeIntValue(drivers, "count");
            ProcessDriversList(driversResponse, aggregation);
        }

        // Fleet Status
        if (content.TryGetProperty("fleet_status", out var fleetStatus))
        {
            ProcessFleetStatus(fleetStatus, aggregation);
        }

        // Altri endpoint...
    }

    private void ProcessVehicleCommandsComplete(JsonElement content, CompleteTeslaDataAggregation aggregation)
    {
        if (content.ValueKind != JsonValueKind.Array) return;

        var commands = content.EnumerateArray().ToList();

        foreach (var command in commands)
        {
            var commandName = GetSafeStringValue(command, "command");
            var timestamp = GetSafeStringValue(command, "timestamp");
            var success = command.TryGetProperty("response", out var resp) && GetSafeBooleanValue(resp, "result");
            var commandKey = $"{commandName}_{timestamp}";

            if (!success && resp.TryGetProperty("reason", out var reason))
            {
                var failureReason = reason.GetString() ?? "Unknown";
                aggregation.CommandFailureReasons[failureReason] =
                    aggregation.CommandFailureReasons.GetValueOrDefault(failureReason) + 1;
            }

            // DEDUPLICAZIONE COMANDI
            if (_processedCommands.Contains(commandKey))
                continue; // Comando già processato
            _processedCommands.Add(commandKey);

            if (!string.IsNullOrEmpty(commandName))
            {
                var category = GetCommandCategoryComplete(commandName);
                var displayName = GetCommandDisplayName(commandName);

                aggregation.CommandCategories[category] = aggregation.CommandCategories.GetValueOrDefault(category) + 1;
                aggregation.CommandSuccess[success] = aggregation.CommandSuccess.GetValueOrDefault(success) + 1;
                aggregation.CommandTypes[displayName] = aggregation.CommandTypes.GetValueOrDefault(displayName) + 1;

                if (!string.IsNullOrEmpty(timestamp) && DateTime.TryParse(timestamp, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var cmdTime))
                {
                    aggregation.CommandTimestamps.Add(cmdTime);

                    // Analisi orari comandi
                    var hourKey = $"{cmdTime.Hour:00}:xx";
                    aggregation.CommandsByHour[hourKey] = aggregation.CommandsByHour.GetValueOrDefault(hourKey) + 1;
                }

                // Aggiungi parametri comandi
                if (command.TryGetProperty("parameters", out var parameters))
                {
                    aggregation.CommandsWithParameters.Add(new CommandWithParameters
                    {
                        CommandName = commandName,
                        Parameters = parameters.ValueKind == JsonValueKind.Null 
                            ? "" 
                            : parameters.GetRawText(),
                        Success = success,
                        Timestamp = timestamp ?? DateTime.Now.ToString("o")
                    });
                }
            }
        }
        
        if (aggregation.CommandTypes.Count != 0)
        {
            var totalCommands = aggregation.CommandTypes.Values.Sum();
            var topCommand = aggregation.CommandTypes.OrderByDescending(kvp => kvp.Value).First();
            
            if (topCommand.Value > totalCommands * 0.5m)
            {
                aggregation.CommandAnalyses["Comando dominante"] = topCommand.Value;
            }
            
            if (aggregation.CommandSuccess.GetValueOrDefault(false) > totalCommands * 0.9m)
            {
                aggregation.CommandAnalyses["Alta affidabilità"] = 1;
            }
        }
    }

    private void ProcessEnergyEndpointsComplete(JsonElement content, CompleteTeslaDataAggregation aggregation)
    {
        if (content.ValueKind != JsonValueKind.Object) return;

        // Live Status con tutti i dettagli
        if (content.TryGetProperty("live_status", out var liveStatus) &&
            liveStatus.TryGetProperty("response", out var liveResponse))
        {
            ProcessLiveStatusComplete(liveResponse, aggregation);
        }

        // Site Info
        if (content.TryGetProperty("site_info", out var siteInfo) &&
            siteInfo.TryGetProperty("response", out var siteResponse))
        {
            ProcessSiteInfoComplete(siteResponse, aggregation);
        }
        
        // Altri endpoint energetici
        if (content.TryGetProperty("charge_history", out var chargeHistory) &&
            chargeHistory.TryGetProperty("response", out var chResponse) &&
            chResponse.TryGetProperty("charge_history", out var chArray))
        {
            foreach (var charge in chArray.EnumerateArray())
            {
                if (charge.TryGetProperty("charge_start_time", out var startTime) &&
                    startTime.TryGetProperty("seconds", out var seconds))
                {
                    var duration = charge.TryGetProperty("charge_duration", out var dur) &&
                                dur.TryGetProperty("seconds", out var durSec)
                        ? durSec.GetInt32() / 3600.0 // converti in ore
                        : 0;

                    var energyAdded = GetSafeIntValue(charge, "energy_added_wh");

                    aggregation.VehicleChargeHistory.Add(new VehicleChargeEntry
                    {
                        StartTime = DateTimeOffset.FromUnixTimeSeconds(seconds.GetInt64())
                            .ToString("o"),
                        DurationHours = duration,
                        EnergyAdded = energyAdded
                    });
                }
            }
        }
    }

    private void ProcessLiveStatusComplete(JsonElement liveResponse, CompleteTeslaDataAggregation aggregation)
    {
        var solarPower = GetSafeIntValue(liveResponse, "solar_power");
        var batteryPower = GetSafeIntValue(liveResponse, "battery_power");
        var loadPower = GetSafeIntValue(liveResponse, "load_power");
        var gridPower = GetSafeIntValue(liveResponse, "grid_power");

        aggregation.EnergySolar.Add(solarPower);
        aggregation.EnergyBattery.Add(batteryPower);
        aggregation.EnergyLoad.Add(loadPower);
        aggregation.EnergyGrid.Add(gridPower);
    }

    private void ProcessSiteInfoComplete(JsonElement siteResponse, CompleteTeslaDataAggregation aggregation)
    {
        var siteName = GetSafeStringValue(siteResponse, "site_name");
        var batteryCount = GetSafeIntValue(siteResponse, "battery_count");

        if (aggregation.EnergySiteInfo == null)
        {
            aggregation.EnergySiteInfo = new EnergySiteInfo
            {
                SiteName = siteName!,
                BatteryCount = batteryCount,
                BackupReservePercent = GetSafeIntValue(siteResponse, "backup_reserve_percent"),
                RealMode = GetSafeStringValue(siteResponse, "default_real_mode") ?? "",
                InstallationDate = GetSafeStringValue(siteResponse, "installation_date") ?? "",
                NameplatePower = GetSafeIntValue(siteResponse, "nameplate_power"),
                NameplateEnergy = GetSafeIntValue(siteResponse, "nameplate_energy"),
                Version = GetSafeStringValue(siteResponse, "version") ?? "",
                HasSolar = siteResponse.TryGetProperty("components", out var comp) 
                    && GetSafeBooleanValue(comp, "solar"),
                HasBattery = siteResponse.TryGetProperty("components", out var comp2) 
                    && GetSafeBooleanValue(comp2, "battery"),
                HasGrid = siteResponse.TryGetProperty("components", out var comp3) 
                    && GetSafeBooleanValue(comp3, "grid")
            };
        }
    }

    private void ProcessPartnerPublicKeyComplete(JsonElement content, CompleteTeslaDataAggregation aggregation)
    {
        var publicKey = GetSafeStringValue(content, "public_key");
        if (!string.IsNullOrEmpty(publicKey))
        {
            aggregation.PublicKeyInfo = new PublicKeyInfo
            {
                KeyLength = publicKey.Length,
                KeyStrength = publicKey.Length >= 128 ? "Forte" : "Media",
                KeyPreview = publicKey.Length > 20 
                    ? publicKey.Substring(0, 20) + "..."
                    : publicKey
            };
        }
    }

    #endregion

    #region ADAPTIVE PROFILE SMS

    private async Task AddAdaptiveProfileDataComplete(int vehicleId, CompleteTeslaDataAggregation aggregation)
    {
        try
        {
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var adaptiveSessions = await ExecuteWithRetry(() =>
                _dbContext.SmsAdaptiveProfile
                    .Where(e => e.VehicleId == vehicleId && e.ReceivedAt >= thirtyDaysAgo)
                    .OrderByDescending(e => e.ReceivedAt)
                    .ToListAsync());

            aggregation.AdaptiveSessionsCount = adaptiveSessions.Count(s => s.ParsedCommand == "ADAPTIVE_PROFILE_ON");
            var offSessions = adaptiveSessions.Count(s => s.ParsedCommand == "ADAPTIVE_PROFILE_OFF");
            aggregation.AdaptiveSessionsStoppedManually = offSessions;
            aggregation.AdaptiveSessionsStoppedAutomatically = Math.Max(0, aggregation.AdaptiveSessionsCount - offSessions);

            aggregation.HasActiveAdaptiveSession = await ExecuteWithRetry(() => GetActiveAdaptiveSession(vehicleId)) != null;

            if (adaptiveSessions.Any())
            {
                // Analisi pattern orari
                var sessionsByHour = adaptiveSessions
                    .Where(s => s.ParsedCommand == "ADAPTIVE_PROFILE_ON")
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
                        && e.ParsedCommand == "ADAPTIVE_PROFILE_ON"
                        && e.ReceivedAt >= fourHoursAgo)
                .OrderByDescending(e => e.ReceivedAt)
                .FirstOrDefaultAsync());
    }

    #endregion

    #region GENERAZIONE OUTPUT OTTIMIZZATO

    private async Task<string> GenerateCompleteOptimizedPromptDataAsync(CompleteTeslaDataAggregation aggregation, int processedRecords, int vehicleId)
    {
        var sb = new StringBuilder();

        // Header con statistiche generali
        var (firstUtc, lastUtc, totalRecords, realDensity) = await GetLightStatsAsync(vehicleId);

        sb.AppendLine("# TESLA DATA INSIGHTS - AGGREGAZIONE COMPLETA");
        sb.AppendLine($"**Periodo analisi**: {processedRecords} record processati");
        sb.AppendLine($"**Range temporale**: {firstUtc:yyyy-MM-dd HH:mm} → {lastUtc:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"**Densità dati**: {realDensity:F1} record/ora ({totalRecords:N0} record totali)");
        sb.AppendLine();

        // 🔋 BATTERIA E RICARICA
        if (aggregation.BatteryLevels.Any() || aggregation.ChargingSessions.Any())
        {
            sb.AppendLine("### 🔋 BATTERIA E RICARICA");

            if (aggregation.BatteryLevels.Any())
            {
                sb.AppendLine($"- **Livello batteria**: Media {aggregation.BatteryLevelAvg:F1}%, Range {aggregation.BatteryLevelMin}-{aggregation.BatteryLevelMax}%");
                sb.AppendLine($"- **Autonomia**: Media {aggregation.BatteryRangeAvg:F1} km, Range {aggregation.BatteryRangeMin:F1}-{aggregation.BatteryRangeMax:F1} km");

                if (aggregation.BatteryAnalyses.Any())
                {
                    var topAnalysis = aggregation.BatteryAnalyses.OrderByDescending(kvp => kvp.Value).First();
                    sb.AppendLine($"- **Stato predominante**: {topAnalysis.Key} ({topAnalysis.Value} rilevazioni)");
                }
            }

            if (aggregation.ChargingSessions.Any())
            {
                sb.AppendLine($"- **Sessioni ricarica**: {aggregation.ChargingSessions.Count} sessioni");
                sb.AppendLine($"- **Durata media**: {aggregation.AvgChargingDuration:F0} minuti");

                if (aggregation.ChargingCosts.Any())
                {
                    sb.AppendLine($"- **Costo medio**: {aggregation.AvgChargingCost:F2} EUR/sessione");

                    if (aggregation.CostPerKwhValues.Any())
                    {
                        var avgCostPerKwh = aggregation.CostPerKwhValues.Average();
                        sb.AppendLine($"- **Tariffa media**: {avgCostPerKwh:F3} EUR/kWh");
                    }
                    
                    // ✅ AGGIUNGI QUESTO:
                    if (aggregation.AvgEnergyEfficiency > 0)
                    {
                        sb.AppendLine($"- **Costo medio normalizzato**: {aggregation.AvgEnergyEfficiency:F2} EUR/kWh");
                    }
                }

                // Analisi per paese e sito
                if (aggregation.ChargingByCountry.Any())
                {
                    var topCountry = aggregation.ChargingByCountry.OrderByDescending(kvp => kvp.Value).First();
                    sb.AppendLine($"- **Paese principale**: {topCountry.Key} ({topCountry.Value} sessioni)");
                }

                if (aggregation.ChargingBySite.Any())
                {
                    var topSite = aggregation.ChargingBySite.OrderByDescending(kvp => kvp.Value).First();
                    sb.AppendLine($"- **Sito preferito**: {topSite.Key} ({topSite.Value} ricariche)");
                }
            }
        }

        // 🌡️ CLIMA E COMFORT
        if (aggregation.InsideTemperatures.Any() || aggregation.OutsideTemperatures.Any())
        {
            sb.AppendLine("### 🌡️ CLIMA E COMFORT");
            sb.AppendLine($"- **Temperature interne**: Media {aggregation.AvgInsideTemp:F1}°C, Range {aggregation.MinInsideTemp:F1}-{aggregation.MaxInsideTemp:F1}°C");
            sb.AppendLine($"- **Temperature esterne**: Media {aggregation.AvgOutsideTemp:F1}°C, Range {aggregation.MinOutsideTemp:F1}-{aggregation.MaxOutsideTemp:F1}°C");
            sb.AppendLine($"- **Uso climatizzatore**: {aggregation.ClimateUsagePercent:F1}% del tempo");

            if (aggregation.ClimateAnalyses.Any())
            {
                var topClimateAnalysis = aggregation.ClimateAnalyses.OrderByDescending(kvp => kvp.Value).First();
                sb.AppendLine($"- **Analisi clima**: {topClimateAnalysis.Key}");
            }
        }

        // 🚗 GUIDA E POSIZIONE
        if (aggregation.Speeds.Any())
        {
            sb.AppendLine("### 🚗 GUIDA E POSIZIONE");
            sb.AppendLine($"- **Velocità media**: {aggregation.AvgSpeed:F1} km/h");
            sb.AppendLine($"- **Stile di guida**: {aggregation.DrivingStyleAnalysis}");

            if (aggregation.GeographicZones.Any())
            {
                sb.AppendLine("- **Zone geografiche più frequentate**:");
                foreach (var zone in aggregation.GeographicZones.OrderByDescending(kvp => kvp.Value))
                {
                    sb.AppendLine($"  • {zone.Key}: {zone.Value} rilevazioni");
                }
            }

            if (aggregation.OdometerReadings.Any())
            {
                var minOdo = aggregation.OdometerReadings.Min();
                var maxOdo = aggregation.OdometerReadings.Max();
                sb.AppendLine($"- **Chilometraggio**: {minOdo:F0}-{maxOdo:F0} km");
            }
        }

        // 🔧 COMANDI E CONTROLLO
        if (aggregation.CommandCategories.Any())
        {
            sb.AppendLine("### 🔧 COMANDI E CONTROLLO");
            sb.AppendLine($"- **Tasso successo**: {aggregation.CommandSuccessRate:F1}%");

            var topCategory = aggregation.CommandCategories.OrderByDescending(kvp => kvp.Value).First();
            sb.AppendLine($"- **Categoria principale**: {topCategory.Key} ({topCategory.Value} comandi)");

            if (aggregation.CommandsByHour.Any())
            {
                var topHour = aggregation.CommandsByHour.OrderByDescending(kvp => kvp.Value).First();
                sb.AppendLine($"- **Orario più attivo**: {topHour.Key} ({topHour.Value} comandi)");
            }
        }

        // 🔒 SICUREZZA E MANUTENZIONE
        if (aggregation.SecurityStates.Any() || aggregation.TirePressures.Any())
        {
            sb.AppendLine("### 🔒 SICUREZZA E MANUTENZIONE");
            sb.AppendLine($"- **Veicolo bloccato**: {aggregation.SecurityUsagePercent:F1}% del tempo");
            sb.AppendLine($"- **Sentry Mode**: {aggregation.SentryUsagePercent:F1}% del tempo");

            if (aggregation.TirePressures.Any())
            {
                sb.AppendLine($"- **Pressioni pneumatici**: Media {aggregation.AvgTirePressure:F2} bar");
                sb.AppendLine($"- **Condizione pneumatici**: {aggregation.TireConditionAnalysis}");
            }
        }

        // ⚡ SISTEMA ENERGETICO DOMESTICO
        if (aggregation.EnergySolar.Any() || aggregation.EnergyBattery.Any())
        {
            sb.AppendLine("### ⚡ SISTEMA ENERGETICO DOMESTICO");
            sb.AppendLine($"- **Bilancio energetico**: {aggregation.EnergyBalanceAnalysis}");

            if (aggregation.EnergySolar.Any())
            {
                var avgSolar = aggregation.EnergySolar.Where(s => s > 0).DefaultIfEmpty(0).Average();
                sb.AppendLine($"- **Produzione solare media**: {avgSolar:F0} W");
            }

            if (aggregation.EnergyLoad.Any())
            {
                var avgLoad = aggregation.EnergyLoad.Where(l => l > 0).DefaultIfEmpty(0).Average();
                sb.AppendLine($"- **Consumo domestico medio**: {avgLoad:F0} W");
            }

            if (aggregation.VehicleChargeHistory.Any())
            {
                var avgChargeFromHome = aggregation.VehicleChargeHistory.Average(v => v.EnergyAdded);
                sb.AppendLine($"- **Ricarica veicoli da casa**: {aggregation.VehicleChargeHistory.Count} sessioni, {avgChargeFromHome:F0} Wh media");
            }
        }

        // 📊 ADAPTIVE PROFILE
        if (aggregation.AdaptiveSessionsCount > 0 || aggregation.HasActiveAdaptiveSession)
        {
            sb.AppendLine("### 📊 ADAPTIVE PROFILE COMPLETO");
            sb.AppendLine($"- **Sessioni Adaptive**: {aggregation.AdaptiveSessionsCount} attivazioni nel periodo");
            sb.AppendLine($"- **Terminazioni**: {aggregation.AdaptiveSessionsStoppedManually} manuali, {aggregation.AdaptiveSessionsStoppedAutomatically} automatiche");
            sb.AppendLine($"- **Sessione attiva**: {(aggregation.HasActiveAdaptiveSession ? "🟢 Sì" : "⚪ No")}");

            if (aggregation.MostActiveAdaptiveHour.HasValue)
            {
                sb.AppendLine($"- **Orario preferito**: {aggregation.MostActiveAdaptiveHour:00}:xx");
            }

            if (aggregation.AdaptiveSessionsByDay.Any())
            {
                var topDays = aggregation.AdaptiveSessionsByDay.OrderByDescending(kvp => kvp.Value).Take(2);
                sb.AppendLine($"- **Giorni più attivi**: {string.Join(", ", topDays.Select(kvp => $"{kvp.Key} ({kvp.Value})"))}");
            }

            if (aggregation.AdaptiveFrequencyValue > 0)
            {
                sb.AppendLine($"- **Frequenza utilizzo**: {aggregation.AdaptiveFrequencyAnalysis} ({aggregation.AdaptiveFrequencyValue:F2} sessioni/giorno)");
            }

            if (aggregation.AdaptiveDataRecordsCount > 0)
            {
                sb.AppendLine($"- **Dati raccolti**: {aggregation.AdaptiveDataRecordsCount:N0} record telemetrici durante sessioni Adaptive");
            }
        }

        // 🚨 AVVISI E ALERT RECENTI
        if (aggregation.RecentAlerts.Any())
        {
            sb.AppendLine("### 🚨 AVVISI E ALERT RECENTI");
            sb.AppendLine($"- **Alert totali**: {aggregation.RecentAlerts.Count}");

            if (aggregation.AlertsByType.Any())
            {
                var topAlert = aggregation.AlertsByType.OrderByDescending(kvp => kvp.Value).First();
                sb.AppendLine($"- **Tipo più frequente**: {topAlert.Key} ({topAlert.Value} occorrenze)");
            }

            // Mostra gli alert più recenti
            var recentestAlerts = aggregation.RecentAlerts.Take(3);
            sb.AppendLine("- **Alert recenti**:");
            foreach (var alert in recentestAlerts)
            {
                sb.AppendLine($"  • {alert.Time}: {alert.Name} - {alert.UserText}");
            }
        }

        if (_processingErrors.Any())
        {
            sb.AppendLine("### ⚠️ STATISTICHE ELABORAZIONE");
            var errorsByType = _processingErrors.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count());

            foreach (var kvp in errorsByType)
            {
                sb.AppendLine($"- **{kvp.Key}**: {kvp.Value} errori");
            }

            sb.AppendLine($"- **Tasso successo**: {((1.0 - (double)_processingErrors.Count / Math.Max(1, processedRecords)) * 100):F1}%");
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("**NOTA**: Dati aggregati e processati con logiche complete ed ottimizzati per l'analisi AI.");

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
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (attempt < _maxRetryAttempts)
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

    #region CLASSI DI VALIDAZIONE E LOG

    public static class DataValidator
    {
        public static bool IsValidBatteryLevel(int level) => level >= 0 && level <= 100;

        public static bool IsValidSpeed(int speed) => speed >= 0 && speed <= 300; // km/h realistici

        public static bool IsValidTemperature(decimal temp) => temp >= -50 && temp <= 70; // Celsius

        public static bool IsValidTirePressure(decimal pressure) => pressure >= 1.0m && pressure <= 5.0m; // bar
    }

    public static class DataNormalizer
    {
        // Normalizzazione date
        public static DateTime? NormalizeDateTime(string dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString)) return null;

            // Gestisce formati comuni Tesla
            var formats = new[]
            {
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-dd HH:mm:ss",
            "MM/dd/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm:ss"
        };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal, out var result))
                {
                    return result;
                }
            }

            // Fallback al parsing standard
            return DateTime.TryParse(dateString, null, DateTimeStyles.AdjustToUniversal, out var fallback)
                ? fallback : null;
        }

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

        // Normalizzazione velocità (mph → km/h se necessario)
        public static int NormalizeSpeed(int speed)
        {
            // Se velocità sembra essere in mph (valori tipici autostrada US)
            if (speed > 0 && speed <= 85) // Range tipico mph
            {
                var kmh = speed * 1.609344;
                if (kmh > 130) // Se risulta troppo alto, probabilmente era già km/h
                    return speed;
                return (int)Math.Round(kmh);
            }
            return speed;
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

        // Normalizzazione testo
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

        // Normalizzazione coordinate (formato consistente)
        public static (decimal lat, decimal lon) NormalizeCoordinates(decimal latitude, decimal longitude)
        {
            // Anonimizza prima di normalizzare
            latitude = Math.Round(latitude, 1);
            longitude = Math.Round(longitude, 1);
            var lat = Math.Round(latitude, 2);
            var lon = Math.Round(longitude, 2);
            return (lat, lon);
        }

        // Normalizzazione valori monetari
        public static decimal NormalizeCurrency(decimal amount, string currency = "EUR")
        {
            // Conversioni di base (dovresti usare API di cambio reali)
            var normalized = currency?.ToUpperInvariant() switch
            {
                "USD" => amount * 0.85m, // Esempio conversione USD→EUR
                "GBP" => amount * 1.15m, // Esempio conversione GBP→EUR
                _ => amount
            };

            return Math.Round(normalized, 2);
        }
    }

    public enum ErrorType
    {
        DatabaseError,
        JsonParsingError,
        ValidationError,
        SystemError,
        UnknownError
    }

    public class ProcessingError
    {
        public ErrorType Type { get; set; }
        public string Message { get; set; } = "";
        public string Details { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    private void LogProcessingStep(string step, string details)
    {
        // 🔸 scrivi su file dedicato (non DB)
        _loggerFileSpecific.Info($"{step}: {details}");

        // 🔹 aggiorna statistiche in memoria
        _processingStats[step] = _processingStats.GetValueOrDefault(step) + 1;
    }

    private async Task LogProcessingPhase(string phase, int count, TimeSpan elapsed)
    {
        await _logger.Info($"IntelligentDataAggregator.{phase}",
            $"Processati {count} elementi in {elapsed.TotalMilliseconds:F0}ms",
            $"Velocità: {(count / Math.Max(0.001, elapsed.TotalSeconds)):F1} elementi/sec");
        _processingTimes[phase] = elapsed;
    }

    private Task LogValidationResult(string dataType, int valid, int invalid)
    {
        if (invalid > 0)
        {
            // caso interessante → resta nel log principale (DB + file giornaliero)
            return _logger.Warning(
                "IntelligentDataAggregator.Validation",
                $"{dataType}: {valid} validi, {invalid} scartati per validazione",
                $"Tasso validazione: {(valid * 100.0 / (valid + invalid)):F1}%"
            );
        }

        // caso “tutti validi” → SOLO nel file specifico
        _loggerFileSpecific.Debug(
            $"Validation {dataType}: tutti {valid} record validi"
        );

        return Task.CompletedTask;
    }

    #endregion

    #region METODI HELPER COMPLETI

    private static string AnonymizeChargingSite(string? site)
    {
        if (string.IsNullOrWhiteSpace(site)) return "";

        // Normalizza il testo
        var normalized = DataNormalizer.NormalizeText(site);

        // Estrai solo la città, rimuovi via/indirizzo
        if (normalized.Contains("MILANO")) return "MILANO";
        if (normalized.Contains("ROMA")) return "ROMA";
        if (normalized.Contains("TORINO")) return "TORINO";
        if (normalized.Contains("NAPOLI")) return "NAPOLI";
        if (normalized.Contains("BOLOGNA")) return "BOLOGNA";
        if (normalized.Contains("FIRENZE")) return "FIRENZE";
        if (normalized.Contains("VENEZIA")) return "VENEZIA";
        if (normalized.Contains("GENOVA")) return "GENOVA";
        if (normalized.Contains("PALERMO")) return "PALERMO";
        if (normalized.Contains("BARI")) return "BARI";

        // Se non trovi città, usa tipo generico
        if (normalized.Contains("SUPERCHARGER")) return "SUPERCHARGER - ITALIA";
        if (normalized.Contains("DESTINATION")) return "DESTINATION - ITALIA";

        return "SITO GENERICO - ITALIA";
    }

    /// <summary>
    /// Sanitizza numeri malformati nel JSON
    /// </summary>
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
            if (nx == ',' || nx == '}' ) return; // ok
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

    private static string TranslateShiftState(string shiftState)
    {
        return shiftState?.ToUpper() switch
        {
            "P" => "Parcheggio",
            "D" => "Avanti",
            "R" => "Retromarcia",
            "N" => "Folle",
            null => "Non disponibile",
            _ => shiftState
        };
    }

    private static string GetCompassDirection(int heading)
    {
        return heading switch
        {
            _ when heading >= 337 || heading <= 22 => "Nord",
            >= 23 and <= 67 => "Nord-Est",
            >= 68 and <= 112 => "Est",
            >= 113 and <= 157 => "Sud-Est",
            >= 158 and <= 202 => "Sud",
            >= 203 and <= 247 => "Sud-Ovest",
            >= 248 and <= 292 => "Ovest",
            >= 293 and <= 336 => "Nord-Ovest",
            _ => "Sconosciuto"
        };
    }

    private static string GetCommandCategoryComplete(string commandName)
    {
        return commandName switch
        {
            var cmd when cmd.StartsWith("charge_", StringComparison.OrdinalIgnoreCase) || cmd.Contains("charge", StringComparison.OrdinalIgnoreCase) => "Ricarica",
            var cmd when cmd.StartsWith("door_", StringComparison.OrdinalIgnoreCase) || cmd.Contains("trunk", StringComparison.OrdinalIgnoreCase) => "Accesso Veicolo",
            var cmd when cmd.Contains("climate", StringComparison.OrdinalIgnoreCase) || cmd.Contains("temp", StringComparison.OrdinalIgnoreCase) || cmd.Contains("heat", StringComparison.OrdinalIgnoreCase) || cmd.Contains("cool", StringComparison.OrdinalIgnoreCase) || cmd.Contains("conditioning", StringComparison.OrdinalIgnoreCase) => "Climatizzazione",
            var cmd when cmd.StartsWith("media_", StringComparison.OrdinalIgnoreCase) || cmd.Contains("volume", StringComparison.OrdinalIgnoreCase) => "Sistema Multimediale",
            var cmd when cmd.StartsWith("navigation_", StringComparison.OrdinalIgnoreCase) => "Navigazione",
            var cmd when cmd.Contains("sentry", StringComparison.OrdinalIgnoreCase) || cmd.Contains("valet", StringComparison.OrdinalIgnoreCase) || cmd.Contains("speed_limit", StringComparison.OrdinalIgnoreCase) || cmd.Contains("pin", StringComparison.OrdinalIgnoreCase) => "Sicurezza",
            var cmd when cmd.Contains("seat", StringComparison.OrdinalIgnoreCase) || cmd.Contains("steering_wheel", StringComparison.OrdinalIgnoreCase) || cmd.Contains("window", StringComparison.OrdinalIgnoreCase) || cmd.Contains("sun_roof", StringComparison.OrdinalIgnoreCase) => "Comfort",
            var cmd when cmd.Contains("software", StringComparison.OrdinalIgnoreCase) || cmd.Contains("schedule", StringComparison.OrdinalIgnoreCase) => "Sistema",
            var cmd when cmd.Contains("lights", StringComparison.OrdinalIgnoreCase) || cmd.Contains("horn", StringComparison.OrdinalIgnoreCase) || cmd.Contains("homelink", StringComparison.OrdinalIgnoreCase) || cmd.Contains("boombox", StringComparison.OrdinalIgnoreCase) => "Funzioni Esterne",
            _ => "Altro"
        };
    }

    private static string GetCommandDisplayName(string commandName)
    {
        return commandName switch
        {
            "actuate_trunk" => "Apertura bagagliaio",
            "charge_start" => "Avvio ricarica",
            "charge_stop" => "Stop ricarica",
            "door_lock" => "Blocco porte",
            "door_unlock" => "Sblocco porte",
            "flash_lights" => "Lampeggio luci",
            "honk_horn" => "Suono clacson",
            "auto_conditioning_start" => "Avvio climatizzazione automatica",
            "auto_conditioning_stop" => "Stop climatizzazione automatica",
            "set_temps" => "Impostazione temperature",
            "set_sentry_mode" => "Modalità sentinella",
            "set_charge_limit" => "Impostazione limite ricarica",
            _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(commandName.Replace("_", " "))
        };
    }

    private void ProcessVehicleList(JsonElement listResponse, CompleteTeslaDataAggregation aggregation)
    {
        foreach (var vehicle in listResponse.EnumerateArray())
        {
            var state = DataNormalizer.NormalizeText(GetSafeStringValue(vehicle, "state") ?? "");
            var accessType = GetSafeStringValue(vehicle, "access_type");

            if (!string.IsNullOrEmpty(state))
                aggregation.AssociatedVehicleStates[state] = aggregation.AssociatedVehicleStates.GetValueOrDefault(state) + 1;
        }
    }

    private void ProcessDriversList(JsonElement driversResponse, CompleteTeslaDataAggregation aggregation)
    {
        var driverCount = 0;
        foreach (var driver in driversResponse.EnumerateArray())
        {
            var firstName = GetSafeStringValue(driver, "driver_first_name");
            if (!string.IsNullOrEmpty(firstName)) 
                driverCount++;
        }
        aggregation.AuthorizedDriversCount = driverCount;
    }

    private void ProcessFleetStatus(JsonElement fleetStatus, CompleteTeslaDataAggregation aggregation)
    {
        if (fleetStatus.TryGetProperty("response", out var fleetResponse))
        {
            aggregation.KeyPairedVehicles = fleetResponse.TryGetProperty("key_paired_vins", out var kpv) ? kpv.GetArrayLength() : 0;
            aggregation.UnpairedVehicles = fleetResponse.TryGetProperty("unpaired_vins", out var uv) ? uv.GetArrayLength() : 0;

            if (fleetResponse.TryGetProperty("vehicle_info", out var vehicleInfo))
            {
                foreach (var prop in vehicleInfo.EnumerateObject())
                {
                    var info = prop.Value;
                    var firmware = GetSafeStringValue(info, "firmware_version");

                    if (!string.IsNullOrEmpty(firmware))
                        aggregation.FirmwareVersions[firmware] = aggregation.FirmwareVersions.GetValueOrDefault(firmware) + 1;
                }
            }
        }
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
            Microsoft.EntityFrameworkCore.DbUpdateException => ErrorType.DatabaseError,
            System.Data.Common.DbException => ErrorType.DatabaseError,
            JsonException or FormatException => ErrorType.JsonParsingError,
            ArgumentException or ArgumentOutOfRangeException => ErrorType.ValidationError,
            OutOfMemoryException or StackOverflowException => ErrorType.SystemError,
            _ => ErrorType.UnknownError
        };
    }
    
    private static decimal AnonymizeDecimal(decimal value, int precision = 1)
    {
        // Arrotonda a 1 decimale invece di mascherare
        // 40.8518 → 40.9 (città visibile, indirizzo no)
        return Math.Round(value, precision);
    }

    #endregion
}

#region CLASSI DI AGGREGAZIONE COMPLETE

/// <summary>
/// Aggregazione COMPLETA di tutti i dati Tesla
/// </summary>
public class CompleteTeslaDataAggregation
{
    // Batteria - come prima ma più completo
    public List<int> BatteryLevels { get; set; } = new();
    public List<decimal> BatteryRanges { get; set; } = new();
    public List<int> ChargeLimits { get; set; } = new();
    public List<decimal> ChargeRates { get; set; } = new();
    public List<int> MinutesToFullReadings { get; set; } = new();
    public Dictionary<string, int> ChargingStates { get; set; } = new();
    public Dictionary<string, int> BatteryAnalyses { get; set; } = new();

    // Ricarica - molto più dettagliata
    public List<ChargingSessionComplete> ChargingSessions { get; set; } = new();
    public List<decimal> ChargingCosts { get; set; } = new();
    public List<decimal> EnergyConsumed { get; set; } = new();
    public List<decimal> CostPerKwhValues { get; set; } = new();
    public Dictionary<string, int> ChargingByCountry { get; set; } = new();
    public Dictionary<string, int> ChargingBySite { get; set; } = new();
    public Dictionary<string, int> InvoiceTypes { get; set; } = new();
    public Dictionary<string, int> PaymentStatus { get; set; } = new();
    public List<PricingTierData> PricingTiers { get; set; } = new();

    // Clima - molto più dettagliato
    public List<decimal> InsideTemperatures { get; set; } = new();
    public List<decimal> OutsideTemperatures { get; set; } = new();
    public List<decimal> DriverTempSettings { get; set; } = new();
    public List<decimal> PassengerTempSettings { get; set; } = new();
    public Dictionary<bool, int> ClimateUsage { get; set; } = new();
    public Dictionary<string, int> ClimateAnalyses { get; set; } = new();
    public Dictionary<string, int> CabinOverheatSettings { get; set; } = new();

    // Guida - con analisi posizioni complete
    public List<int> Speeds { get; set; } = new();
    public Dictionary<string, int> GeographicZones { get; set; } = new();
    public Dictionary<string, int> HeadingDistribution { get; set; } = new();    public List<decimal> OdometerReadings { get; set; } = new();
    public Dictionary<string, int> ShiftStates { get; set; } = new();

    // Sicurezza e manutenzione
    public Dictionary<string, int> SecurityStates { get; set; } = new();
    public List<decimal> TirePressures { get; set; } = new();
    public Dictionary<string, int> TirePressureAnalyses { get; set; } = new();

    // Comandi - analisi molto avanzata
    public Dictionary<string, int> CommandCategories { get; set; } = new();
    public Dictionary<bool, int> CommandSuccess { get; set; } = new();
    public Dictionary<string, int> CommandTypes { get; set; } = new();
    public Dictionary<string, int> CommandsByHour { get; set; } = new();
    public List<CommandWithParameters> CommandsWithParameters { get; set; } = new();
    public Dictionary<string, int> CommandFailureReasons { get; set; } = new();
    public Dictionary<string, int> CommandAnalyses { get; set; } = new();
    public List<DateTime> CommandTimestamps { get; set; } = new();

    // Informazioni veicolo e flotta
    public string? VehicleName { get; set; }
    public Dictionary<string, int> VehicleStates { get; set; } = new();
    public int AssociatedVehiclesCount { get; set; }
    public Dictionary<string, int> AssociatedVehicleStates { get; set; } = new();
    public int AuthorizedDriversCount { get; set; }

    // Fleet Status
    public int KeyPairedVehicles { get; set; }
    public int UnpairedVehicles { get; set; }
    public Dictionary<string, int> FirmwareVersions { get; set; } = new();

    // Sistema energetico domestico
    public List<int> EnergySolar { get; set; } = new();
    public List<int> EnergyBattery { get; set; } = new();
    public List<int> EnergyGrid { get; set; } = new();
    public List<int> EnergyLoad { get; set; } = new();
    public EnergySiteInfo? EnergySiteInfo { get; set; }
    public List<VehicleChargeEntry> VehicleChargeHistory { get; set; } = new();

    // Adaptive Profile
    public int AdaptiveSessionsCount { get; set; }
    public int AdaptiveSessionsStoppedManually { get; set; }
    public int AdaptiveSessionsStoppedAutomatically { get; set; }
    public bool HasActiveAdaptiveSession { get; set; }
    public int? MostActiveAdaptiveHour { get; set; }
    public Dictionary<string, int> AdaptiveSessionsByDay { get; set; } = new();
    public string AdaptiveFrequencyAnalysis { get; set; } = "";
    public double AdaptiveFrequencyValue { get; set; }
    public int AdaptiveDataRecordsCount { get; set; }

    // Profilo utente
    public PublicKeyInfo? PublicKeyInfo { get; set; }

    // Alert e avvisi
    public List<AlertInfo> RecentAlerts { get; set; } = new();
    public Dictionary<string, int> AlertsByType { get; set; } = new();

    // Proprietà calcolate
    public decimal BatteryLevelAvg => BatteryLevels.Any() ? BatteryLevels.Average(x => (decimal)x) : 0;
    public int BatteryLevelMin => BatteryLevels.Any() ? BatteryLevels.Min() : 0;
    public int BatteryLevelMax => BatteryLevels.Any() ? BatteryLevels.Max() : 0;
    public decimal BatteryRangeAvg => BatteryRanges.Any() ? BatteryRanges.Average() : 0;
    public decimal BatteryRangeMin => BatteryRanges.Any() ? BatteryRanges.Min() : 0;
    public decimal BatteryRangeMax => BatteryRanges.Any() ? BatteryRanges.Max() : 0;

    public decimal AvgChargingDuration => ChargingSessions.Any() ? (decimal)ChargingSessions.Average(s => s.Duration) : 0;
    public decimal AvgChargingCost => ChargingCosts.Any() ? ChargingCosts.Average() : 0;
    public decimal AvgEnergyEfficiency => (ChargingCosts.Count != 0 && EnergyConsumed.Count != 0 && EnergyConsumed.Average() != 0)
        ? ChargingCosts.Average() / EnergyConsumed.Average()
        : 0;

    public decimal AvgInsideTemp => InsideTemperatures.Any() ? InsideTemperatures.Average() : 0;
    public decimal MinInsideTemp => InsideTemperatures.Any() ? InsideTemperatures.Min() : 0;
    public decimal MaxInsideTemp => InsideTemperatures.Any() ? InsideTemperatures.Max() : 0;
    public decimal AvgOutsideTemp => OutsideTemperatures.Any() ? OutsideTemperatures.Average() : 0;
    public decimal MinOutsideTemp => OutsideTemperatures.Any() ? OutsideTemperatures.Min() : 0;
    public decimal MaxOutsideTemp => OutsideTemperatures.Any() ? OutsideTemperatures.Max() : 0;
    public decimal ClimateUsagePercent => ClimateUsage.Any()
        ? (ClimateUsage.GetValueOrDefault(true) * 100.0m / Math.Max(1, ClimateUsage.Values.Sum()))
        : 0;

    public decimal AvgSpeed => Speeds.Count > 0 ? Speeds.Average(x => (decimal)x) : 0;
    public decimal SecurityUsagePercent => SecurityStates.ContainsKey("locked") && SecurityStates.Values.Sum() > 0
        ? (SecurityStates["locked"] * 100.0m / SecurityStates.Values.Sum())
        : 0;
    public decimal SentryUsagePercent => SecurityStates.ContainsKey("sentry") && SecurityStates.Values.Sum() > 0
        ? (SecurityStates["sentry"] * 100.0m / SecurityStates.Values.Sum())
        : 0;

    public decimal AvgTirePressure => TirePressures.Any() ? TirePressures.Average() : 0;
    public decimal MinTirePressure => TirePressures.Any() ? TirePressures.Min() : 0;
    public decimal MaxTirePressure => TirePressures.Any() ? TirePressures.Max() : 0;

    public decimal CommandSuccessRate => CommandSuccess.Any() && CommandSuccess.Values.Sum() > 0
        ? (CommandSuccess.GetValueOrDefault(true) * 100.0m / CommandSuccess.Values.Sum())
        : 0;

    // Analisi qualitative
    public string DrivingStyleAnalysis => AvgSpeed switch
    {
        <= 30 => "Guida urbana conservativa",
        <= 50 => "Guida mista equilibrata",
        <= 80 => "Guida scorrevole",
        _ => "Guida sportiva"
    };

    public string TireConditionAnalysis
    {
        get
        {
            if (!TirePressures.Any()) return "Dati non disponibili";
            var maxDiff = MaxTirePressure - MinTirePressure;
            return maxDiff switch
            {
                <= 0.2m => "Pressioni uniformi - ottimo",
                <= 0.5m => "Leggere differenze - buono",
                _ => "Differenze significative - controllo necessario"
            };
        }
    }

    public string EnergyBalanceAnalysis
    {
        get
        {
            if (!EnergySolar.Any() || !EnergyLoad.Any()) return "Dati insufficienti";
            var avgSolar = EnergySolar.Where(s => s > 0).DefaultIfEmpty(0).Average();
            var avgLoad = EnergyLoad.Where(l => l > 0).DefaultIfEmpty(0).Average();

            var balance = avgSolar - avgLoad;
            return balance switch
            {
                > 1000 => "Surplus energetico significativo",
                > 0 => "Leggero surplus energetico",
                > -1000 => "Bilancio energetico equilibrato",
                _ => "Dipendenza dalla rete elettrica"
            };
        }
    }
}

#endregion

#region CLASSI DI SUPPORTO COMPLETE

public class ChargingSessionComplete
{
    public int SessionId { get; set; }
    public double Duration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime StopTime { get; set; }
    public string Site { get; set; } = "";
    public string Country { get; set; } = "";
    public string BillingType { get; set; } = "";
    public string VehicleType { get; set; } = "";
    public string SessionType { get; set; } = "";
    public double DisconnectDelay { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalEnergy { get; set; }
    public decimal CostPerKwh { get; set; }
    public string Currency { get; set; } = "";
    public string CostAnalysis { get; set; } = "";
    public int InvoiceCount { get; set; }
}

public class PricingTierData
{
    public decimal Rate { get; set; }
    public decimal Usage { get; set; }
    public string Currency { get; set; } = "";
    public string Type { get; set; } = "";
}

public class CommandWithParameters
{
    public string CommandName { get; set; } = "";
    public string Parameters { get; set; } = "";
    public bool Success { get; set; }
    public string Timestamp { get; set; } = "";
}

public class EnergySiteInfo
{
    public string SiteName { get; set; } = "";
    public int BackupReservePercent { get; set; }
    public string RealMode { get; set; } = "";
    public string InstallationDate { get; set; } = "";
    public int BatteryCount { get; set; }
    public int NameplatePower { get; set; }
    public int NameplateEnergy { get; set; }
    public string Version { get; set; } = "";
    public bool HasSolar { get; set; }
    public bool HasBattery { get; set; }
    public bool HasGrid { get; set; }
}

public class VehicleChargeEntry
{
    public string StartTime { get; set; } = "";
    public double DurationHours { get; set; }
    public int EnergyAdded { get; set; }
}

public class PublicKeyInfo
{
    public int KeyLength { get; set; }
    public string KeyStrength { get; set; } = "";
    public string KeyPreview { get; set; } = "";
}

public class AlertInfo
{
    public string Name { get; set; } = "";
    public string Time { get; set; } = "";
    public string UserText { get; set; } = "";
}

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

