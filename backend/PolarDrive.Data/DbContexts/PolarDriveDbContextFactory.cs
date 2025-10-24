using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PolarDrive.Data.DbContexts;

public class PolarDriveDbContextFactory : IDesignTimeDbContextFactory<PolarDriveDbContext>
{
    public PolarDriveDbContext CreateDbContext(string[] args)
    {
        // 1) Environment: per console app usa DOTNET_ENVIRONMENT; fallback ASPNETCORE_ENVIRONMENT
        var environment =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";
        Console.WriteLine($"[DbFactory] Environment: {environment}");

        // 2) Determina il base path in modo intelligente
        var basePath = GetConfigurationBasePath();
        Console.WriteLine($"[DbFactory] Config base path: {basePath}");

        // 3) Config: JSON opzionali + ENV obbligatorie
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables() // <-- abilita ConnectionStrings__DefaultConnection
            .Build();

        // 4) Connection string: prima prova GetConnectionString, poi la key esplicita
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? configuration["ConnectionStrings:DefaultConnection"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found. " +
                "Imposta la variabile d'ambiente ConnectionStrings__DefaultConnection " +
                "oppure fornisci appsettings(.{env}).json con la sezione ConnectionStrings."
            );
        }

        Console.WriteLine("[DbFactory] Connection string loaded (hidden for security)");

        // 5) Build DbContext
        var optionsBuilder = new DbContextOptionsBuilder<PolarDriveDbContext>()
            .UseSqlServer(connectionString);

        var context = new PolarDriveDbContext(optionsBuilder.Options);
        Console.WriteLine($"🏭 DbContext factory created successfully for {environment} environment");
        return context;
    }

    private string GetConfigurationBasePath()
    {
        // In Docker: /app è il base path
        if (Directory.Exists("/app") && File.Exists("/app/PolarDriveInitDB.Cli.dll"))
        {
            return "/app";
        }

        // Development: cerca nella struttura del progetto
        var currentDir = AppContext.BaseDirectory;
        
        // Prova a salire fino a trovare la cartella del progetto CLI
        var searchPaths = new[]
        {
            currentDir, // Directory corrente
            Path.Combine(currentDir, "..", "..", "..", "..", "PolarDriveInitDB.Cli"), // Da bin/Debug
            Path.Combine(currentDir, "..", "..", "..", "..", "..", "backend", "PolarDriveInitDB.Cli"), // Da Data project
            Directory.GetCurrentDirectory() // Working directory
        };

        foreach (var path in searchPaths)
        {
            var fullPath = Path.GetFullPath(path);
            
            // Cerca appsettings.json come indicatore
            if (Directory.Exists(fullPath) && 
                (File.Exists(Path.Combine(fullPath, "appsettings.json")) || 
                 File.Exists(Path.Combine(fullPath, "appsettings.Development.json"))))
            {
                return fullPath;
            }
        }

        // Fallback: usa la directory corrente (le variabili d'ambiente funzioneranno comunque)
        return AppContext.BaseDirectory;
    }
}