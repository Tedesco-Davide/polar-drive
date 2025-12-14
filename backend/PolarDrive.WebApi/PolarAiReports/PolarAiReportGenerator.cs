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
        var adsPayload = await GenerateGoogleAdsPayload(aggregatedGoogleAdsData, monitoringPeriod, analysisLevel, dataHours);
        var humanReport = await GenerateHumanReport(aggregatedGoogleAdsData, monitoringPeriod, analysisLevel, dataHours);
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

    private async Task<AiGoogleAdsPayload> GenerateGoogleAdsPayload(string aggregatedData, TimeSpan totalMonitoringPeriod, string analysisLevel, int dataHours)
    {
        var prompt = BuildGenericAdsPayloadPrompt(aggregatedData, totalMonitoringPeriod, analysisLevel, dataHours);
        var maxRetries = _ollamaConfig.MaxRetries;
        var retryDelaySeconds = _ollamaConfig.RetryDelaySeconds;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var aiResponse = await TryGenerateWithPolarAi(prompt, $"AdsPayload-{analysisLevel}");
            if (!string.IsNullOrWhiteSpace(aiResponse))
            {
                try
                {
                    var cleaned = aiResponse.Trim();
                    if (cleaned.StartsWith("```json")) cleaned = cleaned.Substring(7);
                    if (cleaned.StartsWith("```")) cleaned = cleaned.Substring(3);
                    if (cleaned.EndsWith("```")) cleaned = cleaned.Substring(0, cleaned.Length - 3);
                    cleaned = cleaned.Trim();

                    var payload = JsonSerializer.Deserialize<AiGoogleAdsPayload>(cleaned);
                    if (payload != null) return payload;
                }
                catch { }
            }

            if (attempt < maxRetries)
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
        }

        return new AiGoogleAdsPayload();
    }

    private static string BuildGenericAdsPayloadPrompt(string aggregatedData, TimeSpan totalMonitoringPeriod, string analysisLevel, int dataHours)
    {
        return $@"
        # POLAR AI - GOOGLE ADS DATA EXTRACTION

        Analizza i seguenti dati Tesla aggregati e restituisci SOLO un JSON valido, senza alcun testo prima o dopo.

        ## PARAMETRI
        - Livello: {analysisLevel}
        - Periodo Totale: {totalMonitoringPeriod.TotalDays:F1} giorni
        - Finestra: {dataHours} ore

        ## DATI AGGREGATI
        {aggregatedData}

        ## OUTPUT RICHIESTO
        Restituisci SOLO il seguente JSON compilato con valori concreti basati sui dati (nessun testo aggiuntivo):

        {{
            ""driver_profile"": ""efficient|aggressive|urban_commuter|balanced"",
            ""driver_profile_confidence"": 0.XX,
            ""optimization_priority"": ""high|medium|low"",
            ""optimization_priority_score"": XX,
            ""predicted_monthly_usage_change"": XX,
            ""segment"": ""tech_enthusiast|cost_conscious|performance_seeker|mainstream"",
            ""segment_confidence"": 0.XX,
            ""charging_behavior_score"": XX,
            ""efficiency_potential"": XX,
            ""battery_health_trend"": ""improving|stable|declining"",
            ""engagement_level"": ""high|medium|low"",
            ""conversion_likelihood"": 0.XX,
            ""lifetime_value_indicator"": ""high|medium|low"",
            ""recommended_campaign_type"": ""awareness|consideration|conversion"",
            ""key_motivators"": [""motivator1"", ""motivator2"", ""motivator3""]
        }}";
    }

    private async Task<string> GenerateHumanReport(string aggregatedData, TimeSpan totalMonitoringPeriod, string analysisLevel, int dataHours)
    {
        var prompt = BuildHumanReportPrompt(aggregatedData, totalMonitoringPeriod, analysisLevel, dataHours);
        var maxRetries = _ollamaConfig.MaxRetries;
        var retryDelaySeconds = _ollamaConfig.RetryDelaySeconds;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var aiResponse = await TryGenerateWithPolarAi(prompt, $"HumanReport-{analysisLevel}");
            if (!string.IsNullOrWhiteSpace(aiResponse))
                return aiResponse;

            if (attempt < maxRetries)
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
        }

        throw new InvalidOperationException($"Polar AI non disponibile dopo {maxRetries} tentativi per {analysisLevel}");
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

    // Promp per dati aggregati
    private static string BuildHumanReportPrompt(string aggregatedData, TimeSpan totalMonitoringPeriod, string analysisLevel, int dataHours)
    {
        return $@"
        # POLAR AI - DIGITAL MARKETING STRATEGIST & CAMPAIGN MANAGER

        **RUOLO**: Consulente Marketing Digitale specializzato in campagne Google Ads data-driven per professionisti, PMI, grandi aziende e multinazionali.

        ## PARAMETRI PROGETTO
        **Livello Maturità**: {analysisLevel}  
        **Periodo Monitoraggio Totale**: {totalMonitoringPeriod.TotalDays:F1} giorni  
        **Finestra Analisi Corrente**: {dataHours} ore (30 giorni)  
        **Fonte Dati**: Telemetria laboratorio mobile Tesla + aggregazione algoritmica C#

        ## DATI AGGREGATI LABORATORIO MOBILE
        {aggregatedData}

        ## DELIVERABLE RICHIESTI (inizia con ### 1. 📋 PIANO DI CAMPAGNA GOOGLE ADS):

        ### 1. 📋 PIANO DI CAMPAGNA GOOGLE ADS - PERIODO MENSILE
        **Budget Consigliato**: [€ importo basato su dati raccolti]
        **Obiettivo Primario**: [Awareness/Consideration/Conversion]
        **Targeting Geografico**: [Zone identificate dai dati di mobilità con maggior ROI potenziale]
        **Keywords Principali**: [Lista 5-10 keywords derivate da pattern rilevati]
        **Audience**: [Segmenti identificati - es: ""utenti zona X orario Y"", ""profilo business mobile""]
        **Scheduling**: [Fasce orarie ottimali basate su picchi attività rilevati]
        **Messaggi Chiave**: [3-5 copy pubblicitari allineati a comportamenti osservati]
        **KPI Target Mensili**: [Impression attese, CTR obiettivo, CPC stimato, Conversioni previste]

        ### 2. 📊 BRIEF MARKETING STRATEGICO
        **Contesto Business**: [Descrivere attività del cliente basandosi su pattern mobilità]
        **Opportunità Identificate**: [Insights chiave dai dati - es: alta frequenza zona commerciale, orari picco, raggio operativo]
        **Posizionamento Consigliato**: [Come presentare il business in base a dati raccolti]
        **Competitors Locali**: [Suggerimenti su competitive advantage basato su coverage geografica]
        **Unique Selling Proposition**: [Elementi distintivi emersi dai dati di utilizzo]
        **Call-to-Action**: [Azioni consigliate per campagne basate su customer journey rilevato]

        ### 3. 📈 REPORT PERFORMANCE & KPI MENSILI
        **Copertura Geografica**: [Km totali, zone coperte, densità territoriale]
        **Utilizzo Laboratorio Mobile**: [Ore operative, sessioni completate, varietà profili guida]
        **Qualità Dataset Raccolto**: [Densità dati, completezza telemetria, affidabilità insights]
        **Metriche Marketing**:
        - Potenziale Reach Mensile: [Stima impression basata su mobilità]
        - Accuracy Targeting: [Livello di precisione segmentazione possibile]
        - Conversion Likelihood: [Probabilità conversione basata su engagement pattern]
        **ROI Investimento Telemetria**: [Valore dati raccolti vs costo operativo laboratorio]

        ### 4. 🎯 RACCOMANDAZIONI OPERATIVE IMMEDIATE
        **Ottimizzazioni Campagne Google Ads**: [3-5 azioni concrete per migliorare performance]
        **Modifiche Budget/Bid**: [Suggerimenti riallocazione budget su zone/orari ad alto ROI]
        **A/B Test Consigliati**: [Varianti da testare su copy, landing, targeting]
        **Quick Wins 30gg**: [Azioni a impatto immediato basate su dati già disponibili]

        ### 5. 🔮 PREVISIONI & STRATEGIA PROSSIMO MESE
        **Trend Utilizzo Previsto**: [Aumento/diminuzione attività, nuovi pattern attesi]
        **Opportunità Stagionali**: [Eventi, periodi, condizioni da sfruttare]
        **Espansione Raggio Operativo**: [Nuove zone da esplorare per ampliare reach]
        **Diversificazione Profili Guida**: [Raccomandazioni su ADAPTIVE_PROFILE per arricchire dataset]

        ### 6. 💰 ANALISI ROI & KILOMETRAGGIO
        **Kilometraggio Totale Periodo**: [Km percorsi nel mese]
        **Costo per Km Dati Raccolti**: [Investimento telemetria / km]
        **Valore Marketing per Km**: [Insights generati / km - es: ""ogni 100km = 1 nuovo segmento audience""]
        **ROI Stimato Campagne**: [Ritorno atteso su investimento Google Ads basato su qualità dati]
        **Break-even Analysis**: [Quando il valore insights compensa investimento operativo]

        ### 7. 📱 ADAPTIVE_PROFILE - IMPATTO SU QUALITÀ CAMPAGNE
        **Contributo Varietà Dataset**: [Come sessioni ADAPTIVE_PROFILE migliorano targeting]
        **Copertura Temporale Ottimale**: [Frequenza ideale per massimizzare insights]
        **Diversificazione Utilizzatori**: [Valore aggiunto da profili eterogenei per segmentazione]
        **ROI Sessioni ADAPTIVE_PROFILE**: [Ritorno specifico da utilizzi diversificati]

        **VINCOLI DELIVERABLE:**
        - Output professionale audit-ready per Agenzia Entrate/Guardia Finanza
        - Linguaggio tecnico-commerciale tipico di agenzia marketing digitale
        - Ogni sezione deve contenere dati quantitativi concreti (€, km, %, n. campagne)
        - Piano campagna deve essere implementabile direttamente su Google Ads
        - Brief marketing deve riflettere reale operatività business (rifornimenti, consegne, trasferte clienti)
        - KPI misurabili e tracciabili nel tempo
        - Raccomandazioni actionable con timeline definite
        - Collegamento diretto mobilità → valore marketing (es: ""zona X percorsa 40 volte"" → ""geo-targeting campagna locale budget € Y"")";
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
}