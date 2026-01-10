using System.ComponentModel.DataAnnotations;

namespace PolarDrive.Data.Entities;

/// <summary>
/// Entità per mappare numeri di telefono ai veicoli
/// </summary>
public class PhoneVehicleMapping
{
    [Key]
    public int Id { get; set; }

    // Campo PII cifrato (AES-256-GCM) - dimensione aumentata per Base64
    [Required, Phone]
    [StringLength(100)]
    public string PhoneNumber { get; set; } = string.Empty;

    public int VehicleId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    // ===== GDPR Hash Field for Exact Lookup =====
    [StringLength(64)]
    public string? PhoneNumberHash { get; set; }

    // Navigation property
    public ClientVehicle? ClientVehicle { get; set; }
}

/// <summary>
/// Log di tutti gli SMS ricevuti per audit e debugging
/// </summary>
public class SmsAuditLog
{
    public int Id { get; set; }

    [Required]
    public string MessageSid { get; set; } = string.Empty;

    // Campi PII cifrati (AES-256-GCM) - dimensione aumentata per Base64
    [Required]
    [StringLength(100)]
    public string FromPhoneNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ToPhoneNumber { get; set; } = string.Empty;

    [Required]
    public string MessageBody { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }

    public string ProcessingStatus { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public int? VehicleIdResolved { get; set; }

    public string? ResponseSent { get; set; }

    // ===== GDPR Hash Fields for Exact Lookup =====
    [StringLength(64)]
    public string? FromPhoneNumberHash { get; set; }

    [StringLength(64)]
    public string? ToPhoneNumberHash { get; set; }

    public ClientVehicle? ResolvedVehicle { get; set; }
}
