namespace PolarDrive.Data.Entities;

public class VehicleData
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    public DateTime Timestamp { get; set; }

    public string RawJson { get; set; } = string.Empty;

    public ClientVehicle? ClientVehicle { get; set; }
}
