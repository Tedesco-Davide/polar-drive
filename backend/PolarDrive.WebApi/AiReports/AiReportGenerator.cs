using System.Text;
using System.Text.Json;
using PolarDrive.Data.DbContexts;

namespace PolarDrive.WebApi.AiReports;

public class AiReportGenerator(PolarDriveDbContext dbContext)
{
    private readonly HttpClient _httpClient = new();
    private readonly PolarDriveLogger _logger = new(dbContext);

    public async Task<string> GenerateSummaryFromRawJson(List<string> rawJsonList)
    {
        await _logger.Info("AiReportGenerator.GenerateSummaryFromRawJson",
            "Avvio generazione report AI", $"Dati in input: {rawJsonList.Count} record JSON");

        // Usa RawDataPreparser universale (gestisce sia dati reali che mock)
        var parsedPrompt = RawDataPreparser.GenerateInsightPrompt(rawJsonList);
        var dataStats = GenerateDataStatistics(rawJsonList);

        // Prima prova con Mistral locale, poi fallback
        var aiResponse = await TryGenerateWithMistral(parsedPrompt, dataStats);

        if (string.IsNullOrWhiteSpace(aiResponse))
        {
            await _logger.Warning("AiReportGenerator",
                "Mistral non disponibile, uso generatore locale");
            aiResponse = GenerateLocalReport(parsedPrompt, dataStats, rawJsonList);
        }

        return aiResponse;
    }

    private async Task<string?> TryGenerateWithMistral(string parsedPrompt, string dataStats)
    {
        try
        {
            var prompt = $@"
Agisci come un consulente esperto in mobilità elettrica e analisi dati Tesla. 
Analizza questi dati operativi di un veicolo Tesla e produci un report professionale.

CONTESTO DATI:
{dataStats}

DATI DETTAGLIATI:
{parsedPrompt}

STRUTTURA RICHIESTA DEL REPORT:
1. EXECUTIVE SUMMARY (3-4 righe)
2. ANALISI UTILIZZO VEICOLO
- Pattern di utilizzo e stato generale
- Efficienza energetica 
- Utilizzo funzionalità (clima, sicurezza, ecc.)
3. STATO BATTERIA E RICARICA
- Livelli di carica nel periodo
- Efficienza del sistema
4. CLIMATIZZAZIONE E COMFORT
- Utilizzo sistemi clima
- Temperature e consumi correlati
5. SICUREZZA E MANUTENZIONE
- Stato pneumatici e sistemi sicurezza
- Indicatori manutenzione
6. RACCOMANDAZIONI
- Ottimizzazioni operative
- Suggerimenti per efficienza
- Azioni consigliate

Usa un tono professionale ma accessibile. Includi sempre cifre specifiche dove possibile.";

            var requestBody = new
            {
                model = "mistral",
                prompt = prompt,
                stream = false,
                options = new
                {
                    temperature = 0.3,
                    top_p = 0.9,
                    max_tokens = 4000
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            // ✅ TIMEOUT AUMENTATO PER MISTRAL: 3 MINUTI
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var response = await _httpClient.PostAsync("http://localhost:11434/api/generate", content, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var parsed = JsonDocument.Parse(jsonResponse);
                return parsed.RootElement.GetProperty("response").GetString();
            }
        }
        catch (Exception ex)
        {
            await _logger.Debug("AiReportGenerator",
                "Mistral non raggiungibile", ex.Message);
        }

        return null;
    }

    private string GenerateLocalReport(string parsedPrompt, string dataStats, List<string> rawJsonList)
    {
        var sb = new StringBuilder();

        // Analizza i dati per creare un report intelligente
        var analysis = AnalyzeRawData(rawJsonList);

        sb.AppendLine("# REPORT ANALITICO VEICOLO TESLA");
        sb.AppendLine();

        // Executive Summary
        sb.AppendLine("## EXECUTIVE SUMMARY");
        sb.AppendLine($"Analisi di {rawJsonList.Count} campioni di dati raccolti nel periodo di monitoraggio. ");
        sb.AppendLine($"Il veicolo mostra {GetUsagePattern(analysis)} con livelli di efficienza {GetEfficiencyLevel(analysis)}. ");
        sb.AppendLine($"Stato generale del sistema: {GetOverallStatus(analysis)}.");
        sb.AppendLine();

        // Analisi Utilizzo Veicolo
        sb.AppendLine("## ANALISI UTILIZZO VEICOLO");
        sb.AppendLine();
        sb.AppendLine("### Pattern di utilizzo e stato generale");

        if (analysis.HasVehicleData)
        {
            sb.AppendLine($"- **Sicurezza**: Veicolo {(analysis.AvgLocked ? "mantenuto bloccato" : "spesso sbloccato")} durante il periodo");
            sb.AppendLine($"- **Modalità Sentry**: {(analysis.AvgSentryMode ? "Attiva" : "Disattiva")} nella maggior parte del tempo");
            sb.AppendLine($"- **Chilometraggio medio**: {analysis.AvgOdometer:F1} km");
        }

        sb.AppendLine();
        sb.AppendLine("### Efficienza energetica");

        if (analysis.HasChargeData)
        {
            sb.AppendLine($"- **Livello batteria medio**: {analysis.AvgBatteryLevel:F1}%");
            sb.AppendLine($"- **Autonomia media**: {analysis.AvgRange:F1} km");
            sb.AppendLine($"- **Utilizzo vs capacità**: {GetBatteryUsagePattern(analysis)}");
        }

        sb.AppendLine();

        // Stato Batteria e Ricarica
        sb.AppendLine("## STATO BATTERIA E RICARICA");
        sb.AppendLine();

        if (analysis.HasChargeData)
        {
            sb.AppendLine($"**Livello batteria**: Variazione da {analysis.MinBatteryLevel}% a {analysis.MaxBatteryLevel}%");
            sb.AppendLine($"**Autonomia**: Range da {analysis.MinRange:F1} km a {analysis.MaxRange:F1} km");
            sb.AppendLine($"**Limite ricarica**: Impostato mediamente al {analysis.AvgChargeLimit:F1}%");
            sb.AppendLine();
            sb.AppendLine("**Valutazione**: " + GetBatteryHealthAssessment(analysis));
        }
        else
        {
            sb.AppendLine("Dati di ricarica non disponibili nel periodo analizzato.");
        }

        sb.AppendLine();

        // Climatizzazione
        sb.AppendLine("## CLIMATIZZAZIONE E COMFORT");
        sb.AppendLine();

        if (analysis.HasClimateData)
        {
            sb.AppendLine($"**Temperature rilevate**:");
            sb.AppendLine($"- Interna: da {analysis.MinInsideTemp:F1}°C a {analysis.MaxInsideTemp:F1}°C (media: {analysis.AvgInsideTemp:F1}°C)");
            sb.AppendLine($"- Esterna: da {analysis.MinOutsideTemp:F1}°C a {analysis.MaxOutsideTemp:F1}°C (media: {analysis.AvgOutsideTemp:F1}°C)");
            sb.AppendLine();
            sb.AppendLine($"**Utilizzo climatizzazione**: {(analysis.AvgClimateOn ? "Frequente" : "Limitato")}");
            sb.AppendLine($"**Impostazioni medie**: Guidatore {analysis.AvgDriverTemp:F1}°C, Passeggero {analysis.AvgPassengerTemp:F1}°C");
        }
        else
        {
            sb.AppendLine("Dati climatizzazione non disponibili nel periodo analizzato.");
        }

        sb.AppendLine();

        // Sicurezza e Manutenzione
        sb.AppendLine("## SICUREZZA E MANUTENZIONE");
        sb.AppendLine();

        if (analysis.HasTpmsData)
        {
            sb.AppendLine("**Pressioni pneumatici** (medie):");
            sb.AppendLine($"- Anteriore SX: {analysis.AvgTpmsFL:F1} bar");
            sb.AppendLine($"- Anteriore DX: {analysis.AvgTpmsFR:F1} bar");
            sb.AppendLine($"- Posteriore SX: {analysis.AvgTpmsRL:F1} bar");
            sb.AppendLine($"- Posteriore DX: {analysis.AvgTpmsRR:F1} bar");
            sb.AppendLine();
            sb.AppendLine("**Stato pneumatici**: " + GetTireStatus(analysis));
        }

        sb.AppendLine();

        // Raccomandazioni
        sb.AppendLine("## RACCOMANDAZIONI");
        sb.AppendLine();

        var recommendations = GenerateRecommendations(analysis);
        foreach (var rec in recommendations)
        {
            sb.AppendLine($"- **{rec.Category}**: {rec.Text}");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine($"*Report generato il {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC*");
        sb.AppendLine($"*Basato su {rawJsonList.Count} campioni di dati*");

        return sb.ToString();
    }

    private VehicleDataAnalysis AnalyzeRawData(List<string> rawJsonList)
    {
        var analysis = new VehicleDataAnalysis();
        var validSamples = 0;

        foreach (var rawJson in rawJsonList)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                // Prova diverse strutture
                JsonElement dataToAnalyze = root;

                if (root.TryGetProperty("response", out var response) &&
                    response.TryGetProperty("data", out var dataArray) &&
                    dataArray.ValueKind == JsonValueKind.Array)
                {
                    // Processa array di dati
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("content", out var content))
                        {
                            dataToAnalyze = content;
                            break;
                        }
                    }
                }

                ProcessSample(analysis, dataToAnalyze);
                validSamples++;
            }
            catch
            {
                // Ignora campioni non validi
            }
        }

        analysis.FinalizeAverages(validSamples);
        return analysis;
    }

    private void ProcessSample(VehicleDataAnalysis analysis, JsonElement data)
    {
        // Vehicle State
        if (data.TryGetProperty("vehicle_state", out var vehicleState))
        {
            analysis.HasVehicleData = true;

            if (vehicleState.TryGetProperty("locked", out var locked))
                analysis.TotalLocked += locked.GetBoolean() ? 1 : 0;

            if (vehicleState.TryGetProperty("sentry_mode", out var sentry))
                analysis.TotalSentryMode += sentry.GetBoolean() ? 1 : 0;

            if (vehicleState.TryGetProperty("odometer", out var odometer))
            {
                var odometerValue = odometer.GetDecimal();
                analysis.TotalOdometer += odometerValue;
                analysis.OdometerCount++;
            }

            // TPMS Data
            if (vehicleState.TryGetProperty("tpms_pressure_fl", out var tpmsFL))
            {
                analysis.HasTpmsData = true;
                analysis.TotalTpmsFL += tpmsFL.GetDecimal();
                analysis.TotalTpmsFR += vehicleState.GetProperty("tpms_pressure_fr").GetDecimal();
                analysis.TotalTpmsRL += vehicleState.GetProperty("tpms_pressure_rl").GetDecimal();
                analysis.TotalTpmsRR += vehicleState.GetProperty("tpms_pressure_rr").GetDecimal();
                analysis.TpmsCount++;
            }
        }

        // Charge State
        if (data.TryGetProperty("charge_state", out var chargeState))
        {
            analysis.HasChargeData = true;

            if (chargeState.TryGetProperty("battery_level", out var batteryLevel))
            {
                var level = batteryLevel.GetInt32();
                analysis.TotalBatteryLevel += level;
                analysis.MinBatteryLevel = Math.Min(analysis.MinBatteryLevel, level);
                analysis.MaxBatteryLevel = Math.Max(analysis.MaxBatteryLevel, level);
            }

            if (chargeState.TryGetProperty("battery_range", out var range))
            {
                var rangeValue = range.GetDecimal();
                analysis.TotalRange += rangeValue;
                analysis.MinRange = Math.Min(analysis.MinRange, rangeValue);
                analysis.MaxRange = Math.Max(analysis.MaxRange, rangeValue);
            }

            if (chargeState.TryGetProperty("charge_limit_soc", out var chargeLimit))
            {
                analysis.TotalChargeLimit += chargeLimit.GetInt32();
            }

            analysis.ChargeCount++;
        }

        // Climate State
        if (data.TryGetProperty("climate_state", out var climateState))
        {
            analysis.HasClimateData = true;

            if (climateState.TryGetProperty("inside_temp", out var insideTemp))
            {
                var temp = insideTemp.GetDecimal();
                analysis.TotalInsideTemp += temp;
                analysis.MinInsideTemp = Math.Min(analysis.MinInsideTemp, temp);
                analysis.MaxInsideTemp = Math.Max(analysis.MaxInsideTemp, temp);
            }

            if (climateState.TryGetProperty("outside_temp", out var outsideTemp))
            {
                var temp = outsideTemp.GetDecimal();
                analysis.TotalOutsideTemp += temp;
                analysis.MinOutsideTemp = Math.Min(analysis.MinOutsideTemp, temp);
                analysis.MaxOutsideTemp = Math.Max(analysis.MaxOutsideTemp, temp);
            }

            if (climateState.TryGetProperty("is_climate_on", out var climateOn))
                analysis.TotalClimateOn += climateOn.GetBoolean() ? 1 : 0;

            if (climateState.TryGetProperty("driver_temp_setting", out var driverTemp))
                analysis.TotalDriverTemp += driverTemp.GetDecimal();

            if (climateState.TryGetProperty("passenger_temp_setting", out var passengerTemp))
                analysis.TotalPassengerTemp += passengerTemp.GetDecimal();

            analysis.ClimateCount++;
        }

        analysis.TotalSamples++;
    }

    private string GetUsagePattern(VehicleDataAnalysis analysis)
    {
        if (!analysis.HasVehicleData) return "utilizzo normale";

        var securityUsage = analysis.AvgSentryMode ? "alta attenzione alla sicurezza" : "utilizzo standard";
        return securityUsage;
    }

    private string GetEfficiencyLevel(VehicleDataAnalysis analysis)
    {
        if (!analysis.HasChargeData) return "normali";

        return analysis.AvgBatteryLevel switch
        {
            >= 80 => "ottimali",
            >= 60 => "buoni",
            >= 40 => "accettabili",
            _ => "da migliorare"
        };
    }

    private string GetOverallStatus(VehicleDataAnalysis analysis)
    {
        var issues = 0;

        if (analysis.HasChargeData && analysis.AvgBatteryLevel < 30) issues++;
        if (analysis.HasTpmsData && (analysis.AvgTpmsFL < 2.0m || analysis.AvgTpmsFL > 3.0m)) issues++;

        return issues switch
        {
            0 => "Ottimo",
            1 => "Buono con attenzioni minori",
            _ => "Richiede attenzione"
        };
    }

    private string GetBatteryUsagePattern(VehicleDataAnalysis analysis)
    {
        var range = analysis.MaxBatteryLevel - analysis.MinBatteryLevel;
        return range switch
        {
            <= 10 => "Utilizzo limitato, veicolo principalmente fermo",
            <= 30 => "Utilizzo moderato con ricariche regolari",
            <= 50 => "Utilizzo intensivo con buona gestione",
            _ => "Utilizzo molto intensivo"
        };
    }

    private string GetBatteryHealthAssessment(VehicleDataAnalysis analysis)
    {
        if (analysis.AvgBatteryLevel >= 70 && analysis.AvgRange >= 300)
            return "Batteria in ottimo stato con autonomia eccellente.";
        if (analysis.AvgBatteryLevel >= 50 && analysis.AvgRange >= 200)
            return "Batteria in buono stato con autonomia adeguata.";
        return "Batteria richiede attenzione, considerare ottimizzazioni di ricarica.";
    }

    private string GetTireStatus(VehicleDataAnalysis analysis)
    {
        var pressures = new[] { analysis.AvgTpmsFL, analysis.AvgTpmsFR, analysis.AvgTpmsRL, analysis.AvgTpmsRR };
        var inRange = pressures.Count(p => p >= 2.0m && p <= 3.0m);

        return inRange switch
        {
            4 => "Tutte le pressioni sono nel range ottimale.",
            >= 2 => "La maggior parte delle pressioni è corretta, verificare quelle fuori range.",
            _ => "Attenzione: diverse pressioni fuori dal range consigliato."
        };
    }

    private List<(string Category, string Text)> GenerateRecommendations(VehicleDataAnalysis analysis)
    {
        var recommendations = new List<(string, string)>();

        if (analysis.HasChargeData)
        {
            if (analysis.AvgBatteryLevel < 40)
            {
                recommendations.Add(("Efficienza Energetica", "Considerare ricariche più frequenti per mantenere la batteria sopra il 40%"));
            }

            if (analysis.AvgChargeLimit < 80)
            {
                recommendations.Add(("Ricarica", "Valutare l'aumento del limite di ricarica all'80% per uso quotidiano"));
            }
        }

        if (analysis.HasClimateData)
        {
            var tempDiff = Math.Abs(analysis.AvgInsideTemp - analysis.AvgDriverTemp);
            if (tempDiff > 5)
            {
                recommendations.Add(("Comfort", "Ottimizzare le impostazioni climatizzazione per ridurre i consumi"));
            }
        }

        if (analysis.HasTpmsData)
        {
            var pressures = new[] { analysis.AvgTpmsFL, analysis.AvgTpmsFR, analysis.AvgTpmsRL, analysis.AvgTpmsRR };
            if (pressures.Any(p => p < 2.0m || p > 3.0m))
            {
                recommendations.Add(("Manutenzione", "Verificare e regolare le pressioni dei pneumatici secondo le specifiche del costruttore"));
            }
        }

        if (analysis.HasVehicleData && !analysis.AvgSentryMode)
        {
            recommendations.Add(("Sicurezza", "Considerare l'attivazione della modalità Sentry per maggiore sicurezza"));
        }

        // Raccomandazione generica
        recommendations.Add(("Monitoraggio", "Continuare il monitoraggio regolare per identificare pattern di utilizzo e ottimizzazioni"));

        return recommendations;
    }

    private string GenerateDataStatistics(List<string> rawJsonList)
    {
        var stats = new StringBuilder();
        stats.AppendLine($"• Periodo analizzato: {rawJsonList.Count} campioni di dati");
        stats.AppendLine($"• Frequenza di campionamento: circa 1 campione al minuto");
        stats.AppendLine($"• Durata monitoraggio: {rawJsonList.Count} minuti");

        return stats.ToString();
    }
}

// Classe helper per l'analisi dei dati
public class VehicleDataAnalysis
{
    // Vehicle State
    public bool HasVehicleData { get; set; }
    public int TotalLocked { get; set; }
    public int TotalSentryMode { get; set; }
    public decimal TotalOdometer { get; set; }
    public int OdometerCount { get; set; }

    // Charge State
    public bool HasChargeData { get; set; }
    public int TotalBatteryLevel { get; set; }
    public decimal TotalRange { get; set; }
    public int TotalChargeLimit { get; set; }
    public int ChargeCount { get; set; }
    public int MinBatteryLevel { get; set; } = int.MaxValue;
    public int MaxBatteryLevel { get; set; } = int.MinValue;
    public decimal MinRange { get; set; } = decimal.MaxValue;
    public decimal MaxRange { get; set; } = decimal.MinValue;

    // Climate State
    public bool HasClimateData { get; set; }
    public decimal TotalInsideTemp { get; set; }
    public decimal TotalOutsideTemp { get; set; }
    public int TotalClimateOn { get; set; }
    public decimal TotalDriverTemp { get; set; }
    public decimal TotalPassengerTemp { get; set; }
    public int ClimateCount { get; set; }
    public decimal MinInsideTemp { get; set; } = decimal.MaxValue;
    public decimal MaxInsideTemp { get; set; } = decimal.MinValue;
    public decimal MinOutsideTemp { get; set; } = decimal.MaxValue;
    public decimal MaxOutsideTemp { get; set; } = decimal.MinValue;

    // TPMS Data
    public bool HasTpmsData { get; set; }
    public decimal TotalTpmsFL { get; set; }
    public decimal TotalTpmsFR { get; set; }
    public decimal TotalTpmsRL { get; set; }
    public decimal TotalTpmsRR { get; set; }
    public int TpmsCount { get; set; }

    public int TotalSamples { get; set; }

    // Proprietà calcolate
    public bool AvgLocked => TotalSamples > 0 && (double)TotalLocked / TotalSamples > 0.5;
    public bool AvgSentryMode => TotalSamples > 0 && (double)TotalSentryMode / TotalSamples > 0.5;
    public bool AvgClimateOn => ClimateCount > 0 && (double)TotalClimateOn / ClimateCount > 0.5;
    public decimal AvgOdometer => OdometerCount > 0 ? TotalOdometer / OdometerCount : 0;
    public decimal AvgBatteryLevel => ChargeCount > 0 ? (decimal)TotalBatteryLevel / ChargeCount : 0;
    public decimal AvgRange => ChargeCount > 0 ? TotalRange / ChargeCount : 0;
    public decimal AvgChargeLimit => ChargeCount > 0 ? (decimal)TotalChargeLimit / ChargeCount : 0;
    public decimal AvgInsideTemp => ClimateCount > 0 ? TotalInsideTemp / ClimateCount : 0;
    public decimal AvgOutsideTemp => ClimateCount > 0 ? TotalOutsideTemp / ClimateCount : 0;
    public decimal AvgDriverTemp => ClimateCount > 0 ? TotalDriverTemp / ClimateCount : 0;
    public decimal AvgPassengerTemp => ClimateCount > 0 ? TotalPassengerTemp / ClimateCount : 0;
    public decimal AvgTpmsFL => TpmsCount > 0 ? TotalTpmsFL / TpmsCount : 0;
    public decimal AvgTpmsFR => TpmsCount > 0 ? TotalTpmsFR / TpmsCount : 0;
    public decimal AvgTpmsRL => TpmsCount > 0 ? TotalTpmsRL / TpmsCount : 0;
    public decimal AvgTpmsRR => TpmsCount > 0 ? TotalTpmsRR / TpmsCount : 0;

    public void FinalizeAverages(int validSamples)
    {
        // Reset dei minimi se non ci sono dati validi
        if (MinBatteryLevel == int.MaxValue) MinBatteryLevel = 0;
        if (MaxBatteryLevel == int.MinValue) MaxBatteryLevel = 0;
        if (MinRange == decimal.MaxValue) MinRange = 0;
        if (MaxRange == decimal.MinValue) MaxRange = 0;
        if (MinInsideTemp == decimal.MaxValue) MinInsideTemp = 0;
        if (MaxInsideTemp == decimal.MinValue) MaxInsideTemp = 0;
        if (MinOutsideTemp == decimal.MaxValue) MinOutsideTemp = 0;
        if (MaxOutsideTemp == decimal.MinValue) MaxOutsideTemp = 0;
    }
}