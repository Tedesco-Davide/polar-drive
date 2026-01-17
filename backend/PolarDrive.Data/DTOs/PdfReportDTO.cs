namespace PolarDrive.Data.DTOs;

public class PdfReportDTO
{
    public int Id { get; set; }
    public string ReportPeriodStart { get; set; } = string.Empty;
    public string ReportPeriodEnd { get; set; } = string.Empty;
    public string? GeneratedAt { get; set; }
    public string CompanyVatNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string VehicleVin { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public string VehicleBrand { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool HasHtmlFile { get; set; }
    public bool HasPdfFile { get; set; }
    public int DataRecordsCount { get; set; }
    public long HtmlFileSize { get; set; }
    public long PdfFileSize { get; set; }
    public string Status { get; set; } = string.Empty;
    public double MonitoringDurationHours { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string PdfHash { get; set; } = string.Empty;

    // Gap Validation info
    public string? GapValidationStatus { get; set; }
    public string? GapValidationPdfHash { get; set; }
    public bool HasGapValidationPdf { get; set; }
    /// <summary>
    /// True se esiste un PDF di escalation precedente (DocumentType = ESCALATION).
    /// Usato per mostrare 2 bottoni download quando status è COMPLETED o CONTRACT_BREACH.
    /// </summary>
    public bool HadEscalation { get; set; }
}