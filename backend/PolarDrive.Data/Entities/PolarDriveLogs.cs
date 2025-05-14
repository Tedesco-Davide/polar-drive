namespace PolarDrive.Data.Entities;

public class PolarDriveLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = string.Empty;
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}
