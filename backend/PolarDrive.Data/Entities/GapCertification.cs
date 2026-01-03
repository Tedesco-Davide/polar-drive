namespace PolarDrive.Data.Entities;

/// <summary>
/// Certificazione probabilistica per i "Record da validare" (gap temporali)
/// Quando un record orario manca nel database, questa entità traccia la certificazione
/// basata sull'analisi statistica dei dati circostanti
/// </summary>
public class GapCertification
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    /// <summary>
    /// Report PDF originale che contiene i gap certificati
    /// </summary>
    public int? PdfReportId { get; set; }

    /// <summary>
    /// Timestamp esatto del gap (ora mancante)
    /// </summary>
    public DateTime GapTimestamp { get; set; }

    /// <summary>
    /// Percentuale di confidenza calcolata (0-100)
    /// </summary>
    public double ConfidencePercentage { get; set; }

    /// <summary>
    /// Testo descrittivo della giustificazione
    /// </summary>
    public string JustificationText { get; set; } = string.Empty;

    /// <summary>
    /// JSON con i fattori di analisi utilizzati per il calcolo
    /// </summary>
    public string AnalysisFactorsJson { get; set; } = string.Empty;

    /// <summary>
    /// Data/ora in cui è stata generata la certificazione
    /// </summary>
    public DateTime CertifiedAt { get; set; }

    /// <summary>
    /// Hash SHA-256 della certificazione per integrità
    /// </summary>
    public string CertificationHash { get; set; } = string.Empty;

    public ClientVehicle? ClientVehicle { get; set; }

    public PdfReport? PdfReport { get; set; }
}

/// <summary>
/// Risultato dell'analisi di un singolo gap
/// </summary>
public class GapAnalysisResult
{
    public DateTime GapTimestamp { get; set; }
    public double ConfidencePercentage { get; set; }
    public string Justification { get; set; } = string.Empty;
    public GapAnalysisFactors Factors { get; set; } = new();
}

/// <summary>
/// Fattori utilizzati per calcolare la confidenza di un gap
/// </summary>
public class GapAnalysisFactors
{
    /// <summary>
    /// Esiste un record all'ora precedente?
    /// </summary>
    public bool HasPreviousRecord { get; set; }

    /// <summary>
    /// Esiste un record all'ora successiva?
    /// </summary>
    public bool HasNextRecord { get; set; }

    /// <summary>
    /// Variazione batteria rispetto al record precedente
    /// </summary>
    public double? BatteryDeltaPrevious { get; set; }

    /// <summary>
    /// Variazione batteria rispetto al record successivo
    /// </summary>
    public double? BatteryDeltaNext { get; set; }

    /// <summary>
    /// L'ora del gap rientra nelle ore tipiche di utilizzo del veicolo?
    /// </summary>
    public bool IsWithinTypicalUsageHours { get; set; }

    /// <summary>
    /// Numero di gap consecutivi (1 = gap singolo)
    /// </summary>
    public int ConsecutiveGapHours { get; set; }

    /// <summary>
    /// Se disponibile, motivo del fallimento del fetch (da FetchFailureLog)
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Il gap è dovuto a un problema tecnico documentato?
    /// </summary>
    public bool IsTechnicalFailure { get; set; }

    /// <summary>
    /// Variazione km tra record adiacenti (se disponibile)
    /// </summary>
    public double? OdometerDelta { get; set; }
}
