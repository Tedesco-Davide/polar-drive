namespace PolarDrive.Data.DbContexts;

internal static class LoggerPathHelper
{
    private static TimeZoneInfo? _tz;
    public static TimeZoneInfo ItalianTz =>
        _tz ??= GetItalianTz();

    private static TimeZoneInfo GetItalianTz()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"); } // Windows
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"); }                  // Linux
    }

    public static DateTime NowItalian() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ItalianTz);

    public static string ResolveBaseLogsDir()
    {
        // 1) ENV override (consigliato)
        var fromEnv = Environment.GetEnvironmentVariable("POLAR_LOG_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            Directory.CreateDirectory(fromEnv);
            return Path.GetFullPath(fromEnv);
        }

        // 2) Repo root + LOGS_DEV/PROD (quando sei in 'dotnet run' o VS)
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var folderName = env.Equals("Production", StringComparison.OrdinalIgnoreCase) ? "LOGS_PROD" : "LOGS_DEV";
        try
        {
            // risali dalla cartella build (…/bin/Debug/netX.Y/) alla root repo
            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var repoLogs = Path.Combine(repoRoot, folderName);
            Directory.CreateDirectory(repoLogs);
            return repoLogs;
        }
        catch
        {
            // 3) Fallback: /app/Logs (Docker) o bin/…/Logs (locale)
            var fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }
}
