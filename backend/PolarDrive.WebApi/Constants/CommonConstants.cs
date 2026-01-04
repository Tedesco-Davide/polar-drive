namespace PolarDrive.WebApi.Constants
{
    /// <summary>
    /// Costanti compile-time per identificatori e attributi.
    /// </summary>
    public static class CommonConstants
    {
        // Limite massimo dimensione file upload (100MB)
        // NOTA: Questa costante DEVE rimanere compile-time per gli attributi [RequestSizeLimit]
        public const long MAX_UPLOAD_SIZE_BYTES = 100_000_000;

        /// <summary>
        /// Stati dei Report PDF - usati per tracciare lo stato di generazione dei report
        /// </summary>
        public static class ReportStatus
        {
            public const string PROCESSING = "PROCESSING";
            public const string ERROR = "ERROR";
            public const string REGENERATING = "REGENERATING";
            public const string PDF_READY = "PDF-READY";
            public const string PENDING = "PENDING";
            public const string COMPLETED = "COMPLETED";
        }

        /// <summary>
        /// Comandi SMS Adaptive - usati per gestire le risposte SMS degli utenti
        /// </summary>
        public static class SmsCommand
        {
            public const string ADAPTIVE_PROFILE_ON = "ADAPTIVE_PROFILE_ON";
            public const string ADAPTIVE_PROFILE_OFF = "ADAPTIVE_PROFILE_OFF";
            public const string ACCETTO = "ACCETTO";
            public const string STOP = "STOP";
            public const string OFF = "OFF";
        }

        /// <summary>
        /// Tipi di ricerca per i controller - usati nei parametri searchType delle API
        /// </summary>
        public static class SearchType
        {
            public const string ID = "id";
            public const string VAT = "vat";
            public const string NAME = "name";
            public const string STATUS = "status";
            public const string VIN = "vin";
            public const string COMPANY = "company";
            public const string OUTAGE_TYPE = "outageType";
        }

        /// <summary>
        /// Tag per custom variables Google Ads - usati per integrazione marketing
        /// </summary>
        public static class GoogleAdsTag
        {
            public const string BATTERY_HEALTH = "battery_health";
            public const string EFFICIENCY_SCORE = "efficiency_score";
            public const string USAGE_INTENSITY = "usage_intensity";
            public const string AI_DRIVER_PROFILE = "ai_driver_profile";
            public const string AI_DRIVER_CONFIDENCE = "ai_driver_confidence";
            public const string AI_OPTIMIZATION_PRIORITY = "ai_optimization_priority";
            public const string AI_OPTIMIZATION_SCORE = "ai_optimization_score";
            public const string AI_USAGE_CHANGE_PRED = "ai_usage_change_pred";
            public const string AI_SEGMENT = "ai_segment";
            public const string AI_SEGMENT_CONFIDENCE = "ai_segment_confidence";
            public const string AI_CHARGING_SCORE = "ai_charging_score";
            public const string AI_EFFICIENCY_POTENTIAL = "ai_efficiency_potential";
            public const string AI_BATTERY_TREND = "ai_battery_trend";
            public const string AI_ENGAGEMENT = "ai_engagement";
            public const string AI_CONVERSION_LIKELIHOOD = "ai_conversion_likelihood";
            public const string AI_LTV_INDICATOR = "ai_ltv_indicator";
            public const string AI_CAMPAIGN_TYPE = "ai_campaign_type";
            public const string AI_MOTIVATOR_1 = "ai_motivator_1";
            public const string AI_MOTIVATOR_2 = "ai_motivator_2";
            public const string AI_MOTIVATOR_3 = "ai_motivator_3";
        }
    }
}