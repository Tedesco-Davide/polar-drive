using System.Text;
using PolarDrive.Data.Entities;

namespace PolarDrive.Data.DbContexts;

public class PolarDriveLogger
{
    // 🔒 Lock statico per garantire atomicità nella scrittura su file/console
    private static readonly object _fileSync = new();

    public PolarDriveLogger()
    {
    }

    // ===== API con source esplicito =====

    public Task Info(string source, string message, string? details = null)
        => LogAsync(source, PolarDriveLogLevel.INFO, message, details);

    public Task Error(string source, string message, string? details = null)
        => LogAsync(source, PolarDriveLogLevel.ERROR, message, details);

    public Task Warning(string source, string message, string? details = null)
        => LogAsync(source, PolarDriveLogLevel.WARNING, message, details);

    public Task Debug(string source, string message, string? details = null)
        => LogAsync(source, PolarDriveLogLevel.DEBUG, message, details);

    // ===== Implementazione base =====

    public async Task LogAsync(string source, PolarDriveLogLevel level, string message, string? details = null)
    {
        // 🧼 Normalizza per evitare a capo interni che spezzano i log
        var safeMessage = Sanitize(message);
        var safeDetails = Sanitize(details);

        // 📝 Scrive su file/console in modo atomico
        WriteAtomicallyToFile(source, level, safeMessage!, safeDetails);

        await Task.CompletedTask; // per rispettare la firma async, anche se ora non fai altro
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
}