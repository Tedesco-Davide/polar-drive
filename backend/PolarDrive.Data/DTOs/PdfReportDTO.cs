namespace PolarDrive.Data.DTOs;

public class PdfReportDTO
{
    // Proprietà esistenti
    public int Id { get; set; }
    public string ReportPeriodStart { get; set; } = string.Empty;
    public string ReportPeriodEnd { get; set; } = string.Empty;
    public string? GeneratedAt { get; set; }
    public string CompanyVatNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string VehicleVin { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>
    /// Indica se il file PDF esiste fisicamente su disco
    /// </summary>
    public bool HasPdfFile { get; set; }

    /// <summary>
    /// Indica se il file HTML esiste fisicamente su disco
    /// </summary>
    public bool HasHtmlFile { get; set; }

    /// <summary>
    /// Numero di record di dati del veicolo utilizzati per questo report
    /// </summary>
    public int DataRecordsCount { get; set; }

    /// <summary>
    /// Dimensione del file PDF in bytes (0 se non esiste)
    /// </summary>
    public long PdfFileSize { get; set; }

    /// <summary>
    /// Dimensione del file HTML in bytes (0 se non esiste)
    /// </summary>
    public long HtmlFileSize { get; set; }

    /// <summary>
    /// Indica se il report può essere scaricato (ha almeno un file disponibile)
    /// </summary>
    public bool IsDownloadable => HasPdfFile || HasHtmlFile;

    /// <summary>
    /// Stato del report in formato leggibile
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Durata del monitoraggio in ore (calcolata dal periodo)
    /// </summary>
    public double MonitoringDurationHours { get; set; }

    /// <summary>
    /// Data dell'ultimo aggiornamento/rigenerazione (se diversa da GeneratedAt)
    /// </summary>
    public string? LastModified { get; set; }

    /// <summary>
    /// Indica se questo report è stato rigenerato
    /// </summary>
    public bool IsRegenerated { get; set; }

    /// <summary>
    /// Numero di rigenerazioni effettuate su questo report
    /// </summary>
    public int RegenerationCount { get; set; }

    /// <summary>
    /// Tipo di report (Standard, Regenerated, Custom, etc.)
    /// </summary>
    public string ReportType { get; set; } = "Standard";

    /// <summary>
    /// Lista dei formati file disponibili
    /// </summary>
    public List<string> AvailableFormats { get; set; } = [];

    /// <summary>
    /// Ottiene informazioni di riepilogo per dashboard
    /// </summary>
    public ReportSummary GetSummary()
    {
        return new ReportSummary
        {
            Id = Id,
            VehicleVin = VehicleVin,
            CompanyName = CompanyName,
            Status = Status,
            IsDownloadable = IsDownloadable,
            HasIssues = !HasPdfFile && !HasHtmlFile,
            DataQuality = GetDataQualityScore(),
            GeneratedAt = GeneratedAt,
            LastActivity = LastModified ?? GeneratedAt
        };
    }

    /// <summary>
    /// Calcola un punteggio di qualità dei dati (0-100)
    /// </summary>
    private int GetDataQualityScore()
    {
        var score = 0;

        // Base score per avere dati
        if (DataRecordsCount > 0) score += 40;

        // Bonus per quantità di dati
        if (DataRecordsCount >= 10) score += 20;
        if (DataRecordsCount >= 50) score += 10;
        if (DataRecordsCount >= 100) score += 10;

        // Bonus per durata monitoraggio
        if (MonitoringDurationHours >= 1) score += 10;
        if (MonitoringDurationHours >= 24) score += 10;

        return Math.Min(score, 100);
    }
}

/// <summary>
/// Classe helper per riepiloghi veloci dei report
/// </summary>
public class ReportSummary
{
    public int Id { get; set; }
    public string VehicleVin { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsDownloadable { get; set; }
    public bool HasIssues { get; set; }
    public int DataQuality { get; set; } // 0-100
    public string? GeneratedAt { get; set; }
    public string? LastActivity { get; set; }
}