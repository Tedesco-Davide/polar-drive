using Microsoft.AspNetCore.Http;

public class AdminFullClientInsertRequest
{
    // ClientCompany
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyVatNumber { get; set; } = string.Empty;
    public string ReferentName { get; set; } = string.Empty;
    public string ReferentEmail { get; set; } = string.Empty;
    public string ReferentMobile { get; set; } = string.Empty;

    // TeslaVehicle (minimi indispensabili)
    public string TeslaVIN { get; set; } = string.Empty;
    public string TeslaModel { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }

    // Tokens
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;

    // ZIP consenso
    public IFormFile ConsentZip { get; set; } = null!;
}
