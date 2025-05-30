using PolarDrive.Data.Entities;

namespace PolarDrive.Data.DbContexts;

public class PolarDriveLogger(PolarDriveDbContext dbContext)
{
    private readonly PolarDriveDbContext _dbContext = dbContext;

    public async Task LogAsync(string source, PolarDriveLogLevel level, string message, string? details = null)
    {
        try
        {
            _dbContext.PolarDriveLogs.Add(new PolarDriveLog
            {
                Timestamp = DateTime.UtcNow,
                Source = source,
                Level = level,
                Message = message,
                Details = details
            });

            await _dbContext.SaveChangesAsync();
        }
        catch (ObjectDisposedException ex)
        {
            Console.WriteLine($"❌ PolarDriveLogger => DbContext already disposed: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FATAL ERROR => IMPOSSIBLE TO SAVE IN PolarDriveLogger: {ex.Message}");
        }
    }

    public Task Info(string source, string message, string? details = null) =>
        LogAsync(source, PolarDriveLogLevel.INFO, message, details);

    public Task Error(string source, string message, string? details = null) =>
        LogAsync(source, PolarDriveLogLevel.ERROR, message, details);

    public Task Warning(string source, string message, string? details = null) =>
       LogAsync(source, PolarDriveLogLevel.WARNING, message, details);

    public Task Debug(string source, string message, string? details = null) =>
       LogAsync(source, PolarDriveLogLevel.DEBUG, message, details);
}