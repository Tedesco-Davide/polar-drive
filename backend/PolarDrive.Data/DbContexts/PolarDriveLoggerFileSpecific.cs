namespace PolarDrive.Data.DbContexts;

public class PolarDriveLoggerFileSpecific
{
    private readonly string _componentName;
    private readonly string _logDirectory;
    private static readonly object _fileSync = new(); // 🔒 lock statico per thread safety cross-instance

    public PolarDriveLoggerFileSpecific(string componentName = "General")
    {
        _componentName = componentName;
        var baseLogDir = LoggerPathHelper.ResolveBaseLogsDir();
        _logDirectory = Path.Combine(baseLogDir, componentName);
        Directory.CreateDirectory(_logDirectory);
    }

    public void Info (string message, string? details = null) => Write("INFO",  message, details);
    public void Warn (string message, string? details = null) => Write("WARN",  message, details);
    public void Error(string message, string? details = null) => Write("ERROR", message, details);
    public void Debug(string message, string? details = null) => Write("DEBUG", message, details);

    private static string? Sanitize(string? value)
        => string.IsNullOrEmpty(value)
            ? value
            : value.Replace("\r", " ").Replace("\n", " ");

    private void Write(string level, string message, string? details = null)
    {
        try
        {
            var ts = LoggerPathHelper.NowItalian(); // data/ora localizzata
            var safeMessage = Sanitize(message);
            var safeDetails = Sanitize(details);

            var logFilePath = Path.Combine(_logDirectory, $"{_componentName.ToLower()}_{ts:yyyyMMdd}.txt");
            var entry = $"[{ts:yyyy-MM-dd HH:mm:ss}] [{level}] {safeMessage}";
            if (!string.IsNullOrWhiteSpace(safeDetails))
                entry += $" | Details: {safeDetails}";

            // 🔒 Scrittura atomica cross-thread
            lock (_fileSync)
            {
                using var fs = new FileStream(
                    logFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite
                );
                using var sw = new StreamWriter(fs, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                sw.WriteLine(entry);
                sw.Flush();
                fs.Flush(true);
            }

            // Opzionale: stampa in console durante il debug
            Console.WriteLine(entry);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error writing {_componentName} log: {ex.Message}");
        }
    }
}
