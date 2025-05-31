using Microsoft.EntityFrameworkCore;

namespace PolarDrive.Data.DbContexts;

public static class DbInitHelper
{
    public static async Task RunDbInitScriptsAsync(PolarDriveDbContext dbContext)
    {
        var logger = new PolarDriveLogger(dbContext);

        string basePath = AppContext.BaseDirectory;
        string scriptsPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "PolarDrive.Data", "DbInitScripts"));

        if (!Directory.Exists(scriptsPath))
        {
            Console.WriteLine("❌ DbInitScripts folder not found.");
            await logger.Error("DbInitHelper.RunDbInitScriptsAsync", "Scripts folder not found", $"Path attempted: {scriptsPath}");
            return;
        }

        var sqlFiles = Directory.GetFiles(scriptsPath, "*.sql")
                                .OrderBy(f => f)
                                .ToList();

        foreach (var file in sqlFiles)
        {
            string sql = await File.ReadAllTextAsync(file);
            if (!string.IsNullOrWhiteSpace(sql))
            {
                string fileName = Path.GetFileName(file);
                try
                {
                    await dbContext.Database.ExecuteSqlRawAsync(sql);
                    Console.WriteLine($"✅ Executed: {fileName}");

                    await logger.Info(
                        "DbInitHelper.RunDbInitScriptsAsync",
                        $"Executed script: {fileName}"
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error in {fileName}: {ex.Message}");

                    await logger.Error(
                        "DbInitHelper.RunDbInitScriptsAsync",
                        $"Failed to execute script: {fileName}",
                        ex.ToString()
                    );
                }
            }
        }
    }

    public static class DbMockDataHelper
    {
        public static async Task ClearMockDataAsync(PolarDriveDbContext dbContext)
        {
            var logger = new PolarDriveLogger(dbContext);

            try
            {
                Console.WriteLine("🧹 Starting full cleanup of mock data...");
                await logger.Info("DbMockDataHelper", "Starting full cleanup of mock data");

                // Disabilita temporaneamente i vincoli di chiave esterna
                await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");

                // Ordine corretto per evitare violazioni FK
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ClientConsents");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ClientTokens");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM PdfReports");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM VehiclesData");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM OutagePeriods");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ClientVehicles");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ClientCompanies");

                // Riabilita FK
                await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");

                await logger.Info("DbMockDataHelper", "Mock data cleanup completed successfully");
                Console.WriteLine("✅ Full cleanup completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Cleanup failed: {ex.Message}");
                await logger.Error("DbMockDataHelper", "Mock data cleanup failed", ex.ToString());
            }
        }
    }
}