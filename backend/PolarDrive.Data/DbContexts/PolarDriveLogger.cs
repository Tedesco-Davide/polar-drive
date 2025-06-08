using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.Entities;

namespace PolarDrive.Data.DbContexts;

public class PolarDriveLogger(PolarDriveDbContext dbContext)
{
    private readonly PolarDriveDbContext _dbContext = dbContext;

    // ✅ TIMEZONE ITALIANO
    private static readonly TimeZoneInfo ItalianTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");

    private static DateTime GetItalianTime()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ItalianTimeZone);
    }

    public async Task LogAsync(string source, PolarDriveLogLevel level, string message, string? details = null)
    {
        WriteToFile(source, level, message, details);

        try
        {
            if (!CanSafelyUseDb())
            {
                return;
            }

            _dbContext.PolarDriveLogs.Add(new PolarDriveLog
            {
                Timestamp = GetItalianTime(), // ✅ USA ORARIO ITALIANO
                Source = source,
                Level = level,
                Message = message,
                Details = details
            });

            await _dbContext.SaveChangesAsync();
        }
        catch (ObjectDisposedException ex)
        {
            Console.WriteLine($"❌ PolarDriveLogger → DbContext already disposed: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FATAL ERROR → IMPOSSIBLE TO SAVE IN PolarDriveLogger: {ex.Message}\nDetails: {ex.InnerException?.Message}");
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

    private static void WriteToFile(string source, PolarDriveLogLevel level, string message, string? details = null)
    {
        try
        {
            var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(logDirectory))
                Directory.CreateDirectory(logDirectory);

            var italianTime = GetItalianTime(); // ✅ USA ORARIO ITALIANO
            var logFilePath = Path.Combine(logDirectory, $"log_{italianTime:yyyyMMdd}.txt");
            var logEntry = $"[{italianTime:yyyy-MM-dd HH:mm:ss}] [{level}] [{source}] {message}";

            if (!string.IsNullOrWhiteSpace(details))
                logEntry += $" | Details: {details}";

            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR writing log to file: {ex.Message}");
        }
    }

    private bool CanSafelyUseDb()
    {
        try
        {
            // 1. Verifica che il modello sia costruito
            var entityTypes = _dbContext.Model?.GetEntityTypes();
            if (entityTypes == null || !entityTypes.Any())
                return false;

            // 2. Verifica che il DB sia accessibile
            if (!_dbContext.Database.CanConnect())
                return false;

            // 3. Verifica che non ci siano entità non-log da salvare ancora in tracking
            if (_dbContext.ChangeTracker.HasChanges() &&
                !_dbContext.ChangeTracker.Entries().All(e =>
                    e.State != EntityState.Added || e.Entity is PolarDriveLog))
            {
                return false; // evitiamo conflitto col contesto attuale
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}