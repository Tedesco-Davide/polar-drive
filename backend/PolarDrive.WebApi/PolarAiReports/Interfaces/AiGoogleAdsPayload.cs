using System.Text.Json.Serialization;
namespace PolarDrive.WebApi.PolarAiReports;

public class AiGoogleAdsPayload
    {
        [JsonPropertyName("driver_profile")]
        public string DriverProfile { get; set; } = "balanced";

        [JsonPropertyName("driver_profile_confidence")]
        public double DriverProfileConfidence { get; set; }

        [JsonPropertyName("optimization_priority")]
        public string OptimizationPriority { get; set; } = "medium";

        [JsonPropertyName("optimization_priority_score")]
        public int OptimizationPriorityScore { get; set; }

        [JsonPropertyName("predicted_monthly_usage_change")]
        public int PredictedMonthlyUsageChange { get; set; }

        [JsonPropertyName("segment")]
        public string Segment { get; set; } = "mainstream";

        [JsonPropertyName("segment_confidence")]
        public double SegmentConfidence { get; set; }

        [JsonPropertyName("charging_behavior_score")]
        public int ChargingBehaviorScore { get; set; }

        [JsonPropertyName("efficiency_potential")]
        public int EfficiencyPotential { get; set; }

        [JsonPropertyName("battery_health_trend")]
        public string BatteryHealthTrend { get; set; } = "stable";

        [JsonPropertyName("engagement_level")]
        public string EngagementLevel { get; set; } = "medium";

        [JsonPropertyName("conversion_likelihood")]
        public double ConversionLikelihood { get; set; }

        [JsonPropertyName("lifetime_value_indicator")]
        public string LifetimeValueIndicator { get; set; } = "medium";

        [JsonPropertyName("recommended_campaign_type")]
        public string RecommendedCampaignType { get; set; } = "consideration";

        [JsonPropertyName("key_motivators")]
        public List<string> KeyMotivators { get; set; } = new();
    }