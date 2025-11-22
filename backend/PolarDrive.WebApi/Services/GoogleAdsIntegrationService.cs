using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Helpers;
using PolarDrive.WebApi.PolarAiReports;

namespace PolarDrive.WebApi.Services;

public class GoogleAdsIntegrationService
{
    private readonly HttpClient _httpClient = new();
    private readonly PolarDriveLogger _logger = new();

    public async Task SendAiInsightsToGoogleAds(AiGoogleAdsPayload aiPayload, GoogleAdsTeslaDataAggregation aggregation, int vehicleId, string vin)
    {
        var source = "GoogleAdsIntegrationService.SendAiInsightsToGoogleAds";

        await _logger.Info(source, $"📤 Invio insights a Google Ads per {vin}");

        var customerId = Environment.GetEnvironmentVariable("GOOGLE_ADS_CUSTOMER_ID") ?? "YOUR_CUSTOMER_ID";
        var conversionAction = Environment.GetEnvironmentVariable("GOOGLE_ADS_CONVERSION_ACTION") ?? "YOUR_CONVERSION_ACTION";

        // Estrai metriche chiave dagli insights AI
        var metrics = ExtractMetricsFromAiAndAggregation(aiPayload, aggregation);

        var conversion = new
        {
            conversion_action = conversionAction,
            conversion_date_time = DateTime.Now,
            conversion_value = metrics.ConversionValue,
            currency_code = "EUR",
            user_identifiers = new[]
        {
        new { hashed_email = GenericHelpers.ComputeContentHash($"vehicle_{vehicleId}@datapolar.com") }
        },
            custom_variables = new object[]
            {
                new { tag = "battery_health", number_value = metrics.BatteryHealthScore },
                new { tag = "efficiency_score", number_value = metrics.EfficiencyScore },
                new { tag = "usage_intensity", string_value = metrics.UsageIntensity },
                new { tag = "ai_driver_profile", string_value = aiPayload.DriverProfile },
                new { tag = "ai_driver_confidence", number_value = aiPayload.DriverProfileConfidence },
                new { tag = "ai_optimization_priority", string_value = aiPayload.OptimizationPriority },
                new { tag = "ai_optimization_score", number_value = aiPayload.OptimizationPriorityScore },
                new { tag = "ai_usage_change_pred", number_value = aiPayload.PredictedMonthlyUsageChange },
                new { tag = "ai_segment", string_value = aiPayload.Segment },
                new { tag = "ai_segment_confidence", number_value = aiPayload.SegmentConfidence },
                new { tag = "ai_charging_score", number_value = aiPayload.ChargingBehaviorScore },
                new { tag = "ai_efficiency_potential", number_value = aiPayload.EfficiencyPotential },
                new { tag = "ai_battery_trend", string_value = aiPayload.BatteryHealthTrend },
                new { tag = "ai_engagement", string_value = aiPayload.EngagementLevel },
                new { tag = "ai_conversion_likelihood", number_value = aiPayload.ConversionLikelihood },
                new { tag = "ai_ltv_indicator", string_value = aiPayload.LifetimeValueIndicator },
                new { tag = "ai_campaign_type", string_value = aiPayload.RecommendedCampaignType },
                new { tag = "ai_motivator_1", string_value = aiPayload.KeyMotivators.ElementAtOrDefault(0) ?? "" },
                new { tag = "ai_motivator_2", string_value = aiPayload.KeyMotivators.ElementAtOrDefault(1) ?? "" },
                new { tag = "ai_motivator_3", string_value = aiPayload.KeyMotivators.ElementAtOrDefault(2) ?? "" }
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
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

    private static GoogleAdsMetrics ExtractMetricsFromAiAndAggregation(AiGoogleAdsPayload aiPayload, GoogleAdsTeslaDataAggregation aggregation)
    {
        var metrics = new GoogleAdsMetrics
        {
            BatteryHealthScore = aggregation.BatteryLevelAvg > 0 ? (double)aggregation.BatteryLevelAvg : 70,
            EfficiencyScore = aiPayload.EfficiencyPotential > 0 ? aiPayload.EfficiencyPotential :
                            (aggregation.BatteryLevels.Any() ? Math.Min(100, (double)aggregation.BatteryLevelAvg + 15) : 70),
            UsageIntensity = aggregation.AvgSpeed switch { > 60 => "high", > 30 => "medium", _ => "low" }
        };

        metrics.ConversionValue = CalculateConversionValue(metrics) * (1 + aiPayload.ConversionLikelihood);
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
    public double ConversionValue { get; set; }
}