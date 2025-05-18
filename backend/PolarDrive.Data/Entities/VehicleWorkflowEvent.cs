using System.ComponentModel.DataAnnotations;
namespace PolarDrive.Data.Entities;

public class VehicleWorkflowEvent
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    [Required]
    [RegularExpression("IsActiveFlag|FetchDataFlag")]
    public string FieldChanged { get; set; } = string.Empty;

    public bool OldValue { get; set; }

    public bool NewValue { get; set; }

    public DateTime EventTimestamp { get; set; } = DateTime.UtcNow;
    
    public ClientVehicle? ClientVehicle { get; set; }
}