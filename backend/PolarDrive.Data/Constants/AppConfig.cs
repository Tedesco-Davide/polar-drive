using System.Text.Json;

namespace PolarDrive.Data.Constants;

#region JSON Model Classes

public class AppConfigRoot
{
    public SchedulerConfig Scheduler { get; set; } = new();
    public RetryConfig Retry { get; set; } = new();
    public LimitsConfig Limits { get; set; } = new();
    public SmsConfig Sms { get; set; } = new();
    public ThresholdsConfig Thresholds { get; set; } = new();
    public GapAnalysisConfig GapAnalysis { get; set; } = new();
    public TsaConfig Tsa { get; set; } = new();
}

public class SchedulerConfig
{
    public SchedulerDevConfig Development { get; set; } = new();
    public SchedulerProdConfig Production { get; set; } = new();
}

public class SchedulerDevConfig
{
    // Ritardo iniziale prima di avviare il loop di development
    public int InitialDelayMinutes { get; set; }

    // Frequenza del loop principale dello scheduler in development
    public int RepeatDelayMinutes { get; set; }

    // Tempo di attesa in development prima di riprovare un report fallito
    public int RetryMinutes { get; set; }

    // Intervallo minimo tra tentativi di generazione per lo stesso veicolo in development
    public int IntervalMinutes { get; set; }

    // Periodo di grazia prima di fare check sul fetching dei dati effettivo
    public int GracePeriodMinutes { get; set; }

    // Threshold per verificare ogni quanto effettuare il processo "Check periodo di grazia"
    public int GracePeriodInactivityThresholdMinutes { get; set; }

    // Frequenza dei controlli di PDF rimasti in stato PROCESSING o REGENERATING => Spostati in stato ERROR
    public int RetryOrphanPdfRepeatMinutes { get; set; }
    
    // Frequenza di check e retry generazione report falliti spostati in stato ERROR
    public int RetryFailedPdfRepeatMinutes { get; set; }

    // Intervallo di esecuzione del background service OutageDetection in development
    public int OutageCheckIntervalMinutes { get; set; }
}

public class SchedulerProdConfig
{
    // Intervallo di ripetizione per i report mensili in produzione
    public int MonthlyRepeatDays { get; set; }

    // Frequenza dei controlli retry in produzione (ogni ora)
    public int RetryRepeatHours { get; set; }

    // Tempo di attesa in produzione prima di riprovare un report fallito
    public int RetryHours { get; set; }

    // Pausa tra processazione di veicoli diversi in produzione (evita sovraccarico)
    public int VehicleDelayMinutes { get; set; }

    // Periodo di grazia prima di fare check sul fetching dei dati effettivo
    public int GracePeriodHours { get; set; }

    // Threshold per verificare OGNI QUANTO effettuare il processo "Check periodo di grazia"
    public int GracePeriodInactivityThresholdHours { get; set; }

    // Frequenza dei controlli di PDF rimasti in stato PROCESSING o REGENERATING => Spostati in stato ERROR
    public int RetryOrphanPdfRepeatHours { get; set; }

    // Frequenza di check e retry generazione report falliti spostati in stato ERROR
    public int RetryFailedPdfRepeatHours { get; set; }

    // Intervallo di esecuzione del background service OutageDetection in production
    public int OutageCheckIntervalMinutes { get; set; }

    // Orario di esecuzione report mensili in produzione
    public MonthlyExecutionTime MonthlyExecutionTime { get; set; } = new();
}

public class MonthlyExecutionTime
{
    // Ora esecuzione report mensili
    public int Hour { get; set; }

    // Minuto esecuzione report mensili
    public int Minute { get; set; }

    // Secondo esecuzione report mensili
    public int Second { get; set; }
}

public class RetryConfig
{
    // Numero massimo di tentativi di generazione report in caso di fallimento
    public int MaxRetries { get; set; }
}

public class LimitsConfig
{
    /// <summary>
    /// Numero massimo di generazioni PDF contemporanee.
    /// Evita saturazione memoria/CPU e timeout a cascata.
    /// </summary>
    public int PdfMaxConcurrentGenerations { get; set; } = 2;

    // Limite massimo dimensione file upload
    public long MaxUploadSizeBytes { get; set; }

    // Timeout HTTP in minuti per richieste lunghe (es. analisi gap estese)
    public int HttpLongRequestTimeoutMinutes { get; set; }

    // Frequenza di archiviazione dati dalla tabella VehicleData alla tabella VehicleDataArchive
    public int DataArchiveFrequencyHours { get; set; }
}

public class SmsConfig
{
    // Cellulare Operativo PolarDrive, utilizzato per SMS ADAPTIVE_GDPR ed ADAPTIVE_PROFILE
    public string AdaptiveMobileNumber { get; set; } = "";

    // Finestra di tempo in ore, utilizzata per SMS ADAPTIVE_GDPR ed ADAPTIVE_PROFILE
    public int AdaptiveHoursThreshold { get; set; }

    // Finestra di tempo minuti, tra una richiesta SMS ADAPTIVE_GDPR e l'altra, per lo stesso utilizzatore
    public int GdprRequestIntervalMinutes { get; set; }
}

public class ThresholdsConfig
{
    // Ore corrispondenti a 30 giorni, per calcolo di mese intero
    public int MonthlyHours { get; set; }

    // Ore corrispondenti a 1 giorno, per determinare tipo cadenza Report
    public int DailyHours { get; set; }

    // Ore corrispondenti a 1 settimana, per determinare tipo cadenza Report
    public int WeeklyHours { get; set; }
}

public class GapAnalysisConfig
{
    // Pesi per il calcolo della confidenza gap analysis
    public GapAnalysisWeights Weights { get; set; } = new();

    // Soglia minima km per considerare il veicolo "in movimento"
    public double KmThreshold { get; set; }

    // Timeout API per gap analysis
    public GapAnalysisApiTimeouts ApiTimeouts { get; set; } = new();

    // Soglie di intervento per alert automatici
    public GapAnalysisThresholds Thresholds { get; set; } = new();

    // Impatto ADAPTIVE_PROFILE sulla confidenza
    public GapAnalysisAdaptiveProfile AdaptiveProfile { get; set; } = new();

    // Configurazione background monitoring service
    public GapAnalysisMonitoring Monitoring { get; set; } = new();
}

/// <summary>
/// Timeout API per gap analysis (in minuti).
/// </summary>
public class GapAnalysisApiTimeouts
{
    public int AnalysisMinutes { get; set; } = 15;
    public int ValidateMinutes { get; set; } = 5;
    public int StatusMinutes { get; set; } = 2;
    public int DownloadMinutes { get; set; } = 5;
}

/// <summary>
/// Soglie di intervento per generazione alert automatici.
/// Quando una metrica supera la soglia, viene generato un GapAlert.
/// </summary>
public class GapAnalysisThresholds
{
    /// <summary>
    /// Confidenza minima accettabile (0-100).
    /// Gap con confidenza inferiore generano alert LOW_CONFIDENCE.
    /// </summary>
    public double MinConfidencePercent { get; set; } = 70;

    /// <summary>
    /// Percentuale massima di gap sul periodo totale.
    /// Esempio: 15% = se su 720 ore mensili ci sono >108 ore di gap → alert.
    /// </summary>
    public double MaxGapPercentOfPeriod { get; set; } = 15;

    /// <summary>
    /// Ore consecutive massime di gap prima di escalation.
    /// Gap consecutivi oltre questa soglia generano alert CONSECUTIVE_GAPS.
    /// </summary>
    public int MaxConsecutiveGapHours { get; set; } = 4;

    /// <summary>
    /// Percentuale downtime mensile massima.
    /// Superata questa soglia → alert CONTRACT_BREACH (procedura risoluzione).
    /// </summary>
    public double MaxMonthlyDowntimePercent { get; set; } = 20;

    /// <summary>
    /// Confidenza minima richiesta per gap durante periodo ADAPTIVE_PROFILE attivo.
    /// Se durante un periodo profilato la confidenza è sotto questa soglia → anomalia grave.
    /// </summary>
    public double ProfiledPeriodMinConfidencePercent { get; set; } = 85;
}

/// <summary>
/// Configurazione impatto ADAPTIVE_PROFILE sul calcolo confidenza.
/// </summary>
public class GapAnalysisAdaptiveProfile
{
    /// <summary>
    /// Bonus confidenza (%) se gap durante periodo NON profilato.
    /// Logica: se nessuno doveva usare il veicolo, il gap è più "giustificabile".
    /// </summary>
    public double NotProfiledBonusPercent { get; set; } = 15;

    /// <summary>
    /// Malus confidenza (%) se gap durante periodo profilato.
    /// Logica: se qualcuno stava usando il veicolo ma non ci sono dati → anomalia grave.
    /// Valore negativo (es. -30).
    /// </summary>
    public double ProfiledMalusPercent { get; set; } = -30;
}

/// <summary>
/// Configurazione del GapMonitoringBackgroundService.
/// </summary>
public class GapAnalysisMonitoring
{
    /// <summary>
    /// Intervallo tra cicli di monitoraggio (minuti).
    /// Default: 60 minuti (1 ora).
    /// Usato sia da BackgroundService che da Alert Gap Dashboard auto-refresh.
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Delay iniziale prima del primo ciclo (minuti).
    /// Permette agli altri servizi di avviarsi.
    /// </summary>
    public int InitialDelayMinutes { get; set; } = 5;

    /// <summary>
    /// Giorni indietro da analizzare per ogni ciclo.
    /// </summary>
    public int LookbackDays { get; set; } = 7;
}

/// <summary>
/// Configurazione TSA (Timestamp Authority) per marca temporale RFC 3161.
/// DEV: FreeTSA (gratuito, non qualificato)
/// PROD: Aruba TSA (a pagamento, qualificato eIDAS)
/// </summary>
public class TsaConfig
{
    // Abilita/disabilita la richiesta di marca temporale sui PDF
    public bool Enabled { get; set; } = true;

    // Timeout per la richiesta al server TSA (in secondi)
    public int TimeoutSeconds { get; set; } = 30;

    // Numero di tentativi in caso di errore
    public int RetryCount { get; set; } = 3;
}

public class GapAnalysisWeights
{
    // Record esistono prima e dopo?
    public double Continuity { get; set; }

    // Progressione batteria coerente?
    public double Battery { get; set; }

    // Ora tipica di utilizzo?
    public double Pattern { get; set; }

    // Gap singolo vs multipli?
    public double GapLength { get; set; }

    // Pattern storico veicolo
    public double Historical { get; set; }

    // Bonus confidenza in percentuale, nel caso si tratti di un problema tecnico documentato
    public double TechProblemBonus { get; set; }

    // Bonus confidenza per km percorsi (se odometro aumenta tra record adiacenti)
    public double KmBonus { get; set; }

    // Bonus confidenza per gap durante Fleet API outage
    public double FleetApiOutageBonus { get; set; }

    // Bonus confidenza per gap durante Vehicle outage
    public double VehicleOutageBonus { get; set; }
}

#endregion

/// <summary>
/// Servizio per caricare configurazioni operative da app-config.json con supporto hot-reload.
/// Accessibile staticamente come VehicleConstants per coerenza architetturale.
/// </summary>
public static class AppConfig
{
    private static AppConfigRoot? _config;
    private static readonly Lock _lock = new();
    private static readonly string _configPath = "/app/config/app-config.json";
    private static DateTime _lastLoadTime = DateTime.MinValue;
    private static FileSystemWatcher? _watcher;

    /// <summary>
    /// Ottiene la configurazione corrente (lazy loading con cache)
    /// </summary>
    public static AppConfigRoot Config
    {
        get
        {
            if (_config == null)
            {
                lock (_lock)
                {
                    if (_config == null)
                    {
                        _config = LoadConfigFromFile();
                        SetupFileWatcher();
                    }
                }
            }
            return _config;
        }
    }

    private static AppConfigRoot LoadConfigFromFile()
    {
        if (!File.Exists(_configPath))
            throw new FileNotFoundException($"App config file not found at {_configPath}");

        var jsonString = File.ReadAllText(_configPath);
        var config = JsonSerializer.Deserialize<AppConfigRoot>(jsonString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"Invalid app config JSON structure in {_configPath}");
        _lastLoadTime = DateTime.Now;
        Console.WriteLine($"Successfully loaded app config from {_configPath}");
        return config;
    }

    /// <summary>
    /// Ricarica la configurazione (chiamabile manualmente o da FileWatcher)
    /// </summary>
    public static void ReloadConfig()
    {
        lock (_lock)
        {
            _config = LoadConfigFromFile();
        }
    }

    /// <summary>
    /// Configura un FileSystemWatcher per hot-reload automatico
    /// </summary>
    private static void SetupFileWatcher()
    {
        try
        {
            _watcher?.Dispose();

            var directory = Path.GetDirectoryName(_configPath);
            var fileName = Path.GetFileName(_configPath);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += (_, _) =>
            {
                // Debounce: ignora eventi troppo ravvicinati
                if ((DateTime.Now - _lastLoadTime).TotalSeconds < 2)
                    return;

                Console.WriteLine("AppConfig file changed, reloading...");
                ReloadConfig();
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARNING: Could not setup config file watcher: {ex.Message}");
        }
    }

    #region Shortcut Properties (retrocompatibilita' con CommonConstants)

    // === Scheduler Development ===

    // Ritardo iniziale prima di avviare il loop di development
    public static int DEV_INITIAL_DELAY_MINUTES => Config.Scheduler.Development.InitialDelayMinutes;

    // Frequenza del loop principale dello scheduler in development
    public static int DEV_REPEAT_DELAY_MINUTES => Config.Scheduler.Development.RepeatDelayMinutes;

    // Tempo di attesa in development prima di riprovare un report fallito
    public static int DEV_RETRY_MINUTES => Config.Scheduler.Development.RetryMinutes;

    // Intervallo minimo tra tentativi di generazione per lo stesso veicolo in development
    public static int DEV_INTERVAL_MINUTES => Config.Scheduler.Development.IntervalMinutes;

    // Periodo di grazia prima di fare check sul fetching dei dati effettivo
    public static int DEV_GRACE_PERIOD_MINUTES => Config.Scheduler.Development.GracePeriodMinutes;

    // Treshold per verificare OGNI QUANTO effettuare il processo "Check periodo di grazia"
    public static int DEV_GRACE_PERIOD_INACTIVITY_TRESHOLD_MINUTES => Config.Scheduler.Development.GracePeriodInactivityThresholdMinutes;

    // Frequenza dei controlli di PDF rimasti in stato PROCESSING o REGENERATING => Spostati in stato ERROR
    public static int DEV_RETRY_ORPHAN_PDF_REPEAT_MINUTES => Config.Scheduler.Development.RetryOrphanPdfRepeatMinutes;

    // Frequenza di check e retry generazione report falliti spostati in stato ERROR
    public static int DEV_RETRY_FAILED_PDF_REPEAT_MINUTES => Config.Scheduler.Development.RetryFailedPdfRepeatMinutes;

    // === Scheduler Production ===

    // Intervallo di ripetizione per i report mensili in produzione
    public static int PROD_MONTHLY_REPEAT_DAYS => Config.Scheduler.Production.MonthlyRepeatDays;

    // Frequenza dei controlli retry in produzione (ogni ora)
    public static int PROD_RETRY_REPEAT_HOURS => Config.Scheduler.Production.RetryRepeatHours;

    // Tempo di attesa in produzione prima di riprovare un report fallito
    public static int PROD_RETRY_HOURS => Config.Scheduler.Production.RetryHours;

    // Pausa tra processazione di veicoli diversi in produzione (evita sovraccarico)
    public static int VEHICLE_DELAY_MINUTES => Config.Scheduler.Production.VehicleDelayMinutes;

    // Periodo di grazia prima di fare check sul fetching dei dati effettivo
    public static int PROD_GRACE_PERIOD_HOURS => Config.Scheduler.Production.GracePeriodHours;

    // Treshold per verificare OGNI QUANTO effettuare il processo "Check periodo di grazia"
    public static int PROD_GRACE_PERIOD_INACTIVITY_TRESHOLD_HOURS => Config.Scheduler.Production.GracePeriodInactivityThresholdHours;

    // Frequenza dei controlli di PDF rimasti in stato PROCESSING o REGENERATING => Spostati in stato ERROR
    public static int PROD_RETRY_ORPHAN_PDF_REPEAT_HOURS => Config.Scheduler.Production.RetryOrphanPdfRepeatHours;

    // Frequenza di check e retry generazione report falliti spostati in stato ERROR
    public static int PROD_RETRY_FAILED_PDF_REPEAT_HOURS => Config.Scheduler.Production.RetryFailedPdfRepeatHours;

    // Intervallo di esecuzione del background service OutageDetection in development (minuti)
    public static int DEV_OUTAGE_CHECK_INTERVAL_MINUTES => Config.Scheduler.Development.OutageCheckIntervalMinutes;

    // Intervallo di esecuzione del background service OutageDetection in production (minuti)
    public static int PROD_OUTAGE_CHECK_INTERVAL_MINUTES => Config.Scheduler.Production.OutageCheckIntervalMinutes;

    // Orario di esecuzione report mensili in produzione (5:00 AM)
    public static int PROD_MONTHLY_EXECUTION_HOUR => Config.Scheduler.Production.MonthlyExecutionTime.Hour;
    public static int PROD_MONTHLY_EXECUTION_MINUTE => Config.Scheduler.Production.MonthlyExecutionTime.Minute;
    public static int PROD_MONTHLY_EXECUTION_SECOND => Config.Scheduler.Production.MonthlyExecutionTime.Second;

    // === Retry ===

    // Numero massimo di tentativi di generazione report in caso di fallimento
    public static int MAX_RETRIES => Config.Retry.MaxRetries;

    // === Limits ===

    /// <summary>
    /// Numero massimo di generazioni PDF contemporanee.
    /// Evita saturazione memoria/CPU e timeout a cascata.
    /// </summary>
    public static int PDF_MAX_CONCURRENT_GENERATIONS => Config.Limits.PdfMaxConcurrentGenerations;

    // Limite massimo dimensione file upload
    public static long MAX_UPLOAD_SIZE_BYTES => Config.Limits.MaxUploadSizeBytes;

    // Timeout HTTP in minuti per richieste lunghe (es. analisi gap estese)
    // Usato per RequestHeadersTimeout, KeepAliveTimeout ed altri timeout frontend
    public static int HTTP_LONG_REQUEST_TIMEOUT_MINUTES => Config.Limits.HttpLongRequestTimeoutMinutes;

    // Frequenza di archiviazione dati dalla tabella VehicleData alla tabella VehicleDataArchive
    public static int DATA_ARCHIVE_FREQUENCY_HOURS => Config.Limits.DataArchiveFrequencyHours;

    // === SMS ===

    // Cellulare Operativo PolarDrive, utilizzato per SMS ADAPTIVE_GDPR ed ADAPTIVE_PROFILE
    public static string SMS_ADAPTIVE_MOBILE_NUMBER => Config.Sms.AdaptiveMobileNumber;

    // Finestra di tempo in ore, utilizzata per SMS ADAPTIVE_GDPR ed ADAPTIVE_PROFILE
    public static int SMS_ADAPTIVE_HOURS_THRESHOLD => Config.Sms.AdaptiveHoursThreshold;

    // Finestra di tempo minuti, tra una richiesta SMS ADAPTIVE_GDPR e l'altra, per lo stesso utilizzatore
    public static int SMS_ADAPTIVE_GDPR_REQUEST_INTERVAL_MINUTES => Config.Sms.GdprRequestIntervalMinutes;

    // === Thresholds ===

    // Ore corrispondenti a 30 giorni, per calcolo di mese intero, ed ottimizzazione procedure dati sulla base del periodo mensile
    public static int MONTHLY_HOURS_THRESHOLD => Config.Thresholds.MonthlyHours;

    // Valori usati nel controller dei Reports, insieme anche a monthly, per determinare tipo cadenza Report
    public static int DAILY_HOURS_THRESHOLD => Config.Thresholds.DailyHours;
    public static int WEEKLY_HOURS_THRESHOLD => Config.Thresholds.WeeklyHours;

    // === Validazione Probabilistica Gap ===
    // Parametri per GapAnalysisService.cs:
    // Stampa PDF contenente analisi Validazione Probabilistica Gap temporali sul fetch dati.
    // Pesi per il calcolo della confidenza

    // Record esistono prima e dopo?
    public static double GAP_ANALYSIS_WEIGHT_CONTINUITY => Config.GapAnalysis.Weights.Continuity;

    // Progressione batteria coerente?
    public static double GAP_ANALYSIS_WEIGHT_BATTERY => Config.GapAnalysis.Weights.Battery;

    // Ora tipica di utilizzo?
    public static double GAP_ANALYSIS_WEIGHT_PATTERN => Config.GapAnalysis.Weights.Pattern;

    // Gap singolo vs multipli?
    public static double GAP_ANALYSIS_WEIGHT_GAP_LENGTH => Config.GapAnalysis.Weights.GapLength;

    // Pattern storico veicolo
    public static double GAP_ANALYSIS_WEIGHT_HISTORICAL => Config.GapAnalysis.Weights.Historical;

    // Bonus confidenza in percentuale, nel caso si tratti di un problema tecnico documentato
    public static double GAP_ANALYSIS_WEIGHT_TECH_PROBLEM => Config.GapAnalysis.Weights.TechProblemBonus;

    // Bonus confidenza per km percorsi (se odometro aumenta > GAP_ANALYSIS_KM_THRESHOLD tra record adiacenti)
    public static double GAP_ANALYSIS_KM_BONUS => Config.GapAnalysis.Weights.KmBonus;

    // Soglia minima km per considerare il veicolo "in movimento"
    public static double GAP_ANALYSIS_KM_THRESHOLD => Config.GapAnalysis.KmThreshold;

    // Bonus confidenza per gap durante Fleet API outage
    public static double GAP_ANALYSIS_FLEET_OUTAGE_BONUS => Config.GapAnalysis.Weights.FleetApiOutageBonus;

    // Bonus confidenza per gap durante Vehicle outage
    public static double GAP_ANALYSIS_VEHICLE_OUTAGE_BONUS => Config.GapAnalysis.Weights.VehicleOutageBonus;

    // === Gap Analysis Thresholds (Soglie per Alert) ===

    /// <summary>
    /// Confidenza minima accettabile (0-100).
    /// Gap con confidenza inferiore generano alert LOW_CONFIDENCE.
    /// </summary>
    public static double GAP_THRESHOLD_MIN_CONFIDENCE => Config.GapAnalysis.Thresholds.MinConfidencePercent;

    /// <summary>
    /// Percentuale massima di gap sul periodo totale.
    /// </summary>
    public static double GAP_THRESHOLD_MAX_GAP_PERCENT => Config.GapAnalysis.Thresholds.MaxGapPercentOfPeriod;

    /// <summary>
    /// Ore consecutive massime di gap prima di escalation.
    /// </summary>
    public static int GAP_THRESHOLD_MAX_CONSECUTIVE_HOURS => Config.GapAnalysis.Thresholds.MaxConsecutiveGapHours;

    /// <summary>
    /// Percentuale downtime mensile massima.
    /// </summary>
    public static double GAP_THRESHOLD_MAX_MONTHLY_DOWNTIME => Config.GapAnalysis.Thresholds.MaxMonthlyDowntimePercent;

    /// <summary>
    /// Confidenza minima richiesta per gap durante periodo ADAPTIVE_PROFILE attivo.
    /// </summary>
    public static double GAP_THRESHOLD_PROFILED_MIN_CONFIDENCE => Config.GapAnalysis.Thresholds.ProfiledPeriodMinConfidencePercent;

    // === Gap Analysis Adaptive Profile (Bonus/Malus) ===

    /// <summary>
    /// Bonus confidenza (%) se gap durante periodo NON profilato.
    /// </summary>
    public static double GAP_ADAPTIVE_NOT_PROFILED_BONUS => Config.GapAnalysis.AdaptiveProfile.NotProfiledBonusPercent;

    /// <summary>
    /// Malus confidenza (%) se gap durante periodo profilato.
    /// Valore negativo (es. -30).
    /// </summary>
    public static double GAP_ADAPTIVE_PROFILED_MALUS => Config.GapAnalysis.AdaptiveProfile.ProfiledMalusPercent;

    // === Gap Monitoring BackgroundService ===

    /// <summary>
    /// Intervallo tra cicli di monitoraggio (minuti).
    /// Usato sia da BackgroundService che da Alert Gap Dashboard auto-refresh.
    /// </summary>
    public static int GAP_MONITORING_CHECK_INTERVAL_MINUTES => Config.GapAnalysis.Monitoring.CheckIntervalMinutes;

    /// <summary>
    /// Delay iniziale prima del primo ciclo (minuti).
    /// </summary>
    public static int GAP_MONITORING_INITIAL_DELAY_MINUTES => Config.GapAnalysis.Monitoring.InitialDelayMinutes;

    /// <summary>
    /// Giorni indietro da analizzare per ogni ciclo.
    /// </summary>
    public static int GAP_MONITORING_LOOKBACK_DAYS => Config.GapAnalysis.Monitoring.LookbackDays;

    // ===== TSA (Timestamp Authority) Configuration =====
    // Marca temporale RFC 3161 per "verifica probatoria ex post" (interpello AdE)
    // DEV: FreeTSA (gratuito, non qualificato) - PROD: Aruba TSA (qualificato eIDAS)

    /// <summary>
    /// Abilita/disabilita la richiesta di marca temporale sui PDF.
    /// Se disabilitato, i PDF verranno generati senza TSA.
    /// </summary>
    public static bool TSA_ENABLED => Config.Tsa.Enabled;

    /// <summary>
    /// Timeout per la richiesta al server TSA (in secondi).
    /// Default: 30 secondi.
    /// </summary>
    public static int TSA_TIMEOUT_SECONDS => Config.Tsa.TimeoutSeconds;

    /// <summary>
    /// Numero di tentativi in caso di errore TSA.
    /// Default: 3 tentativi.
    /// </summary>
    public static int TSA_RETRY_COUNT => Config.Tsa.RetryCount;

    /// <summary>
    /// URL server TSA (letto da variabile ambiente).
    /// DEV: FreeTSA (https://freetsa.org/tsr)
    /// PROD: Aruba TSA (configurare in .env.prod)
    /// </summary>
    public static string TSA_SERVER_URL =>
        Environment.GetEnvironmentVariable("TSA_SERVER_URL") ?? "https://freetsa.org/tsr";

    /// <summary>
    /// Username Aruba TSA (solo PROD).
    /// Lasciare vuoto per DEV con FreeTSA.
    /// </summary>
    public static string TSA_ARUBA_USERNAME =>
        Environment.GetEnvironmentVariable("TSA_ARUBA_USERNAME") ?? "";

    /// <summary>
    /// Password Aruba TSA (solo PROD).
    /// Lasciare vuoto per DEV con FreeTSA.
    /// </summary>
    public static string TSA_ARUBA_PASSWORD =>
        Environment.GetEnvironmentVariable("TSA_ARUBA_PASSWORD") ?? "";

    /// <summary>
    /// OID Policy per Aruba TSA (solo PROD).
    /// Aruba policy OID: 1.3.6.1.4.1.29741.1.1.1
    /// </summary>
    public static string TSA_ARUBA_POLICY_OID =>
        Environment.GetEnvironmentVariable("TSA_ARUBA_POLICY_OID") ?? "";

    // ===== GDPR Encryption (Crittografia PII) =====
    // Chiave AES-256 per cifratura dati PII in conformita GDPR.
    // Letta da variabile ambiente Gdpr__EncryptionKey.
    // ATTENZIONE: La perdita della chiave rende IRRECUPERABILI tutti i dati PII!

    /// <summary>
    /// Chiave di crittografia GDPR (AES-256, 32 bytes = 64 caratteri hex).
    /// DEV: Chiave fissa condivisibile tra sviluppatori.
    /// PROD: Chiave privata.
    /// </summary>
    public static string GDPR_ENCRYPTION_KEY =>
        Environment.GetEnvironmentVariable("Gdpr__EncryptionKey") ?? "";

    #endregion
}
