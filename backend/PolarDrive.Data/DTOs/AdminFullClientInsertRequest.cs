using Microsoft.AspNetCore.Http;
namespace PolarDrive.Data.DTOs;

public class AdminFullClientInsertRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyVatNumber { get; set; } = string.Empty;
    public string ReferentName { get; set; } = string.Empty;
    public string ReferentEmail { get; set; } = string.Empty;
    public string VehicleMobileNumber { get; set; } = string.Empty;
    public string VehicleVIN { get; set; } = string.Empty;
    public string VehicleFuelType { get; set; } = string.Empty;
    public string VehicleBrand { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public string? VehicleTrim { get; set; } = "";
    public string? VehicleColor { get; set; } = "";
    public DateTime UploadDate { get; set; }
    public IFormFile ConsentZip { get; set; } = null!;
}