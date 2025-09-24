namespace PolarDrive.WebApi.Constants
{
    public static class CommonConstants
    {
        // Ore corrispondenti a 30 giorni, per calcolo di mese intero
        public const int MONTHLY_HOURS_THRESHOLD = 720;

        // Finestra di tempo in ore, utilizzata per SMS ADAPTIVE
        public const int SMS_ADPATIVE_HOURS_THRESHOLD = 4;
        
        // Valori usati nel controller dei Reports, insieme anche a monthly, per determinare tipo cadenza Report
        public const int DAILY_HOURS_THRESHOLD = 24;
        public const int WEEKLY_HOURS_THRESHOLD = 168;
        
        // Valori usati per determinare se in stato WAITING-RECORDS
        public const int MIN_RECORDS_FOR_GENERATION = 5;
        
        // Numero massimo di tentativi di rigenerazione report in caso di fallimento
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
    }
}