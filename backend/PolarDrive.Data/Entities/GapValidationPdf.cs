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

    // Navigation property
    public PdfReport? PdfReport { get; set; }
}
