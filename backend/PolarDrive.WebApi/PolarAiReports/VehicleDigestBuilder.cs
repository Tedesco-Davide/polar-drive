using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Builder che aggrega TUTTI i dati in metriche precise e deterministiche
/// Sostituisce il preprocessing raw e genera digest pronti per LLM
/// </summary>
public class VehicleDigestBuilder(PolarDriveDbContext dbContext)
{
    private readonly PolarDriveLogger _logger = new(dbContext);

    public async Task<VehicleDataDigest> BuildDigestAsync(int vehicleId, DateTime periodStart, DateTime periodEnd)
    {
        var source = "VehicleDigestBuilder.BuildDigest";
        
        await _logger.Info(source, "Inizio aggregazione digest", 
            $"VehicleId: {vehicleId}, Period: {periodStart:yyyy-MM-dd} → {periodEnd:yyyy-MM-dd}");

        // Recupera TUTTI i raw data del periodo
        var rawData = await GetRawDataAsync(vehicleId, periodStart, periodEnd);
        
        if (!rawData.Any())
        {
            await _logger.Warning(source, "Nessun dato nel periodo specificato");
            return CreateEmptyDigest(periodStart, periodEnd);
        }

        // Aggrega tutti i dati in strutture tipizzate
        var processedData = ProcessAllRawData(rawData);
        
        // Calcola metriche aggregate
        var digest = new VehicleDataDigest
        {
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            TotalHours = (int)(periodEnd - periodStart).TotalHours,
            TotalSamples = rawData.Count,
            
            Battery = CalculateBatteryMetrics(processedData),
            Charging = await CalculateChargingMetricsAsync(processedData, vehicleId),
            Driving = CalculateDrivingMetrics(processedData),
            Climate = CalculateClimateMetrics(processedData),
            Efficiency = CalculateEfficiencyMetrics(processedData),
            Quality = await CalculateQualityMetricsAsync(vehicleId, periodStart, periodEnd)
        };

        await _logger.Info(source, "Digest completato", 
            $"Samples: {digest.TotalSamples}, Quality: {digest.Quality.QualityScore}");

        return digest;
    }

    private async Task<List<VehicleData>> GetRawDataAsync(int vehicleId, DateTime start, DateTime end)
    {
        return await dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId && 
                        vd.Timestamp >= start && 
                        vd.Timestamp <= end)
            .OrderBy(vd => vd.Timestamp)
            .ToListAsync();
    }

    private List<ProcessedVehicleData> ProcessAllRawData(List<VehicleData> rawData)
    {
        var processed = new List<ProcessedVehicleData>();

        foreach (var raw in rawData)
        {
            try
            {
                if (string.IsNullOrEmpty(raw.RawJson)) continue;

                var doc = JsonDocument.Parse(raw.RawJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("response", out var response) || 
                    !response.TryGetProperty("data", out var dataArray))
                    continue;

                var processedItem = new ProcessedVehicleData
                {
                    Timestamp = raw.Timestamp,
                    IsAdaptiveProfiling = raw.IsAdaptiveProfiling
                };

                // Processa ogni sezione dei dati
                foreach (var item in dataArray.EnumerateArray())
                {
                    var type = item.GetProperty("type").GetString();
                    var content = item.GetProperty("content");

                    switch (type)
                    {
                        case "vehicle_endpoints":
                            ProcessVehicleEndpoints(content, processedItem);
                            break;
                        case "charging_history":
                            ProcessChargingHistory(content, processedItem);
                            break;
                        case "energy_endpoints":
                            ProcessEnergyEndpoints(content, processedItem);
                            break;
                    }
                }

                if (IsValidProcessedData(processedItem))
                    processed.Add(processedItem);
            }
            catch (Exception)
            {
                // Log e continua - non bloccare per un singolo record corrotto
                continue;
            }
        }

        return processed;
    }

    private void ProcessVehicleEndpoints(JsonElement content, ProcessedVehicleData item)
    {
        if (!content.TryGetProperty("vehicle_data", out var vehicleData) ||
            !vehicleData.TryGetProperty("response", out var response))
            return;

        // Battery/Charge State
        if (response.TryGetProperty("charge_state", out var chargeState))
        {
            item.BatteryLevel = GetSafeInt(chargeState, "battery_level");
            item.BatteryRange = GetSafeDecimal(chargeState, "battery_range");
            item.ChargeLimit = GetSafeInt(chargeState, "charge_limit_soc");
            item.ChargingState = GetSafeString(chargeState, "charging_state");
            item.ChargeRate = GetSafeDecimal(chargeState, "charge_rate");
        }

        // Climate State
        if (response.TryGetProperty("climate_state", out var climateState))
        {
            item.InsideTemp = GetSafeDecimal(climateState, "inside_temp");
            item.OutsideTemp = GetSafeDecimal(climateState, "outside_temp");
            item.IsClimateOn = GetSafeBool(climateState, "is_climate_on");
            item.DriverTempSetting = GetSafeDecimal(climateState, "driver_temp_setting");
        }

        // Drive State
        if (response.TryGetProperty("drive_state", out var driveState))
        {
            item.Latitude = GetSafeDecimal(driveState, "latitude");
            item.Longitude = GetSafeDecimal(driveState, "longitude");
            item.Speed = GetSafeInt(driveState, "speed");
            item.Power = GetSafeInt(driveState, "power");
            item.Heading = GetSafeInt(driveState, "heading");
        }

        // Vehicle State
        if (response.TryGetProperty("vehicle_state", out var vehicleState))
        {
            item.Odometer = GetSafeDecimal(vehicleState, "odometer");
            item.IsLocked = GetSafeBool(vehicleState, "locked");
        }
    }

    private void ProcessChargingHistory(JsonElement content, ProcessedVehicleData item)
    {
        var sessions = new List<ChargingSession>();

        if (content.TryGetProperty("sessionId", out var sessionId))
        {
            var session = new ChargingSession
            {
                SessionId = sessionId.GetInt32(),
                StartTime = GetSafeDateTime(content, "chargeStartDateTime"),
                EndTime = GetSafeDateTime(content, "chargeStopDateTime"),
                SiteName = GetSafeString(content, "siteLocationName")
            };

            // Processa fees per calcolare costi
            if (content.TryGetProperty("fees", out var feesArray))
            {
                foreach (var fee in feesArray.EnumerateArray())
                {
                    if (GetSafeString(fee, "feeType") == "CHARGING")
                    {
                        session.TotalCost += GetSafeDecimal(fee, "totalDue");
                        session.EnergyDelivered += GetSafeDecimal(fee, "usageBase") + 
                                                   GetSafeDecimal(fee, "usageTier2");
                        session.Currency = GetSafeString(fee, "currencyCode");
                    }
                }
            }

            if (session.EnergyDelivered > 0)
                session.CostPerKwh = session.TotalCost / session.EnergyDelivered;

            sessions.Add(session);
        }

        item.ChargingSessions = sessions;
    }

    private void ProcessEnergyEndpoints(JsonElement content, ProcessedVehicleData item)
    {
        // Solar e grid data per calcoli di sostenibilità
        if (content.TryGetProperty("live_status", out var liveStatus) &&
            liveStatus.TryGetProperty("response", out var response))
        {
            item.SolarPower = GetSafeInt(response, "solar_power");
            item.GridPower = GetSafeInt(response, "grid_power");
            item.BatteryPower = GetSafeInt(response, "battery_power");
        }
    }

    private BatteryMetrics CalculateBatteryMetrics(List<ProcessedVehicleData> data)
    {
        var batteryData = data.Where(d => d.BatteryLevel > 0).ToList();
        if (!batteryData.Any()) return new BatteryMetrics();

        var levels = batteryData.Select(d => d.BatteryLevel).ToList();
        var ranges = batteryData.Where(d => d.BatteryRange > 0).Select(d => d.BatteryRange).ToList();

        // Calcola cicli di ricarica (quando battery level aumenta significativamente)
        var chargeCycles = 0;
        for (int i = 1; i < batteryData.Count; i++)
        {
            var previousLevel = batteryData[i - 1].BatteryLevel;
            var currentLevel = batteryData[i].BatteryLevel;
            if (currentLevel > previousLevel + 10) // Incremento > 10% = ciclo ricarica
                chargeCycles++;
        }

        // Calcola health score basato su range/efficiency
        var healthScore = ranges.Any() ? Math.Min(100, ranges.Average() / 400 * 100) : 85;

        return new BatteryMetrics
        {
            AvgLevel = (decimal)levels.Average(),
            MinLevel = levels.Min(),
            MaxLevel = levels.Max(),
            AvgRange = ranges.Any() ? ranges.Average() : 0,
            MinRange = ranges.Any() ? ranges.Min() : 0,
            MaxRange = ranges.Any() ? ranges.Max() : 0,
            ChargeCycles = chargeCycles,
            HealthScore = (decimal)Math.Round(healthScore, 1)
        };
    }

    private Task<ChargingMetrics> CalculateChargingMetricsAsync(List<ProcessedVehicleData> data, int vehicleId)
    {
        var allSessions = data.SelectMany(d => d.ChargingSessions).ToList();
        if (!allSessions.Any()) 
            return Task.FromResult(new ChargingMetrics());

        var validSessions = allSessions.Where(s => s.EnergyDelivered > 0 && s.TotalCost > 0).ToList();

        var totalEnergy = validSessions.Sum(s => s.EnergyDelivered);
        var totalCost = validSessions.Sum(s => s.TotalCost);
        var costPerKwhValues = validSessions.Where(s => s.CostPerKwh > 0).Select(s => s.CostPerKwh).ToList();

        // Calcola stazioni più usate
        var stationUsage = allSessions
            .Where(s => !string.IsNullOrEmpty(s.SiteName))
            .GroupBy(s => s.SiteName)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => g.Key)
            .ToList();

        // Stima home charging (sessioni con nomi tipo "Home" o costi bassi)
        var homeChargingCount = allSessions.Count(s => 
            s.SiteName?.Contains("Home", StringComparison.OrdinalIgnoreCase) == true ||
            s.CostPerKwh < 0.25m);
        var homeChargingPercentage = allSessions.Any() ? 
            (decimal)homeChargingCount / allSessions.Count * 100 : 0;

        var result = new ChargingMetrics
        {
            TotalSessions = allSessions.Count,
            TotalEnergyAdded = totalEnergy,
            TotalCost = totalCost,
            AvgCostPerKwh = costPerKwhValues.Any() ? costPerKwhValues.Average() : 0,
            MinCostPerKwh = costPerKwhValues.Any() ? costPerKwhValues.Min() : 0,
            MaxCostPerKwh = costPerKwhValues.Any() ? costPerKwhValues.Max() : 0,
            AvgSessionDuration = allSessions.Where(s => s.EndTime > s.StartTime)
                .Select(s => (decimal)(s.EndTime - s.StartTime).TotalMinutes)
                .DefaultIfEmpty(0).Average(),
            TopStations = stationUsage,
            HomeChargingPercentage = homeChargingPercentage
        };

        return Task.FromResult(result);
    }

    private DrivingMetrics CalculateDrivingMetrics(List<ProcessedVehicleData> data)
    {
        var drivingData = data.Where(d => d.Speed > 0).ToList();
        var allData = data.Where(d => d.Odometer > 0).ToList();

        if (!allData.Any()) return new DrivingMetrics();

        // Calcola distanza totale
        var odometerReadings = allData.Select(d => d.Odometer).OrderBy(x => x).ToList();
        var totalDistance = odometerReadings.Any() ? odometerReadings.Last() - odometerReadings.First() : 0;

        // Analisi direzioni
        var directions = new Dictionary<string, int> { ["N"] = 0, ["S"] = 0, ["E"] = 0, ["W"] = 0 };
        foreach (var item in drivingData)
        {
            var heading = item.Heading;
            var direction = heading switch
            {
                >= 315 or < 45 => "N",
                >= 45 and < 135 => "E", 
                >= 135 and < 225 => "S",
                _ => "W"
            };
            directions[direction]++;
        }

        // Normalizza percentuali
        var totalDirectionSamples = directions.Values.Sum();
        if (totalDirectionSamples > 0)
        {
            directions = directions.ToDictionary(
                kvp => kvp.Key, 
                kvp => kvp.Value * 100 / totalDirectionSamples
            );
        }

        // Stima trips (quando speed passa da 0 a >0)
        var trips = 0;
        var wasMoving = false;
        foreach (var item in data.OrderBy(d => d.Timestamp))
        {
            var isMoving = item.Speed > 5; // Soglia 5 km/h
            if (isMoving && !wasMoving) trips++;
            wasMoving = isMoving;
        }

        // Calcola energia rigenerata (power negativi durante movimento)
        var regenEnergy = drivingData.Where(d => d.Power < 0).Sum(d => Math.Abs(d.Power)) / 1000.0m; // kWh

        return new DrivingMetrics
        {
            TotalDistance = totalDistance,
            AvgSpeed = drivingData.Any() ? (decimal)drivingData.Average(d => d.Speed) : 0,
            MaxSpeed = drivingData.Any() ? (decimal)drivingData.Max(d => d.Speed) : 0,
            AvgPowerConsumption = drivingData.Where(d => d.Power > 0).Any() ? 
                (decimal)drivingData.Where(d => d.Power > 0).Average(d => d.Power) : 0,
            RegenerationEnergy = regenEnergy,
            TripsCount = trips,
            AvgTripDistance = trips > 0 ? totalDistance / trips : 0,
            DirectionDistribution = directions
        };
    }

    private ClimateMetrics CalculateClimateMetrics(List<ProcessedVehicleData> data)
    {
        var climateData = data.Where(d => d.InsideTemp != 0 || d.OutsideTemp != 0).ToList();
        if (!climateData.Any()) return new ClimateMetrics();

        var insideTemps = climateData.Where(d => d.InsideTemp != 0).Select(d => d.InsideTemp).ToList();
        var outsideTemps = climateData.Where(d => d.OutsideTemp != 0).Select(d => d.OutsideTemp).ToList();
        
        var climateOnCount = climateData.Count(d => d.IsClimateOn);
        var climateUsagePercentage = (decimal)climateOnCount / climateData.Count * 100;

        var avgTempDiff = insideTemps.Any() && outsideTemps.Any() ? 
            Math.Abs(insideTemps.Average() - outsideTemps.Average()) : 0;

        // Stima impatto energia clima (maggiore differenza = maggiore impatto)
        var energyImpact = avgTempDiff switch
        {
            < 5 => 5,
            < 15 => 15,
            < 25 => 25,
            _ => 35
        };

        return new ClimateMetrics
        {
            AvgInsideTemp = insideTemps.Any() ? insideTemps.Average() : 0,
            AvgOutsideTemp = outsideTemps.Any() ? outsideTemps.Average() : 0,
            MinInsideTemp = insideTemps.Any() ? insideTemps.Min() : 0,
            MaxInsideTemp = insideTemps.Any() ? insideTemps.Max() : 0,
            ClimateUsagePercentage = climateUsagePercentage,
            AvgTempDifference = avgTempDiff,
            ClimateEnergyImpact = energyImpact
        };
    }

    private EfficiencyMetrics CalculateEfficiencyMetrics(List<ProcessedVehicleData> data)
    {
        var drivingData = data.Where(d => d.Speed > 0 && d.Power > 0).ToList();
        if (!drivingData.Any()) return new EfficiencyMetrics();

        // Calcola efficienza generale (km percorsi per energia consumata)
        var totalEnergyConsumed = drivingData.Sum(d => d.Power) / 1000.0m; // kWh
        var totalDistance = CalculateDistanceFromSamples(drivingData);
        var overallEfficiency = totalEnergyConsumed > 0 ? totalDistance / totalEnergyConsumed : 0;

        // Efficienza per velocità (città vs autostrada)
        var cityData = drivingData.Where(d => d.Speed <= 60).ToList();
        var highwayData = drivingData.Where(d => d.Speed > 60).ToList();
        
        var cityEfficiency = CalculateEfficiencyForData(cityData);
        var highwayEfficiency = CalculateEfficiencyForData(highwayData);

        // Trova velocità ottimale (range con migliore efficienza)
        var optimalSpeed = FindOptimalSpeedRange(drivingData);

        var tips = GenerateEfficiencyTips(overallEfficiency, cityEfficiency, highwayEfficiency);

        return new EfficiencyMetrics
        {
            OverallEfficiency = overallEfficiency,
            CityEfficiency = cityEfficiency,
            HighwayEfficiency = highwayEfficiency,
            OptimalSpeedRange = optimalSpeed,
            EfficiencyTips = tips
        };
    }

    private async Task<DataQualityMetrics> CalculateQualityMetricsAsync(int vehicleId, DateTime start, DateTime end)
    {
        // Usa la logica esistente del tuo PolarAiReportGenerator per qualità dati
        var totalRecords = await dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= start && vd.Timestamp <= end)
            .CountAsync();

        var actualPeriod = end - start;
        var expectedSamples = actualPeriod.TotalHours; // 1 campione/ora ideale
        var samplingFrequency = totalRecords > 0 ? totalRecords / Math.Max(actualPeriod.TotalHours, 1) : 0;
        
        // Calcola gaps (riutilizza logica esistente)
        var gaps = await AnalyzeDataGaps(vehicleId, start, end);
        var uptimePercentage = CalculateUptimePercentage(gaps, actualPeriod);
        
        var qualityScore = CalculateQualityScore(totalRecords, uptimePercentage, gaps.majorGaps, actualPeriod);
        var qualityLabel = GetQualityLabel(qualityScore);

        return new DataQualityMetrics
        {
            UptimePercentage = (decimal)uptimePercentage,
            DataGaps = gaps.totalGaps,
            QualityScore = (int)qualityScore,
            QualityLabel = qualityLabel,
            SamplingFrequency = (decimal)samplingFrequency
        };
    }

    // Helper methods (riutilizza logiche esistenti)
    private bool IsValidProcessedData(ProcessedVehicleData item)
    {
        // Almeno un campo significativo deve essere popolato
        return item.BatteryLevel > 0 || item.Speed >= 0 || item.Odometer > 0 || 
               item.ChargingSessions.Any() || item.SolarPower >= 0;
    }

    private VehicleDataDigest CreateEmptyDigest(DateTime start, DateTime end)
    {
        return new VehicleDataDigest
        {
            PeriodStart = start,
            PeriodEnd = end,
            TotalHours = (int)(end - start).TotalHours,
            TotalSamples = 0,
            Quality = new DataQualityMetrics { QualityLabel = "Nessun dato" }
        };
    }

    // Utility methods per parsing JSON sicuro
    private int GetSafeInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number 
            ? prop.GetInt32() : 0;

    private decimal GetSafeDecimal(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number 
            ? prop.GetDecimal() : 0m;

    private bool GetSafeBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.True;

    private string GetSafeString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String 
            ? prop.GetString() ?? "" : "";

    private DateTime GetSafeDateTime(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            if (DateTime.TryParse(prop.GetString(), out var date))
                return date;
        }
        return default;
    }

    private decimal CalculateDistanceFromSamples(List<ProcessedVehicleData> data)
    {
        if (data.Count < 2) return 0;
        
        var orderedData = data.OrderBy(d => d.Timestamp).ToList();
        var odometerReadings = orderedData.Where(d => d.Odometer > 0).Select(d => d.Odometer).ToList();
        
        return odometerReadings.Any() ? odometerReadings.Last() - odometerReadings.First() : 0;
    }

    private decimal CalculateEfficiencyForData(List<ProcessedVehicleData> data)
    {
        if (!data.Any()) return 0;
        
        var totalEnergy = data.Sum(d => d.Power) / 1000.0m; // kWh
        var distance = CalculateDistanceFromSamples(data);
        
        return totalEnergy > 0 ? distance / totalEnergy : 0;
    }

    private decimal FindOptimalSpeedRange(List<ProcessedVehicleData> data)
    {
        // Raggruppa per range di velocità e trova quello più efficiente
        var speedRanges = data
            .Where(d => d.Speed > 0 && d.Power > 0)
            .GroupBy(d => (d.Speed / 10) * 10) // Raggruppa per decine
            .Where(g => g.Count() > 5) // Solo range con dati sufficienti
            .Select(g => new 
            { 
                SpeedRange = g.Key,
                Efficiency = CalculateEfficiencyForData(g.ToList())
            })
            .OrderByDescending(x => x.Efficiency)
            .FirstOrDefault();

        return speedRanges?.SpeedRange ?? 50; // Default 50 km/h
    }

    private List<string> GenerateEfficiencyTips(decimal overall, decimal city, decimal highway)
    {
        var tips = new List<string>();

        if (city > highway * 1.1m)
            tips.Add("Ottima efficienza in città - mantieni la guida dolce");
        else if (highway > city * 1.1m)
            tips.Add("Efficienza autostradale buona - considera più viaggi lunghi");

        if (overall < 4)
            tips.Add("Riduci accelerazioni brusche per migliorare efficienza");

        if (tips.Count < 3)
            tips.Add("Usa la rigenerazione al massimo in discesa");

        return tips.Take(3).ToList();
    }

    // Metodi riutilizzati dal tuo codice esistente
    private async Task<(int totalGaps, int majorGaps, TimeSpan totalGapTime)> AnalyzeDataGaps(int vehicleId, DateTime start, DateTime end)
    {
        try
        {
            var timestamps = await dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= start && vd.Timestamp <= end)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .ToListAsync();

            if (timestamps.Count < 2)
                return (0, 0, TimeSpan.Zero);

            int totalGaps = 0;
            int majorGaps = 0;
            TimeSpan totalGapTime = TimeSpan.Zero;

            for (int i = 1; i < timestamps.Count; i++)
            {
                var gap = timestamps[i] - timestamps[i - 1];

                if (gap.TotalMinutes > 30) // Gap > 30 minuti
                {
                    totalGaps++;
                    totalGapTime = totalGapTime.Add(gap);

                    if (gap.TotalHours > 2) // Gap maggiore > 2 ore
                        majorGaps++;
                }
            }

            return (totalGaps, majorGaps, totalGapTime);
        }
        catch
        {
            return (0, 0, TimeSpan.Zero);
        }
    }

    private double CalculateUptimePercentage((int totalGaps, int majorGaps, TimeSpan totalGapTime) gaps, TimeSpan period)
    {
        if (period.TotalHours <= 0) return 0;
        
        var activeTime = period - gaps.totalGapTime;
        return Math.Max(0, Math.Min(100, (activeTime.TotalHours / period.TotalHours) * 100));
    }

    private double CalculateQualityScore(int totalRecords, double uptimePercentage, int majorGaps, TimeSpan period)
    {
        double score = 0;

        // 40% - Uptime
        score += (uptimePercentage / 100) * 40;

        // 30% - Densità records
        var recordDensity = totalRecords / Math.Max(period.TotalHours, 1);
        var densityScore = Math.Min(1, recordDensity / 1.0);
        score += densityScore * 30;

        // 20% - Stabilità
        var stabilityPenalty = Math.Min(20, majorGaps * 2);
        score += Math.Max(0, 20 - stabilityPenalty);

        // 10% - Maturità dataset
        if (period.TotalDays >= 30) score += 10;
        else if (period.TotalDays >= 7) score += 7;
        else if (period.TotalDays >= 1) score += 3;

        return Math.Max(0, Math.Min(100, score));
    }

    private string GetQualityLabel(double score) => score switch
    {
        >= 90 => "Eccellente",
        >= 80 => "Ottimo", 
        >= 70 => "Buono",
        >= 60 => "Discreto",
        >= 50 => "Sufficiente",
        _ => "Migliorabile"
    };
}

// Classi di supporto per dati processati
public class ProcessedVehicleData
{
    public DateTime Timestamp { get; set; }
    public bool IsAdaptiveProfiling { get; set; }
    
    // Battery data
    public int BatteryLevel { get; set; }
    public decimal BatteryRange { get; set; }
    public int ChargeLimit { get; set; }
    public string ChargingState { get; set; } = "";
    public decimal ChargeRate { get; set; }
    
    // Climate data
    public decimal InsideTemp { get; set; }
    public decimal OutsideTemp { get; set; }
    public bool IsClimateOn { get; set; }
    public decimal DriverTempSetting { get; set; }
    
    // Drive data
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int Speed { get; set; }
    public int Power { get; set; }
    public int Heading { get; set; }
    
    // Vehicle data
    public decimal Odometer { get; set; }
    public bool IsLocked { get; set; }
    
    // Energy data
    public int SolarPower { get; set; }
    public int GridPower { get; set; }
    public int BatteryPower { get; set; }
    
    // Charging sessions
    public List<ChargingSession> ChargingSessions { get; set; } = new();
}

public class ChargingSession
{
    public int SessionId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string SiteName { get; set; } = "";
    public decimal TotalCost { get; set; }
    public decimal EnergyDelivered { get; set; }
    public decimal CostPerKwh { get; set; }
    public string Currency { get; set; } = "EUR";
}