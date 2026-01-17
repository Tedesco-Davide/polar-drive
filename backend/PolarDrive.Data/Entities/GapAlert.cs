namespace PolarDrive.Data.Entities;

/// <summary>
/// Alert per anomalie Gap rilevate dal GapMonitoringBackgroundService.
/// Generato automaticamente quando le soglie configurate vengono superate.
/// </summary>
public class GapAlert
{
    public int Id { get; set; }

    /// <summary>
    /// Veicolo a cui si riferisce l'alert
    /// </summary>
    public int VehicleId { get; set; }

    /// <summary>
    /// Report PDF correlato (opzionale)
    /// </summary>
    public int? PdfReportId { get; set; }

    /// <summary>
    /// Tipo alert: LOW_CONFIDENCE, CONSECUTIVE_GAPS, PROFILED_ANOMALY, HIGH_GAP_PERCENTAGE, MONTHLY_THRESHOLD
    /// </summary>
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// Severita: INFO, WARNING, CRITICAL
    /// </summary>
    public string Severity { get; set; } = "WARNING";

    /// <summary>
    /// Data/ora rilevamento dell'anomalia
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Descrizione testuale dell'anomalia
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON con metriche dettagliate (confidenza media, ore gap, percentuali, etc.)
    /// </summary>
    public string MetricsJson { get; set; } = string.Empty;

    /// <summary>
    /// Status gestione: OPEN, ESCALATED, COMPLETED, CONTRACT_BREACH
    /// - OPEN: Nuovo, richiede gestione
    /// - ESCALATED: PDF Escalation generato, in attesa decisione finale
    /// - COMPLETED: PDF Certification generato, chiuso (stato finale)
    /// - CONTRACT_BREACH: PDF Contract Breach generato, chiuso (stato finale)
    /// </summary>
    public string Status { get; set; } = "OPEN";

    /// <summary>
    /// Data/ora completamento (quando status diventa COMPLETED o CONTRACT_BREACH)
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Note inserite dall'operatore alla risoluzione
    /// </summary>
    public string? ResolutionNotes { get; set; }

    // Navigation properties
    public ClientVehicle? ClientVehicle { get; set; }
    public PdfReport? PdfReport { get; set; }
}

/// <summary>
/// Costanti per i tipi di alert
/// </summary>
public static class GapAlertTypes
{
    public const string LOW_CONFIDENCE = "LOW_CONFIDENCE";
    public const string CONSECUTIVE_GAPS = "CONSECUTIVE_GAPS";
    public const string PROFILED_ANOMALY = "PROFILED_ANOMALY";
    public const string HIGH_GAP_PERCENTAGE = "HIGH_GAP_PERCENTAGE";
    public const string MONTHLY_THRESHOLD = "MONTHLY_THRESHOLD";
}

/// <summary>
/// Costanti per i livelli di severita
/// </summary>
public static class GapAlertSeverity
{
    public const string INFO = "INFO";
    public const string WARNING = "WARNING";
    public const string CRITICAL = "CRITICAL";
}

/// <summary>
/// Costanti per gli status degli alert
/// </summary>
public static class GapAlertStatus
{
    public const string OPEN = "OPEN";
    public const string PROCESSING = "PROCESSING";
    public const string ESCALATED = "ESCALATED";
    public const string COMPLETED = "COMPLETED";
    public const string CONTRACT_BREACH = "CONTRACT_BREACH";
    public const string ERROR = "ERROR";
}
