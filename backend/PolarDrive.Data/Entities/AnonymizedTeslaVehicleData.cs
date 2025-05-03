namespace PolarDrive.Data.Entities;

public class AnonymizedTeslaVehicleData
{
    public int Id { get; set; }

    public int OriginalDataId { get; set; }

    public DateTime Timestamp { get; set; }

    public int TeslaVehicleId { get; set; }

    public string AnonymizedJson { get; set; } = string.Empty;

    public TeslaVehicleData? OriginalData { get; set; }

    public ClientTeslaVehicle? ClientTeslaVehicle { get; set; }
}