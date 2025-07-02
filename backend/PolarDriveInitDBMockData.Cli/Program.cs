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
            "AdaptiveProfilingSmsEvents",
            "SmsAuditLogs",
            "PhoneVehicleMappings",
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
            await db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('ClientCompanies', 'ClientVehicles', 'ClientConsents', 'OutagePeriods', 'PdfReports', 'AdminFileManager', 'PhoneVehicleMappings', 'SmsAuditLogs', 'AdaptiveProfilingSmsEvents')");
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
                IsActiveFlag = true,              // ← ATTIVO per default
                IsFetchingDataFlag = true,        // ← FETCHING attivo per default
                ClientOAuthAuthorized = true,     // ← AUTORIZZATO per default
                FirstActivationAt = DateTime.Now,
                CreatedAt = DateTime.Now
            }
        ];

        db.ClientVehicles.AddRange(vehicles);
        await db.SaveChangesAsync();
        Console.WriteLine($"✅ Created {vehicles.Length} mock vehicles (ACTIVE & AUTHORIZED)");
        await logger.Info("PolarDriveInitDBMockData.Cli", $"Inserted {vehicles.Length} mock vehicles");
    }

    // ───────────────────────────────
    // 5. 📱 Mock Phone Mapping per SMS
    // ───────────────────────────────
    Console.WriteLine("📱 Creating mock phone mappings...");

    var existingMapping = await db.PhoneVehicleMappings
        .FirstOrDefaultAsync(m => m.PhoneNumber == "+393334455666");

    if (existingMapping == null)
    {
        var phoneMapping = new PhoneVehicleMapping
        {
            PhoneNumber = "+393334455666",
            VehicleId = vehicles[0].Id,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsActive = true,
            Notes = "TESTSMS - Numero mock per test SMS automatico"
        };

        db.PhoneVehicleMappings.Add(phoneMapping);
        await db.SaveChangesAsync();
        Console.WriteLine("✅ Created mock phone mapping: +393334455666 → Vehicle 1");
        await logger.Info("PolarDriveInitDBMockData.Cli", "Created mock phone mapping for SMS testing");
    }
    else
    {
        Console.WriteLine("✅ Using existing phone mapping");
    }

    // ───────────────────────────────
    // 6. 📲 Mock SMS Audit Log di successo
    // ───────────────────────────────
    Console.WriteLine("📲 Creating mock SMS audit log...");

    var existingSmsLog = await db.SmsAuditLogs
        .FirstOrDefaultAsync(s => s.MessageSid == "SM_MOCK_SUCCESS_12345");

    if (existingSmsLog == null)
    {
        var smsLog = new SmsAuditLog
        {
            MessageSid = "SM_MOCK_SUCCESS_12345",
            FromPhoneNumber = "+393334455666",
            ToPhoneNumber = "+393901234567",
            MessageBody = "ADAPTIVE 0001 test session",
            ReceivedAt = DateTime.Now.AddMinutes(-5), // 5 minuti fa
            ProcessingStatus = "SUCCESS",
            ErrorMessage = null,
            VehicleIdResolved = vehicles[0].Id,
            ResponseSent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Response>
    <Message>✅ Adaptive Profiling activated for 4 hours
&#128663; Veicolo: 5YJ3000000NEXUS01
&#127970; Cliente: Paninoteca Rossi
⏰ Fino alle: " + DateTime.Now.AddHours(4).ToString("HH:mm") + @" UTC
&#128202; Modalit&#224;: Adaptive Profiling ATTIVA
</Message>
</Response>"
        };

        db.SmsAuditLogs.Add(smsLog);
        await db.SaveChangesAsync();
        Console.WriteLine("✅ Created mock SMS audit log (SUCCESS)");
        await logger.Info("PolarDriveInitDBMockData.Cli", "Created mock SMS audit log");
    }
    else
    {
        Console.WriteLine("✅ Using existing SMS audit log");
    }

    // ───────────────────────────────
    // 7. 🎯 Mock Adaptive Profiling SMS Event
    // ───────────────────────────────
    Console.WriteLine("🎯 Creating mock adaptive profiling SMS event...");

    var existingEvent = await db.AdaptiveProfilingSmsEvents
        .FirstOrDefaultAsync(e => e.VehicleId == vehicles[0].Id);

    if (existingEvent == null)
    {
        var adaptiveEvent = new AdaptiveProfilingSmsEvent
        {
            VehicleId = vehicles[0].Id,
            ReceivedAt = DateTime.Now.AddMinutes(-5),
            MessageContent = "ADAPTIVE 0001 test session",
            ParsedCommand = "ADAPTIVE_PROFILING_ON"
        };

        db.AdaptiveProfilingSmsEvents.Add(adaptiveEvent);
        await db.SaveChangesAsync();
        Console.WriteLine("✅ Created mock adaptive profiling SMS event");
        await logger.Info("PolarDriveInitDBMockData.Cli", "Created mock adaptive profiling SMS event");
    }
    else
    {
        Console.WriteLine("✅ Using existing adaptive profiling SMS event");
    }

    // ───────────────────────────────
    // 8. 🔑🔐 Consent for vehicles
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
    // 🎉 RIEPILOGO FINALE
    // ───────────────────────────────
    Console.WriteLine();
    Console.WriteLine("🎉 ===== MOCK DATA SETUP COMPLETED ===== 🎉");
    Console.WriteLine($"🏢 Company: {companies[0].Name} (ID: {companies[0].Id})");
    Console.WriteLine($"🚗 Vehicle: {vehicles[0].Vin} (ID: {vehicles[0].Id}) - ACTIVE & AUTHORIZED");
    Console.WriteLine($"📱 Phone Mapping: +393334455666 → Vehicle {vehicles[0].Id}");
    Console.WriteLine($"📲 SMS Audit: Sample SUCCESS log created");
    Console.WriteLine($"🎯 Adaptive Event: Profiling session mock created");
    Console.WriteLine($"🔐 Consent: Mock consent file created");
    Console.WriteLine();
    Console.WriteLine("🚀 Ready for SMS testing! Use:");
    Console.WriteLine("   From: +393334455666");
    Console.WriteLine("   Body: ADAPTIVE 0001 test session");
    Console.WriteLine("=========================================");

    await logger.Info("PolarDriveInitDBMockData.Cli", "Mock data setup completed successfully with SMS support");
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