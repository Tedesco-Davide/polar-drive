using System.Text.Json;
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

        // Check if Google Ads is enabled
        var isEnabled = Environment.GetEnvironmentVariable("GOOGLE_ADS_ENABLED")?.ToLower() == "true";
        if (!isEnabled)
        {
            await _logger.Info(source, $"⏭️ Google Ads integration disabled, skipping for {vin}");
            return;
        }

        await _logger.Info(source, $"📤 Invio insights a Google Ads per {vin}");

        var customerId = Environment.GetEnvironmentVariable("GOOGLE_ADS_CUSTOMER_ID") ?? "YOUR_CUSTOMER_ID";
        var conversionAction = Environment.GetEnvironmentVariable("GOOGLE_ADS_CONVERSION_ACTION") ?? "YOUR_CONVERSION_ACTION";
        var developerToken = Environment.GetEnvironmentVariable("GOOGLE_ADS_DEVELOPER_TOKEN");

        // Estrai metriche chiave dagli insights AI
        var metrics = ExtractMetricsFromAiAndAggregation(aiPayload, aggregation);

        // Format conversion action as resource name
        var conversionActionResourceName = $"customers/{customerId}/conversionActions/{conversionAction}";

        // Format datetime as required by Google Ads API (yyyy-MM-dd HH:mm:sszzz)
        var conversionDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:sszzz");

        var conversion = new
        {
            conversionAction = conversionActionResourceName,
            conversionDateTime = conversionDateTime,
            conversionValue = metrics.ConversionValue,
            currencyCode = "EUR",
            // Use orderId as unique identifier for this conversion (required for deduplication)
            orderId = $"polardrive_{vehicleId}_{DateTime.Now:yyyyMMddHHmmss}"
        };

        try
        {
            // Get OAuth2 access token
            var accessToken = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                await _logger.Error(source, "Failed to obtain Google Ads access token", "Check OAuth2 credentials");
                return;
            }

            // Build authenticated request - using uploadClickConversions endpoint
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://googleads.googleapis.com/v18/customers/{customerId}:uploadClickConversions");

            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Headers.Add("developer-token", developerToken);
            request.Headers.Add("login-customer-id", customerId);

            // Payload format for uploadClickConversions
            var payload = new
            {
                customerId = customerId,
                conversions = new[] { conversion },
                partialFailure = true
            };
            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                await _logger.Info(source, $"✅ Insights inviati a Google Ads per {vin}");
                await _logger.Info(source, $"📊 Response: {responseContent}");
            }
            else
            {
                await _logger.Warning(source, $"⚠️ Google Ads response: {response.StatusCode}");
                await _logger.Warning(source, $"📄 Response body: {responseContent}");
            }
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore invio Google Ads", ex.Message);
        }
    }

    private async Task<string?> GetAccessTokenAsync()
    {
        var source = "GoogleAdsIntegrationService.GetAccessTokenAsync";

        try
        {
            var clientId = Environment.GetEnvironmentVariable("GOOGLE_ADS_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("GOOGLE_ADS_CLIENT_SECRET");
            var refreshToken = Environment.GetEnvironmentVariable("GOOGLE_ADS_REFRESH_TOKEN");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(refreshToken))
            {
                await _logger.Error(source, "Missing OAuth2 credentials",
                    "Ensure GOOGLE_ADS_CLIENT_ID, GOOGLE_ADS_CLIENT_SECRET, and GOOGLE_ADS_REFRESH_TOKEN are set");
                return null;
            }

            var tokenRequest = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            };

            var response = await _httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(tokenRequest));

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                await _logger.Error(source, $"Token refresh failed: {response.StatusCode}", responseContent);
                return null;
            }

            var json = JsonDocument.Parse(responseContent);
            var accessToken = json.RootElement.GetProperty("access_token").GetString();

            await _logger.Info(source, "🔑 Access token obtained successfully");
            return accessToken;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Exception during token refresh", ex.Message);
            return null;
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