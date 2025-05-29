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
}