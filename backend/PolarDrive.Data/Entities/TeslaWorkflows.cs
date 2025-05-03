using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PolarDrive.Data.Entities;

public class TeslaWorkflow
{
    [Key, ForeignKey(nameof(ClientTeslaVehicle))]
    public int TeslaVehicleId { get; set; }

    public bool IsActiveFlag { get; set; } = true;

    public bool IsFetchingDataFlag { get; set; } = true;

    public DateTime LastStatusChangeAt { get; set; } = DateTime.UtcNow;
    
    public ClientTeslaVehicle? ClientTeslaVehicle { get; set; }
}