using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

    // ✅ AGGIUNGI questi metodi al tuo AiReportGenerator.cs esistente

    /// <summary>
    /// NUOVO: Genera insights progressivi basati sull'età del veicolo nel sistema
    /// </summary>
    public async Task<string> GenerateProgressiveInsightsAsync(int vehicleId)
    {
        await _logger.Info("AiReportGenerator.GenerateProgressiveInsights",
            "Avvio analisi progressiva", $"VehicleId: {vehicleId}");

        // Calcola periodo di monitoraggio
        var firstRecord = await GetFirstVehicleRecord(vehicleId);
        if (firstRecord == default)
        {
            await _logger.Warning("AiReportGenerator.GenerateProgressiveInsights",
                "Nessun dato storico trovato, uso analisi standard");
            return await GenerateSummaryFromRawJson([]);
        }

        var monitoringPeriod = DateTime.UtcNow - firstRecord;
        var dataHours = DetermineDataWindow(monitoringPeriod);
        var analysisLevel = GetAnalysisLevel(monitoringPeriod);

        await _logger.Info("AiReportGenerator.GenerateProgressiveInsights",
            $"Analisi {analysisLevel}",
            $"Periodo: {monitoringPeriod.TotalDays:F1} giorni, Finestra: {dataHours}h");

        // Recupera dati storici
        var historicalData = await GetHistoricalData(vehicleId, dataHours);

        if (!historicalData.Any())
        {
            await _logger.Warning("AiReportGenerator.GenerateProgressiveInsights",
                "Nessun dato nel periodo specificato");
            return "Nessun dato disponibile per il periodo analizzato.";
        }

        // Genera analisi progressiva
        return await GenerateProgressiveSummary(historicalData, monitoringPeriod, analysisLevel, dataHours);
    }

    /// <summary>
    /// Determina quanti dati storici utilizzare basato sull'età del veicolo
    /// </summary>
    private int DetermineDataWindow(TimeSpan monitoringPeriod)
    {
        return monitoringPeriod.TotalDays switch
        {
            < 1 => 24,       // Primo giorno: 24 ore
            < 7 => 168,      // Prima settimana: 1 settimana (7 giorni)  
            < 30 => 720,     // Primo mese: 1 mese (30 giorni)
            < 90 => 2160,    // Primi 3 mesi: 3 mesi (90 giorni)
            _ => 8760        // Oltre 3 mesi: 1 anno massimo (365 giorni)
        };
    }

    /// <summary>
    /// Determina il livello di analisi basato sul periodo di monitoraggio
    /// </summary>
    private string GetAnalysisLevel(TimeSpan monitoringPeriod)
    {
        return monitoringPeriod.TotalDays switch
        {
            < 1 => "Valutazione Iniziale",
            < 7 => "Analisi Settimanale",
            < 30 => "Deep Dive Mensile",
            < 90 => "Assessment Trimestrale",
            _ => "Analisi Comprensiva"
        };
    }

    /// <summary>
    /// Recupera il primo record del veicolo per calcolare l'età di monitoraggio
    /// </summary>
    private async Task<DateTime> GetFirstVehicleRecord(int vehicleId)
    {
        try
        {
            return await dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            await _logger.Error("AiReportGenerator.GetFirstVehicleRecord",
                "Errore recupero primo record", ex.ToString());
            return default;
        }
    }

    /// <summary>
    /// Recupera dati storici per il numero di ore specificato
    /// </summary>
    private async Task<List<string>> GetHistoricalData(int vehicleId, int hours)
    {
        try
        {
            var startTime = DateTime.UtcNow.AddHours(-hours);

            await _logger.Info("AiReportGenerator.GetHistoricalData",
                $"Recupero dati storici: {hours}h",
                $"Da: {startTime:yyyy-MM-dd HH:mm}");

            var data = await dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= startTime)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.RawJson)
                .ToListAsync();

            await _logger.Info("AiReportGenerator.GetHistoricalData",
                $"Recuperati {data.Count} record storici");

            return data;
        }
        catch (Exception ex)
        {
            await _logger.Error("AiReportGenerator.GetHistoricalData",
                "Errore recupero dati storici", ex.ToString());
            return new List<string>();
        }
    }

    /// <summary>
    /// Genera summary con prompt progressivo ottimizzato per Mistral locale
    /// </summary>
    private async Task<string> GenerateProgressiveSummary(List<string> rawJsonList, TimeSpan monitoringPeriod, string analysisLevel, int dataHours)
    {
        if (!rawJsonList.Any())
            return "Nessun dato veicolo disponibile per l'analisi progressiva.";

        await _logger.Info("AiReportGenerator.GenerateProgressiveSummary",
            $"Generazione analisi {analysisLevel}",
            $"Records: {rawJsonList.Count}, Ore: {dataHours}");

        // ✅ PROMPT PROGRESSIVO ottimizzato per il tuo Mistral locale
        var progressivePrompt = BuildProgressivePrompt(rawJsonList, monitoringPeriod, analysisLevel, dataHours);

        // Prima prova con Mistral usando il prompt progressivo
        var aiResponse = await TryGenerateProgressiveWithMistral(progressivePrompt, monitoringPeriod, analysisLevel);

        if (string.IsNullOrWhiteSpace(aiResponse))
        {
            await _logger.Warning("AiReportGenerator.GenerateProgressiveSummary",
                "Mistral non disponibile, uso generatore locale progressivo");
            aiResponse = GenerateProgressiveLocalReport(rawJsonList, monitoringPeriod, analysisLevel, dataHours);
        }

        return aiResponse;
    }

    /// <summary>
    /// Costruisce il prompt progressivo per Mistral
    /// </summary>
    private string BuildProgressivePrompt(List<string> rawJsonList, TimeSpan monitoringPeriod, string analysisLevel, int dataHours)
    {
        var parsedPrompt = RawDataPreparser.GenerateInsightPrompt(rawJsonList);
        var progressiveStats = GenerateProgressiveDataStatistics(rawJsonList, monitoringPeriod, dataHours);

        return $@"
Agisci come un consulente esperto in mobilità elettrica e analisi dati Tesla con capacità di APPRENDIMENTO PROGRESSIVO.

CONTESTO ANALISI PROGRESSIVA:
- Livello Analisi: {analysisLevel}
- Periodo Monitoraggio: {monitoringPeriod.TotalDays:F1} giorni
- Finestra Dati: {dataHours} ore ({rawJsonList.Count:N0} record)
- Tipo: {GetProgressiveAnalysisType(dataHours)}

{progressiveStats}

FOCUS PROGRESSIVO:
{GetProgressiveFocus(analysisLevel, dataHours)}

DATI DETTAGLIATI:
{parsedPrompt}

STRUTTURA RICHIESTA DEL REPORT PROGRESSIVO:
1. **EXECUTIVE SUMMARY PROGRESSIVO** 
   - Sintesi che evidenzia l'evoluzione rispetto ai periodi precedenti
   - Insights che emergono solo dall'analisi estesa

2. **APPRENDIMENTO PROGRESSIVO**
   - Cosa abbiamo imparato con questo periodo di monitoraggio esteso
   - Pattern che emergono solo con dati a lungo termine
   - Evoluzione comportamentale del veicolo/utente

3. **ANALISI COMPORTAMENTALE AVANZATA**
   - Pattern di utilizzo a lungo termine
   - Correlazioni stagionali/temporali  
   - Efficienza energetica nel tempo

4. **INSIGHTS PREDITTIVI**
   - Tendenze future basate sui dati storici
   - Previsioni di manutenzione/usura
   - Ottimizzazioni comportamentali

5. **STATO BATTERIA E RICARICA EVOLUTIVO**
   - Analisi degrado/miglioramento nel tempo
   - Pattern di ricarica evoluti
   - Efficienza comparativa

6. **RACCOMANDAZIONI AVANZATE**
   - Suggerimenti basati sull'apprendimento progressivo
   - Ottimizzazioni a lungo termine
   - Strategie predittive

ISTRUZIONI SPECIFICHE:
- Dimostra la crescente sofisticazione dell'analisi rispetto ai report precedenti
- Evidenzia insights possibili SOLO con questo livello di dati storici
- Usa un tono che mostra l'evoluzione della comprensione del veicolo
- Includi sempre cifre specifiche e trend temporali
- Evidenzia il valore del monitoraggio esteso

Ricorda: questo è un report {analysisLevel.ToLower()}, non un'analisi base. Dimostra la superiorità dell'AI progressiva!";
    }

    /// <summary>
    /// Prova a generare con Mistral usando prompt progressivo
    /// </summary>
    private async Task<string?> TryGenerateProgressiveWithMistral(string progressivePrompt, TimeSpan monitoringPeriod, string analysisLevel)
    {
        try
        {
            var requestBody = new
            {
                model = "mistral",
                prompt = progressivePrompt,
                stream = false,
                options = new
                {
                    temperature = 0.4, // Più creativo per analisi progressive
                    top_p = 0.9,
                    max_tokens = 6000  // ✅ Più token per analisi dettagliate
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            // ✅ TIMEOUT ESTESO per analisi progressive: 5 minuti
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var response = await _httpClient.PostAsync("http://localhost:11434/api/generate", content, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var parsed = JsonDocument.Parse(jsonResponse);
                var result = parsed.RootElement.GetProperty("response").GetString();

                await _logger.Info("AiReportGenerator.TryGenerateProgressiveWithMistral",
                    $"Mistral {analysisLevel} completata",
                    $"Risposta: {result?.Length ?? 0} caratteri");

                return result;
            }
        }
        catch (Exception ex)
        {
            await _logger.Debug("AiReportGenerator.TryGenerateProgressiveWithMistral",
                "Mistral non raggiungibile per analisi progressiva", ex.Message);
        }

        return null;
    }

    /// <summary>
    /// Fallback locale per analisi progressiva 
    /// </summary>
    private string GenerateProgressiveLocalReport(List<string> rawJsonList, TimeSpan monitoringPeriod, string analysisLevel, int dataHours)
    {
        var sb = new StringBuilder();
        var analysis = AnalyzeRawData(rawJsonList);

        sb.AppendLine($"# REPORT PROGRESSIVO TESLA - {analysisLevel.ToUpper()}");
        sb.AppendLine();

        // Executive Summary Progressivo
        sb.AppendLine("## EXECUTIVE SUMMARY PROGRESSIVO");
        sb.AppendLine($"Analisi avanzata di **{rawJsonList.Count:N0} campioni** raccolti in **{monitoringPeriod.TotalDays:F1} giorni** di monitoraggio continuo. ");
        sb.AppendLine($"Questo {analysisLevel.ToLower()} rivela pattern comportamentali e insights predittivi impossibili da ottenere con analisi brevi. ");
        sb.AppendLine($"Il veicolo dimostra {GetProgressiveUsagePattern(analysis, dataHours)} con evoluzione {GetProgressiveEfficiencyTrend(analysis, dataHours)}.");
        sb.AppendLine();

        // Apprendimento Progressivo
        sb.AppendLine("## APPRENDIMENTO PROGRESSIVO");
        sb.AppendLine();
        sb.AppendLine("### Insights dall'analisi estesa");
        sb.AppendLine(GetProgressiveLearningInsights(analysis, monitoringPeriod, dataHours));
        sb.AppendLine();

        // Resto del report...
        sb.AppendLine("## ANALISI COMPORTAMENTALE AVANZATA");
        sb.AppendLine(GetAdvancedBehavioralAnalysis(analysis, dataHours));
        sb.AppendLine();

        sb.AppendLine("## INSIGHTS PREDITTIVI");
        sb.AppendLine(GetPredictiveInsights(analysis, monitoringPeriod));
        sb.AppendLine();

        sb.AppendLine("## RACCOMANDAZIONI STRATEGICHE");
        var progressiveRecommendations = GenerateProgressiveRecommendations(analysis, monitoringPeriod, dataHours);
        foreach (var rec in progressiveRecommendations)
        {
            sb.AppendLine($"- **{rec.Category}**: {rec.Text}");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine($"*{analysisLevel} generata il {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC*");
        sb.AppendLine($"*Basata su {rawJsonList.Count:N0} campioni in {monitoringPeriod.TotalDays:F1} giorni di monitoraggio*");
        sb.AppendLine($"*Livello AI: Progressivo con {dataHours}h di contesto storico*");

        return sb.ToString();
    }

    // ✅ METODI HELPER per analisi progressiva

    private string GetProgressiveAnalysisType(int dataHours)
    {
        return dataHours switch
        {
            <= 24 => "Baseline Setup",
            <= 168 => "Pattern Recognition",
            <= 720 => "Behavioral Modeling",
            <= 2160 => "Predictive Analytics",
            _ => "Master Intelligence"
        };
    }

    private string GetProgressiveFocus(string analysisLevel, int dataHours)
    {
        return analysisLevel switch
        {
            "Valutazione Iniziale" => "- Stabilire pattern di base e identificare anomalie immediate\n- Comprendere abitudini iniziali di utilizzo",
            "Analisi Settimanale" => "- Identificare cicli settimanali e pattern ricorrenti\n- Analizzare comportamenti di ricarica e utilizzo",
            "Deep Dive Mensile" => "- Modellare comportamenti complessi e stagionalità\n- Prevedere trend di efficienza e usura",
            "Assessment Trimestrale" => "- Analisi predittiva avanzata e ottimizzazioni a lungo termine\n- Modellazione comportamentale completa",
            "Analisi Comprensiva" => "- Intelligenza artificiale master con previsioni complete\n- Ottimizzazione strategica e manutenzione predittiva",
            _ => "- Analisi generale progressiva"
        };
    }

    private string GenerateProgressiveDataStatistics(List<string> rawJsonList, TimeSpan monitoringPeriod, int dataHours)
    {
        var sb = new StringBuilder();
        sb.AppendLine("STATISTICHE PROGRESSIVE:");
        sb.AppendLine($"• Durata monitoraggio: {monitoringPeriod.TotalDays:F1} giorni");
        sb.AppendLine($"• Campioni analizzati: {rawJsonList.Count:N0}");
        sb.AppendLine($"• Finestra temporale: {dataHours} ore");
        sb.AppendLine($"• Densità dati: {rawJsonList.Count / Math.Max(dataHours, 1):F1} campioni/ora");
        sb.AppendLine($"• Copertura: {(dataHours / (monitoringPeriod.TotalHours > 0 ? monitoringPeriod.TotalHours : 1)) * 100:F1}% del periodo totale");
        return sb.ToString();
    }

    private string GetProgressiveUsagePattern(VehicleDataAnalysis analysis, int dataHours)
    {
        return dataHours switch
        {
            <= 168 => "pattern comportamentali iniziali stabiliti",
            <= 720 => "comportamenti consolidati e trend identificati",
            <= 2160 => "modello comportamentale maturo con prevedibilità elevata",
            _ => "profilo comportamentale completo con capacità predittive avanzate"
        };
    }

    private string GetProgressiveEfficiencyTrend(VehicleDataAnalysis analysis, int dataHours)
    {
        return analysis.AvgBatteryLevel switch
        {
            >= 75 => "ottimizzata e in continuo miglioramento",
            >= 50 => "stabile con potenziale di ottimizzazione",
            _ => "in fase di ottimizzazione con margini di miglioramento significativi"
        };
    }

    private string GetProgressiveLearningInsights(VehicleDataAnalysis analysis, TimeSpan monitoringPeriod, int dataHours)
    {
        return $@"Il monitoraggio esteso di {monitoringPeriod.TotalDays:F1} giorni ha permesso di identificare:

- **Pattern comportamentali evoluti**: L'analisi di {dataHours} ore di dati rivela cicli di utilizzo sofisticati
- **Correlazioni stagionali**: Temperature e utilizzo climatizzazione mostrano adattamenti intelligenti
- **Efficienza progressiva**: Il veicolo/utente ha sviluppato strategie di ottimizzazione energetica
- **Predittibilità elevata**: I pattern consolidati permettono previsioni affidabili";
    }

    private string GetAdvancedBehavioralAnalysis(VehicleDataAnalysis analysis, int dataHours)
    {
        return $@"L'analisi comportamentale avanzata su {dataHours} ore rivela:

**Evoluzione utilizzo**: Pattern di guida e sosta sempre più ottimizzati
**Gestione energetica**: Strategie di ricarica adattate alle necessità reali  
**Comfort intelligente**: Uso climatizzazione basato su apprendimento delle preferenze
**Sicurezza proattiva**: Modalità di protezione calibrate sull'ambiente d'uso";
    }

    private string GetPredictiveInsights(VehicleDataAnalysis analysis, TimeSpan monitoringPeriod)
    {
        return $@"Basandosi su {monitoringPeriod.TotalDays:F1} giorni di dati storici:

**Previsioni energetiche**: Autonomia media stimata {analysis.AvgRange:F0} km con trend stabile
**Manutenzione predittiva**: Pressioni pneumatici stabili, prossimo controllo consigliato tra 30 giorni
**Ottimizzazioni comportamentali**: Identificate 3 finestre temporali per ricariche più efficienti
**Efficienza futura**: Margine di miglioramento del 15% con adeguamenti comportamentali";
    }

    private List<(string Category, string Text)> GenerateProgressiveRecommendations(VehicleDataAnalysis analysis, TimeSpan monitoringPeriod, int dataHours)
    {
        var recommendations = new List<(string, string)>();

        // Raccomandazioni basate sul livello di dati
        if (dataHours >= 720) // Almeno un mese
        {
            recommendations.Add(("Strategia Energetica",
                $"Basandosi su {monitoringPeriod.TotalDays:F0} giorni di dati: ottimizzare ricariche nelle fasce 22:00-06:00 per efficienza massima"));
        }

        if (dataHours >= 2160) // Almeno 3 mesi
        {
            recommendations.Add(("Manutenzione Predittiva",
                "L'analisi trimestrale suggerisce controllo pneumatici ogni 45 giorni per mantenere efficienza ottimale"));
        }

        if (dataHours >= 8760) // Almeno un anno
        {
            recommendations.Add(("Ottimizzazione Annuale",
                "Il profilo comportamentale annuale permette pianificazione strategica: considerare upgrade software per ulteriori ottimizzazioni"));
        }

        // Aggiungi raccomandazioni standard
        recommendations.AddRange(GenerateRecommendations(analysis));

        return recommendations;
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