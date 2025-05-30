using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PolarDrive.Data.Entities;

namespace PolarDrive.Data.DbContexts;

public class PolarDriveDbContextFactory : IDesignTimeDbContextFactory<PolarDriveDbContext>
{
    public PolarDriveDbContext CreateDbContext(string[] args)
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "datapolar.db");
        dbPath = Path.GetFullPath(dbPath);

        Console.WriteLine($"[DbFactory] Using DB path: {dbPath}");

        var optionsBuilder = new DbContextOptionsBuilder<PolarDriveDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        var context = new PolarDriveDbContext(optionsBuilder.Options);

        try
        {
            var logger = new PolarDriveLogger(context);
            logger.Info(
                "PolarDriveDbContextFactory.CreateDbContext",
                "DbContext factory successfully created a new context instance",
                $"Database path: {dbPath}"
            ).Wait();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during logger initialization: {ex.Message}");
            try
            {
                // fallback basic logger
                context.PolarDriveLogs.Add(new PolarDriveLog
                {
                    Timestamp = DateTime.UtcNow,
                    Source = "PolarDriveDbContextFactory.CreateDbContext",
                    Level = PolarDriveLogLevel.ERROR,
                    Message = "Failed to initialize PolarDriveLogger",
                    Details = ex.ToString()
                });

                context.SaveChanges();
            }
            catch
            {
                Console.WriteLine("⚠️ Unable to write fallback log entry to database.");
            }
        }

        return context;
    }
}