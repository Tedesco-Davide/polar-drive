using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;

Console.WriteLine("🚀 Starting PolarDrive DB initialization...");

using var db = new PolarDriveDbContextFactory().CreateDbContext(args);

// 1. Applica tutte le migrations (se non già fatte)
db.Database.Migrate();
Console.WriteLine("✅ Migrations applied.");

// 2. Esegui tutti gli script .sql (trigger, index, view, ecc.)
DbInitHelper.RunDbInitScripts(db);

Console.WriteLine("🏁 Done.");