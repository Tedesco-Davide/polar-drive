using System.Text;
using System.Text.Json;

namespace PolarDrive.WebApi.AiReports;

public static class AiReportGenerator
{
    private static readonly HttpClient _httpClient = new();

    public static async Task<string> GenerateSummaryFromRawJson(List<string> rawJsonList)
    {
        var parsedPrompt = RawDataPreparser.GenerateInsightPrompt(rawJsonList);

        var prompt = @$"
        Agisci come un data analyst. Analizza questi eventi operativi del veicolo, estratti da dati grezzi, 
        e riassumi in italiano pattern ricorrenti, eventuali anomalie, giorni più attivi, e considerazioni utili per fini fiscali.

        Eventi estratti:
        {parsedPrompt}";

        var requestBody = new
        {
            model = "mistral",
            prompt = prompt,
            stream = false
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("http://localhost:11434/api/generate", content);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Errore nella chiamata a Mistral AI: {response.StatusCode}");
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var parsed = JsonDocument.Parse(jsonResponse);
        return parsed.RootElement.GetProperty("response").GetString() ?? "⚠️ Nessuna risposta AI ricevuta.";
    }
}