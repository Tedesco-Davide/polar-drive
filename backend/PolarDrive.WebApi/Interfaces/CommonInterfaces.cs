
namespace PolarDrive.WebApi.Interfaces
{
    public static class CommonInterfaces
    {
        public const int DAILY_HOURS_THRESHOLD = 24;
        public const int WEEKLY_HOURS_THRESHOLD = 168;
        public const int MONTHLY_HOURS_THRESHOLD = 720;
        public const int MIN_RECORDS_FOR_GENERATION = 5;
        public const int MAX_RETRIES = 5;
        public const int PROD_RETRY_HOURS = 5;
        public const int DEV_RETRY_MINUTES = 1;
        public const int DEV_INTERVAL_MINUTES = 5;
        public const int VEHICLE_DELAY_MINUTES = 2;
        public const int DEV_INITIAL_DELAY_MINUTES = 1;
        public const int DEV_REPEAT_DELAY_MINUTES = 1;
    }
}
