using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Helpers;

Console.WriteLine("🚀 Starting PolarDrive DB initialization...");

// ✅ Usa la stessa logica del DbContextFactory
var environment = 
    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") 
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
    ?? "Production";

// 🛡️ PROTEZIONE 1: Blocco immediato se Production
if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("❌ FATAL: This tool is DISABLED in Production environment!");
    Console.WriteLine("❌ Database initialization in production must be done manually.");
    Console.WriteLine("❌ This is a safety feature that cannot be overridden.");
    Console.ResetColor();
    Environment.Exit(1); // Exit code 1 = errore
    return;
}

// 🛡️ PROTEZIONE 2: Solo Development o Test sono permessi
var allowedEnvironments = new[] { "Development", "Test", "Testing", "Local" };
if (!allowedEnvironments.Contains(environment, StringComparer.OrdinalIgnoreCase))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ FATAL: Environment '{environment}' is not allowed!");
    Console.WriteLine($"❌ Allowed environments: {string.Join(", ", allowedEnvironments)}");
    Console.ResetColor();
    Environment.Exit(1);
    return;
}

using var db = new PolarDriveDbContextFactory().CreateDbContext(args);

// Prendi nome DB per log/safeguard
var dbName = db.Database.GetDbConnection().Database;

Console.WriteLine($"📍 Environment: {environment}");
Console.WriteLine($"🗄️  Database: {dbName}");

// 🛡️ PROTEZIONE 3: Whitelist rigida dei database permessi
var allowedDatabases = new[] 
{ 
    "PolarDriveDB_DEV",
    "PolarDriveDB_TEST", 
    "DataPolar_PolarDrive_DB_DEV", 
    "DataPolar_PolarDrive_DB_TEST",
    "PolarDriveDB_Local"
};

if (!allowedDatabases.Any(allowed => dbName.Equals(allowed, StringComparison.OrdinalIgnoreCase)))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ FATAL: Database '{dbName}' is NOT in the whitelist!");
    Console.WriteLine($"❌ This tool can only operate on: {string.Join(", ", allowedDatabases)}");
    Console.WriteLine($"❌ This is a safety feature to prevent accidental production data loss.");
    Console.ResetColor();
    Environment.Exit(1);
    return;
}

// 🛡️ PROTEZIONE 4: Controllo nome database per pattern production
var productionPatterns = new[] { "PROD", "PRODUCTION", "LIVE", "RELEASE" };
if (productionPatterns.Any(pattern => dbName.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ FATAL: Database name '{dbName}' contains production keyword!");
    Console.WriteLine($"❌ This tool cannot operate on production-like databases.");
    Console.ResetColor();
    Environment.Exit(1);
    return;
}

// 🛡️ PROTEZIONE 5: Controllo connection string per server production
var connectionString = db.Database.GetConnectionString();
var productionServers = new[] { "prod", "production", "live" };
if (productionServers.Any(srv => connectionString!.Contains(srv, StringComparison.OrdinalIgnoreCase)))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ FATAL: Connection string points to a production server!");
    Console.WriteLine($"❌ This tool cannot connect to production servers.");
    Console.ResetColor();
    Environment.Exit(1);
    return;
}

// ✅ Se arriviamo qui, siamo SICURI di essere in ambiente DEV/TEST
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"✅ Safety checks passed: Operating in {environment} on {dbName}");
Console.ResetColor();

try
{
    Console.WriteLine("📋 Step 1: Checking existing database...");

    var dbExists = await DatabaseHelper.DatabaseExistsAsync(db);
    if (dbExists)
    {
        var activeConnections = await DatabaseHelper.GetActiveConnectionsCountAsync(db);
        Console.WriteLine($"ℹ️  Found existing database with {activeConnections} active connection(s)");

        // In Development, proviamo prima a pulire, poi se fallisce eliminiamo
        Console.WriteLine("🧹 Attempting to clean database...");
        
        try
        {
            // Prova a eliminare tutte le tabelle
            await db.Database.ExecuteSqlRawAsync(@"
                -- Disabilita tutti i constraints
                EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all'
                
                -- Elimina tutte le foreign keys
                DECLARE @sql NVARCHAR(MAX) = ''
                SELECT @sql = @sql + 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) 
                    + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) 
                    + ' DROP CONSTRAINT ' + QUOTENAME(name) + ';'
                FROM sys.foreign_keys
                EXEC sp_executesql @sql
                
                -- Elimina tutte le tabelle
                SET @sql = ''
                SELECT @sql = @sql + 'DROP TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) 
                    + '.' + QUOTENAME(name) + ';'
                FROM sys.tables
                EXEC sp_executesql @sql
            ");
            
            Console.WriteLine("✅ Database cleaned successfully");
        }
        catch (Exception cleanEx)
        {
            Console.WriteLine($"⚠️  Clean failed: {cleanEx.Message}");
            Console.WriteLine("🗑️  Attempting to delete and recreate...");
            
            try
            {
                await DatabaseHelper.ForceDeleteDatabaseAsync(db);
                Console.WriteLine("✅ Database deleted");
            }
            catch (Exception deleteEx)
            {
                Console.WriteLine($"⚠️  Delete failed: {deleteEx.Message}");
                Console.WriteLine("⚠️  Will attempt to work with existing database");
            }
        }
    }
    else
    {
        Console.WriteLine("ℹ️  No existing database found");
    }

    Console.WriteLine("📋 Step 2: Creating/Updating database schema...");
    
    try
    {
        await db.Database.EnsureCreatedAsync();
        Console.WriteLine("✅ Database schema created successfully");
    }
    catch (Exception createEx)
    {
        Console.WriteLine($"⚠️  Schema creation issue: {createEx.Message}");
        
        // Se il database esiste ma lo schema no, proviamo a crearlo
        if (await db.Database.CanConnectAsync())
        {
            Console.WriteLine("📋 Database exists, creating schema...");
            var createScript = db.Database.GenerateCreateScript();
            
            // Esegui lo script in blocchi per evitare problemi
            var statements = createScript.Split("GO", StringSplitOptions.RemoveEmptyEntries);
            foreach (var statement in statements)
            {
                if (!string.IsNullOrWhiteSpace(statement))
                {
                    await db.Database.ExecuteSqlRawAsync(statement);
                }
            }
            Console.WriteLine("✅ Schema created via script");
        }
        else
        {
            throw;
        }
    }

    // ORA possiamo inizializzare il logger
    var logger = new PolarDriveLogger(db);
    await logger.Info("PolarDriveInitDB.Cli", 
        $"Database initialization completed - Environment: {environment}, DB: {dbName}");

    Console.WriteLine("📋 Step 3: Running initialization scripts...");

    // Execute extra SQL scripts
    await DbInitHelper.RunDbInitScriptsAsync(db);
    await logger.Info("PolarDriveInitDB.Cli", "DB initialization scripts executed successfully");

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"✅ Initialization completed successfully for {environment} environment");
    Console.ResetColor();
    
    await logger.Info("PolarDriveInitDB.Cli", 
        $"Complete initialization process finished successfully in {environment} mode");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ FATAL ERROR during initialization: {ex.Message}");
    Console.ResetColor();
    Console.WriteLine($"Details: {ex}");

    // Solo prova a loggare se il database esiste
    try
    {
        if (await DatabaseHelper.DatabaseExistsAsync(db))
        {
            var logger = new PolarDriveLogger(db);
            await logger.Error(
                "PolarDriveInitDB.Cli",
                $"Initialization failed in {environment} environment",
                $"Exception: {ex}"
            );
        }
    }
    catch
    {
        Console.WriteLine("⚠️  Could not log error to database");
    }

    Environment.Exit(1); // Exit con errore
}