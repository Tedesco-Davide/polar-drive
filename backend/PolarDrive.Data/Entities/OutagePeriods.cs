using System.ComponentModel.DataAnnotations;
namespace PolarDrive.Data.Entities;

public class OutagePeriod
{
    public int Id { get; set; }

    public bool AutoDetected { get; set; } = true;

    [Required]
    [RegularExpression("Outage Vehicle|Outage Fleet Api")]
    public string OutageType { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime OutageStart { get; set; }

    public DateTime? OutageEnd { get; set; }

    public int? TeslaVehicleId { get; set; }

    public int? ClientCompanyId { get; set; }

    public string? ZipFilePath { get; set; }

    public string Notes { get; set; } = string.Empty;

    public ClientTeslaVehicle? ClientTeslaVehicle { get; set; }

    public ClientCompany? ClientCompany { get; set; }
}