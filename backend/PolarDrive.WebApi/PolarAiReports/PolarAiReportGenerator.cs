using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;

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

            var aiResponse = await TryGenerateWithPolarAi(prompt, analysisLevel);
            //var aiResponse = "TEST_POLAR_AI_NO_ELAB";

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

                ## DATASET TELEMETRICO E ADAPTIVE PROFILING
                ⚠️ **IMPORTANTE**: I dati seguenti includono informazioni SMS Adaptive Profiling che DEVONO essere integrate nel report finale, specialmente nella sezione ""Apprendimento Progressivo"".

                ```json
                {parsedPrompt}
                ```

                ## ISTRUZIONI SPECIALI PER ADAPTIVE PROFILING SMS
                - Se presenti dati ""ADAPTIVE PROFILING SMS"", integrarli nella sezione ""📈 APPRENDIMENTO PROGRESSIVO""
                - Menzionare sessioni attive, pattern di utilizzo e frequenza delle attivazioni
                - Includere analisi dei dati raccolti durante le sessioni adaptive
                - Non ignorare mai le informazioni SMS se presenti nel dataset

                ## FORMATO OUTPUT RICHIESTO

                ### 1. 🎯 EXECUTIVE SUMMARY
                - **Stato attuale**: Valutazione sintetica delle performance
                - **Evoluzione**: Cambiamenti significativi rispetto ai baseline precedenti
                - **KPI principali**: Batteria, efficienza, utilizzo (con percentuali precise)
                - **Alert**: Eventuali anomalie o trend preoccupanti

                ### 2. 📈 APPRENDIMENTO PROGRESSIVO
                - **Sessioni Adaptive Profiling**: Se presenti nel dataset, analizzare sessioni attive, pattern temporali, frequenza utilizzo
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
                **COMPLETEZZA**: Non omettere MAI dati presenti nel dataset, inclusi SMS Adaptive Profiling

                ## ELEMENTI OBBLIGATORI

                ✅ **Metriche quantitative** in ogni sezione  
                ✅ **Trend temporali** con direzione e velocità  
                ✅ **Confidence level** per le previsioni  
                ✅ **Impatto economico** stimato  
                ✅ **Timeline** per implementazione raccomandazioni
                ✅ **Integrazione dati SMS Adaptive Profiling** se presenti

                ## STILE OUTPUT

                - **Formato**: Markdown professionale con emoji per sezioni
                - **Lunghezza**: Proporzioanle al livello di analisi ({analysisLevel})
                - **Tone**: Consultoriale esperto, fiducioso ma non presuntuoso
                - **Focus**: Valore business e ottimizzazione pratica

                ---
                **GENERA REPORT {analysisLevel.ToUpper()} SECONDO QUESTE SPECIFICHE**
                **ASSICURATI DI INCLUDERE TUTTI I DATI PRESENTI NEL DATASET, INCLUSI ADAPTIVE PROFILING SMS**";
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