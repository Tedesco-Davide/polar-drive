using System.ComponentModel.DataAnnotations;
namespace PolarDrive.Data.Entities;

public class ClientConsent
{
    public int Id { get; set; }

    public int ClientCompanyId { get; set; }

    public int VehicleId { get; set; }

    public DateTime UploadDate { get; set; }

    public byte[]? ZipContent { get; set; }

    public string ConsentHash { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"Consent Activation|Consent Deactivation|Consent Stop Data Fetching|Consent Reactivation")]
    public string ConsentType { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public ClientCompany? ClientCompany { get; set; }
    
    public ClientVehicle? ClientVehicle { get; set; }
}