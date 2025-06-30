using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.PolarAiReports;

public class PolarAiReportGenerator
{
    private readonly PolarDriveDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly PolarDriveLogger _logger;
    public PolarAiReportGenerator(PolarDriveDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

        _logger = new PolarDriveLogger(_dbContext);

        _httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
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

    public async Task<string> QueueReportGenerationAsync(int vehicleId)
    {
        // Prima recupera il veicolo per ottenere il ClientCompanyId
        var vehicle = await _dbContext.ClientVehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId);

        if (vehicle == null)
        {
            throw new ArgumentException($"Vehicle with ID {vehicleId} not found");
        }

        // Salva lo stato "in elaborazione"
        var newReport = new PdfReport
        {
            ClientVehicleId = vehicleId,
            ClientCompanyId = vehicle.ClientCompanyId,
            Status = "PROCESSING",
            CreatedAt = DateTime.UtcNow,
            ReportPeriodStart = DateTime.UtcNow.AddDays(-1),
            ReportPeriodEnd = DateTime.UtcNow
        };

        await _dbContext.PdfReports.AddAsync(newReport);
        await _dbContext.SaveChangesAsync();

        // Usa l'Id generato dal database
        var jobId = newReport.Id.ToString();

        // Accoda per elaborazione asincrona
        BackgroundJob.Enqueue(() => ProcessReportAsync(vehicleId, newReport.Id));

        return jobId;
    }

    public async Task ProcessReportAsync(int vehicleId, int reportId)
    {
        try
        {
            // Chiama il metodo rinominato che contiene la logica esistente
            var result = await GeneratePolarAiInsightsAsync(vehicleId);

            // Aggiorna stato a completato
            var report = await _dbContext.PdfReports.FindAsync(reportId);
            if (report != null)
            {
                report.Status = "COMPLETED";
                report.GeneratedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Aggiorna stato a errore
            var report = await _dbContext.PdfReports.FindAsync(reportId);
            if (report != null)
            {
                report.Status = "ERROR";
                report.Notes = $"Error: {ex.Message}";
                await _dbContext.SaveChangesAsync();
            }
        }
    }

    public async Task<string> GeneratePolarAiInsightsAsync(int vehicleId)
    {
        // 0) log di avvio
        await _logger.Info(
            "PolarAiReportGenerator.GenerateInsights",
            "Avvio analisi",
            $"VehicleId: {vehicleId}");

        // Verifica se ho già generato almeno un report per questo veicolo
        var alreadyGenerated = await _dbContext.PdfReports
            .AnyAsync(r => r.ClientVehicleId == vehicleId);

        TimeSpan monitoringPeriod;
        int dataHours;
        string analysisLevel;

        if (!alreadyGenerated)
        {
            // PRIMO PDF: uso il parziale del giorno corrente
            monitoringPeriod = TimeSpan.FromHours(24);
            dataHours = 24;
            analysisLevel = "Valutazione Iniziale";
        }
        else
        {
            // Report successivi: usa la logica progressiva CORRETTA
            var firstRecord = await GetFirstVehicleRecord(vehicleId);

            if (firstRecord == default)
            {
                monitoringPeriod = TimeSpan.FromHours(24);
                dataHours = 24;
                analysisLevel = "Valutazione Iniziale";
            }
            else
            {
                monitoringPeriod = DateTime.UtcNow - firstRecord;

                // Conta quanti report esistono già per decidere la finestra
                var reportCount = await _dbContext.PdfReports
                    .CountAsync(r => r.ClientVehicleId == vehicleId);

                // Se è il primo report usa sempre 24h
                if (reportCount == 0)
                {
                    dataHours = 24;
                }
                else
                {
                    dataHours = DetermineDataWindow(monitoringPeriod);
                }

                analysisLevel = GetAnalysisLevel(monitoringPeriod);
            }
        }

        // 1) log del tipo di analisi e finestra scelta
        await _logger.Info(
            "PolarAiReportGenerator.GenerateInsights",
            $"Analisi {analysisLevel}",
            $"Finestra: {dataHours}h (Period: {monitoringPeriod.TotalDays:F1} giorni)");

        // 2) recupero dati
        var historicalData = await GetHistoricalData(vehicleId, dataHours);

        if (historicalData.Count == 0)
        {
            await _logger.Warning(
                "PolarAiReportGenerator.GenerateInsights",
                "Nessun dato nel periodo specificato",
                null);
            return "Nessun dato disponibile per il periodo analizzato.";
        }

        // 3) genero e ritorno il report
        return await GenerateSummary(historicalData, monitoringPeriod, analysisLevel, dataHours, vehicleId);
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
            return await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            await _logger.Error("PolarAiReportGenerator.GetFirstVehicleRecord",
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

            await _logger.Info("PolarAiReportGenerator.GetHistoricalData",
                $"Recupero dati storici: {hours}h",
                $"Da: {startTime:yyyy-MM-dd HH:mm}");

            var data = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= startTime)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.RawJson)
                .ToListAsync();

            await _logger.Info("PolarAiReportGenerator.GetHistoricalData",
                $"Recuperati {data.Count} record storici");

            return data;
        }
        catch (Exception ex)
        {
            await _logger.Error("PolarAiReportGenerator.GetHistoricalData",
                "Errore recupero dati storici", ex.ToString());
            return new List<string>();
        }
    }

    private async Task<string> GenerateSummary(List<string> rawJsonList, TimeSpan monitoringPeriod, string analysisLevel, int dataHours, int vehicleId)
    {
        if (!rawJsonList.Any())
            return "Nessun dato veicolo disponibile per l'analisi.";

        await _logger.Info("PolarAiReportGenerator.GenerateSummary",
            $"Generazione analisi {analysisLevel}",
            $"Records: {rawJsonList.Count}, Ore: {dataHours}");

        // ✅ PROMPT ottimizzato per Polar Ai
        var prompt = await BuildPrompt(rawJsonList, monitoringPeriod, analysisLevel, dataHours, vehicleId);
        const int maxRetries = 3;
        const int retryDelaySeconds = 30;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            await _logger.Info("PolarAiReportGenerator.GenerateSummary",
                $"Tentativo {attempt}/{maxRetries} con Polar Ai",
                $"Analisi: {analysisLevel}");

            //var aiResponse = await TryGenerateWithPolarAi(prompt, analysisLevel);
            var aiResponse = "TEST_POLAR_AI_NO_ELAB";

            if (!string.IsNullOrWhiteSpace(aiResponse))
            {
                await _logger.Info("PolarAiReportGenerator.GenerateSummary",
                    $"Polar Ai completata al tentativo {attempt}",
                    $"Risposta: {aiResponse.Length} caratteri");
                return aiResponse;
            }

            // ✅ Se non è l'ultimo tentativo, aspetta prima di riprovare
            if (attempt < maxRetries)
            {
                await _logger.Warning("PolarAiReportGenerator.GenerateSummary",
                    $"Tentativo {attempt} fallito, riprovo tra {retryDelaySeconds}s",
                    null);
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
            }
        }

        // ✅ Dopo tutti i tentativi falliti, lancia eccezione
        var errorMessage = $"Polar Ai non disponibile dopo {maxRetries} tentativi per {analysisLevel}";
        await _logger.Error("PolarAiReportGenerator.GenerateSummary", errorMessage, null);
        throw new InvalidOperationException(errorMessage);
    }

    /// <summary>
    /// Costruisce il prompt ottimizzato per Polar Ai
    /// </summary>
    private async Task<string> BuildPrompt(List<string> rawJsonList, TimeSpan monitoringPeriod, string analysisLevel, int dataHours, int vehicleId)
    {
        var parsedPrompt = await RawDataPreparser.GenerateInsightPrompt(rawJsonList, vehicleId, _dbContext);
        var stats = GenerateDataStatistics(rawJsonList, monitoringPeriod, dataHours);

        return $@"
                    # POLAR AI - CONSULENTE ESPERTO MOBILITÀ ELETTRICA

                    **RUOLO**: Senior Data Analyst specializzato in veicoli Tesla con sistema di apprendimento progressivo avanzato.

                    ## PARAMETRI ANALISI CORRENTE
                    **Livello**: {analysisLevel}  
                    **Periodo Totale**: {monitoringPeriod.TotalDays:F1} giorni  
                    **Finestra Analizzata**: {dataHours} ore  
                    **Dataset**: {rawJsonList.Count:N0} record telemetrici  
                    **Tipologia**: {GetAnalysisType(dataHours)}

                    {stats}

                    ## OBIETTIVI PROGRESSIVI SPECIFICI
                    {GetFocus(analysisLevel, dataHours)}

                    ## DATASET TELEMETRICO
                    ```json
                    {parsedPrompt}
                    ```

                    ## FORMATO OUTPUT RICHIESTO

                    ### 1. 🎯 EXECUTIVE SUMMARY
                    - **Stato attuale**: Valutazione sintetica delle performance
                    - **Evoluzione**: Cambiamenti significativi rispetto ai baseline precedenti
                    - **KPI principali**: Batteria, efficienza, utilizzo (con percentuali precise)
                    - **Alert**: Eventuali anomalie o trend preoccupanti

                    ### 2. 📈 APPRENDIMENTO PROGRESSIVO
                    - **Nuovi pattern identificati**: Cosa emerge SOLO con questo livello di dati
                    - **Correlazioni inedite**: Relazioni scoperte nell'analisi estesa
                    - **Comportamento evolutivo**: Come cambia l'utilizzo nel tempo
                    - **Baseline aggiornati**: Nuovi parametri di riferimento stabiliti

                    ### 3. 🔍 ANALISI COMPORTAMENTALE AVANZATA
                    - **Cicli temporali**: Pattern giornalieri/settimanali/mensili
                    - **Efficienza energetica**: Trend di consumo e ottimizzazioni
                    - **Modalità di guida**: Stili di utilizzo e loro impatti
                    - **Ricarica intelligente**: Strategie adottate e risultati

                    ### 4. 🔮 INSIGHTS PREDITTIVI
                    - **Previsioni a breve termine** (1-4 settimane)
                    - **Trend di degrado batteria** (con modelli matematici)
                    - **Manutenzione predittiva** (componenti e tempistiche)
                    - **Ottimizzazioni comportamentali** (ROI stimato)

                    ### 5. 🔋 ANALISI BATTERIA & RICARICA EVOLUTIVA
                    - **Salute batteria**: Trend di capacità e degrado
                    - **Efficienza ricarica**: Velocità, costi, pattern temporali
                    - **Cicli di vita**: Analisi deep/shallow cycles
                    - **Confronto benchmarks**: Performance vs standard di settore

                    ### 6. 💡 RACCOMANDAZIONI STRATEGICHE
                    - **Immediate** (implementabili subito)
                    - **A medio termine** (1-3 mesi) 
                    - **Strategiche** (3+ mesi)
                    - **ROI stimato** per ogni raccomandazione

                    ## VINCOLI DI QUALITÀ

                    **PRECISIONE NUMERICA**: Tutti i valori devono essere specifici e verificabili
                    **CONSISTENZA**: Mantenere coerenza con analisi precedenti dello stesso veicolo
                    **PROFESSIONALITÀ**: Linguaggio tecnico ma accessibile, evitare speculazioni
                    **ACTIONABILITY**: Ogni insight deve tradursi in azioni concrete
                    **COMPARABILITÀ**: Fornire sempre benchmark e confronti temporali

                    ## ELEMENTI OBBLIGATORI

                    ✅ **Metriche quantitative** in ogni sezione  
                    ✅ **Trend temporali** con direzione e velocità  
                    ✅ **Confidence level** per le previsioni  
                    ✅ **Impatto economico** stimato  
                    ✅ **Timeline** per implementazione raccomandazioni  

                    ## STILE OUTPUT

                    - **Formato**: Markdown professionale con emoji per sezioni
                    - **Lunghezza**: Proporzioanle al livello di analisi ({analysisLevel})
                    - **Tone**: Consultoriale esperto, fiducioso ma non presuntuoso
                    - **Focus**: Valore business e ottimizzazione pratica

                    ---
                    **GENERA REPORT {analysisLevel.ToUpper()} SECONDO QUESTE SPECIFICHE**";
    }

    /// <summary>
    /// Prova a generare con Polar Ai usando prompt
    /// </summary>
    private async Task<string?> TryGenerateWithPolarAi(string prompt, string analysisLevel)
    {
        try
        {
            var requestBody = new
            {
                model = "deepseek-r1:8b",
                prompt = prompt,
                temperature = 0.3,
                top_p = 0.9,
                stream = false,
                options = new
                {
                    num_ctx = 20000,
                    num_predict = 2048
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(
                "http://127.0.0.1:11434/api/generate",
                content
            );

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                await _logger.Error("PolarAiReportGenerator.TryGenerateWithPolarAi",
                    $"Errore {response.StatusCode}", err);
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            var text = doc.RootElement
                          .GetProperty("response")
                          .GetString();

            await _logger.Info("PolarAiReportGenerator.TryGenerateWithPolarAi",
                $"Polar Ai {analysisLevel} completata",
                $"Risposta: {text?.Length ?? 0} caratteri");

            return text;
        }
        catch (Exception ex)
        {
            await _logger.Debug("PolarAiReportGenerator.TryGenerateWithPolarAi",
                "Polar Ai non raggiungibile per analisi", ex.Message);
            return null;
        }
    }

    private string GetAnalysisType(int dataHours)
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

    private string GetFocus(string analysisLevel, int dataHours)
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

    private string GenerateDataStatistics(List<string> rawJsonList, TimeSpan monitoringPeriod, int dataHours)
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
}