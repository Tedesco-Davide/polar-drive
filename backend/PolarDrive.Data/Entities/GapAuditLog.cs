namespace PolarDrive.Data.Entities;

/// <summary>
/// Audit trail per tutte le azioni eseguite sui Gap.
/// Traccia creazione alert, certificazioni, escalation e contract breach.
/// </summary>
public class GapAuditLog
{
    public int Id { get; set; }

    /// <summary>
    /// Alert correlato (opzionale)
    /// </summary>
    public int? GapAlertId { get; set; }

    /// <summary>
    /// Validazione correlata (opzionale)
    /// </summary>
    public int? GapValidationId { get; set; }

    /// <summary>
    /// Veicolo a cui si riferisce l'azione
    /// </summary>
    public int VehicleId { get; set; }

    /// <summary>
    /// Data/ora dell'azione
    /// </summary>
    public DateTime ActionAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tipo azione: ALERT_CREATED, CERTIFIED, ESCALATED, CONTRACT_BREACH, AUTO_DETECTED
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Operatore che ha eseguito l'azione (null se automatica)
    /// </summary>
    public string? ActionBy { get; set; }

    /// <summary>
    /// Note inserite dall'operatore
    /// </summary>
    public string? ActionNotes { get; set; }

    /// <summary>
    /// Esito verifica: VALID, INVALID, NEEDS_REVIEW
    /// </summary>
    public string? VerificationOutcome { get; set; }

    /// <summary>
    /// Decisione finale: ACCEPTED, REJECTED, ESCALATED
    /// </summary>
    public string? FinalDecision { get; set; }

    // Navigation properties
    public GapAlert? GapAlert { get; set; }
    public GapValidation? GapValidation { get; set; }
    public ClientVehicle? ClientVehicle { get; set; }
}

/// <summary>
/// Costanti per i tipi di azione nell'audit log
/// </summary>
public static class GapAuditActionTypes
{
    public const string ALERT_CREATED = "ALERT_CREATED";
    public const string CERTIFIED = "CERTIFIED";
    public const string ESCALATED = "ESCALATED";
    public const string CONTRACT_BREACH = "CONTRACT_BREACH";
    public const string AUTO_DETECTED = "AUTO_DETECTED";
}
