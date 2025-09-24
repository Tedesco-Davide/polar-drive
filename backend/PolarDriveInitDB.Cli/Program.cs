using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Helpers;

Console.WriteLine("🚀 Starting PolarDrive DB initialization...");

using var db = new PolarDriveDbContextFactory().CreateDbContext(args);

try
{
    Console.WriteLine("📋 Step 1: Checking existing database...");
    
    var dbExists = await DatabaseHelper.DatabaseExistsAsync(db);
    if (dbExists)
    {
        var activeConnections = await DatabaseHelper.GetActiveConnectionsCountAsync(db);
        Console.WriteLine($"ℹ️ Found existing database with {activeConnections} active connection(s)");
        
        Console.WriteLine("🗑️ Force deleting existing database...");
        await DatabaseHelper.ForceDeleteDatabaseAsync(db);
    }
    else
    {
        Console.WriteLine("ℹ️ No existing database found");
    }

    Console.WriteLine("📋 Step 2: Creating new database...");
    await db.Database.EnsureCreatedAsync();
    Console.WriteLine("✅ Database created successfully");

    // ✅ ORA possiamo inizializzare il logger DOPO che il DB è stato creato
    var logger = new PolarDriveLogger(db);
    await logger.Info("PolarDriveInitDB.Cli", "Database initialization completed - Logger now active");

    Console.WriteLine("📋 Step 3: Running initialization scripts...");

    // 2. Execute extra SQL scripts
    await DbInitHelper.RunDbInitScriptsAsync(db);
    await logger.Info("PolarDriveInitDB.Cli", "DB initialization scripts executed successfully");

    Console.WriteLine("🏁 Final Initialization Done.");
    await logger.Info("PolarDriveInitDB.Cli", "Complete initialization process finished successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ FATAL ERROR during initialization: {ex.Message}");
    Console.WriteLine($"Details: {ex}");

    // ✅ Solo prova a loggare se il database esiste
    try
    {
        if (await DatabaseHelper.DatabaseExistsAsync(db))
        {
            var logger = new PolarDriveLogger(db);
            await logger.Error(
                "PolarDriveInitDB.Cli",
                "An error occurred during the database initialization process",
                $"Exception: {ex}"
            );
        }
    }
    catch
    {
        Console.WriteLine("⚠️ Could not log error to database");
    }

    throw; // Re-throw per far fallire il processo
}