using System.ComponentModel.DataAnnotations;

namespace PolarDrive.Data.Entities;

/// <summary>
/// Entità per mappare numeri di telefono ai veicoli
/// </summary>
public class PhoneVehicleMapping
{
    [Key]
    public int Id { get; set; }

    [Required, Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    public int VehicleId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

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

    [Required]
    public string FromPhoneNumber { get; set; } = string.Empty;

    [Required]
    public string ToPhoneNumber { get; set; } = string.Empty;

    [Required]
    public string MessageBody { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }

    public string ProcessingStatus { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public int? VehicleIdResolved { get; set; }

    public string? ResponseSent { get; set; }

    public ClientVehicle? ResolvedVehicle { get; set; }
}
