using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Helpers;
using PolarDrive.WebApi.PolarAiReports;
using static PolarDrive.WebApi.Constants.CommonConstants;

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
                new { tag = GoogleAdsTag.BATTERY_HEALTH, number_value = metrics.BatteryHealthScore },
                new { tag = GoogleAdsTag.EFFICIENCY_SCORE, number_value = metrics.EfficiencyScore },
                new { tag = GoogleAdsTag.USAGE_INTENSITY, string_value = metrics.UsageIntensity },
                new { tag = GoogleAdsTag.AI_DRIVER_PROFILE, string_value = aiPayload.DriverProfile },
                new { tag = GoogleAdsTag.AI_DRIVER_CONFIDENCE, number_value = aiPayload.DriverProfileConfidence },
                new { tag = GoogleAdsTag.AI_OPTIMIZATION_PRIORITY, string_value = aiPayload.OptimizationPriority },
                new { tag = GoogleAdsTag.AI_OPTIMIZATION_SCORE, number_value = aiPayload.OptimizationPriorityScore },
                new { tag = GoogleAdsTag.AI_USAGE_CHANGE_PRED, number_value = aiPayload.PredictedMonthlyUsageChange },
                new { tag = GoogleAdsTag.AI_SEGMENT, string_value = aiPayload.Segment },
                new { tag = GoogleAdsTag.AI_SEGMENT_CONFIDENCE, number_value = aiPayload.SegmentConfidence },
                new { tag = GoogleAdsTag.AI_CHARGING_SCORE, number_value = aiPayload.ChargingBehaviorScore },
                new { tag = GoogleAdsTag.AI_EFFICIENCY_POTENTIAL, number_value = aiPayload.EfficiencyPotential },
                new { tag = GoogleAdsTag.AI_BATTERY_TREND, string_value = aiPayload.BatteryHealthTrend },
                new { tag = GoogleAdsTag.AI_ENGAGEMENT, string_value = aiPayload.EngagementLevel },
                new { tag = GoogleAdsTag.AI_CONVERSION_LIKELIHOOD, number_value = aiPayload.ConversionLikelihood },
                new { tag = GoogleAdsTag.AI_LTV_INDICATOR, string_value = aiPayload.LifetimeValueIndicator },
                new { tag = GoogleAdsTag.AI_CAMPAIGN_TYPE, string_value = aiPayload.RecommendedCampaignType },
                new { tag = GoogleAdsTag.AI_MOTIVATOR_1, string_value = aiPayload.KeyMotivators.ElementAtOrDefault(0) ?? "" },
                new { tag = GoogleAdsTag.AI_MOTIVATOR_2, string_value = aiPayload.KeyMotivators.ElementAtOrDefault(1) ?? "" },
                new { tag = GoogleAdsTag.AI_MOTIVATOR_3, string_value = aiPayload.KeyMotivators.ElementAtOrDefault(2) ?? "" }
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