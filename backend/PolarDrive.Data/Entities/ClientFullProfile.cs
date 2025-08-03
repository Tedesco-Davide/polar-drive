namespace PolarDrive.Data.Entities;

/// <summary>
/// Modello per i dati aggregati del profilo cliente
/// </summary>
public class ClientProfileData
{
    public CompanyProfileInfo CompanyInfo { get; set; } = new();
    public List<VehicleProfileInfo> Vehicles { get; set; } = new();
}

/// <summary>
/// Informazioni del profilo dell'azienda
/// </summary>
public class CompanyProfileInfo
{
    public int Id { get; set; }
    public string VatNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PecAddress { get; set; }
    public string? LandlineNumber { get; set; }
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
}

/// <summary>
/// Informazioni del profilo del veicolo
/// </summary>
public class VehicleProfileInfo
{
    public int Id { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsFetching { get; set; }
    public bool IsAuthorized { get; set; }
    public DateTime? VehicleCreatedAt { get; set; }
    public DateTime? FirstActivationAt { get; set; }
    public DateTime? LastDeactivationAt { get; set; }
    public int TotalConsents { get; set; }
    public int TotalOutages { get; set; }
    public int TotalReports { get; set; }
    public int TotalSmsEvents { get; set; }
    public DateTime? LastConsentDate { get; set; }
    public DateTime? LastOutageStart { get; set; }
    public DateTime? LastReportGenerated { get; set; }
    public int? DaysSinceFirstActivation { get; set; }
    public int VehicleOutageDays { get; set; }
    public string? ReferentName { get; set; }
    public string? ReferentMobileNumber { get; set; }
    public string? ReferentEmail { get; set; }
}