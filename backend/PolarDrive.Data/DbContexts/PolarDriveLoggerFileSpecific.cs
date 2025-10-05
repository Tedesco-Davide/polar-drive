using System;
using System.IO;

namespace PolarDrive.Data.DbContexts;

public class PolarDriveLoggerFileSpecific
{
    private readonly string _componentName;
    private readonly string _logDirectory;

    private static readonly TimeZoneInfo ItalianTimeZone = 
        TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");

    public PolarDriveLoggerFileSpecific(string componentName = "General")
    {
        _componentName = componentName;
        _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", componentName);

        if (!Directory.Exists(_logDirectory))
            Directory.CreateDirectory(_logDirectory);
    }

    private static DateTime GetItalianTime()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ItalianTimeZone);
    }

    public void Info(string message, string? details = null) => Write("INFO", message, details);
    public void Warn(string message, string? details = null) => Write("WARN", message, details);
    public void Error(string message, string? details = null) => Write("ERROR", message, details);
    public void Debug(string message, string? details = null) => Write("DEBUG", message, details);

    private readonly Lock _lock = new();

    private void Write(string level, string message, string? details = null)
    {
        try
        {
            var ts = GetItalianTime();
            var logFilePath = Path.Combine(_logDirectory, $"{_componentName.ToLower()}_{ts:yyyyMMdd}.txt");
            var entry = $"[{ts:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            if (!string.IsNullOrWhiteSpace(details))
                entry += $" | Details: {details}";

            lock (_lock)
            {
                File.AppendAllText(logFilePath, entry + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error writing {_componentName} log: {ex.Message}");
        }
    }
}
