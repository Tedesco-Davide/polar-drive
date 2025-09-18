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
        _logger = new PolarDriveLogger(_dbContext);
        _ollamaConfig = ollama?.Value ?? throw new ArgumentNullException(nameof(ollama));
        _httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<string> GeneratePolarAiInsightsAsync(int vehicleId)
    {
        var source = "PolarAiReportGenerator.GenerateInsights";
        await _logger.Info(source, "Avvio analisi AI ottimizzata", $"VehicleId: {vehicleId}");

        // ✅ SEMPRE FINESTRA MENSILE - Calcola il periodo di monitoraggio per il context
        var monitoringPeriod = await CalculateMonitoringPeriod(vehicleId);
        var analysisLevel = GetAnalysisLevel(monitoringPeriod);

        // ✅ SEMPRE 720 ORE (30 GIORNI) - Finestra dati unificata
        const int dataHours = MONTHLY_HOURS_THRESHOLD;

        await _logger.Info(source, $"Analisi {analysisLevel}",
            $"Finestra UNIFICATA: {dataHours}h ({dataHours / 24} giorni) - Periodo totale: {monitoringPeriod.TotalDays:F1} giorni");

        // 🚀 Recupero dati
        var historicalData = await GetHistoricalData(vehicleId, dataHours);

        if (historicalData.Count == 0)
        {
            await _logger.Warning(source, "Nessun dato nel periodo mensile specificato", null);
            return "Nessun dato disponibile per l'analisi mensile.";
        }

        // 🎯 AGGREGAZIONE INTELLIGENTE - Riduzione drastica dei token
        var aggregator = new IntelligentDataAggregator(_dbContext);
        var aggregatedData = await aggregator.GenerateAggregatedInsights(historicalData, vehicleId);

        await _logger.Info(source, "Aggregazione completata",
            $"Da {historicalData.Sum(d => d.Length)} char → {aggregatedData.Length} char");

        // 🧠 GENERAZIONE INSIGHTS AI con dati ottimizzati
        return await GenerateSummary(aggregatedData, monitoringPeriod, analysisLevel, dataHours, vehicleId);
    }

    /// <summary>
    /// Calcola il periodo totale di monitoraggio (per context)
    /// </summary>
    private async Task<TimeSpan> CalculateMonitoringPeriod(int vehicleId)
    {
        try
        {
            var firstRecord = await GetFirstVehicleRecord(vehicleId);
            return firstRecord == default ? TimeSpan.FromDays(1) : DateTime.UtcNow - firstRecord;
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
                $"Recupero dati mensili: {hours}h ({hours / 24} giorni)",
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

    private async Task<string> GenerateSummary(string aggregatedData, TimeSpan totalMonitoringPeriod, string analysisLevel, int dataHours, int vehicleId)
    {
        if (string.IsNullOrWhiteSpace(aggregatedData))
            return "Nessun dato veicolo disponibile per l'analisi mensile.";

        await _logger.Info("PolarAiReportGenerator.GenerateSummary",
            $"Generazione analisi {analysisLevel}",
            $"Dati aggregati: {aggregatedData.Length} caratteri, Finestra: {dataHours}h");

        // ✅ PROMPT ottimizzato per dati aggregati
        var prompt = BuildOptimizedPrompt(aggregatedData, totalMonitoringPeriod, analysisLevel, dataHours, vehicleId);
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

            // ✅ Se non è l'ultimo tentativo, aspetta prima di riprovare
            if (attempt < maxRetries)
            {
                await _logger.Warning("PolarAiReportGenerator.GenerateSummary",
                    $"Tentativo {attempt} fallito, riprovo tra {retryDelaySeconds}s", null);
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
            }
        }

        // ✅ Dopo tutti i tentativi falliti, lancia eccezione
        var errorMessage = $"Polar AI non disponibile dopo {maxRetries} tentativi per {analysisLevel}";
        await _logger.Error("PolarAiReportGenerator.GenerateSummary", errorMessage, null);
        throw new InvalidOperationException(errorMessage);
    }

    /// <summary>
    /// 🚀 NUOVO PROMPT OTTIMIZZATO per dati aggregati
    /// </summary>
    private string BuildOptimizedPrompt(string aggregatedData, TimeSpan totalMonitoringPeriod, string analysisLevel, int dataHours, int vehicleId)
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

```
{aggregatedData}
```

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
**FOCUS TECNICO**: Concentrati su analisi comportamentale e predittiva utilizzando i dati aggregati
**PROFESSIONALITÀ**: Linguaggio tecnico ma accessibile
**ACTIONABILITY**: Ogni insight mensile deve tradursi in azioni concrete
**COMPARABILITÀ**: Fornire benchmark e confronti mensili
**COMPLETEZZA**: Analizzare TUTTI i dati aggregati forniti, inclusi SMS Adaptive Profiling

## ELEMENTI OBBLIGATORI MENSILI

✅ **Metriche quantitative mensili** in ogni sezione  
✅ **Trend mensili** con direzione e velocità  
✅ **Confidence level** per previsioni mensili  
✅ **Impatto economico mensile** stimato  
✅ **Timeline mensile** per implementazione raccomandazioni
✅ **Integrazione completa dati aggregati forniti**

## STILE OUTPUT

- **Formato**: Markdown professionale con emoji per sezioni
- **Lunghezza**: Proporzionale al livello {analysisLevel} ma focus mensile
- **Tone**: Consultoriale esperto, focus su performance mensili
- **Focus**: Valore business e ottimizzazione basata su dati aggregati
- **Evita**: Ripetizione delle statistiche di certificazione (già nel PDF)

---
**GENERA REPORT {analysisLevel.ToUpper()} CON FOCUS TECNICO-COMPORTAMENTALE**
**ANALIZZA I DATI AGGREGATI NEL CONTESTO DI {totalMonitoringPeriod.TotalDays:F0} GIORNI TOTALI**
**LA CERTIFICAZIONE DATAPOLAR È GIÀ INCLUSA NEL PDF - CONCENTRATI SULL'ANALISI**";
    }

    /// <summary>
    /// Genera con Polar AI usando prompt ottimizzato
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
}