using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

Console.WriteLine("🚀 Starting PolarDrive DB initialization...");

using var db = new PolarDriveDbContextFactory().CreateDbContext(args);
var logger = new PolarDriveLogger(db);

try
{
    await logger.Info("PolarDriveInitDB.Cli", "Starting DB initialization process");

    // 1. Apply all migrations (PRODUCTION ONLY)
    // await db.Database.MigrateAsync(); // Uncomment when switching to production
    // await logger.Info("PolarDriveInitDB.Cli", "Migrations applied successfully");

    // 1. Delete / Create DB (DEV only)
    await db.Database.EnsureDeletedAsync();
    await logger.Info("PolarDriveInitDB.Cli", "Database deleted successfully");

    await db.Database.EnsureCreatedAsync();
    await logger.Info("PolarDriveInitDB.Cli", "Database created successfully");

    // 2. Execute extra SQL scripts
    await DbInitHelper.RunDbInitScriptsAsync(db);
    await logger.Info("PolarDriveInitDB.Cli", "DB initialization scripts executed successfully");

    Console.WriteLine("🏁 Final Initialization Done.");
}
catch (Exception ex)
{
    await logger.Error(
        "PolarDriveInitDB.Cli",
        "An error occurred during the database initialization process",
        $"Exception: {ex}"
    );

    Console.WriteLine($"❌ FATAL ERROR during initialization: {ex.Message}");
}