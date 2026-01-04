namespace PolarDrive.Data.DTOs;

public class ClientVehicleDTO
{
    public int Id { get; set; }
    public int ClientCompanyId { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Trim { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; }
    public bool IsFetching { get; set; }
    public string? FirstActivationAt { get; set; }
    public string? LastDeactivationAt { get; set; }
    public string? ReferentName { get; set; }
    public string VehicleMobileNumber { get; set; } = string.Empty;
    public string? ReferentEmail { get; set; }
}
