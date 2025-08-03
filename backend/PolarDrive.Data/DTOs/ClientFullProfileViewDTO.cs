namespace PolarDrive.Data.Entities;

/// <summary>
/// DTO per i dati della view vw_ClientFullProfile
/// </summary>
public class ClientFullProfileViewDto
{
    public int CompanyId { get; set; }
    public string VatNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PecAddress { get; set; }
    public string? LandlineNumber { get; set; }
    public string? ReferentName { get; set; }
    public string? ReferentMobileNumber { get; set; }
    public string? ReferentEmail { get; set; }
    public DateTime CompanyCreatedAt { get; set; }
    public int DaysRegistered { get; set; }
    public int TotalVehicles { get; set; }
    public int ActiveVehicles { get; set; }
    public int FetchingVehicles { get; set; }
    public int AuthorizedVehicles { get; set; }
    public int UniqueBrands { get; set; }
    public int TotalConsentsCompany { get; set; }
    public int TotalOutagesCompany { get; set; }
    public int TotalReportsCompany { get; set; }
    public int TotalSmsEventsCompany { get; set; }
    public DateTime? FirstVehicleActivation { get; set; }
    public DateTime? LastReportGeneratedCompany { get; set; }
    public string? LandlineNumbers { get; set; }
    public string? MobileNumbers { get; set; }
    public string? AssociatedPhones { get; set; }

    // Dati del veicolo (nullable perché potrebbero non esserci veicoli)
    public int? VehicleId { get; set; }
    public string? Vin { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? FuelType { get; set; }
    public bool VehicleIsActive { get; set; }
    public bool VehicleIsFetching { get; set; }
    public bool VehicleIsAuthorized { get; set; }
    public DateTime? VehicleFirstActivation { get; set; }
    public DateTime? VehicleLastDeactivation { get; set; }
    public DateTime? VehicleCreatedAt { get; set; }
    public int VehicleConsents { get; set; }
    public int VehicleOutages { get; set; }
    public int VehicleReports { get; set; }
    public int VehicleSmsEvents { get; set; }
    public DateTime? VehicleLastConsent { get; set; }
    public DateTime? VehicleLastOutage { get; set; }
    public DateTime? VehicleLastReport { get; set; }
    public int? DaysSinceFirstActivation { get; set; }
    public int VehicleOutageDays { get; set; }
}