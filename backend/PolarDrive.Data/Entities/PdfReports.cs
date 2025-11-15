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