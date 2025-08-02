namespace PolarDrive.Data.DTOs;

public class AdminWorkflowExtendedDTO
{
    public int Id { get; set; }
    public string Vin { get; set; } = "";
    public string FuelType { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string Trim { get; set; } = "";
    public string Color { get; set; } = "";
    public bool ClientOAuthAuthorized { get; set; }
    public bool IsActive { get; set; }
    public bool IsFetching { get; set; }
    public string? FirstActivationAt { get; set; }
    public string? LastDeactivationAt { get; set; }
    public string? LastFetchingDataAt { get; set; }
    public string? ReferentName { get; set; }
    public string? ReferentMobileNumber { get; set; }
    public string? ReferentEmail { get; set; }
    public string? ReferentPecAddress { get; set; }
    public ClientCompanyDTO ClientCompany { get; set; } = new();
}