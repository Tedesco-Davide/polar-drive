using System.ComponentModel.DataAnnotations;

namespace PolarDrive.Data.Entities;

public class SmsAdaptiveProfiling
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    [Required]
    [StringLength(20)]
    public string AdaptiveNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string AdaptiveSurnameName { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public string MessageContent { get; set; } = string.Empty;

    [Required]
    [RegularExpression("ADAPTIVE_PROFILING_ON|ADAPTIVE_PROFILING_OFF")]
    public string ParsedCommand { get; set; } = string.Empty;

    [Required]
    public bool ConsentAccepted { get; set; } = false;

    // Foreign Key verso SmsAdaptiveGdpr
    [Required]
    public int SmsAdaptiveGdprId { get; set; }

    // Navigation properties
    public ClientVehicle? ClientVehicle { get; set; }
    public SmsAdaptiveGdpr? SmsAdaptiveGdpr { get; set; }
}