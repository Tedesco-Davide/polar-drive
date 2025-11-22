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
            try
            {
                Directory.CreateDirectory(fromEnv);
                var full = Path.GetFullPath(fromEnv);
                Console.WriteLine($"[LoggerPathHelper] Using POLAR_LOG_DIR: {full}");
                return full;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LoggerPathHelper] Failed POLAR_LOG_DIR='{fromEnv}': {ex.Message}");
                // va avanti con gli altri branch
            }
        }

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var folderName = env.Equals("Production", StringComparison.OrdinalIgnoreCase) ? "LOGS_PROD" : "LOGS_DEV";

        try
        {
            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var repoLogs = Path.Combine(repoRoot, folderName);
            Directory.CreateDirectory(repoLogs);
            Console.WriteLine($"[LoggerPathHelper] Using repo logs dir: {repoLogs}");
            return repoLogs;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoggerPathHelper] Repo logs resolution failed: {ex.Message}");
            var fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(fallback);
            Console.WriteLine($"[LoggerPathHelper] Using fallback logs dir: {fallback}");
            return fallback;
        }
    }
}
