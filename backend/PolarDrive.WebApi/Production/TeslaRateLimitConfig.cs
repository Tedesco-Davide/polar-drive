namespace PolarDrive.WebApi.Production
{
    public class TeslaRateLimitConfig
    {
        public int RequestsPerHourPerVehicle { get; set; } = 200;
        public int DelayBetweenVehiclesMs { get; set; } = 15000;
        public int DelayBetweenTokenRefreshMs { get; set; } = 5000;
        public int WakeUpDelayMs { get; set; } = 15000;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 30000;
        public int CooldownAfterErrorMs { get; set; } = 300000;
    }

    public class TeslaMonitoringConfig
    {
        public double ErrorRateThreshold { get; set; } = 0.20; // 20%
        public int ConsecutiveErrorThreshold { get; set; } = 5;
        public int HourlyCallWarningThreshold { get; set; } = 180;
        public int StaleDataHours { get; set; } = 2;
        public bool EmailAlertsEnabled { get; set; } = true;
        public List<string> AlertEmails { get; set; } = new();
        public string? WebhookUrl { get; set; }
    }
}