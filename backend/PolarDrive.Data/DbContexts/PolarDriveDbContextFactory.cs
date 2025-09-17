using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PolarDrive.Data.DbContexts;

public class PolarDriveDbContextFactory : IDesignTimeDbContextFactory<PolarDriveDbContext>
{
    public PolarDriveDbContext CreateDbContext(string[] args)
    {
        // Connection string per SQL Server (Default Instance)
        var connectionString = "Server=localhost;Database=DataPolar_PolarDrive_DB_DEV;Trusted_Connection=true;TrustServerCertificate=true;";

        Console.WriteLine($"[DbFactory] Using SQL Server connection: {connectionString}");

        var optionsBuilder = new DbContextOptionsBuilder<PolarDriveDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        var context = new PolarDriveDbContext(optionsBuilder.Options);

        Console.WriteLine($"✅ DbContext factory created successfully");

        return context;
    }
}