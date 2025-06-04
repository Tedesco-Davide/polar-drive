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

        await _logger.Info("AiReportGenerator.GenerateSummaryFromRawJson", "Avvio generazione report AI", $"Dati in input: {rawJsonList.Count} record JSON");

        var parsedPrompt = RawDataPreparser.GenerateInsightPrompt(rawJsonList);

        // Aggiungi statistiche di base per dare contesto all'AI
        var dataStats = GenerateDataStatistics(rawJsonList);

        var prompt = @$"
                        Agisci come un consulente esperto in mobilità elettrica e analisi dati Tesla. 
                        Analizza questi dati operativi di un veicolo Tesla per un periodo di 30 giorni e produci un report professionale.

                        CONTESTO DATI:
                        {dataStats}

                        DATI DETTAGLIATI:
                        {parsedPrompt}

                        STRUTTURA RICHIESTA DEL REPORT:
                        1. EXECUTIVE SUMMARY (3-4 righe)
                        2. ANALISI UTILIZZO VEICOLO
                        - Pattern di guida e spostamenti
                        - Efficienza energetica 
                        - Utilizzo funzionalità (clima, sicurezza, ecc.)
                        3. ANALISI RICARICHE
                        - Frequenza e tempistiche
                        - Costi e tariffe
                        - Efficienza delle sessioni
                        4. SISTEMA ENERGETICO DOMESTICO (se presente)
                        - Bilancio energetico casa
                        - Integrazione con veicolo
                        - Ottimizzazioni possibili
                        5. CONSIDERAZIONI FISCALI E BUSINESS
                        - Chilometraggi per categoria d'uso
                        - Costi operativi deducibili
                        - Suggerimenti per ottimizzazione fiscale
                        6. RACCOMANDAZIONI
                        - Ottimizzazioni operative
                        - Potenziali risparmi
                        - Azioni consigliate

                        Usa un tono professionale ma accessibile. Includi sempre cifre specifiche e percentuali dove possibile."
                    ;

        await _logger.Debug("AiReportGenerator.GenerateSummaryFromRawJson", "Prompt generato per AI", $"Lunghezza prompt: {prompt.Length} caratteri");

        var requestBody = new
        {
            model = "mistral",
            prompt = prompt,
            stream = false,
            options = new
            {
                temperature = 0.3, // Più deterministico per report professionali
                top_p = 0.9,
                max_tokens = 4000 // Assicura risposte complete
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        try
        {
            await _logger.Info("AiReportGenerator.GenerateSummaryFromRawJson", "Invio richiesta a Mistral AI");

            var response = await _httpClient.PostAsync("http://localhost:11434/api/generate", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                await _logger.Error("AiReportGenerator.GenerateSummaryFromRawJson", "Errore dalla AI", $"Status: {response.StatusCode} - Body: {errorContent}");
                throw new Exception($"Errore nella chiamata a Mistral AI: {response.StatusCode} - {errorContent}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var parsed = JsonDocument.Parse(jsonResponse);

            var aiResponse = parsed.RootElement.GetProperty("response").GetString();

            await _logger.Debug("AiReportGenerator.GenerateSummaryFromRawJson", "Risposta AI ricevuta", $"Lunghezza: {aiResponse?.Length} caratteri");

            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                throw new Exception("Risposta AI vuota o non valida");
            }

            return aiResponse;
        }
        catch (JsonException ex)
        {
            await _logger.Error("AiReportGenerator.GenerateSummaryFromRawJson", "Errore parsing JSON dalla AI", ex.Message);
            throw new Exception($"Errore nel parsing della risposta AI: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            await _logger.Error("AiReportGenerator.GenerateSummaryFromRawJson", "Errore connessione con Mistral", ex.Message);
            throw new Exception($"Errore di connessione a Mistral AI: {ex.Message}");
        }
    }

    private static string GenerateDataStatistics(List<string> rawJsonList)
    {
        var stats = new StringBuilder();
        stats.AppendLine($"• Periodo analizzato: {rawJsonList.Count} ore di dati");
        stats.AppendLine($"• Data range: approssimativamente 30 giorni");

        // Conta i tipi di dati presenti
        var dataTypes = new Dictionary<string, int>();

        foreach (var rawJson in rawJsonList)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("response", out var response) &&
                    response.TryGetProperty("data", out var dataArray))
                {
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        var type = item.GetProperty("type").GetString();
                        if (type != null)
                        {
                            dataTypes[type] = dataTypes.GetValueOrDefault(type, 0) + 1;
                        }
                    }
                }
            }
            catch
            {
                // Ignora errori di parsing per statistiche
            }
        }

        stats.AppendLine("• Tipi di dati raccolti:");
        foreach (var kvp in dataTypes.OrderByDescending(x => x.Value))
        {
            var displayName = kvp.Key switch
            {
                "charging_history" => "Cronologia ricariche",
                "energy_endpoints" => "Dati sistema energetico",
                "vehicle_commands" => "Comandi veicolo",
                "vehicle_endpoints" => "Stato completo veicolo",
                "user_profile" => "Profilo utente",
                "partner_public_key" => "Configurazione API",
                _ => kvp.Key
            };
            stats.AppendLine($"  - {displayName}: {kvp.Value} campioni");
        }

        return stats.ToString();
    }
}