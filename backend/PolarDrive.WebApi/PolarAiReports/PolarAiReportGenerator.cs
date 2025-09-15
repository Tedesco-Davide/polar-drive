using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PolarDrive.Data.DbContexts;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.PolarAiReports;

public class PolarAiReportGenerator
{
    private readonly PolarDriveDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly PolarDriveLogger _logger;
    private readonly OllamaConfig _ollamaConfig;

    public PolarAiReportGenerator(PolarDriveDbContext dbContext, IOptionsSnapshot<OllamaConfig> ollama)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = new PolarDriveLogger(_dbContext);
        _ollamaConfig = ollama?.Value ?? throw new ArgumentNullException(nameof(ollama));
        _httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<string> GeneratePolarAiInsightsAsync(int vehicleId)
    {
        // 0) log di avvio
        await _logger.Info(
            "PolarAiReportGenerator.GenerateInsights",
            "Avvio analisi",
            $"VehicleId: {vehicleId}");

        // ✅ SEMPRE FINESTRA MENSILE - Calcola il periodo di monitoraggio per il context
        var monitoringPeriod = await CalculateMonitoringPeriod(vehicleId);
        var analysisLevel = GetAnalysisLevel(monitoringPeriod);

        // ✅ SEMPRE 720 ORE (30 GIORNI) - Finestra dati unificata
        const int dataHours = MONTHLY_HOURS_THRESHOLD;

        // 1) log del tipo di analisi e finestra unificata
        await _logger.Info(
            "PolarAiReportGenerator.GenerateInsights",
            $"Analisi {analysisLevel}",
            $"Finestra UNIFICATA: {dataHours}h ({dataHours / 24} giorni) - Period totale: {monitoringPeriod.TotalDays:F1} giorni");

        // 2) recupero dati mensili
        var historicalData = await GetHistoricalData(vehicleId, dataHours);

        if (historicalData.Count == 0)
        {
            await _logger.Warning(
                "PolarAiReportGenerator.GenerateInsights",
                "Nessun dato nel periodo mensile specificato",
                null);
            return "Nessun dato disponibile per l'analisi mensile.";
        }

        // 3) genero e ritorno il report
        return await GenerateSummary(historicalData, monitoringPeriod, analysisLevel, dataHours, vehicleId);
    }

    /// <summary>
    /// Calcola il periodo totale di monitoraggio (per context)
    /// </summary>
    private async Task<TimeSpan> CalculateMonitoringPeriod(int vehicleId)
    {
        try
        {
            var firstRecord = await GetFirstVehicleRecord(vehicleId);

            if (firstRecord == default)
            {
                // Se non ci sono record, considera solo il giorno corrente
                return TimeSpan.FromDays(1);
            }

            return DateTime.UtcNow - firstRecord;
        }
        catch (Exception ex)
        {
            await _logger.Error("PolarAiReportGenerator.CalculateMonitoringPeriod",
                "Errore calcolo periodo monitoraggio", ex.ToString());
            return TimeSpan.FromDays(1);
        }
    }

    /// <summary>
    /// Determina il livello di analisi basato sul periodo TOTALE di monitoraggio
    /// I dati utilizzati sono sempre gli ultimi 30 giorni, ma il livello cambia in base alla maturità
    /// </summary>
    private string GetAnalysisLevel(TimeSpan totalMonitoringPeriod)
    {
        return totalMonitoringPeriod.TotalDays switch
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
    /// Recupera sempre gli ultimi 30 giorni (720 ore)
    /// </summary>
    private async Task<List<string>> GetHistoricalData(int vehicleId, int hours)
    {
        try
        {
            var startTime = DateTime.UtcNow.AddHours(-hours);

            await _logger.Info("PolarAiReportGenerator.GetHistoricalData",
                $"Recupero dati MENSILI: {hours}h ({hours / 24} giorni)",
                $"Da: {startTime:yyyy-MM-dd HH:mm}");

            var data = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= startTime)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.RawJson)
                .ToListAsync();

            await _logger.Info("PolarAiReportGenerator.GetHistoricalData",
                $"Recuperati {data.Count} record mensili");

            return data;
        }
        catch (Exception ex)
        {
            await _logger.Error("PolarAiReportGenerator.GetHistoricalData",
                "Errore recupero dati mensili", ex.ToString());
            return new List<string>();
        }
    }

    private async Task<string> GenerateSummary(List<string> rawJsonList, TimeSpan totalMonitoringPeriod, string analysisLevel, int dataHours, int vehicleId)
    {
        if (!rawJsonList.Any())
            return "Nessun dato veicolo disponibile per l'analisi mensile.";

        await _logger.Info("PolarAiReportGenerator.GenerateSummary",
            $"Generazione analisi {analysisLevel}",
            $"Records mensili: {rawJsonList.Count}, Finestra: {dataHours}h");

        // ✅ PROMPT ottimizzato per analisi mensile unificata
        var prompt = await BuildPrompt(rawJsonList, totalMonitoringPeriod, analysisLevel, dataHours, vehicleId);
        var maxRetries = _ollamaConfig.MaxRetries;
        var retryDelaySeconds = _ollamaConfig.RetryDelaySeconds;

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
    /// Costruisce il prompt ottimizzato con certificazione obbligatoria
    /// </summary>
    private async Task<string> BuildPrompt(List<string> rawJsonList, TimeSpan totalMonitoringPeriod, string analysisLevel, int dataHours, int vehicleId)
    {
        var parsedPrompt = await RawDataPreparser.GenerateInsightPrompt(rawJsonList, vehicleId, _dbContext);
        var stats = await GenerateDataStatistics(rawJsonList, totalMonitoringPeriod, dataHours, vehicleId);

        return $@"
                # POLAR AI - CONSULENTE ESPERTO MOBILITÀ ELETTRICA

                **RUOLO**: Senior Data Analyst specializzato in veicoli Tesla con sistema di analisi mensile unificata.

                ## PARAMETRI ANALISI MENSILE UNIFICATA
                **Livello**: {analysisLevel}  
                **Periodo Totale Monitoraggio**: {totalMonitoringPeriod.TotalDays:F1} giorni  
                **Finestra Dati Analizzata**: SEMPRE {dataHours} ore (30 giorni)  
                **Dataset**: {rawJsonList.Count:N0} record telemetrici mensili  
                **Strategia**: Analisi mensile consistente con context evolutivo

                ## ⚠️ IMPORTANTE: CERTIFICAZIONE DATAPOLAR NEL PDF
                **NOTA CRITICA**: La certificazione DataPolar sarà automaticamente inclusa nel PDF finale attraverso il sistema HTML. 
                Tu concentrati sull'analisi tecnica e comportamentale senza ripetere le statistiche di certificazione.
                
                **Le seguenti statistiche sono per il tuo context di analisi:**
                {stats}

                ## OBIETTIVI ANALISI MENSILE PER LIVELLO
                {GetMonthlyFocus(analysisLevel, totalMonitoringPeriod)}

                ## DATASET TELEMETRICO E ADAPTIVE PROFILING (ULTIMI 30 GIORNI)
                ⚠️ **IMPORTANTE**: I dati seguenti rappresentano gli ultimi 30 giorni di telemetria e DEVONO essere integrati nel report finale, specialmente le informazioni SMS Adaptive Profiling.

                ```json
                {parsedPrompt}
                ```

                ## ISTRUZIONI SPECIALI PER ADAPTIVE PROFILING SMS
                - Se presenti dati ""ADAPTIVE PROFILING SMS"" nel periodo mensile, integrarli nella sezione ""📈 APPRENDIMENTO PROGRESSIVO""
                - Menzionare sessioni attive, pattern di utilizzo mensili e frequenza delle attivazioni
                - Includere analisi dei dati raccolti durante le sessioni adaptive del mese
                - Non ignorare mai le informazioni SMS se presenti nel dataset mensile

                ## FORMATO OUTPUT RICHIESTO (ANALISI MENSILE)

                ### 1. 🎯 EXECUTIVE SUMMARY MENSILE
                - **Stato attuale**: Valutazione performance ultimo mese
                - **Evoluzione**: Cambiamenti rispetto al contesto di {totalMonitoringPeriod.TotalDays:F0} giorni totali
                - **KPI mensili**: Batteria, efficienza, utilizzo (con percentuali precise)
                - **Alert mensili**: Anomalie o trend preoccupanti nel periodo

                ### 2. 📈 APPRENDIMENTO PROGRESSIVO (CONTESTO {analysisLevel.ToUpper()})
                - **Sessioni Adaptive Profiling**: Analisi delle sessioni negli ultimi 30 giorni
                - **Pattern mensili identificati**: Cosa emerge dall'analisi mensile
                - **Correlazioni mensili**: Relazioni scoperte nei 30 giorni
                - **Evoluzione comportamentale**: Come il comportamento è cambiato nel mese
                - **Baseline mensili**: Parametri di riferimento del periodo

                ### 3. 🔍 ANALISI COMPORTAMENTALE MENSILE
                - **Cicli mensili**: Pattern identificati nei 30 giorni
                - **Efficienza energetica mensile**: Trend e ottimizzazioni del mese
                - **Modalità di guida mensile**: Stili di utilizzo e impatti
                - **Ricarica intelligente mensile**: Strategie e risultati

                ### 4. 🔮 INSIGHTS PREDITTIVI (BASE MENSILE)
                - **Previsioni prossimo mese**: Basate sui dati mensili correnti
                - **Trend batteria mensile**: Con proiezioni
                - **Manutenzione predittiva**: Basata su usage mensile
                - **Ottimizzazioni comportamentali**: ROI per il prossimo periodo

                ### 5. 🔋 ANALISI BATTERIA & RICARICA (PERFORMANCE MENSILE)
                - **Salute batteria mensile**: Trend e degrado nel periodo
                - **Efficienza ricarica mensile**: Performance degli ultimi 30 giorni
                - **Cicli di vita mensili**: Analisi dei cicli nel periodo
                - **Confronto mensile**: Performance vs standard

                ### 6. 💡 RACCOMANDAZIONI STRATEGICHE (FOCUS MENSILE)
                - **Immediate**: Basate su pattern mensili identificati
                - **Prossimo mese**: Ottimizzazioni per i prossimi 30 giorni
                - **Trimestrali**: Strategie a medio termine
                - **ROI mensile**: Stima benefici implementazione

                ## VINCOLI DI QUALITÀ MENSILE

                **PRECISIONE NUMERICA**: Tutti i valori devono riferirsi al periodo mensile analizzato
                **FOCUS TECNICO**: Concentrati su analisi comportamentale e predittiva (non ripetere statistiche di base)
                **PROFESSIONALITÀ**: Linguaggio tecnico ma accessibile
                **ACTIONABILITY**: Ogni insight mensile deve tradursi in azioni concrete
                **COMPARABILITÀ**: Fornire benchmark e confronti mensili
                **COMPLETEZZA**: Analizzare TUTTI i dati mensili, inclusi SMS Adaptive Profiling

                ## ELEMENTI OBBLIGATORI MENSILI

                ✅ **Metriche quantitative mensili** in ogni sezione  
                ✅ **Trend mensili** con direzione e velocità  
                ✅ **Confidence level** per previsioni mensili  
                ✅ **Impatto economico mensile** stimato  
                ✅ **Timeline mensile** per implementazione raccomandazioni
                ✅ **Integrazione completa dati SMS Adaptive Profiling mensili**

                ## STILE OUTPUT

                - **Formato**: Markdown professionale con emoji per sezioni
                - **Lunghezza**: Proporzionale al livello {analysisLevel} ma focus mensile
                - **Tone**: Consultoriale esperto, focus su performance mensili
                - **Focus**: Valore business e ottimizzazione basata su dati mensili
                - **Evita**: Ripetizione delle statistiche di certificazione (già nel PDF)

                ---
                **GENERA REPORT {analysisLevel.ToUpper()} CON FOCUS TECNICO-COMPORTAMENTALE**
                **ANALIZZA I {dataHours / 24} GIORNI DI DATI NEL CONTESTO DI {totalMonitoringPeriod.TotalDays:F0} GIORNI TOTALI**
                **LA CERTIFICAZIONE DATAPOLAR È GIÀ INCLUSA NEL PDF - CONCENTRATI SULL'ANALISI**";
    }

    /// <summary>
    /// Genera con Polar Ai usando prompt
    /// </summary>
    private async Task<string?> TryGenerateWithPolarAi(string prompt, string analysisLevel)
    {
        try
        {
            var requestBody = new
            {
                model = _ollamaConfig.Model,
                prompt = prompt,
                temperature = _ollamaConfig.Temperature,
                top_p = _ollamaConfig.TopP,
                stream = false,
                options = new
                {
                    num_ctx = _ollamaConfig.ContextWindow,
                    num_predict = _ollamaConfig.MaxTokens,
                    repeat_penalty = _ollamaConfig.RepeatPenalty,
                    top_k = _ollamaConfig.TopK
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(
                $"{_ollamaConfig.Endpoint}/api/generate",
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

    /// <summary>
    /// Focus specifico per analisi mensile in base al livello
    /// </summary>
    private string GetMonthlyFocus(string analysisLevel, TimeSpan totalMonitoringPeriod)
    {
        var contextInfo = totalMonitoringPeriod.TotalDays < 30
            ? $"(Dati parziali: {totalMonitoringPeriod.TotalDays:F0} giorni disponibili)"
            : "(Mese completo di dati)";

        return analysisLevel switch
        {
            "Valutazione Iniziale" => $"- Stabilire baseline mensili e identificare pattern iniziali {contextInfo}\n- Comprendere abitudini di utilizzo nel primo periodo",
            "Analisi Settimanale" => $"- Identificare cicli settimanali nel contesto mensile {contextInfo}\n- Analizzare comportamenti ricorrenti negli ultimi 30 giorni",
            "Deep Dive Mensile" => $"- Modellare comportamenti mensili complessi {contextInfo}\n- Prevedere trend basati su pattern mensili consolidati",
            "Assessment Trimestrale" => $"- Analisi mensile nel contesto trimestrale {contextInfo}\n- Ottimizzazioni mensili con visione a medio termine",
            "Analisi Comprensiva" => $"- Intelligence mensile avanzata nel contesto storico {contextInfo}\n- Ottimizzazione strategica basata su analisi mensile approfondita",
            _ => $"- Analisi mensile generale {contextInfo}"
        };
    }

    /// <summary>
    /// ✅ CERTIFICAZIONE COMPLETA: Genera statistiche certificate con valore aggiunto DataPolar
    /// </summary>
    private async Task<string> GenerateDataStatistics(List<string> rawJsonList, TimeSpan totalMonitoringPeriod, int dataHours, int vehicleId)
    {
        var sb = new StringBuilder();

        var certification = await GenerateDataCertification(vehicleId, totalMonitoringPeriod);
        sb.AppendLine(certification);
        sb.AppendLine();

        // 📊 STATISTICHE ANALISI MENSILE
        sb.AppendLine("📊 STATISTICHE ANALISI MENSILE:");
        sb.AppendLine($"• Durata monitoraggio totale: {totalMonitoringPeriod.TotalDays:F1} giorni");
        sb.AppendLine($"• Campioni mensili analizzati: {rawJsonList.Count:N0}");
        sb.AppendLine($"• Finestra UNIFICATA: {dataHours} ore (30 giorni)");
        sb.AppendLine($"• Densità dati mensile: {rawJsonList.Count / Math.Max(dataHours, 1):F1} campioni/ora");
        sb.AppendLine($"• Copertura dati: {Math.Min(100, (dataHours / Math.Max(totalMonitoringPeriod.TotalHours, 1)) * 100):F1}% del periodo totale");
        sb.AppendLine($"• Strategia: Analisi mensile consistente con context evolutivo");

        return sb.ToString();
    }

    /// <summary>
    /// 🏆 CERTIFICAZIONE DATAPOLAR: Genera certificazione completa qualità dati
    /// </summary>
    private async Task<string> GenerateDataCertification(int vehicleId, TimeSpan totalMonitoringPeriod)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("📋 CERTIFICAZIONE DATI DATAPOLAR:");

            // 1️⃣ CALCOLO ORE TOTALI CERTIFICATE
            var totalRecords = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .CountAsync();

            var firstRecord = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            var lastRecord = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .OrderByDescending(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            if (firstRecord == default || lastRecord == default)
            {
                sb.AppendLine("• Status: ⚠️ Dati insufficienti per certificazione");
                return sb.ToString();
            }

            var actualMonitoringPeriod = lastRecord - firstRecord;
            var totalCertifiedHours = actualMonitoringPeriod.TotalHours;

            // 2️⃣ CALCOLO UPTIME E GAP ANALYSIS
            var gaps = await AnalyzeDataGaps(vehicleId, firstRecord, lastRecord);
            var uptimePercentage = CalculateUptimePercentage(gaps, actualMonitoringPeriod);

            // 3️⃣ QUALITÀ DATASET
            var qualityScore = CalculateQualityScore(totalRecords, uptimePercentage, gaps.majorGaps, actualMonitoringPeriod);
            var qualityStars = GetQualityStars(qualityScore);

            // 4️⃣ OUTPUT CERTIFICAZIONE
            sb.AppendLine($"• Ore totali certificate: {totalCertifiedHours:F0} ore ({totalCertifiedHours / 24:F1} giorni)");
            sb.AppendLine($"• Uptime raccolta: {uptimePercentage:F1}%");
            sb.AppendLine($"• Gap maggiori: {gaps.majorGaps} interruzioni > 2h");
            sb.AppendLine($"• Qualità dataset: {qualityStars} ({GetQualityLabel(qualityScore)})");
            sb.AppendLine($"• Primo record: {firstRecord:yyyy-MM-dd HH:mm} UTC");
            sb.AppendLine($"• Ultimo record: {lastRecord:yyyy-MM-dd HH:mm} UTC");
            sb.AppendLine($"• Records totali: {totalRecords:N0}");
            sb.AppendLine($"• Frequenza media: {(totalRecords / Math.Max(totalCertifiedHours, 1)):F1} campioni/ora");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            await _logger.Error("PolarAiReportGenerator.GenerateDataCertification",
                "Errore generazione certificazione", ex.ToString());
            return "📋 CERTIFICAZIONE DATI: ⚠️ Errore durante la certificazione";
        }
    }

    /// <summary>
    /// 🔍 ANALISI GAP: Identifica interruzioni nella raccolta dati
    /// </summary>
    private async Task<(int totalGaps, int majorGaps, TimeSpan totalGapTime)> AnalyzeDataGaps(int vehicleId, DateTime firstRecord, DateTime lastRecord)
    {
        try
        {
            // Recupera timestamps ordinati per identificare gap
            var timestamps = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .ToListAsync();

            if (timestamps.Count < 2)
                return (0, 0, TimeSpan.Zero);

            int totalGaps = 0;
            int majorGaps = 0; // > 2 ore
            TimeSpan totalGapTime = TimeSpan.Zero;

            for (int i = 1; i < timestamps.Count; i++)
            {
                var gap = timestamps[i] - timestamps[i - 1];

                // Considera gap se > 30 minuti (normale intervallo telemetria Tesla ~5-15 min)
                if (gap.TotalMinutes > 30)
                {
                    totalGaps++;
                    totalGapTime = totalGapTime.Add(gap);

                    // Gap maggiore se > 2 ore
                    if (gap.TotalHours > 2)
                    {
                        majorGaps++;
                    }
                }
            }

            return (totalGaps, majorGaps, totalGapTime);
        }
        catch (Exception ex)
        {
            await _logger.Error("PolarAiReportGenerator.AnalyzeDataGaps",
                "Errore analisi gap", ex.ToString());
            return (0, 0, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// 📊 CALCOLO UPTIME: Percentuale di copertura temporale effettiva
    /// </summary>
    private double CalculateUptimePercentage((int totalGaps, int majorGaps, TimeSpan totalGapTime) gaps, TimeSpan actualMonitoringPeriod)
    {
        if (actualMonitoringPeriod.TotalHours <= 0)
            return 0;

        var activeTime = actualMonitoringPeriod - gaps.totalGapTime;
        return Math.Max(0, Math.Min(100, (activeTime.TotalHours / actualMonitoringPeriod.TotalHours) * 100));
    }

    /// <summary>
    /// ⭐ QUALITY SCORE: Calcola punteggio qualità dataset (0-100)
    /// </summary>
    private double CalculateQualityScore(int totalRecords, double uptimePercentage, int majorGaps, TimeSpan monitoringPeriod)
    {
        double score = 0;

        // 40% - Uptime (più importante)
        score += (uptimePercentage / 100) * 40;

        // 30% - Densità records (target: 1+ record/ora)
        var recordDensity = totalRecords / Math.Max(monitoringPeriod.TotalHours, 1);
        var densityScore = Math.Min(1, recordDensity / 1.0); // Normalizzato a 1 record/ora
        score += densityScore * 30;

        // 20% - Stabilità (penalità per gap maggiori)
        var stabilityPenalty = Math.Min(20, majorGaps * 2); // -2 punti per gap maggiore
        score += Math.Max(0, 20 - stabilityPenalty);

        // 10% - Maturità dataset (bonus per dataset maturi)
        if (monitoringPeriod.TotalDays >= 30) score += 10;
        else if (monitoringPeriod.TotalDays >= 7) score += 7;
        else if (monitoringPeriod.TotalDays >= 1) score += 3;

        return Math.Max(0, Math.Min(100, score));
    }

    /// <summary>
    /// ⭐ QUALITY STARS: Converte score in stelle visuali
    /// </summary>
    private string GetQualityStars(double score)
    {
        return score switch
        {
            >= 90 => "⭐⭐⭐⭐⭐",
            >= 80 => "⭐⭐⭐⭐⚪",
            >= 70 => "⭐⭐⭐⚪⚪",
            >= 60 => "⭐⭐⚪⚪⚪",
            >= 50 => "⭐⚪⚪⚪⚪",
            _ => "⚪⚪⚪⚪⚪"
        };
    }

    /// <summary>
    /// 🏷️ QUALITY LABEL: Etichetta qualitativa per il punteggio
    /// </summary>
    private string GetQualityLabel(double score)
    {
        return score switch
        {
            >= 90 => "Eccellente",
            >= 80 => "Ottimo",
            >= 70 => "Buono",
            >= 60 => "Discreto",
            >= 50 => "Sufficiente",
            _ => "Migliorabile"
        };
    }
}