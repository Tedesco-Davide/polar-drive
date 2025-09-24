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

    public int RegenerationCount { get; set; } = 0;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ClientCompany? ClientCompany { get; set; }

    public ClientVehicle? ClientVehicle { get; set; }
}