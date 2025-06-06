using Microsoft.EntityFrameworkCore;

namespace PolarDrive.Data.DbContexts;

public static class DbMockDataHelper
{
    public static async Task ClearMockDataAsync(PolarDriveDbContext dbContext)
    {
        var logger = new PolarDriveLogger(dbContext);

        try
        {
            Console.WriteLine("🧹 Starting full cleanup of mock data...");
            await logger.Info("DbMockDataHelper", "Starting full cleanup of mock data");

            try
            {
                await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
            }
            catch { /* Ignora se non è SQLite */ }

            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ClientConsents");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ClientTokens");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM PdfReports");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM VehiclesData");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM OutagePeriods");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ScheduledFileJobs");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ClientVehicles");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ClientCompanies");

            try
            {
                await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");
            }
            catch { /* Ignora se non è SQLite */ }

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