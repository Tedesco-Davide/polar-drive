namespace PolarDrive.Data.Entities;

/// <summary>
/// PDF di Validazione Probabilistica Gap per un report.
/// Relazione 1:1 con PdfReport - contiene il PDF generato e i metadati.
/// Una volta generato, è immutabile.
/// </summary>
public class GapValidationPdf
{
    public int Id { get; set; }

    /// <summary>
    /// Report PDF di riferimento (FK)
    /// </summary>
    public int PdfReportId { get; set; }

    /// <summary>
    /// Contenuto binario del PDF di certificazione
    /// </summary>
    public byte[]? PdfContent { get; set; }

    /// <summary>
    /// Hash SHA-256 del PDF per integrità e immutabilità
    /// </summary>
    public string? PdfHash { get; set; }

    /// <summary>
    /// Stato della certificazione: PROCESSING, COMPLETED, ERROR
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Data/ora di generazione del PDF
    /// </summary>
    public DateTime? GeneratedAt { get; set; }

    /// <summary>
    /// Numero di gap certificati nel documento
    /// </summary>
    public int GapsCertified { get; set; }

    /// <summary>
    /// Confidenza media percentuale di tutti i gap
    /// </summary>
    public double AverageConfidence { get; set; }

    /// <summary>
    /// Data creazione record (quando inizia il processing)
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // ===== TSA (Timestamp Authority) Fields =====
    // Marca temporale RFC 3161 per "verifica probatoria ex post" (interpello AdE)

    /// <summary>
    /// RFC 3161 timestamp token (DER-encoded bytes) - Marca temporale.
    /// DEV: FreeTSA (gratuito, non qualificato)
    /// PROD: Aruba TSA (qualificato eIDAS)
    /// </summary>
    public byte[]? TsaTimestamp { get; set; }

    /// <summary>
    /// URL del server TSA utilizzato (FreeTSA o Aruba).
    /// </summary>
    public string? TsaServerUrl { get; set; }

    /// <summary>
    /// Data/ora estratta dal timestamp RFC 3161.
    /// </summary>
    public DateTime? TsaTimestampDate { get; set; }

    /// <summary>
    /// Hash dell'impronta del documento nel token TSA (MessageImprint).
    /// Deve corrispondere a PdfHash.
    /// </summary>
    public string? TsaMessageImprint { get; set; }

    /// <summary>
    /// True se il token TSA è stato ottenuto con successo.
    /// </summary>
    public bool TsaVerified { get; set; } = false;

    /// <summary>
    /// Eventuale errore durante richiesta TSA.
    /// </summary>
    public string? TsaError { get; set; }

    // ===== Document Type Fields =====

    /// <summary>
    /// Tipo documento: CERTIFICATION, ESCALATION, CONTRACT_BREACH
    /// - CERTIFICATION: Certificazione gap (default, esistente)
    /// - ESCALATION: PDF di escalation (stato intermedio)
    /// - CONTRACT_BREACH: PDF contract breach (stato finale grave)
    /// </summary>
    public string DocumentType { get; set; } = GapValidationDocumentTypes.CERTIFICATION;

    /// <summary>
    /// Alert correlato (opzionale) - per tracciare da quale alert e stato generato
    /// </summary>
    public int? GapAlertId { get; set; }

    // Navigation properties
    public PdfReport? PdfReport { get; set; }
    public GapAlert? GapAlert { get; set; }
}

/// <summary>
/// Costanti per i tipi di documento GapValidationPdf
/// </summary>
public static class GapValidationDocumentTypes
{
    public const string CERTIFICATION = "CERTIFICATION";
    public const string ESCALATION = "ESCALATION";
    public const string CONTRACT_BREACH = "CONTRACT_BREACH";
}
