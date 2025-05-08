namespace PolarDrive.Data.DTOs;

public class ClientConsentDTO
{
    public int Id { get; set; }
    public int ClientCompanyId { get; set; }
    public int TeslaVehicleId { get; set; }
    public string CompanyVatNumber { get; set; } = string.Empty;
    public string TeslaVehicleVIN { get; set; } = string.Empty;
    public string UploadDate { get; set; } = string.Empty; // Formato: "dd/MM/yyyy"
    public string ZipFilePath { get; set; } = string.Empty;
    public string ConsentHash { get; set; } = string.Empty;
    public string ConsentType { get; set; } = string.Empty; // Validato lato frontend
    public string? Notes { get; set; } // Campo opzionale lato frontend
}