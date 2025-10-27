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

            // SQL Server: Disabilita temporaneamente i constraint di foreign key
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0");
            }
            catch
            {
                // Se non supporta FOREIGN_KEY_CHECKS (SQL Server usa un approccio diverso)
                // Elimineremo in ordine corretto invece
            }

            // Elimina i dati in ordine per rispettare le foreign key constraints
            // Prima le tabelle "child", poi le "parent"
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ClientConsents");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ClientTokens");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM PdfReports");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM VehiclesData");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM SmsAdaptiveProfile");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM AnonymizedVehiclesData");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM OutagePeriods");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM PhoneVehicleMappings");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM SmsAuditLog");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM AdminFileManager");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ClientVehicles");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ClientCompanies");

            // SQL Server: Reset degli Identity counters
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('ClientCompanies', RESEED, 0)");
                await dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('ClientVehicles', RESEED, 0)");
                await dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('ClientConsents', RESEED, 0)");
                await dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('OutagePeriods', RESEED, 0)");
                await dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('PdfReports', RESEED, 0)");
                await dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('AdminFileManager', RESEED, 0)");
                await dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('PhoneVehicleMappings', RESEED, 0)");
                await dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('SmsAuditLog', RESEED, 0)");
                await dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('SmsAdaptiveProfile', RESEED, 0)");
                await dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('AnonymizedVehiclesData', RESEED, 0)");
                Console.WriteLine("✅ Identity counters reset successfully");
            }
            catch (Exception identityEx)
            {
                Console.WriteLine($"⚠️ Identity reset warning: {identityEx.Message}");
                // Non è un errore critico se fallisce
            }

            // Riabilita i constraint se erano stati disabilitati
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1");
            }
            catch { /* Ignora se non è MySQL */ }

            await logger.Info("DbMockDataHelper", "Mock data cleanup completed successfully");
            Console.WriteLine("✅ Full cleanup completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Cleanup failed: {ex.Message}");
            await logger.Error("DbMockDataHelper", "Mock data cleanup failed", ex.ToString());
            throw; // Re-throw per debugging
        }
    }
}