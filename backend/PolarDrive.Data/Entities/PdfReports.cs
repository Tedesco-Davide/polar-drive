namespace PolarDrive.Data.Entities;

public class PdfReport
{
    public int Id { get; set; }

    public int ClientCompanyId { get; set; }

    public int VehicleId { get; set; }

    public DateTime ReportPeriodStart { get; set; }

    public DateTime ReportPeriodEnd { get; set; }

    public DateTime? GeneratedAt { get; set; }

    public string Notes { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string? PdfHash { get; set; } = string.Empty;

    public byte[]? PdfContent { get; set; }

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

    public ClientCompany? ClientCompany { get; set; }

    public ClientVehicle? ClientVehicle { get; set; }
}

/// <summary>
/// Classi di supporto
/// </summary>
public class ReportPeriodInfo
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int DataHours { get; set; }
    public string AnalysisLevel { get; set; } = string.Empty;
    public double MonitoringDays { get; set; }
}