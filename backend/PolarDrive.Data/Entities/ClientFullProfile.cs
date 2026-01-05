namespace PolarDrive.Data.Entities;

/// <summary>
/// Modello per i dati aggregati del profilo cliente
/// </summary>
public class ClientProfileData
{
    public CompanyProfileInfo CompanyInfo { get; set; } = new();
    public List<VehicleProfileInfo> Vehicles { get; set; } = [];

    // ✅ NUOVO: Dati ADAPTIVE_GDPR
    public List<AdaptiveGdprConsentDto> AdaptiveGdprConsents { get; set; } = [];

    // ✅ NUOVO: Dati ADAPTIVE_PROFILE aggregati
    public List<AdaptiveProfileUserDto> AdaptiveProfileUsers { get; set; } = [];

    // ✅ NUOVO: Dettagli adaptive per veicolo
    public Dictionary<int, VehicleAdaptiveProfileDto> VehicleAdaptiveProfiles { get; set; } = [];

    // ✅ NUOVO: Dati OUTAGES
    public OutagesSummaryDto OutagesSummary { get; set; } = new();
    public List<OutageDetailDto> Outages { get; set; } = [];
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
    
    // ✅ Statistiche SMS aggregate a livello aziendale
    public int TotalSmsEventsCompany { get; set; }
    public int AdaptiveOnEventsCompany { get; set; }
    public int AdaptiveOffEventsCompany { get; set; }
    public int ActiveSessionsCompany { get; set; }
    public DateTime? LastSmsReceivedCompany { get; set; }
    public DateTime? LastActiveSessionExpiresCompany { get; set; }
    public DateTime? FirstVehicleActivation { get; set; }
    public DateTime? LastReportGeneratedCompany { get; set; }
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
    
    // ✅ Statistiche SMS dettagliate per veicolo
    public int TotalSmsEvents { get; set; }
    public int AdaptiveOnEvents { get; set; }
    public int AdaptiveOffEvents { get; set; }
    public int ActiveSessions { get; set; }
    public DateTime? LastSmsReceived { get; set; }
    public DateTime? ActiveSessionExpires { get; set; }
    public DateTime? LastConsentDate { get; set; }
    public DateTime? LastOutageStart { get; set; }
    public DateTime? LastReportGenerated { get; set; }
    public int? DaysSinceFirstActivation { get; set; }
    public int VehicleOutageDays { get; set; }
    public string? ReferentName { get; set; }
    public string? VehicleMobileNumber { get; set; }
    public string? ReferentEmail { get; set; }
}

/// <summary>
/// DTO per consensi ADAPTIVE_GDPR
/// </summary>
public class AdaptiveGdprConsentDto
{
    public int Id { get; set; }
    public string AdaptiveNumber { get; set; } = string.Empty;
    public string AdaptiveSurnameName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ConsentGivenAt { get; set; }
    public bool ConsentAccepted { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// DTO per utilizzatori ADAPTIVE_PROFILE aggregati
/// </summary>
public class AdaptiveProfileUserDto
{
    public string AdaptiveNumber { get; set; } = string.Empty;
    public string AdaptiveSurnameName { get; set; } = string.Empty;
    public DateTime FirstActivation { get; set; }
    public DateTime LastActivation { get; set; }
    public DateTime LastExpiry { get; set; }
    public int TotalSessions { get; set; }
    public bool HasRevokedConsent { get; set; }
    public List<int> VehicleIds { get; set; } = new();
    public List<string> VehicleVins { get; set; } = new();
}

/// <summary>
/// DTO per statistiche adaptive per veicolo
/// </summary>
public class VehicleAdaptiveProfileDto
{
    public int VehicleId { get; set; }
    public string Vin { get; set; } = string.Empty;
    public List<AdaptiveProfileUserDto> Users { get; set; } = [];
    public int TotalSessionsCount { get; set; }
    public int CertifiedRecordsCount { get; set; }
}

/// <summary>
/// DTO per dettagli outage
/// </summary>
public class OutageDetailDto
{
    public int Id { get; set; }
    public string OutageType { get; set; } = string.Empty; // "Outage Vehicle" or "Outage Fleet Api"
    public string OutageBrand { get; set; } = string.Empty;
    public DateTime OutageStart { get; set; }
    public DateTime? OutageEnd { get; set; }
    public bool IsOngoing => OutageEnd == null;
    public string Status => OutageEnd.HasValue ? "Risolto" : "In corso";
    public int DurationDays { get; set; }
    public int DurationHours { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool AutoDetected { get; set; }

    // Per outages specifici di veicolo
    public int? VehicleId { get; set; }
    public string? Vin { get; set; }
    public string? VehicleModel { get; set; }

    // Per outages brand-level
    public List<string> AffectedVehicleVins { get; set; } = [];
    public int AffectedVehicleCount { get; set; }
}

/// <summary>
/// DTO per statistiche aggregate outages
/// </summary>
public class OutagesSummaryDto
{
    public int TotalOutages { get; set; }
    public int OngoingOutages { get; set; }
    public int ResolvedOutages { get; set; }
    public int TotalDowntimeDays { get; set; }
    public int BrandLevelOutages { get; set; }
    public int VehicleSpecificOutages { get; set; }
    public Dictionary<string, int> OutagesByBrand { get; set; } = [];
    public DateTime? FirstOutageDate { get; set; }
    public DateTime? LastOutageDate { get; set; }
    public double AverageOutageDurationDays { get; set; }
    public int TotalVehiclesAffected { get; set; }
}