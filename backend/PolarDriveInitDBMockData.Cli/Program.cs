using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.IO.Compression;

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
            "VehicleData",           // Potrebbe non esistere
            "AnonymizedVehiclesData", // Potrebbe non esistere
            "DemoSmsEvents",         // Potrebbe non esistere
            "ClientVehicles",
            "ClientCompanies"
        };

        foreach (var tableName in tableNames)
        {
            try
            {
                Console.WriteLine($"🗑️ Clearing {tableName}...");
                await db.Database.ExecuteSqlRawAsync($"DELETE FROM {tableName}");
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
        companies = new[] { existingCompany };
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
    // 3. Setup directories
    // ───────────────────────────────
    foreach (var company in companies)
    {
        var companyDir = Path.Combine(wwwRoot, $"company-{company.Id}");
        var consentsDir = Path.Combine(companyDir, "consents-zip");
        var historyDir = Path.Combine(companyDir, "history-pdf");
        var reportsDir = Path.Combine(companyDir, "reports-pdf");

        Directory.CreateDirectory(consentsDir);
        Directory.CreateDirectory(historyDir);
        Directory.CreateDirectory(reportsDir);
        Console.WriteLine($"✅ Created directories for company {company.Id}");
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

    // ───────────────────────────────
    // 7. Create mock ZIPs used in outages
    // ───────────────────────────────
    Console.WriteLine("📦 Creating mock outage ZIPs...");

    var zipsDir = Path.Combine(wwwRoot, "zips-outages");
    Directory.CreateDirectory(zipsDir);

    var zip1Path = Path.Combine(zipsDir, "20250418-rossi-vehicle01.zip");
    if (!File.Exists(zip1Path))
    {
        using var zip = ZipFile.Open(zip1Path, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("mock_outage_rossi.pdf");
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync("%PDF-1.4\n%OUTAGE ZIP Rossi\n%%EOF");
        Console.WriteLine($"✅ Created outage ZIP: {zip1Path}");
    }

    var zip2Path = Path.Combine(zipsDir, "20250425-manual-fleetapi.zip");
    if (!File.Exists(zip2Path))
    {
        using var zip = ZipFile.Open(zip2Path, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("manual_fleetapi.pdf");
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync("%PDF-1.4\n%OUTAGE ZIP FleetApi\n%%EOF");
        Console.WriteLine($"✅ Created outage ZIP: {zip2Path}");
    }

    // ───────────────────────────────
    // 8. Insert mock outage records (solo se non esistono)
    // ───────────────────────────────
    Console.WriteLine("🚨 Creating mock outages...");

    var existingOutagesCount = await db.OutagePeriods.CountAsync();

    if (existingOutagesCount == 0)
    {
        var rand = new Random();
        int offset = rand.Next(1, 10000);
        var outages = new List<OutagePeriod>
        {
            new()
            {
                VehicleId = vehicles[0].Id,
                ClientCompanyId = vehicles[0].ClientCompanyId, // ✅ CORREZIONE: Usa sempre [0]
                AutoDetected = true,
                OutageType = "Outage Vehicle",
                OutageBrand = "Tesla",
                CreatedAt = DateTime.Parse("2025-04-20T10:30:00Z").AddSeconds(offset),
                OutageStart = DateTime.Parse("2025-04-18T04:00:00Z").AddSeconds(offset),
                OutageEnd = DateTime.Parse("2025-04-19T18:00:00Z").AddSeconds(offset),
                ZipFilePath = "zips-outages/20250418-rossi-vehicle01.zip",
                Notes = "Rilevato automaticamente, comunicazione PEC non ricevuta."
            },
            new()
            {
                VehicleId = vehicles[0].Id,
                ClientCompanyId = vehicles[0].ClientCompanyId, // ✅ CORREZIONE: Usa sempre [0]
                AutoDetected = false,
                OutageType = "Outage Vehicle",
                OutageBrand = "Tesla",
                CreatedAt = DateTime.Parse("2025-04-21T09:45:00Z").AddSeconds(offset),
                OutageStart = DateTime.Parse("2025-04-15T00:00:00Z").AddSeconds(offset),
                OutageEnd = DateTime.Parse("2025-04-20T23:50:00Z").AddSeconds(offset),
                Notes = "Outage manuale: veicolo in assistenza per aggiornamento batteria."
            },
            new()
            {
                VehicleId = vehicles[0].Id,
                ClientCompanyId = vehicles[0].ClientCompanyId, // ✅ CORREZIONE: Usa sempre [0]
                AutoDetected = true,
                OutageType = "Outage Vehicle",
                OutageBrand = "Tesla",
                CreatedAt = DateTime.Parse("2025-04-24T07:10:00Z").AddSeconds(offset),
                OutageStart = DateTime.Parse("2025-04-23T22:00:00Z").AddSeconds(offset),
                OutageEnd = null,
                Notes = "Inattività in corso: nessun dato da oltre 8 ore."
            },
            new()
            {
                VehicleId = null,
                ClientCompanyId = null,
                AutoDetected = true,
                OutageType = "Outage Fleet Api",
                OutageBrand = "Tesla",
                CreatedAt = DateTime.Parse("2025-04-22T12:00:00Z").AddSeconds(offset),
                OutageStart = DateTime.Parse("2025-04-22T08:00:00Z").AddSeconds(offset),
                OutageEnd = DateTime.Parse("2025-04-22T11:30:00Z").AddSeconds(offset),
                Notes = "API ufficiale non rispondeva: HTTP 503 da tutte le richieste."
            },
            new()
            {
                VehicleId = null,
                ClientCompanyId = null,
                AutoDetected = false,
                OutageType = "Outage Fleet Api",
                OutageBrand = "Polestar",
                CreatedAt = DateTime.Parse("2025-04-25T09:00:00Z").AddSeconds(offset),
                OutageStart = DateTime.Parse("2025-04-25T07:00:00Z").AddSeconds(offset),
                OutageEnd = null,
                ZipFilePath = "zips-outages/20250425-manual-fleetapi.zip",
                Notes = "Inserito manualmente: timeout frequenti da client."
            },
        };

        db.OutagePeriods.AddRange(outages);
        await db.SaveChangesAsync();
        Console.WriteLine($"✅ Created {outages.Count} mock outages");
        await logger.Info("PolarDriveInitDBMockData.Cli", $"Inserted {outages.Count} mock outage records.");
    }
    else
    {
        Console.WriteLine($"✅ Found {existingOutagesCount} existing outages");
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