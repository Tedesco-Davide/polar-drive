using System;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.Entities;

namespace PolarDrive.Data.DbContexts
{
    /// <summary>
    /// Logger core: scrive su file, console e opzionalmente su DB.
    /// Viene usato internamente da PolarDriveLogger&lt;TCategoryName&gt;.
    /// </summary>
    public class PolarDriveLogger
    {
        private readonly PolarDriveDbContext _dbContext;

        // 🔒 Lock statico per garantire atomicità nella scrittura su file/console
        private static readonly object _fileSync = new();

        public PolarDriveLogger(PolarDriveDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // ==== API "core" (con source esplicito) ====

        public Task Info(string source, string message, string? details = null)
            => LogAsync(source, PolarDriveLogLevel.INFO, message, details);

        public Task Error(string source, string message, string? details = null)
            => LogAsync(source, PolarDriveLogLevel.ERROR, message, details);

        public Task Warning(string source, string message, string? details = null)
            => LogAsync(source, PolarDriveLogLevel.WARNING, message, details);

        public Task Debug(string source, string message, string? details = null)
            => LogAsync(source, PolarDriveLogLevel.DEBUG, message, details);

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
                var baseDir = LoggerPathHelper.ResolveBaseLogsDir();
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

                // Evita di interferire con altre entity tracciate diverse da PolarDriveLog
                if (_dbContext.ChangeTracker.HasChanges() &&
                    !_dbContext.ChangeTracker.Entries().All(e =>
                        e.State != EntityState.Added || e.Entity is PolarDriveLog))
                {
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Wrapper tipizzato: da usare nei servizi/controller al posto di ILogger&lt;T&gt;.
    /// </summary>
    public class PolarDriveLogger<TCategoryName>
    {
        private readonly PolarDriveLogger _inner;
        private readonly string _source;

        public PolarDriveLogger(PolarDriveLogger inner)
        {
            _inner = inner;
            _source = typeof(TCategoryName).FullName ?? typeof(TCategoryName).Name;
        }

        // ===== Metodi "fire-and-forget" (come ILogger, non devi mettere await) =====

        public void Debug(string message, string? details = null)
            => _ = _inner.Debug(_source, message, details);

        public void Debug(string messageTemplate, params object[] args)
            => _ = _inner.Debug(_source, FormatWithArgs(messageTemplate, args));

        public void Info(string message, string? details = null)
            => _ = _inner.Info(_source, message, details);

        public void Info(string messageTemplate, params object[] args)
            => _ = _inner.Info(_source, FormatWithArgs(messageTemplate, args));

        public void Warning(string message, string? details = null)
            => _ = _inner.Warning(_source, message, details);

        public void Warning(string messageTemplate, params object[] args)
            => _ = _inner.Warning(_source, FormatWithArgs(messageTemplate, args));

        public void Error(string message, string? details = null)
            => _ = _inner.Error(_source, message, details);

        public void Error(string messageTemplate, params object[] args)
            => _ = _inner.Error(_source, FormatWithArgs(messageTemplate, args));

        public void Error(Exception ex, string message, string? details = null)
            => _ = _inner.Error(_source, message, BuildDetails(details, ex));

        public void Error(Exception ex, string messageTemplate, params object[] args)
            => _ = _inner.Error(
                _source,
                FormatWithArgs(messageTemplate, args),
                ex.ToString()
            );

        // ===== Versioni async opzionali, se vuoi proprio awaitare il log =====

        public Task DebugAsync(string message, string? details = null)
            => _inner.Debug(_source, message, details);

        public Task InfoAsync(string message, string? details = null)
            => _inner.Info(_source, message, details);

        public Task WarningAsync(string message, string? details = null)
            => _inner.Warning(_source, message, details);

        public Task ErrorAsync(string message, string? details = null)
            => _inner.Error(_source, message, details);

        public Task ErrorAsync(Exception ex, string message, string? details = null)
            => _inner.Error(_source, message, BuildDetails(details, ex));

        // ===== Helpers =====

        private static string FormatWithArgs(string template, params object[] args)
        {
            if (args == null || args.Length == 0)
                return template;

            var sb = new StringBuilder();
            sb.Append(template);
            sb.Append(" | Args: ");
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(args[i]?.ToString());
            }
            return sb.ToString();
        }

        private static string BuildDetails(string? details, Exception ex)
        {
            if (string.IsNullOrWhiteSpace(details))
                return ex.ToString();

            return details + " | Exception: " + ex;
        }
    }
}
