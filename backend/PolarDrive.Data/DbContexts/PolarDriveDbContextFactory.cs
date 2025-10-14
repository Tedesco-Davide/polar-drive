using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PolarDrive.Data.DbContexts;

public class PolarDriveDbContextFactory : IDesignTimeDbContextFactory<PolarDriveDbContext>
{
    public PolarDriveDbContext CreateDbContext(string[] args)
    {
        // 1. Leggi l'environment (default: Development)
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        
        Console.WriteLine($"[DbFactory] Environment: {environment}");

        // 2. Cerca appsettings nel progetto CLI (parent directory)
        var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PolarDriveInitDB.Cli"));
        
        // Fallback: se non trova, usa la directory corrente
        if (!Directory.Exists(basePath))
        {
            basePath = Directory.GetCurrentDirectory();
        }

        Console.WriteLine($"[DbFactory] Config base path: {basePath}");

        // 3. Carica configurazione
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // 4. Leggi connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'DefaultConnection' not found in appsettings.json or appsettings.{environment}.json"
            );
        }

        Console.WriteLine($"[DbFactory] Connection string loaded");
        // ⚠️ Non stampare la connection string completa per sicurezza!

        // 5. Crea DbContext
        var optionsBuilder = new DbContextOptionsBuilder<PolarDriveDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        var context = new PolarDriveDbContext(optionsBuilder.Options);

        Console.WriteLine($"✅ DbContext factory created successfully for {environment} environment");

        return context;
    }
}