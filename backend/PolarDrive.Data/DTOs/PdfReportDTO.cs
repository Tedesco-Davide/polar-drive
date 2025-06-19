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
    public string? Notes { get; set; }
    public bool HasHtmlFile { get; set; }
    public bool HasPdfFile { get; set; }
    public int DataRecordsCount { get; set; }
    public long HtmlFileSize { get; set; }
    public long PdfFileSize { get; set; }
    public string Status { get; set; } = string.Empty;
    public double MonitoringDurationHours { get; set; }
    public bool IsRegenerated { get; set; }
    public int RegenerationCount { get; set; }
    public string? LastRegenerated { get; set; }
    public string ReportType { get; set; } = string.Empty;
}