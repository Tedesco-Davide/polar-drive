namespace PolarDrive.Data.Entities;

public class AnonymizedVehicleData
{
    public int Id { get; set; }

    public int OriginalDataId { get; set; }

    public DateTime Timestamp { get; set; }

    public int VehicleId { get; set; }

    public string AnonymizedJson { get; set; } = string.Empty;

    public VehicleData? OriginalData { get; set; }

    public ClientVehicle? ClientVehicle { get; set; }
}