using System.Text;
using System.Text.Json;

namespace PolarDrive.WebApi.AiReports;

public static class AiReportGenerator
{
    private static readonly HttpClient _httpClient = new();

    public static async Task<string> GenerateSummaryFromRawJson(List<string> rawJsonList)
    {
        var combinedJson = string.Join("\n", rawJsonList);

        var prompt = @$"
        Agisci come un data analyst. Analizza i seguenti dati grezzi JSON di utilizzo veicolo, 
        estrai e riassumi insight rilevanti come durata media dei viaggi, pattern di ricarica, 
        utilizzo ricorrente, anomalie, giorni più attivi o stazionari. Rispondi in italiano.

        Dati: {combinedJson}";

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