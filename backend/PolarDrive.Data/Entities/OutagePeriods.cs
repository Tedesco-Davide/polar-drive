using System.ComponentModel.DataAnnotations;
namespace PolarDrive.Data.Entities;

public class OutagePeriod
{
    public int Id { get; set; }

    public bool AutoDetected { get; set; } = true;

    [Required]
    [RegularExpression("Outage Vehicle|Outage Fleet Api")]
    public string OutageType { get; set; } = string.Empty;

    public string OutageBrand { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime OutageStart { get; set; }

    public DateTime? OutageEnd { get; set; }

    public int? VehicleId { get; set; }

    public int? ClientCompanyId { get; set; }

    public byte[]? ZipContent { get; set; }
    
    public string ZipHash { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public ClientVehicle? ClientVehicle { get; set; }

    public ClientCompany? ClientCompany { get; set; }
}