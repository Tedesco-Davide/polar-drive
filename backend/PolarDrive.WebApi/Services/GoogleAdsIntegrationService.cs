using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Helpers;

namespace PolarDrive.WebApi.Services;

public class GoogleAdsIntegrationService
{
    private readonly HttpClient? _httpClient;
    private readonly PolarDriveLogger? _logger;

    public async Task SendAiInsightsToGoogleAds(string aiInsights, int vehicleId, string vin)
    {
        var source = "GoogleAdsIntegrationService.SendAiInsightsToGoogleAds";
        
        await _logger!.Info(source, $"📤 Invio insights a Google Ads per {vin}");

        var customerId = Environment.GetEnvironmentVariable("GOOGLE_ADS_CUSTOMER_ID") ?? "YOUR_CUSTOMER_ID";
        var conversionAction = Environment.GetEnvironmentVariable("GOOGLE_ADS_CONVERSION_ACTION") ?? "YOUR_CONVERSION_ACTION";

        // Estrai metriche chiave dagli insights AI
        var metrics = ExtractMetricsFromAiInsights(aiInsights);

        var conversion = new
        {
            conversion_action = conversionAction,
            conversion_date_time = DateTime.Now,
            conversion_value = metrics.ConversionValue,
            currency_code = "EUR",
            
            user_identifiers = new[]
            {
                new { hashed_email =  GenericHelpers.ComputeContentHash($"vehicle_{vehicleId}@datapolar.com") }
            },
        };

        try
        {
            var response = await _httpClient!.PostAsJsonAsync(
                $"https://googleads.googleapis.com/v15/customers/{customerId}:uploadConversions",
                new { conversions = new[] { conversion } }
            );

            if (response.IsSuccessStatusCode)
                await _logger.Info(source, $"✅ Insights inviati a Google Ads per {vin}");
            else
                await _logger.Warning(source, $"⚠️ Google Ads response: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore invio Google Ads", ex.Message);
        }
    }

    private static GoogleAdsMetrics ExtractMetricsFromAiInsights(string aiInsights)
    {
        var metrics = new GoogleAdsMetrics();

        if (aiInsights.Contains("Batteria ben carica") || aiInsights.Contains("Buon livello"))
            metrics.BatteryHealthScore = 85;
        else if (aiInsights.Contains("Batteria scarica"))
            metrics.BatteryHealthScore = 45;
        else
            metrics.BatteryHealthScore = 70;

        if (aiInsights.Contains("EUR/kWh"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(aiInsights, @"(\d+\.?\d*)\s*EUR/kWh");
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var cost))
                metrics.EfficiencyScore = cost < 0.40m ? 90 : cost < 0.55m ? 70 : 50;
        }

        if (aiInsights.Contains("Uso quotidiano"))
            metrics.UsageIntensity = "high";
        else if (aiInsights.Contains("Uso occasionale"))
            metrics.UsageIntensity = "low";
        else
            metrics.UsageIntensity = "medium";

        if (aiInsights.Contains("Ricarica veloce"))
            metrics.ChargingPattern = "fast_frequent";
        else if (aiInsights.Contains("Ricarica completa"))
            metrics.ChargingPattern = "slow_complete";
        else
            metrics.ChargingPattern = "balanced";

        metrics.RiskLevel = aiInsights.Contains("controllo necessario") ? "high" : "low";
        metrics.PredictedMaintenance = aiInsights.Contains("Manutenzione") ? "yes" : "no";
        metrics.OptimizationPotential = aiInsights.Contains("ottimizzazione") ? 25 : 10;
        
        metrics.ConversionValue = CalculateConversionValue(metrics);

        return metrics;
    }

    private static double CalculateConversionValue(GoogleAdsMetrics metrics)
    {
        double value = 30.0;
        
        if (metrics.BatteryHealthScore > 80) value += 20;
        if (metrics.EfficiencyScore > 80) value += 15;
        if (metrics.UsageIntensity == "high") value += 10;
        
        return value;
    }
}

public class GoogleAdsMetrics
{
    public double BatteryHealthScore { get; set; }
    public double EfficiencyScore { get; set; }
    public string UsageIntensity { get; set; } = "";
    public string ChargingPattern { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public string PredictedMaintenance { get; set; } = "";
    public double OptimizationPotential { get; set; }
    public double ConversionValue { get; set; }
}