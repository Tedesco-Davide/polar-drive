namespace PolarDrive.Data.Entities;

public class PdfReport
{
    public int Id { get; set; }

    public DateTime ReportPeriodStart { get; set; }

    public DateTime ReportPeriodEnd { get; set; }

    public string PdfFilePath { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public string CompanyVatNumber { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string VehicleVin { get; set; } = string.Empty;

    public string VehicleDisplayName { get; set; } = string.Empty;
    
    public string Notes { get; set; } = string.Empty;
}