namespace PolarDrive.Data.Entities;

public class VehicleDataArchive
{
    public int Id { get; set; }
    public bool IsSmsAdaptiveProfile { get; set; } = false;
    public int VehicleId { get; set; }
    public DateTime Timestamp { get; set; }
    public string RawJsonAnonymized { get; set; } = string.Empty;
}