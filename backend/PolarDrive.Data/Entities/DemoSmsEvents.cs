using System.ComponentModel.DataAnnotations;
namespace PolarDrive.Data.Entities;

public class DemoSmsEvent
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    public DateTime ReceivedAt { get; set; }

    public string MessageContent { get; set; } = string.Empty;

    [Required]
    [RegularExpression("DEMO_ON|DEMO_OFF")]
    public string ParsedCommand { get; set; } = string.Empty;
    
    public ClientVehicle? ClientVehicle { get; set; }
}