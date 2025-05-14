using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;

Console.WriteLine("🚀 Starting PolarDrive DB initialization...");

using var db = new PolarDriveDbContextFactory().CreateDbContext(args);

// 1. Applica tutte le migrations
// 1.1 MIGRATION DA ABILITARE QUANDO SI ANDRA' IN PRODUZIONE
// - Quando vorrai tornare a usare le migration (es. per staging o produzione):
// - Esegui bash: dotnet ef migrations add InitialProductionSchema
// - Torna a usare: await db.Database.MigrateAsync();
// Console.WriteLine("✅ Migrations applied.");

// 1. Check su Delete / Create DB
await db.Database.EnsureDeletedAsync();
await db.Database.EnsureCreatedAsync();
// Console.WriteLine("✅ Create DONE!");

// 2. Esegui script SQL extra
await DbInitHelper.RunDbInitScriptsAsync(db);

Console.WriteLine("🏁 Final Initialization Done.");
