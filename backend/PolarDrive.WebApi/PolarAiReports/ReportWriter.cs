using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PolarDrive.Data.DbContexts;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Writer che converte digest aggregati in JSON strutturato tramite LLM
/// Input: VehicleDataDigest (pochi KB)
/// Output: ReportOutputSchema JSON validato
/// </summary>
public class ReportWriter(PolarDriveDbContext dbContext, IOptionsSnapshot<OllamaConfig> ollama, HttpClient httpClient)
{
    private readonly PolarDriveLogger _logger = new(dbContext);
    private readonly OllamaConfig _ollamaConfig = ollama.Value;

    public async Task<ReportOutputSchema> GenerateReportJsonAsync(VehicleDataDigest digest, string analysisLevel)
    {
        var source = "ReportWriter.GenerateReportJson";
        
        await _logger.Info(source, "Inizio generazione report JSON", 
            $"AnalysisLevel: {analysisLevel}, Samples: {digest.TotalSamples}");

        // Costruisci prompt minimalista con schema JSON
        var prompt = BuildStructuredPrompt(digest, analysisLevel);
        
        // Prova generazione con retry
        for (int attempt = 1; attempt <= _ollamaConfig.MaxRetries; attempt++)
        {
            try
            {
                var jsonResponse = await CallLlmWithJsonFormatAsync(prompt, attempt);
                
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    var parsed = ValidateAndParseJson(jsonResponse);
                    if (parsed != null)
                    {
                        await _logger.Info(source, $"Report JSON generato al tentativo {attempt}");
                        return parsed;
                    }
                }
            }
            catch (Exception ex)
            {
                await _logger.Warning(source, $"Tentativo {attempt} fallito", ex.Message);
                
                if (attempt == _ollamaConfig.MaxRetries)
                    throw new InvalidOperationException($"Generazione JSON fallita dopo {_ollamaConfig.MaxRetries} tentativi", ex);
                
                await Task.Delay(_ollamaConfig.RetryDelaySeconds * 1000);
            }
        }

        throw new InvalidOperationException("Generazione report JSON fallita");
    }

    private string BuildStructuredPrompt(VehicleDataDigest digest, string analysisLevel)
    {
        var sb = new StringBuilder();

        // Header conciso
        sb.AppendLine("# TESLA VEHICLE ANALYSIS REPORT GENERATOR");
        sb.AppendLine($"Analysis Level: {analysisLevel}");
        sb.AppendLine($"Period: {digest.PeriodStart:yyyy-MM-dd} to {digest.PeriodEnd:yyyy-MM-dd}");
        sb.AppendLine($"Total Samples: {digest.TotalSamples} over {digest.TotalHours} hours");
        sb.AppendLine();

        // Dati aggregati essenziali
        sb.AppendLine("## AGGREGATED METRICS");
        AppendBatteryMetrics(sb, digest.Battery);
        AppendChargingMetrics(sb, digest.Charging);
        AppendDrivingMetrics(sb, digest.Driving);
        AppendClimateMetrics(sb, digest.Climate);
        AppendEfficiencyMetrics(sb, digest.Efficiency);
        AppendQualityMetrics(sb, digest.Quality);

        // Schema JSON di output
        sb.AppendLine();
        sb.AppendLine("## OUTPUT REQUIREMENTS");
        sb.AppendLine("Generate a JSON response following this EXACT schema:");
        sb.AppendLine(GetJsonSchema());

        // Istruzioni specifiche
        sb.AppendLine();
        sb.AppendLine("## INSTRUCTIONS");
        sb.AppendLine("- Base your analysis ONLY on the provided aggregated metrics");
        sb.AppendLine("- Write in professional Italian");
        sb.AppendLine("- Be specific with numbers from the data");
        sb.AppendLine("- Focus on actionable insights");
        sb.AppendLine("- Output ONLY valid JSON, no explanations");
        sb.AppendLine($"- Tailor analysis depth to '{analysisLevel}' level");

        return sb.ToString();
    }

    private void AppendBatteryMetrics(StringBuilder sb, BatteryMetrics battery)
    {
        sb.AppendLine("### Battery Performance");
        sb.AppendLine($"- Average Level: {battery.AvgLevel:F1}%");
        sb.AppendLine($"- Range: {battery.MinLevel}% to {battery.MaxLevel}%");
        sb.AppendLine($"- Average Range: {battery.AvgRange:F1} km");
        sb.AppendLine($"- Charge Cycles: {battery.ChargeCycles}");
        sb.AppendLine($"- Health Score: {battery.HealthScore:F1}/100");
        sb.AppendLine();
    }

    private void AppendChargingMetrics(StringBuilder sb, ChargingMetrics charging)
    {
        sb.AppendLine("### Charging Behavior");
        sb.AppendLine($"- Total Sessions: {charging.TotalSessions}");
        sb.AppendLine($"- Energy Added: {charging.TotalEnergyAdded:F1} kWh");
        sb.AppendLine($"- Total Cost: €{charging.TotalCost:F2}");
        sb.AppendLine($"- Avg Cost/kWh: €{charging.AvgCostPerKwh:F3}");
        sb.AppendLine($"- Cost Range: €{charging.MinCostPerKwh:F3} - €{charging.MaxCostPerKwh:F3}");
        sb.AppendLine($"- Avg Session Duration: {charging.AvgSessionDuration:F0} minutes");
        sb.AppendLine($"- Home Charging: {charging.HomeChargingPercentage:F1}%");
        if (charging.TopStations.Any())
            sb.AppendLine($"- Top Stations: {string.Join(", ", charging.TopStations)}");
        sb.AppendLine();
    }

    private void AppendDrivingMetrics(StringBuilder sb, DrivingMetrics driving)
    {
        sb.AppendLine("### Driving Patterns");
        sb.AppendLine($"- Total Distance: {driving.TotalDistance:F1} km");
        sb.AppendLine($"- Average Speed: {driving.AvgSpeed:F1} km/h");
        sb.AppendLine($"- Max Speed: {driving.MaxSpeed:F1} km/h");
        sb.AppendLine($"- Avg Power Consumption: {driving.AvgPowerConsumption:F0} W");
        sb.AppendLine($"- Regeneration Energy: {driving.RegenerationEnergy:F1} kWh");
        sb.AppendLine($"- Trips Count: {driving.TripsCount}");
        sb.AppendLine($"- Avg Trip Distance: {driving.AvgTripDistance:F1} km");
        
        if (driving.DirectionDistribution.Any())
        {
            var directions = string.Join(", ", driving.DirectionDistribution.Select(d => $"{d.Key}: {d.Value}%"));
            sb.AppendLine($"- Direction Distribution: {directions}");
        }
        sb.AppendLine();
    }

    private void AppendClimateMetrics(StringBuilder sb, ClimateMetrics climate)
    {
        sb.AppendLine("### Climate Control");
        sb.AppendLine($"- Average Inside Temperature: {climate.AvgInsideTemp:F1}°C");
        sb.AppendLine($"- Average Outside Temperature: {climate.AvgOutsideTemp:F1}°C");
        sb.AppendLine($"- Temperature Range: {climate.MinInsideTemp:F1}°C to {climate.MaxInsideTemp:F1}°C");
        sb.AppendLine($"- Climate Usage: {climate.ClimateUsagePercentage:F1}% of time");
        sb.AppendLine($"- Avg Temp Difference: {climate.AvgTempDifference:F1}°C");
        sb.AppendLine($"- Estimated Energy Impact: {climate.ClimateEnergyImpact:F1}%");
        sb.AppendLine();
    }

    private void AppendEfficiencyMetrics(StringBuilder sb, EfficiencyMetrics efficiency)
    {
        sb.AppendLine("### Energy Efficiency");
        sb.AppendLine($"- Overall Efficiency: {efficiency.OverallEfficiency:F1} km/kWh");
        sb.AppendLine($"- City Efficiency: {efficiency.CityEfficiency:F1} km/kWh");
        sb.AppendLine($"- Highway Efficiency: {efficiency.HighwayEfficiency:F1} km/kWh");
        sb.AppendLine($"- Optimal Speed Range: {efficiency.OptimalSpeedRange:F0} km/h");
        
        if (efficiency.EfficiencyTips.Any())
            sb.AppendLine($"- Efficiency Tips: {string.Join("; ", efficiency.EfficiencyTips)}");
        sb.AppendLine();
    }

    private void AppendQualityMetrics(StringBuilder sb, DataQualityMetrics quality)
    {
        sb.AppendLine("### Data Quality");
        sb.AppendLine($"- Uptime: {quality.UptimePercentage:F1}%");
        sb.AppendLine($"- Data Gaps: {quality.DataGaps}");
        sb.AppendLine($"- Quality Score: {quality.QualityScore}/100 ({quality.QualityLabel})");
        sb.AppendLine($"- Sampling Frequency: {quality.SamplingFrequency:F1} samples/hour");
        sb.AppendLine();
    }

    private string GetJsonSchema()
    {
        // Schema JSON esatto che il modello deve seguire
        return @"{
                ""ExecutiveSummary"": {
                    ""CurrentStatus"": ""string - breve valutazione stato attuale"",
                    ""KeyFindings"": [""string"", ""string"", ""string"", ""string""],
                    ""CriticalAlerts"": [""string"", ""string""],
                    ""OverallTrend"": ""string - direzione generale trend""
                },
                ""TechnicalAnalysis"": {
                    ""BatteryAssessment"": ""string - analisi dettagliata batteria"",
                    ""ChargingBehavior"": ""string - comportamento ricarica"",
                    ""DrivingPatterns"": ""string - pattern di guida"",
                    ""EfficiencyAnalysis"": ""string - analisi efficienza""
                },
                ""PredictiveInsights"": {
                    ""NextMonthPrediction"": ""string - previsione prossimo mese"",
                    ""RiskFactors"": [""string"", ""string"", ""string""],
                    ""OpportunityAreas"": [""string"", ""string"", ""string""],
                    ""MaintenanceRecommendations"": ""string - raccomandazioni manutenzione""
                },
                ""Recommendations"": {
                    ""ImmediateActions"": [
                    {""Action"": ""string"", ""Benefit"": ""string"", ""Timeline"": ""string""},
                    {""Action"": ""string"", ""Benefit"": ""string"", ""Timeline"": ""string""},
                    {""Action"": ""string"", ""Benefit"": ""string"", ""Timeline"": ""string""}
                    ],
                    ""MediumTermActions"": [
                    {""Action"": ""string"", ""Benefit"": ""string"", ""Timeline"": ""string""},
                    {""Action"": ""string"", ""Benefit"": ""string"", ""Timeline"": ""string""},
                    {""Action"": ""string"", ""Benefit"": ""string"", ""Timeline"": ""string""}
                    ],
                    ""OptimizationStrategy"": ""string - strategia ottimizzazione generale""
                }
                }";
    }

    private async Task<string?> CallLlmWithJsonFormatAsync(string prompt, int attempt)
    {
        var source = "ReportWriter.CallLlmWithJsonFormat";
        
        try
        {
            var requestBody = new
            {
                model = _ollamaConfig.Model,
                prompt = prompt,
                temperature = 0.3, // Bassa per consistenza
                top_p = 0.9,
                stream = false,
                format = "json", // Forza output JSON
                options = new
                {
                    num_ctx = 4096, // Context ridotto per prompt più piccoli
                    num_predict = 2048, // Abbastanza per JSON completo
                    repeat_penalty = 1.1,
                    top_k = 20
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            await _logger.Debug(source, $"Chiamata LLM tentativo {attempt}", 
                $"Prompt size: {prompt.Length} chars");

            var response = await httpClient.PostAsync(
                $"{_ollamaConfig.Endpoint}/api/generate",
                content
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                await _logger.Error(source, $"HTTP {response.StatusCode}", error);
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            var text = doc.RootElement.GetProperty("response").GetString();
            
            await _logger.Debug(source, "Risposta LLM ricevuta", 
                $"Response size: {text?.Length ?? 0} chars");

            return text;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore chiamata LLM", ex.ToString());
            return null;
        }
    }

    private ReportOutputSchema? ValidateAndParseJson(string jsonText)
    {
        try
        {
            // Pulizia JSON (rimuove eventuali caratteri extra)
            var cleanJson = CleanJsonResponse(jsonText);
            
            // Parse e validazione
            var parsed = JsonSerializer.Deserialize<ReportOutputSchema>(cleanJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });

            // Validazione contenuto
            if (IsValidReportSchema(parsed))
                return parsed;

            return null;
        }
        catch (JsonException ex)
        {
            _logger.Warning("ReportWriter.ValidateAndParseJson", "JSON parsing failed", ex.Message);
            return null;
        }
    }

    private string CleanJsonResponse(string jsonText)
    {
        if (string.IsNullOrEmpty(jsonText)) return "{}";

        // Rimuovi eventuali wrapper markdown
        jsonText = jsonText.Trim();
        if (jsonText.StartsWith("```json"))
            jsonText = jsonText.Substring(7);
        if (jsonText.EndsWith("```"))
            jsonText = jsonText.Substring(0, jsonText.Length - 3);

        // Trova il primo { e l'ultimo }
        var firstBrace = jsonText.IndexOf('{');
        var lastBrace = jsonText.LastIndexOf('}');
        
        if (firstBrace >= 0 && lastBrace > firstBrace)
            jsonText = jsonText.Substring(firstBrace, lastBrace - firstBrace + 1);

        return jsonText.Trim();
    }

    private bool IsValidReportSchema(ReportOutputSchema? schema)
    {
        if (schema == null) return false;

        // Validazioni essenziali
        if (string.IsNullOrEmpty(schema.ExecutiveSummary?.CurrentStatus)) return false;
        if (schema.ExecutiveSummary.KeyFindings?.Count < 2) return false;
        if (string.IsNullOrEmpty(schema.TechnicalAnalysis?.BatteryAssessment)) return false;
        if (schema.Recommendations?.ImmediateActions?.Count < 1) return false;

        return true;
    }

    /// <summary>
    /// Genera report di fallback quando LLM non è disponibile
    /// </summary>
    public ReportOutputSchema GenerateFallbackReport(VehicleDataDigest digest, string analysisLevel)
    {
        return new ReportOutputSchema
        {
            ExecutiveSummary = new ExecutiveSummary
            {
                CurrentStatus = GenerateFallbackStatus(digest),
                KeyFindings = GenerateFallbackFindings(digest),
                CriticalAlerts = GenerateFallbackAlerts(digest),
                OverallTrend = "Analisi automatica disponibile - per insights dettagliati riavviare il sistema AI"
            },
            TechnicalAnalysis = new TechnicalAnalysis
            {
                BatteryAssessment = $"Livello medio batteria: {digest.Battery.AvgLevel:F1}%, Health Score: {digest.Battery.HealthScore:F1}/100",
                ChargingBehavior = $"Completate {digest.Charging.TotalSessions} sessioni di ricarica con costo medio di €{digest.Charging.AvgCostPerKwh:F3}/kWh",
                DrivingPatterns = $"Percorsi {digest.Driving.TotalDistance:F1} km in {digest.Driving.TripsCount} viaggi",
                EfficiencyAnalysis = $"Efficienza complessiva: {digest.Efficiency.OverallEfficiency:F1} km/kWh"
            },
            PredictiveInsights = new PredictiveInsights
            {
                NextMonthPrediction = "Analisi predittiva limitata - dati numerici disponibili nella sezione tecnica",
                RiskFactors = new List<string> { "Sistema AI temporaneamente non disponibile" },
                OpportunityAreas = new List<string> { "Riavviare sistema per analisi complete" },
                MaintenanceRecommendations = "Controllare stato sistema AI per raccomandazioni personalizzate"
            },
            Recommendations = new Recommendations
            {
                ImmediateActions = new List<ActionItem>
                {
                    new() { Action = "Verificare connessione sistema AI", Benefit = "Ripristino analisi complete", Timeline = "Immediato" }
                },
                MediumTermActions = new List<ActionItem>
                {
                    new() { Action = "Monitorare metriche base", Benefit = "Continuità operativa", Timeline = "1-7 giorni" }
                },
                OptimizationStrategy = "Utilizzare dati numerici disponibili fino al ripristino del sistema AI completo"
            }
        };
    }

    private string GenerateFallbackStatus(VehicleDataDigest digest)
    {
        if (digest.TotalSamples == 0)
            return "Nessun dato disponibile per il periodo analizzato";

        var qualityLabel = digest.Quality.QualityLabel ?? "Sconosciuta";
        return $"Analisi su {digest.TotalSamples} campioni, qualità dati: {qualityLabel}";
    }

    private List<string> GenerateFallbackFindings(VehicleDataDigest digest)
    {
        var findings = new List<string>();

        if (digest.Battery.AvgLevel > 0)
            findings.Add($"Livello medio batteria: {digest.Battery.AvgLevel:F1}%");

        if (digest.Charging.TotalSessions > 0)
            findings.Add($"Completate {digest.Charging.TotalSessions} sessioni di ricarica");

        if (digest.Driving.TotalDistance > 0)
            findings.Add($"Percorsi {digest.Driving.TotalDistance:F1} km totali");

        if (digest.Quality.QualityScore > 0)
            findings.Add($"Qualità dati: {digest.Quality.QualityScore}/100");

        return findings.Take(4).ToList();
    }

    private List<string> GenerateFallbackAlerts(VehicleDataDigest digest)
    {
        var alerts = new List<string>();

        if (digest.Battery.AvgLevel < 20)
            alerts.Add("Livello batteria medio basso - considerare ricariche più frequenti");

        if (digest.Quality.QualityScore < 60)
            alerts.Add("Qualità dati sotto standard - verificare connettività");

        return alerts.Take(2).ToList();
    }
}