namespace PolarDrive.WebApi.Constants
{
    public static class CommonConstants
    {
        // Ore corrispondenti a 30 giorni, per calcolo di mese intero, ed ottimizzazione procedure dati sulla base del periodo mensile
        public const int MONTHLY_HOURS_THRESHOLD = 720;

        // Cellulare Operativo PolarDrive, utilizzato per SMS ADAPTIVE_GDPR ed ADAPTIVE_PROFILE
        public const string SMS_ADAPTIVE_MOBILE_NUMBER = "447441446357";

        // Finestra di tempo in ore, utilizzata per SMS ADAPTIVE_GDPR ed ADAPTIVE_PROFILE
        public const int SMS_ADAPTIVE_HOURS_THRESHOLD = 24;

        // Finestra di tempo minuti, tra una richiesta SMS ADAPTIVE_GDPR e l'altra, per lo stesso utilizzatore
        public const int SMS_ADAPTIVE_GDPR_REQUEST_INTERVAL_MINUTES = 35;

        // Valori usati nel controller dei Reports, insieme anche a monthly, per determinare tipo cadenza Report
        public const int DAILY_HOURS_THRESHOLD = 24;
        public const int WEEKLY_HOURS_THRESHOLD = 168;

        // Numero massimo di tentativi di generazione report in caso di fallimento
        public const int MAX_RETRIES = 5;

        // Tempo di attesa in produzione prima di riprovare un report fallito
        public const int PROD_RETRY_HOURS = 5;

        // Tempo di attesa in development prima di riprovare un report fallito
        public const int DEV_RETRY_MINUTES = 1;

        // Intervallo minimo tra tentativi di generazione per lo stesso veicolo in development
        public const int DEV_INTERVAL_MINUTES = 5;

        // Pausa tra processazione di veicoli diversi in produzione (evita sovraccarico)
        public const int VEHICLE_DELAY_MINUTES = 2;

        // Ritardo iniziale prima di avviare il loop di development
        public const int DEV_INITIAL_DELAY_MINUTES = 1;

        // Frequenza del loop principale dello scheduler in development
        public const int DEV_REPEAT_DELAY_MINUTES = 20;

        // Intervallo di ripetizione per i report mensili in produzione
        public const int PROD_MONTHLY_REPEAT_DAYS = 30;

        // Frequenza dei controlli retry in produzione (ogni ora)
        public const int PROD_RETRY_REPEAT_HOURS = 1;

        // Orario di esecuzione report mensili in produzione (5:00 AM)
        public const int PROD_MONTHLY_EXECUTION_HOUR = 5;
        public const int PROD_MONTHLY_EXECUTION_MINUTE = 0;
        public const int PROD_MONTHLY_EXECUTION_SECOND = 0;

        // Periodo di grazia prima di fare check sul fetching dei dati effettivo
        public const int DEV_GRACE_PERIOD_MINUTES = 5;
        public const int PROD_GRACE_PERIOD_HOURS = 2;

        // Treshold per verificare OGNI QUANTO effettuare il processo "Check periodo di grazia"
        public const int DEV_GRACE_PERIOD_INACTIVITY_TRESHOLD_MINUTES = 10;
        public const int PROD_GRACE_PERIOD_INACTIVITY_TRESHOLD_HOURS = 6;

        // Frequenza dei controlli di PDF rimasti in stato PROCESSING o REGENERATING => Spostati in stato ERROR
        public const int DEV_RETRY_ORPHAN_PDF_REPEAT_MINUTES = 2;
        public const int PROD_RETRY_ORPHAN_PDF_REPEAT_HOURS = 1;

        // Frequenza di check e retry generazione report falliti spostati in stato ERROR
        public const int DEV_RETRY_FAILED_PDF_REPEAT_MINUTES = 2;
        public const int PROD_RETRY_FAILED_PDF_REPEAT_HOURS = 1;

        // Frequenza di archiviazione dati dalla tabella VehicleData alla tabella VehicleDataArchive
        public const int DATA_ARCHIVE_FREQUENCY_HOURS = 24;

        // Parametri per GapAnalysisService.cs:
        // Stampa PDF contenente analisi certificazione gap temporali sul fetch dati.
        // Pesi per il calcolo della confidenza

        // Record esistono prima e dopo?
        public const double GAP_ANALYSIS_WEIGHT_CONTINUITY = 0.30; // 30%
        // Progressione batteria coerente?
        public const double GAP_ANALYSIS_WEIGHT_BATTERY = 0.25;    // 25%      
        // Ora tipica di utilizzo?
        public const double GAP_ANALYSIS_WEIGHT_PATTERN = 0.20;    // 20%    
        // Gap singolo vs multipli?
        public const double GAP_ANALYSIS_WEIGHT_GAP_LENGTH = 0.15; // 15%
        // Pattern storico veicolo    
        public const double GAP_ANALYSIS_WEIGHT_HISTORICAL = 0.10; // 10%
        // Bonus confidenza in percentuale, nel caso si tratti di un problema tecnico documentato
        public const double GAP_ANALYSIS_WEIGHT_TECH_PROBLEM = 15; // 15%
        // Bonus confidenza per km percorsi (se odometro aumenta > GAP_ANALYSIS_KM_THRESHOLD tra record adiacenti)
        public const double GAP_ANALYSIS_KM_BONUS = 5; // 5%
        // Soglia minima km per considerare il veicolo "in movimento"
        public const double GAP_ANALYSIS_KM_THRESHOLD = 10; // 10km

        // Timeout HTTP in minuti per richieste lunghe (es. analisi gap estese)
        // Usato per RequestHeadersTimeout, KeepAliveTimeout ed altri timeout frontend
        public const int HTTP_LONG_REQUEST_TIMEOUT_MINUTES = 15; // 15 minuti

        // Limite massimo dimensione file upload (100MB)
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
        /// Brand veicoli supportati
        /// </summary>
        public static class VehicleBrand
        {
            public const string TESLA = "tesla";
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