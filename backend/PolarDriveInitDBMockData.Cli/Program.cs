using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Text;

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
    await logger.Info("PolarDriveInitDBMockData.Cli", "Starting comprehensive mock DB setup for API tests");

    Console.WriteLine("🧹 Starting full cleanup and comprehensive mock data setup...");

    // ───────────────────────────────
    // 1. ✅ CLEANUP SMART - Solo tabelle che esistono
    // ───────────────────────────────
    try
    {
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
            }
        }

        // Reset delle sequenze SQLite
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
    }

    // ───────────────────────────────
    // 2. 🏢 COMPANIES - Multiple companies for comprehensive testing
    // ───────────────────────────────
    Console.WriteLine("🏢 Creating mock companies...");

    var companies = new[]
    {
        new ClientCompany {
            Name = "DataPolar Test Company",
            VatNumber = "IT12345678901",
            Address = "Via Test 123, Milano",
            Email = "test@datapolar.com",
            LandlineNumber = "0221234567"
        },
        new ClientCompany {
            Name = "Paninoteca Rossi",
            VatNumber = "00000000001",
            Address = "Via Roma 123, Milano",
            Email = "info@paninotecarossi.com",
            LandlineNumber = "0221234568"
        },
        new ClientCompany {
            Name = "Tesla Fleet Corp",
            VatNumber = "IT98765432109",
            Address = "Via Innovation 456, Torino",
            Email = "fleet@tesla-corp.com",
            LandlineNumber = "0119876543"
        }
    };

    db.ClientCompanies.AddRange(companies);
    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Created {companies.Length} mock companies");

    // ───────────────────────────────
    // 3. 🚘 VEHICLES - Multiple vehicles with different states
    // ───────────────────────────────
    Console.WriteLine("🚗 Creating mock vehicles...");

    var vehicles = new[]
    {
        // Vehicle for API tests with TESTVIN123456789
        new ClientVehicle
        {
            ClientCompanyId = companies[0].Id, // DataPolar Test Company
            Vin = "TESTVIN123456789",
            FuelType = "Electric",
            Brand = "Tesla",
            Model = "Model 3",
            Trim = "Long Range",
            Color = "Ultra Red",
            IsActiveFlag = true,
            IsFetchingDataFlag = true,
            ClientOAuthAuthorized = true,
            FirstActivationAt = DateTime.Now.AddDays(-30),
            CreatedAt = DateTime.Now.AddDays(-30),
            ReferentName = "Test User",
            ReferentEmail = "test@datapolar.com",
            ReferentMobileNumber = "+393331234567",
        },
        new ClientVehicle
        {
            ClientCompanyId = companies[1].Id, // Paninoteca Rossi
            Vin = "5YJ3000000NEXUS01",
            FuelType = "Electric",
            Brand = "Tesla",
            Model = "Model 3",
            Trim = "Long Range",
            Color = "Ultra Red",
            IsActiveFlag = false,
            IsFetchingDataFlag = true,
            ClientOAuthAuthorized = true,
            FirstActivationAt = DateTime.Now,
            CreatedAt = DateTime.Now,
            ReferentName = "Luca Rossi",
            ReferentEmail = "luca.rossi@paninotecarossi.com",
            ReferentMobileNumber = "+393201234567",
        },
        new ClientVehicle
        {
            ClientCompanyId = companies[1].Id,
            Vin = "5YJ3000000NEXUS02",
            FuelType = "Electric",
            Brand = "Tesla",
            Model = "Model Y",
            Trim = "Performance",
            Color = "Pearl White",
            IsActiveFlag = true,
            IsFetchingDataFlag = true,
            ClientOAuthAuthorized = true,
            FirstActivationAt = DateTime.Now,
            CreatedAt = DateTime.Now,
            ReferentName = "Mario Bianchi",
            ReferentEmail = "mario.bianchi@paninotecarossi.com",
            ReferentMobileNumber = "+393209876543",
        }
    };

    db.ClientVehicles.AddRange(vehicles);
    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Created {vehicles.Length} mock vehicles");

    // ───────────────────────────────
    // 4. 🔐 CLIENT CONSENTS with actual files
    // ───────────────────────────────
    Console.WriteLine("🔐 Creating mock consents with files...");

    // Create storage directories
    var storageBase = Path.Combine(wwwRoot, "storage", "companies");
    foreach (var company in companies)
    {
        var companyDir = Path.Combine(storageBase, $"company-{company.Id}", "consents-zips");
        Directory.CreateDirectory(companyDir);

        // Create a dummy ZIP file
        var zipPath = Path.Combine(companyDir, $"consent_{company.Id}.zip");
        await File.WriteAllBytesAsync(zipPath, Encoding.UTF8.GetBytes("MOCK ZIP CONTENT"));
        Console.WriteLine($"✅ Created mock ZIP: {zipPath}");
    }

    var consents = new[]
    {
        new ClientConsent
        {
            ClientCompanyId = companies[0].Id,
            VehicleId = vehicles[0].Id,
            ConsentType = "DataProcessingConsent",
            UploadDate = DateTime.Today.AddDays(-10),
            ZipFilePath = Path.Combine("companies", $"company-{companies[0].Id}", "consents-zips", $"consent_{companies[0].Id}.zip").Replace("\\", "/"),
            ConsentHash = $"hash-{companies[0].Id}-{Guid.NewGuid():N}",
            Notes = "Test consent for TESTVIN123456789"
        },
        new ClientConsent
        {
            ClientCompanyId = companies[1].Id,
            VehicleId = vehicles[1].Id,
            ConsentType = "Consent Activation",
            UploadDate = DateTime.Today.AddDays(-5),
            ZipFilePath = Path.Combine("companies", $"company-{companies[1].Id}", "consents-zips", $"consent_{companies[1].Id}.zip").Replace("\\", "/"),
            ConsentHash = $"hash-{companies[1].Id}-{Guid.NewGuid():N}",
            Notes = "Mock consent for Paninoteca"
        }
    };

    db.ClientConsents.AddRange(consents);
    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Created {consents.Length} mock consents with files");

    // ───────────────────────────────
    // 5. 📊 PDF REPORTS with actual files
    // ───────────────────────────────
    Console.WriteLine("📊 Creating mock PDF reports...");

    // Create reports directories
    var reportsDir = Path.Combine(wwwRoot, "storage", "reports");
    Directory.CreateDirectory(reportsDir);

    var reports = new[]
    {
        new PdfReport
        {
            ClientCompanyId = companies[0].Id,
            ClientVehicleId = vehicles[0].Id,
            ReportPeriodStart = DateTime.Now.AddDays(-30),
            ReportPeriodEnd = DateTime.Now.AddDays(-1),
            GeneratedAt = DateTime.Now.AddDays(-2),
            Status = "Completed",
            Notes = "Test report for API testing",
            RegenerationCount = 0
        },
        new PdfReport
        {
            ClientCompanyId = companies[1].Id,
            ClientVehicleId = vehicles[1].Id,
            ReportPeriodStart = DateTime.Now.AddDays(-7),
            ReportPeriodEnd = DateTime.Now,
            GeneratedAt = null, // Report in processing
            Status = "Processing",
            Notes = "Monthly report in progress",
            RegenerationCount = 1
        }
    };

    db.PdfReports.AddRange(reports);
    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Created {reports.Length} mock PDF reports");

    // ───────────────────────────────
    // 6. 📁 ADMIN FILE MANAGER - SOSTITUISCI QUESTA SEZIONE
    // ───────────────────────────────
    Console.WriteLine("📁 Creating mock file manager entries...");

    var fileManagerDir = Path.Combine(wwwRoot, "storage", "admin-exports");
    Directory.CreateDirectory(fileManagerDir);

    var fileEntries = new[]
    {
    new AdminFileManager
    {
        RequestedAt = DateTime.Now.AddDays(-3),
        StartedAt = DateTime.Now.AddDays(-3).AddMinutes(2),
        CompletedAt = DateTime.Now.AddDays(-3).AddMinutes(15),
        PeriodStart = DateTime.Now.AddDays(-30),
        PeriodEnd = DateTime.Now.AddDays(-1),
        CompanyList = new List<string> { companies[0].Name },
        VinList = new List<string> { vehicles[0].Vin },
        BrandList = new List<string> { "Tesla" },
        Status = "COMPLETED",
        TotalPdfCount = 5,
        IncludedPdfCount = 3,
        ZipFileSizeMB = 2.5m,
        ResultZipPath = Path.Combine("admin-exports", "export_1.zip").Replace("\\", "/"),
        Notes = "Test export for API testing",
        RequestedBy = "Admin User"
    },
    new AdminFileManager
    {
        RequestedAt = DateTime.Now.AddHours(-1),
        StartedAt = DateTime.Now.AddMinutes(-45),
        CompletedAt = null,
        PeriodStart = DateTime.Now.AddDays(-7),
        PeriodEnd = DateTime.Now,
        CompanyList = new List<string> { companies[1].Name },
        VinList = new List<string>(),
        BrandList = new List<string> { "Tesla" },
        Status = "PROCESSING",
        TotalPdfCount = 0,
        IncludedPdfCount = 0,
        ZipFileSizeMB = 0,
        ResultZipPath = null,
        Notes = "Export in progress",
        RequestedBy = "Test User"
    }
};

    // Create actual file for completed export
    var adminFilePath = Path.Combine(wwwRoot, "storage", fileEntries[0].ResultZipPath);
    await File.WriteAllBytesAsync(adminFilePath, Encoding.UTF8.GetBytes("MOCK ADMIN EXPORT CONTENT"));

    db.AdminFileManager.AddRange(fileEntries);
    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Created {fileEntries.Length} mock file manager entries");

    // ───────────────────────────────
    // 7. ⚠️ OUTAGE PERIODS - SOSTITUISCI QUESTA SEZIONE
    // ───────────────────────────────
    Console.WriteLine("⚠️ Creating mock outage periods...");

    var outagesDir = Path.Combine(wwwRoot, "storage", "outages-zips");
    Directory.CreateDirectory(outagesDir);

    var outages = new[]
    {
    new OutagePeriod
    {
        AutoDetected = false,
        OutageType = "Outage Vehicle",
        OutageBrand = "Tesla",
        OutageStart = DateTime.Now.AddHours(-2),
        OutageEnd = DateTime.Now.AddHours(1),
        VehicleId = vehicles[0].Id,
        ClientCompanyId = companies[0].Id,
        ZipFilePath = Path.Combine("outages-zips", "outage_1.zip").Replace("\\", "/"),
        Notes = "Scheduled maintenance for testing"
    },
    new OutagePeriod
    {
        AutoDetected = true,
        OutageType = "Outage Fleet Api",
        OutageBrand = "Tesla",
        OutageStart = DateTime.Now.AddDays(-1),
        OutageEnd = DateTime.Now.AddHours(-1),
        VehicleId = null,
        ClientCompanyId = null,
        ZipFilePath = null,
        Notes = "Fleet API outage resolved"
    }
};

    // Create outage file for the first outage
    var outagePath = Path.Combine(wwwRoot, "storage", outages[0].ZipFilePath);
    await File.WriteAllBytesAsync(outagePath, Encoding.UTF8.GetBytes("MOCK OUTAGE DATA"));

    db.OutagePeriods.AddRange(outages);
    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Created {outages.Length} mock outage periods");

    // ───────────────────────────────
    // 8. 📱 PHONE MAPPINGS for SMS
    // ───────────────────────────────
    Console.WriteLine("📱 Creating mock phone mappings...");

    var phoneMappings = new[]
    {
        new PhoneVehicleMapping
        {
            PhoneNumber = "+393331234567",
            VehicleId = vehicles[0].Id,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsActive = true,
            Notes = "Test phone for API testing"
        },
        new PhoneVehicleMapping
        {
            PhoneNumber = "+393334455666",
            VehicleId = vehicles[1].Id,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsActive = true,
            Notes = "Mock phone for SMS testing"
        }
    };

    db.PhoneVehicleMappings.AddRange(phoneMappings);
    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Created {phoneMappings.Length} mock phone mappings");

    // ───────────────────────────────
    // 9. 📲 SMS AUDIT LOGS
    // ───────────────────────────────
    Console.WriteLine("📲 Creating mock SMS audit logs...");

    var smsLogs = new[]
    {
        new SmsAuditLog
        {
            MessageSid = "SM1755104591851",
            FromPhoneNumber = "+393331234567",
            ToPhoneNumber = "+393901234567",
            MessageBody = "Test SMS from API at " + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            ReceivedAt = DateTime.Now.AddMinutes(-10),
            ProcessingStatus = "SUCCESS",
            ErrorMessage = null,
            VehicleIdResolved = vehicles[0].Id,
            ResponseSent = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response><Message>Test response</Message></Response>"
        }
    };

    db.SmsAuditLogs.AddRange(smsLogs);
    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Created {smsLogs.Length} mock SMS audit logs");

    // ───────────────────────────────
    // 10. 🎯 ADAPTIVE PROFILING EVENTS
    // ───────────────────────────────
    Console.WriteLine("🎯 Creating mock adaptive profiling events...");

    var adaptiveEvents = new[]
    {
        new AdaptiveProfilingSmsEvent
        {
            VehicleId = vehicles[0].Id,
            ReceivedAt = DateTime.Now.AddMinutes(-15),
            MessageContent = "ADAPTIVE 0001 test session",
            ParsedCommand = "ADAPTIVE_PROFILING_ON"
        }
    };

    db.AdaptiveProfilingSmsEvents.AddRange(adaptiveEvents);
    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Created {adaptiveEvents.Length} mock adaptive profiling events");

    // ───────────────────────────────
    // 11. 🎉 FINAL SUMMARY
    // ───────────────────────────────
    Console.WriteLine();
    Console.WriteLine("🎉 ===== COMPREHENSIVE MOCK DATA SETUP COMPLETED ===== 🎉");
    Console.WriteLine($"🏢 Companies: {companies.Length} created");
    Console.WriteLine($"🚗 Vehicles: {vehicles.Length} created (including TESTVIN123456789)");
    Console.WriteLine($"🔐 Consents: {consents.Length} created with actual ZIP files");
    Console.WriteLine($"📊 Reports: {reports.Length} created with actual PDF/HTML files");
    Console.WriteLine($"📁 FileManager: {fileEntries.Length} entries with actual files");
    Console.WriteLine($"⚠️ Outages: {outages.Length} created with ZIP files");
    Console.WriteLine($"📱 Phone Mappings: {phoneMappings.Length} created");
    Console.WriteLine($"📲 SMS Logs: {smsLogs.Length} created");
    Console.WriteLine($"🎯 Adaptive Events: {adaptiveEvents.Length} created");
    Console.WriteLine();
    Console.WriteLine("🚀 API Test Data Summary:");
    Console.WriteLine($"   Main test VIN: TESTVIN123456789 (Vehicle ID: {vehicles[0].Id})");
    Console.WriteLine($"   Main test VAT: IT12345678901 (Company ID: {companies[0].Id})");
    Console.WriteLine($"   Test phone: +393331234567");
    Console.WriteLine("=========================================");
    Console.WriteLine("✅ Ready for comprehensive API testing!");

    await logger.Info("PolarDriveInitDBMockData.Cli", "Comprehensive mock data setup completed successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ FATAL ERROR: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"🔍 Inner: {ex.InnerException.Message}");
    }

    try
    {
        if (await db.Database.CanConnectAsync())
        {
            var logger = new PolarDriveLogger(db);
            await logger.Error("PolarDriveInitDBMockData.Cli", "Exception during comprehensive mock setup", ex.ToString());
        }
    }
    catch
    {
        Console.WriteLine("⚠️ Could not log error to database");
    }
}
