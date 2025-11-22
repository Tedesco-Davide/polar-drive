using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using static PolarDrive.WebApi.Constants.CommonConstants;
using Microsoft.Extensions.Options;

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
        _logger = new PolarDriveLogger();
        _ollamaConfig = ollama?.Value ?? throw new ArgumentNullException(nameof(ollama));
        _httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<(string humanReport, AiGoogleAdsPayload adsPayload, GoogleAdsTeslaDataAggregation aggregation)> GeneratePolarAiInsightsAsync(int vehicleId)
    {
        var source = "PolarAiReportGenerator.GenerateInsights";
        await _logger.Info(source, "Avvio analisi AI ottimizzata", $"VehicleId: {vehicleId}");

        // Finestra mensile - Calcola il periodo di monitoraggio per il context
        var monitoringPeriod = await CalculateMonitoringPeriod(vehicleId);
        var analysisLevel = GetAnalysisLevel(monitoringPeriod);

        // 720 ore (30 GIORNI) - Finestra dati unificata
        const int dataHours = MONTHLY_HOURS_THRESHOLD;

        await _logger.Info(source, $"Analisi {analysisLevel}",
            $"Finestra unificata: {dataHours}h ({dataHours / 24} giorni) - Periodo totale: {monitoringPeriod.TotalDays:F1} giorni");

        // 🚀 Recupero dati
        var historicalData = await GetHistoricalData(vehicleId, dataHours);

        if (historicalData.Count == 0)
        {
            await _logger.Warning(source, "Nessun dato nel periodo mensile specificato", null);
            return ("Nessun dato disponibile per l'analisi mensile.", new AiGoogleAdsPayload(), new GoogleAdsTeslaDataAggregation());
        }

        // 🎯 Aggregazione intelligente
        var aggregator = new IntelligentDataAggregator(_dbContext);
        var (aggregatedGoogleAdsData, aggregation) = await aggregator.GenerateGoogleAdsAggregatedInsights(historicalData, vehicleId);

        await _logger.Info(source, "Aggregazione dati per Google Ads completata",
            $"Da {historicalData.Sum(d => d.Length)} char → {aggregatedGoogleAdsData.Length} char");

        // 🧠 Generazione Insights AI con dati ottimizzati
        var aiResponseRaw = await GenerateSummary(aggregatedGoogleAdsData, monitoringPeriod, analysisLevel, dataHours);
        var (humanReport, adsPayload) = ExtractAdsPayloadFromResponse(aiResponseRaw);
        var aiInsightsSection = new StringBuilder();
        aiInsightsSection.AppendLine(humanReport);

        await _logger.Info(source, "Sezione Insights stampata nel PDF",
            $"AI Insights Section: {aiInsightsSection.Length}");

        return (aiInsightsSection.ToString(), adsPayload, aggregation);
    }

    // Calcola il periodo totale di monitoraggio (per context)
    private async Task<TimeSpan> CalculateMonitoringPeriod(int vehicleId)
    {
        try
        {
            var firstRecord = await GetFirstVehicleRecord(vehicleId);
            return firstRecord == default ? TimeSpan.FromDays(1) : DateTime.Now - firstRecord;
        }
        catch (Exception ex)
        {
            await _logger.Error("PolarAiReportGenerator.CalculateMonitoringPeriod",
                "Errore calcolo periodo monitoraggio", ex.ToString());
            return TimeSpan.FromDays(1);
        }
    }

    // Determina il livello di analisi basato sul periodo totale di monitoraggio
    // I dati utilizzati sono sempre gli ultimi 30 giorni, ma il livello cambia in base alla maturità
    private static string GetAnalysisLevel(TimeSpan totalMonitoringPeriod)
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

    // Recupera il primo record del veicolo per calcolare l'età di monitoraggio
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

    // Recupera gli ultimi N. record disponibili per il veicolo ( lo standard equivale a 720 record ),
    // rispettando il loro periodo reale di riferimento e restituendoli in ordine cronologico crescente.
    private async Task<List<string>> GetHistoricalData(int vehicleId, int recordsCount)
    {
        try
        {
            // Prendo gli ultimi N. record in base al Timestamp (più recenti per primi)
            var itemsDesc = await _dbContext.VehiclesData
                .AsNoTracking()
                .Where(vd => vd.VehicleId == vehicleId)
                .OrderByDescending(vd => vd.Timestamp)
                .Select(vd => new { vd.Timestamp, vd.RawJsonAnonymized })
                .Take(recordsCount)
                .ToListAsync();

            // Se non ci sono dati, log e return vuoto
            if (itemsDesc.Count == 0)
            {
                await _logger.Warning("PolarAiReportGenerator.GetHistoricalData",
                    $"Nessun dato disponibile per VehicleId={vehicleId}", null);
                return [];
            }

            // Riordino in senso cronologico (dal più vecchio al più recente)
            itemsDesc.Reverse();

            var firstTs = itemsDesc.First().Timestamp;
            var lastTs = itemsDesc.Last().Timestamp;
            var span = lastTs - firstTs;

            await _logger.Info("PolarAiReportGenerator.GetHistoricalData",
                $"Recupero ultimi {recordsCount} record disponibili (periodo effettivo)",
                $"Da: {firstTs:yyyy-MM-dd HH:mm} a {lastTs:yyyy-MM-dd HH:mm} - Durata: {span.TotalDays:F1} giorni");

            var data = itemsDesc.Select(x => x.RawJsonAnonymized).ToList();

            await _logger.Info("PolarAiReportGenerator.GetHistoricalData",
                $"Recuperati {data.Count} record (copertura effettiva: {(int)Math.Round(span.TotalDays)} giorni)");

            return data;
        }
        catch (Exception ex)
        {
            await _logger.Error("PolarAiReportGenerator.GetHistoricalData",
                "Errore recupero ultimi N record", ex.ToString());
            return [];
        }
    }

    private async Task<string> GenerateSummary(string aggregatedData, TimeSpan totalMonitoringPeriod, string analysisLevel, int dataHours)
    {
        if (string.IsNullOrWhiteSpace(aggregatedData))
            return "Nessun dato veicolo disponibile per l'analisi mensile.";

        await _logger.Info("PolarAiReportGenerator.GenerateSummary",
            $"Generazione analisi {analysisLevel}",
            $"Dati aggregati: {aggregatedData.Length} caratteri, Finestra: {dataHours}h");

        var prompt = BuildOptimizedPrompt(aggregatedData, totalMonitoringPeriod, analysisLevel, dataHours);
        var maxRetries = _ollamaConfig.MaxRetries;
        var retryDelaySeconds = _ollamaConfig.RetryDelaySeconds;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            await _logger.Info("PolarAiReportGenerator.GenerateSummary",
                $"Tentativo {attempt}/{maxRetries} con Polar AI",
                $"Analisi: {analysisLevel}");

            var aiResponse = await TryGenerateWithPolarAi(prompt, analysisLevel);

            if (!string.IsNullOrWhiteSpace(aiResponse))
            {
                await _logger.Info("PolarAiReportGenerator.GenerateSummary",
                    $"Polar AI completata al tentativo {attempt}",
                    $"Risposta: {aiResponse.Length} caratteri");
                return aiResponse;
            }

            // Se non è l'ultimo tentativo, aspetta prima di riprovare
            if (attempt < maxRetries)
            {
                await _logger.Warning("PolarAiReportGenerator.GenerateSummary",
                    $"Tentativo {attempt} fallito, riprovo tra {retryDelaySeconds}s", null);
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
            }
        }

        // Dopo tutti i tentativi falliti, lancia eccezione
        var errorMessage = $"Polar AI non disponibile dopo {maxRetries} tentativi per {analysisLevel}";
        await _logger.Error("PolarAiReportGenerator.GenerateSummary", errorMessage, null);
        throw new InvalidOperationException(errorMessage);
    }

    // Promp per dati aggregati
    private static string BuildOptimizedPrompt(string aggregatedData, TimeSpan totalMonitoringPeriod, string analysisLevel, int dataHours)
    {
        return $@"
        # POLAR AI - CONSULENTE ESPERTO MOBILITÀ ELETTRICA

        **RUOLO**: Senior Data Analyst specializzato in veicoli Tesla con sistema di analisi mensile unificata.

        ## PARAMETRI ANALISI MENSILE UNIFICATA
        **Livello**: {analysisLevel}  
        **Periodo Totale Monitoraggio**: {totalMonitoringPeriod.TotalDays:F1} giorni  
        **Finestra Dati Analizzata**: SEMPRE {dataHours} ore (30 giorni)  
        **Dataset**: Dati aggregati e processati da C# per ottimizzazione AI  
        **Strategia**: Analisi mensile consistente con context evolutivo

        ## ⚠️ IMPORTANTE: CERTIFICAZIONE DATAPOLAR NEL PDF
        **NOTA CRITICA**: La certificazione DataPolar sarà automaticamente inclusa nel PDF finale attraverso il sistema HTML. 
        Tu concentrati sull'analisi tecnica e comportamentale utilizzando i dati aggregati forniti.

        ## OBIETTIVI ANALISI MENSILE PER LIVELLO
        {GetMonthlyFocus(analysisLevel, totalMonitoringPeriod)}

        ## 🎯 DATI AGGREGATI TESLA (OTTIMIZZATI PER AI)
        I seguenti dati sono stati pre-processati da algoritmi C# per ridurre la complessità computazionale, 
        mantenendo tutte le informazioni essenziali per un'analisi approfondita:

        {aggregatedData}

        ## ISTRUZIONI OUTPUT CRITICHE

        La tua risposta deve contenere ESATTAMENTE due parti separate da una riga vuota:

        PARTE 1 (riga 1): JSON compatto su singola riga, senza testo prima o dopo.
        Formato: {{{{""driver_profile"": ""value"", ""driver_profile_confidence"": 0.XX, ...}}}}

        PARTE 2 (dopo riga vuota): Report markdown che inizia DIRETTAMENTE con ### 1. 🎯 EXECUTIVE SUMMARY MENSILE

        NON scrivere:
        - ""PRIMA: JSON per Google Ads""
        - ""DOPO: Report di Analisi""
        - Alcun testo introduttivo

        SCHEMA JSON (usa valori CONCRETI basati sui dati):
        {{{{
          ""driver_profile"": ""efficient|aggressive|urban_commuter|balanced"",
          ""driver_profile_confidence"": <0.00-1.00>,
          ""optimization_priority"": ""high|medium|low"",
          ""optimization_priority_score"": <0-100>,
          ""predicted_monthly_usage_change"": <-100 a +100>,
          ""segment"": ""tech_enthusiast|cost_conscious|performance_seeker|mainstream"",
          ""segment_confidence"": <0.00-1.00>,
          ""charging_behavior_score"": <0-100>,
          ""efficiency_potential"": <0-100>,
          ""battery_health_trend"": ""improving|stable|declining"",
          ""engagement_level"": ""high|medium|low"",
          ""conversion_likelihood"": <0.00-1.00>,
          ""lifetime_value_indicator"": ""high|medium|low"",
          ""recommended_campaign_type"": ""awareness|consideration|conversion"",
          ""key_motivators"": [""cost_savings"", ""performance"", ""sustainability"", ""technology""]
        }}}}
        
        CONTENUTO REPORT (inizia con ### 1. 🎯 EXECUTIVE SUMMARY MENSILE):

        ### 1. 🎯 EXECUTIVE SUMMARY MENSILE
        - Qualità del dataset raccolto nel mese (densità dati, copertura oraria, varietà contesti)
        - Evoluzione della raccolta dati rispetto al contesto totale di {totalMonitoringPeriod.TotalDays:F0} giorni
        - KPI marketing: potenziale di targeting, segmentazione utenti, affidabilità insights
        - Valore strategico dei dati per campagne Google Ads (impression potenziali, accuracy targeting)

        ### 2. 📊 INSIGHTS PER TARGETING & SEGMENTAZIONE
        - Profili comportamentali identificati negli ultimi 30 giorni (frequent driver, urban commuter, long-distance, efficiency-focused)
        - Pattern di mobilità utili per geo-targeting (zone più frequentate, orari picco, raggio operativo)
        - Segmenti di pubblico ideali per campagne (es: ""early adopters tech"", ""cost-conscious EV users"", ""performance seekers"")
        - Opportunità di remarketing basate su comportamenti ricorrenti

        ### 3. 🎯 RACCOMANDAZIONI CAMPAGNE GOOGLE ADS
        - Strategie di targeting consigliate (keywords, audience, posizionamenti geografici)
        - Messaggi pubblicitari allineati ai pattern rilevati (es: enfasi su risparmio, performance, sostenibilità)
        - Budget allocation ottimale per fasce orarie e zone ad alta densità di utilizzo
        - Tipo di campagna consigliata (awareness, consideration, conversion) in base al livello di engagement

        ### 4. 🔮 PREVISIONI & OTTIMIZZAZIONI
        - Trend di utilizzo previsto per il prossimo mese (aumento/diminuzione attività, nuovi pattern)
        - Opportunità stagionali o eventi ricorrenti da sfruttare nelle campagne
        - Raccomandazioni su diversificazione profili guida per arricchire dataset (ADAPTIVE_PROFILE)
        - Potenziale ROI marketing stimato in base alla qualità dati raccolti

        ### 5. 💡 AZIONI IMMEDIATE PER MASSIMIZZARE IL VALORE
        - Strategie per incrementare varietà dataset (coinvolgimento utilizzatori terzi, espansione geografica)
        - Miglioramenti nella raccolta dati per insights più accurati (frequenza ADAPTIVE_PROFILE, copertura oraria)
        - Quick wins pubblicitari basati su dati già disponibili (campagne locali, retargeting comportamentale)
        - Priorità investimento marketing nei prossimi 30 giorni

        ### 6. 📈 ADAPTIVE PROFILE - IMPATTO SU MARKETING (se presente nei dati)
        - Contributo sessioni ADAPTIVE_PROFILE alla qualità del dataset per Google Ads
        - Frequenza utilizzo e copertura temporale ottimale per campagne data-driven
        - Profili utilizzatori diversificati e valore per segmentazione audience
        - Raccomandazioni per massimizzare ROI delle sessioni ADAPTIVE_PROFILE

        **VINCOLI:**
        - Linguaggio marketing-oriented, focalizzato su ROI e performance campagne
        - Ogni insight deve tradursi in azioni concrete per Google Ads (targeting, messaging, budget)
        - Evitare dettagli tecnici Tesla: concentrati su come i dati influenzano le strategie pubblicitarie
        - Collegare sempre pattern di mobilità a opportunità di marketing (es: ""alta frequenza zona X"" → ""geo-targeting campagna locale"")
        - Quantificare il valore commerciale dei dati raccolti (potenziale reach, accuracy segmentazione, conversion likelihood)";
    }

    // Genera con Polar AI usando prompt ottimizzato
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
                $"Polar AI {analysisLevel} completata",
                $"Risposta: {text?.Length ?? 0} caratteri");

            return text;
        }
        catch (HttpRequestException httpEx)
        {
            await _logger.Error("PolarAiReportGenerator.TryGenerateWithPolarAi",
                "Errore connessione HTTP a Ollama", $"Status: {httpEx.Message}");
            return null;
        }
        catch (TaskCanceledException timeoutEx) when (timeoutEx.InnerException is TimeoutException)
        {
            await _logger.Error("PolarAiReportGenerator.TryGenerateWithPolarAi",
                "Timeout connessione Ollama", timeoutEx.Message);
            return null;
        }
        catch (JsonException jsonEx)
        {
            await _logger.Error("PolarAiReportGenerator.TryGenerateWithPolarAi",
                "Errore parsing risposta JSON da Ollama", jsonEx.Message);
            return null;
        }
        catch (Exception ex)
        {
            await _logger.Debug("PolarAiReportGenerator.TryGenerateWithPolarAi",
                "Polar AI non raggiungibile per analisi", ex.Message);
            return null;
        }
    }

    // Focus specifico per analisi mensile in base al livello
    private static string GetMonthlyFocus(string analysisLevel, TimeSpan totalMonitoringPeriod)
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

    private static (string humanReport, AiGoogleAdsPayload) ExtractAdsPayloadFromResponse(string aiResponse)
    {
        try
        {
            // Step 1: Rimuovi prefissi testuali dell'AI
            aiResponse = System.Text.RegularExpressions.Regex.Replace(
                aiResponse,
                @"(PRIMA|PARTE\s*1|JSON\s*per\s*Google\s*Ads):.*?\n*",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            
            aiResponse = System.Text.RegularExpressions.Regex.Replace(
                aiResponse,
                @"(DOPO|PARTE\s*2|Report\s*di\s*Analisi):.*?\n*",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            
            // Step 2: Trova le graffe del JSON
            var jsonStart = aiResponse.IndexOf("{");
            if (jsonStart < 0)
                return (aiResponse, new AiGoogleAdsPayload());
            
            // Conta graffe bilanciate per trovare la fine
            int braceCount = 0;
            int jsonEnd = -1;
            
            for (int i = jsonStart; i < aiResponse.Length; i++)
            {
                if (aiResponse[i] == '{') braceCount++;
                if (aiResponse[i] == '}') braceCount--;
                
                if (braceCount == 0)
                {
                    jsonEnd = i + 1;
                    break;
                }
            }
            
            if (jsonEnd <= jsonStart)
                return (aiResponse, new AiGoogleAdsPayload());
            
            // Step 3: Estrai e deserializza JSON
            var jsonStr = aiResponse.Substring(jsonStart, jsonEnd - jsonStart);
            var payload = JsonSerializer.Deserialize<AiGoogleAdsPayload>(jsonStr);
            
            if (payload != null)
            {
                // Step 4: Rimuovi JSON dalla risposta
                var beforeJson = aiResponse.Substring(0, jsonStart).Trim();
                var afterJson = aiResponse.Substring(jsonEnd).Trim();
                
                // Se c'è solo spazio prima del JSON, ignora
                var humanReport = string.IsNullOrWhiteSpace(beforeJson) 
                    ? afterJson 
                    : beforeJson + "\n\n" + afterJson;
                
                return (humanReport.Trim(), payload);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Errore parsing JSON: {ex.Message}");
        }

        return (aiResponse, new AiGoogleAdsPayload());
    }
}