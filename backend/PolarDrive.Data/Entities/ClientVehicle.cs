namespace PolarDrive.Data.Entities;

public class ClientVehicle
{
    public int Id { get; set; }

    public int ClientCompanyId { get; set; }

    public string Vin { get; set; } = string.Empty;

    public string FuelType { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? Trim { get; set; }

    public string? Color { get; set; }

    public bool ClientOAuthAuthorized { get; set; } = false;

    public bool IsActiveFlag { get; set; } = true;

    public bool IsFetchingDataFlag { get; set; } = true;

    public DateTime? FirstActivationAt { get; set; }

    public DateTime? LastDeactivationAt { get; set; }

    public DateTime? LastFetchingDataAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ClientCompany? ClientCompany { get; set; }
}