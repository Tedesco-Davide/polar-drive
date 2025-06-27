using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

var basePath = AppContext.BaseDirectory;
var dbPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "PolarDriveInitDB.Cli", "datapolar.db"));
var wwwRoot = Path.Combine(basePath, "..", "..", "..", "..", "PolarDrive.WebApi", "wwwroot");

var options = new DbContextOptionsBuilder<PolarDriveDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;

using var db = new PolarDriveDbContext(options);

try
{
    // ✅ Inizializza il logger DOPO aver verificato la connessione
    if (!await db.Database.CanConnectAsync())
    {
        Console.WriteLine("❌ Cannot connect to database. Make sure to run PolarDriveInitDB.Cli first.");
        return;
    }

    var logger = new PolarDriveLogger(db);
    await logger.Info("PolarDriveInitDBMockData.Cli", "Starting mock DB cleanup");

    Console.WriteLine("🧹 Starting full cleanup of mock data...");

    // ───────────────────────────────
    // 1. ✅ CLEANUP SMART - Solo tabelle che esistono
    // ───────────────────────────────
    try
    {
        // ✅ Verifica quali tabelle esistono prima di fare cleanup
        var tableNames = new[]
        {
            "AdminFileManager",
            "OutagePeriods",
            "PdfReports",
            "ClientConsents",
            "VehicleData",
            "AnonymizedVehiclesData",
            "DemoSmsEvents",
            "ClientVehicles",
            "ClientCompanies"
        };

        foreach (var tableName in tableNames)
        {
            try
            {
                Console.WriteLine($"🗑️ Clearing {tableName}...");
                await db.Database.ExecuteSqlAsync($"DELETE FROM {tableName}");
                Console.WriteLine($"✅ Cleared {tableName}");
            }
            catch (Exception tableEx)
            {
                Console.WriteLine($"⚠️ Table {tableName} not found or already empty: {tableEx.Message}");
                await logger.Warning("PolarDriveInitDBMockData.Cli", $"Table cleanup warning for {tableName}", tableEx.Message);
            }
        }

        // Reset delle sequenze SQLite per le tabelle principali
        try
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('ClientCompanies', 'ClientVehicles', 'ClientConsents', 'OutagePeriods', 'PdfReports', 'AdminFileManager')");
            Console.WriteLine("✅ Reset sequence counters");
        }
        catch (Exception seqEx)
        {
            Console.WriteLine($"⚠️ Sequence reset warning: {seqEx.Message}");
        }

        Console.WriteLine("✅ Cleanup completed successfully");
        await logger.Info("PolarDriveInitDBMockData.Cli", "Cleared all available tables");
    }
    catch (Exception cleanupEx)
    {
        Console.WriteLine($"⚠️ Cleanup had issues: {cleanupEx.Message}");
        await logger.Warning("PolarDriveInitDBMockData.Cli", "Cleanup had warnings", cleanupEx.Message);
    }

    // ───────────────────────────────
    // 2. ✅ SMART INSERT - Controlla esistenza prima di inserire
    // ───────────────────────────────
    Console.WriteLine("🏢 Creating mock companies...");

    // Controlla se esistono già aziende con lo stesso VatNumber
    var existingCompany = await db.ClientCompanies
        .FirstOrDefaultAsync(c => c.VatNumber == "00000000001");

    ClientCompany[] companies;
    if (existingCompany != null)
    {
        Console.WriteLine("✅ Using existing company");
        companies = [existingCompany];
        await logger.Info("PolarDriveInitDBMockData.Cli", "Using existing company instead of creating duplicate");
    }
    else
    {
        companies = new[]
        {
            new ClientCompany {
                Name = "Paninoteca Rossi",
                VatNumber = "00000000001",
                ReferentName = "Luca Rossi",
                ReferentEmail = "luca@paninotecarossi.com",
                ReferentMobileNumber = "3201234567"
            },
        };

        db.ClientCompanies.AddRange(companies);
        await db.SaveChangesAsync();
        Console.WriteLine($"✅ Created {companies.Length} mock companies");
        await logger.Info("PolarDriveInitDBMockData.Cli", $"Inserted {companies.Length} mock companies");
    }

    // ───────────────────────────────
    // 4. 🚘 Mock Vehicles
    // ───────────────────────────────
    Console.WriteLine("🚗 Creating mock vehicles...");

    var existingVehicle = await db.ClientVehicles
        .FirstOrDefaultAsync(v => v.Vin == "5YJ3000000NEXUS01");

    ClientVehicle[] vehicles;
    if (existingVehicle != null)
    {
        Console.WriteLine("✅ Using existing vehicle");
        vehicles = [existingVehicle];
    }
    else
    {
        vehicles =
        [
            new ClientVehicle
            {
                ClientCompanyId = companies[0].Id,
                Vin = "5YJ3000000NEXUS01",
                FuelType = "Electric",
                Brand = "Tesla",
                Model = "Model 3",
                Trim = "Long Range",
                Color = "Ultra Red",
                IsActiveFlag = false,
                IsFetchingDataFlag = false,
                ClientOAuthAuthorized = false,
                FirstActivationAt = null,
                CreatedAt = DateTime.UtcNow
            }
        ];

        db.ClientVehicles.AddRange(vehicles);
        await db.SaveChangesAsync();
        Console.WriteLine($"✅ Created {vehicles.Length} mock vehicles");
        await logger.Info("PolarDriveInitDBMockData.Cli", $"Inserted {vehicles.Length} mock vehicles");
    }

    // ───────────────────────────────
    // 6. 🔑🔐 Consent for vehicles
    // ───────────────────────────────
    Console.WriteLine("🔐 Creating mock consents...");

    var existingConsent = await db.ClientConsents
        .FirstOrDefaultAsync(c => c.VehicleId == vehicles[0].Id);

    if (existingConsent == null)
    {
        var consent = new ClientConsent
        {
            ClientCompanyId = companies[0].Id,
            VehicleId = vehicles[0].Id,
            ConsentType = "Consent Activation",
            UploadDate = DateTime.Today,
            ZipFilePath = Path.Combine("companies", $"company-{companies[0].Id}", "consents-zip", "consent_1.zip").Replace("\\", "/"),
            ConsentHash = $"mockhash-{companies[0].Id}-abc123",
            Notes = "Mock consent automatico"
        };
        db.ClientConsents.Add(consent);
        await db.SaveChangesAsync();
        Console.WriteLine("✅ Created mock consent");
    }
    else
    {
        Console.WriteLine("✅ Using existing consent");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ FATAL ERROR: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"🔍 Inner: {ex.InnerException.Message}");
    }

    // Solo prova a loggare se il database è disponibile
    try
    {
        if (await db.Database.CanConnectAsync())
        {
            var logger = new PolarDriveLogger(db);
            await logger.Error("PolarDriveInitDBMockData.Cli", "Exception during mock setup", ex.ToString());
        }
    }
    catch
    {
        Console.WriteLine("⚠️ Could not log error to database");
    }
}