namespace PolarDrive.Data.Entities;

public enum PolarDriveLogLevel
{
    INFO,
    ERROR,
    WARNING,
    DEBUG
}

public class PolarDriveLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = string.Empty;
    public PolarDriveLogLevel Level { get; set; } = PolarDriveLogLevel.INFO;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}
