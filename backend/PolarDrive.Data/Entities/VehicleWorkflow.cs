using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PolarDrive.Data.Entities;

public class VehicleWorkflow
{
    [Key, ForeignKey(nameof(ClientVehicle))]
    public int VehicleId { get; set; }

    public bool IsActiveFlag { get; set; } = true;

    public bool IsFetchingDataFlag { get; set; } = true;

    public DateTime LastStatusChangeAt { get; set; } = DateTime.UtcNow;
    
    public ClientVehicle? ClientVehicle { get; set; }
}