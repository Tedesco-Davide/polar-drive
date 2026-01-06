namespace PolarDrive.Data.DTOs;

public class ClientConsentDTO
{
    public int Id { get; set; }
    public int ClientCompanyId { get; set; }
    public int VehicleId { get; set; }
    public string CompanyVatNumber { get; set; } = string.Empty;
    public string VehicleVIN { get; set; } = string.Empty;
    public string UploadDate { get; set; } = string.Empty;
    public long ZipFileSize { get; set; } 
    public string ConsentHash { get; set; } = string.Empty;
    public string ConsentType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool HasZipFile { get; set; }
}