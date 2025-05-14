using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

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

        return new PolarDriveDbContext(optionsBuilder.Options);
    }
}