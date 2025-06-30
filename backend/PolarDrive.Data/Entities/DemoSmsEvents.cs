using System.ComponentModel.DataAnnotations;
namespace PolarDrive.Data.Entities;

public class AdaptiveProfilingSmsEvent
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    public DateTime ReceivedAt { get; set; }

    public string MessageContent { get; set; } = string.Empty;

    [Required]
    [RegularExpression("ADAPTIVE_PROFILING_ON|ADAPTIVE_PROFILING_OFF")]
    public string ParsedCommand { get; set; } = string.Empty;

    public ClientVehicle? ClientVehicle { get; set; }
}