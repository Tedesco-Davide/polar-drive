using System.ComponentModel.DataAnnotations;

namespace PolarDrive.Data.Entities;

public class SmsAdaptiveProfile
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    // Campi PII cifrati (AES-256-GCM) - dimensione aumentata per Base64
    [Required]
    [StringLength(100)]
    public string AdaptiveNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(350)]
    public string AdaptiveSurnameName { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public string MessageContent { get; set; } = string.Empty;

    [Required]
    [RegularExpression("ADAPTIVE_PROFILE_ON|ADAPTIVE_PROFILE_OFF")]
    public string ParsedCommand { get; set; } = string.Empty;

    [Required]
    public bool ConsentAccepted { get; set; } = false;

    // Foreign Key verso SmsAdaptiveGdpr
    [Required]
    public int SmsAdaptiveGdprId { get; set; }

    // Navigation properties
    public ClientVehicle? ClientVehicle { get; set; }
    public SmsAdaptiveGdpr? SmsAdaptiveGdpr { get; set; }

    // ===== GDPR Hash Fields for Exact Lookup =====
    // Questi campi permettono query WHERE esatte sui dati PII cifrati

    [StringLength(64)]
    public string? AdaptiveNumberHash { get; set; }

    [StringLength(64)]
    public string? AdaptiveSurnameNameHash { get; set; }
}