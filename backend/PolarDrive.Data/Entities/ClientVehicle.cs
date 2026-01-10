using System.ComponentModel.DataAnnotations;

namespace PolarDrive.Data.Entities;

public class ClientVehicle
{
    public int Id { get; set; }

    public int ClientCompanyId { get; set; }

    // Campo PII cifrato (AES-256-GCM) - dimensione aumentata per Base64
    [StringLength(100)]
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

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? LastDataUpdate { get; set; }

    // Campi PII cifrati (AES-256-GCM) - dimensione aumentata per Base64
    [StringLength(500)]
    public string? ReferentName { get; set; }

    [StringLength(100)]
    public string VehicleMobileNumber { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ReferentEmail { get; set; }

    // ===== GDPR Hash Fields for Exact Lookup =====
    // Questi campi permettono query WHERE esatte sui dati PII cifrati

    [StringLength(64)]
    public string? VinHash { get; set; }

    [StringLength(64)]
    public string? VehicleMobileNumberHash { get; set; }

    [StringLength(64)]
    public string? ReferentNameHash { get; set; }

    [StringLength(64)]
    public string? ReferentEmailHash { get; set; }

    public ClientCompany? ClientCompany { get; set; }

    public virtual ICollection<VehicleData> VehiclesData { get; set; } = [];

    public virtual ICollection<PdfReport> PdfReports { get; set; } = [];
}