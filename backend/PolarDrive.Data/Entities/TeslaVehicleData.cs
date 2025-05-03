namespace PolarDrive.Data.Entities;

public class TeslaVehicleData
{
    public int Id { get; set; }

    public int TeslaVehicleId { get; set; }

    public DateTime Timestamp { get; set; }

    public string RawJson { get; set; } = string.Empty;

    public ClientTeslaVehicle? ClientTeslaVehicle { get; set; }
}
