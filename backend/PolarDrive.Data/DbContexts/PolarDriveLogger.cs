using System.Text;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.Entities;

namespace PolarDrive.Data.DbContexts;

public class PolarDriveLogger(PolarDriveDbContext dbContext)
{
    private readonly PolarDriveDbContext _dbContext = dbContext;

    // 🔒 Lock statico per garantire atomicità nella scrittura su file/console
    private static readonly object _fileSync = new();

    public Task Info   (string source, string message, string? details = null) => LogAsync(source, PolarDriveLogLevel.INFO,    message, details);
    public Task Error  (string source, string message, string? details = null) => LogAsync(source, PolarDriveLogLevel.ERROR,   message, details);
    public Task Warning(string source, string message, string? details = null) => LogAsync(source, PolarDriveLogLevel.WARNING, message, details);
    public Task Debug  (string source, string message, string? details = null) => LogAsync(source, PolarDriveLogLevel.DEBUG,   message, details);

    public async Task LogAsync(string source, PolarDriveLogLevel level, string message, string? details = null)
    {
        // 🧼 Normalizza per evitare a capo interni che spezzano i log
        var safeMessage = Sanitize(message);
        var safeDetails = Sanitize(details);

        // 📝 Scrive su file/console in modo atomico
        WriteAtomicallyToFile(source, level, safeMessage!, safeDetails);

        // 💾 Prova a salvare su DB (fuori dal lock)
        try
        {
            if (!CanSafelyUseDb()) return;

            _dbContext.PolarDriveLogs.Add(new PolarDriveLog
            {
                Timestamp = LoggerPathHelper.NowItalian(),
                Source = source,
                Level = level,
                Message = safeMessage!,
                Details = safeDetails
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

    private static string? Sanitize(string? value)
        => string.IsNullOrEmpty(value)
            ? value
            : value.Replace("\r", " ").Replace("\n", " ");

    private static void WriteAtomicallyToFile(string source, PolarDriveLogLevel level, string message, string? details)
    {
        try
        {
            var baseDir      = LoggerPathHelper.ResolveBaseLogsDir();
            var logDirectory = Path.Combine(baseDir, "General"); // cartella generale
            Directory.CreateDirectory(logDirectory);

            var ts = LoggerPathHelper.NowItalian();
            var logFilePath = Path.Combine(logDirectory, $"log_{ts:yyyyMMdd}.txt");

            // Costruisci l'intera riga UNA SOLA VOLTA
            var line = $"[{ts:yyyy-MM-dd HH:mm:ss}] [{level}] [{source}] {message}";
            if (!string.IsNullOrWhiteSpace(details))
                line += $" | Details: {details}";

            // 🔒 Scrittura atomica protetta da lock: una sola WriteLine
            lock (_fileSync)
            {
                // Scrive su console (opzionale ma utile in dev)
                Console.WriteLine(line);

                // Scrive su file in append con UTF-8, condiviso per lettura
                using var fs = new FileStream(
                    logFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite
                );
                using var sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                sw.WriteLine(line);
                sw.Flush();
                fs.Flush(true); // forza flush su disco per evitare perdite in crash
            }
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
            var entityTypes = _dbContext.Model?.GetEntityTypes();
            if (entityTypes == null || !entityTypes.Any()) return false;
            if (!_dbContext.Database.CanConnect()) return false;

            if (_dbContext.ChangeTracker.HasChanges() &&
                !_dbContext.ChangeTracker.Entries().All(e =>
                    e.State != EntityState.Added || e.Entity is PolarDriveLog))
            {
                return false;
            }
            return true;
        }
        catch { return false; }
    }
}
