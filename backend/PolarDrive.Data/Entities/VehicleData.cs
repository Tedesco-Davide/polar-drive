namespace PolarDrive.Data.Entities;

public class VehicleData
{
    /// <summary>
    /// Indica se questo dato è stato raccolto durante una sessione di Adaptive Profiling
    /// </summary>
    public bool IsAdaptiveProfiling { get; set; } = false;

    public int Id { get; set; }

    public int VehicleId { get; set; }

    public DateTime Timestamp { get; set; }

    public string RawJsonAnonymized { get; set; } = string.Empty;

    public ClientVehicle? ClientVehicle { get; set; }
}
