using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.Text;
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
    await logger.Info("PolarDriveInitDBMockData.Cli", "Starting comprehensive mock DB setup for API tests");

    Console.WriteLine("🧹 Starting full cleanup and comprehensive mock data setup...");

    // ───────────────────────────────
    // 1. ✅ CLEANUP SMART - FIX SQL SYNTAX
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
                // ✅ FIX: Usa ExecuteSqlRaw invece di ExecuteSqlAsync con interpolazione
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
    // 2. 🏢 COMPANIES
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
    // 3. 🚘 VEHICLES
    // ───────────────────────────────
    Console.WriteLine("🚗 Creating mock vehicles...");

    var vehicles = new[]
    {
        new ClientVehicle
        {
            ClientCompanyId = companies[0].Id,
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
            ClientCompanyId = companies[1].Id,
            Vin = "5YJ3000000NEXUS01",
            FuelType = "Electric",
            Brand = "Tesla",
            Model = "Model 3",
            Trim = "Long Range",
            Color = "Ultra Red",
            IsActiveFlag = false,
            IsFetchingDataFlag = false,
            ClientOAuthAuthorized = true,
            FirstActivationAt = DateTime.Now.AddDays(-5),
            CreatedAt = DateTime.Now.AddDays(-5),
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
            FirstActivationAt = DateTime.Now.AddMinutes(-30),
            CreatedAt = DateTime.Now.AddHours(-1),
            ReferentName = "Mario Bianchi",
            ReferentEmail = "mario.bianchi@paninotecarossi.com",
            ReferentMobileNumber = "+393209876543",
        }
    };

    db.ClientVehicles.AddRange(vehicles);
    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Created {vehicles.Length} mock vehicles");

    // ───────────────────────────────
    // 4. 🔐 CLIENT CONSENTS
    // ───────────────────────────────
    Console.WriteLine("🔐 Creating mock consents with files...");

    // Create storage directories
    var storageBase = Path.Combine(wwwRoot, "storage", "companies");
    foreach (var company in companies)
    {
        var companyDir = Path.Combine(storageBase, $"company-{company.Id}", "consents-zips");
        Directory.CreateDirectory(companyDir);
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
    // 4.1. 🔐 CREATE REAL ZIP FILES FOR CONSENTS
    // ───────────────────────────────
    Console.WriteLine("🗜️ Creating real ZIP files for consents...");

    foreach (var consent in consents)
    {
        if (!string.IsNullOrEmpty(consent.ZipFilePath))
        {
            var fullZipPath = Path.Combine(wwwRoot, "storage", consent.ZipFilePath);
            var zipDir = Path.GetDirectoryName(fullZipPath);
            Directory.CreateDirectory(zipDir!);

            // ✅ FIX: Chiudi correttamente i ZIP stream
            await CreateMockConsentZip(fullZipPath, consent.ClientCompanyId, consent.VehicleId);
            Console.WriteLine($"✅ Created real ZIP: {consent.ZipFilePath}");
        }
    }

    // ───────────────────────────────
    // 5. 📊 PDF REPORTS
    // ───────────────────────────────
    Console.WriteLine("📊 Creating mock PDF reports...");

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
            GeneratedAt = null,
            Status = "Processing",
            Notes = "Monthly report in progress",
            RegenerationCount = 1
        }
    };

    db.PdfReports.AddRange(reports);
    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Created {reports.Length} mock PDF reports");

    // Create actual files for completed reports
    foreach (var report in reports)
    {
        if (report.GeneratedAt.HasValue)
        {
            var pdfPath = Path.Combine(wwwRoot, "storage", "reports", $"report_{report.Id}.pdf");
            await File.WriteAllBytesAsync(pdfPath, GenerateMockPdfBytes($"Report {report.Id}"));

            var htmlPath = Path.Combine(wwwRoot, "storage", "reports", $"report_{report.Id}.html");
            var htmlContent = GenerateMockReportHtml($"Report {report.Id}", report.ClientCompanyId, report.ClientVehicleId);
            await File.WriteAllTextAsync(htmlPath, htmlContent);

            Console.WriteLine($"✅ Created files for report {report.Id}: PDF & HTML");
        }
    }

    // ───────────────────────────────
    // 6. 📁 ADMIN FILE MANAGER
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

    // Create actual files for completed exports
    foreach (var entry in fileEntries.Where(e => !string.IsNullOrEmpty(e.ResultZipPath)))
    {
        var adminFilePath = Path.Combine(wwwRoot, "storage", entry.ResultZipPath!);
        await File.WriteAllBytesAsync(adminFilePath, Encoding.UTF8.GetBytes("MOCK ADMIN EXPORT CONTENT"));
        Console.WriteLine($"✅ Created admin file: {entry.ResultZipPath}");
    }

    db.AdminFileManager.AddRange(fileEntries);
    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Created {fileEntries.Length} mock file manager entries");

    // ───────────────────────────────
    // 7. ⚠️ OUTAGE PERIODS
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
        },
        new OutagePeriod
        {
            AutoDetected = false,
            OutageType = "Outage Vehicle",
            OutageBrand = "Tesla",
            OutageStart = DateTime.Now.AddDays(-1),
            OutageEnd = DateTime.Now.AddHours(-2),
            VehicleId = vehicles[1].Id,
            ClientCompanyId = companies[1].Id,
            ZipFilePath = Path.Combine("outages-zips", "outage_2.zip").Replace("\\", "/"),
            Notes = "Test outage for validation"
        }
    };

    db.OutagePeriods.AddRange(outages);
    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Created {outages.Length} mock outage periods");

    // Create real outage ZIP files
    Console.WriteLine("🗜️ Creating real outage ZIP files...");
    foreach (var outage in outages.Where(o => !string.IsNullOrEmpty(o.ZipFilePath)))
    {
        var fullZipPath = Path.Combine(wwwRoot, "storage", outage.ZipFilePath!);
        var zipDir = Path.GetDirectoryName(fullZipPath);
        Directory.CreateDirectory(zipDir!);

        await CreateMockOutageZip(fullZipPath, outage.Id);
        Console.WriteLine($"✅ Created real outage ZIP: {outage.ZipFilePath}");
    }

    // ───────────────────────────────
    // 8. 📱 PHONE MAPPINGS, SMS LOGS, ADAPTIVE EVENTS
    // ───────────────────────────────
    Console.WriteLine("📱 Creating phone mappings, SMS logs, and adaptive events...");

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

    db.PhoneVehicleMappings.AddRange(phoneMappings);
    db.SmsAuditLogs.AddRange(smsLogs);
    db.AdaptiveProfilingSmsEvents.AddRange(adaptiveEvents);
    await db.SaveChangesAsync();

    Console.WriteLine($"✅ Created {phoneMappings.Length} phone mappings");
    Console.WriteLine($"✅ Created {smsLogs.Length} SMS logs");
    Console.WriteLine($"✅ Created {adaptiveEvents.Length} adaptive events");

    // ───────────────────────────────
    // 🎉 FINAL SUMMARY
    // ───────────────────────────────
    Console.WriteLine();
    Console.WriteLine("🎉 ===== COMPREHENSIVE MOCK DATA SETUP COMPLETED ===== 🎉");
    Console.WriteLine($"🏢 Companies: {companies.Length} created");
    Console.WriteLine($"🚗 Vehicles: {vehicles.Length} created (including TESTVIN123456789)");
    Console.WriteLine($"🔐 Consents: {consents.Length} created with actual ZIP files");
    Console.WriteLine($"📊 Reports: {reports.Length} created");
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

// ───────────────────────────────
// ✅ HELPER METHODS - CORRETTI
// ───────────────────────────────

static byte[] GenerateMockPdfBytes(string title)
{
    var pdfContent = $@"%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
>>
endobj

2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj

3 0 obj
<<
/Type /Page
/Parent 2 0 R
/MediaBox [0 0 612 792]
/Contents 4 0 R
>>
endobj

4 0 obj
<<
/Length 44
>>
stream
BT
/F1 12 Tf
100 700 Td
({title}) Tj
ET
endstream
endobj

xref
0 5
0000000000 65535 f 
0000000010 00000 n 
0000000079 00000 n 
0000000173 00000 n 
0000000301 00000 n 
trailer
<<
/Size 5
/Root 1 0 R
>>
startxref
398
%%EOF";

    return System.Text.Encoding.ASCII.GetBytes(pdfContent);
}

static string GenerateMockReportHtml(string title, int companyId, int vehicleId)
{
    return $@"<!DOCTYPE html>
<html>
<head>
    <title>{title}</title>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; }}
        .header {{ background: #f0f0f0; padding: 20px; border-radius: 5px; }}
        .content {{ margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>{title}</h1>
        <p>Company ID: {companyId}</p>
        <p>Vehicle ID: {vehicleId}</p>
        <p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
    </div>
    <div class='content'>
        <h2>Mock Report Content</h2>
        <p>This is a mock report generated for testing purposes.</p>
        <ul>
            <li>Status: Active</li>
            <li>Data Points: 1,234</li>
            <li>Last Update: {DateTime.Now:yyyy-MM-dd}</li>
        </ul>
    </div>
</body>
</html>";
}

// ✅ FIX: USING STATEMENTS CORRETTI PER ZIP
static async Task CreateMockConsentZip(string zipPath, int companyId, int vehicleId)
{
    using var fileStream = new FileStream(zipPath, FileMode.Create);
    using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

    // Consent form
    var consentEntry = archive.CreateEntry("consent_form.pdf");
    using (var consentStream = consentEntry.Open())
    {
        var consentContent = GenerateMockPdfBytes($"Consent Form - Company {companyId} - Vehicle {vehicleId}");
        await consentStream.WriteAsync(consentContent, 0, consentContent.Length);
    } // ✅ Stream chiuso automaticamente

    // Metadata
    var metadataEntry = archive.CreateEntry("metadata.json");
    using (var metadataStream = metadataEntry.Open())
    {
        var metadata = $@"{{
    ""companyId"": {companyId},
    ""vehicleId"": {vehicleId},
    ""createdAt"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ssZ}"",
    ""type"": ""consent"",
    ""version"": ""1.0""
}}";
        var metadataBytes = System.Text.Encoding.UTF8.GetBytes(metadata);
        await metadataStream.WriteAsync(metadataBytes, 0, metadataBytes.Length);
    } // ✅ Stream chiuso automaticamente
}

static async Task CreateMockOutageZip(string zipPath, int outageId)
{
    using var fileStream = new FileStream(zipPath, FileMode.Create);
    using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

    // Log file
    var logEntry = archive.CreateEntry("outage_log.txt");
    using (var logStream = logEntry.Open())
    {
        var logContent = $@"OUTAGE LOG - ID: {outageId}
==========================
Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
Type: Vehicle Outage
Status: Resolved
Duration: 2h 15m
Cause: Scheduled maintenance
Actions: System restart, data sync
";
        var logBytes = System.Text.Encoding.UTF8.GetBytes(logContent);
        await logStream.WriteAsync(logBytes, 0, logBytes.Length);
    } // ✅ Stream chiuso automaticamente

    // Diagnostics file
    var diagEntry = archive.CreateEntry("diagnostics.json");
    using (var diagStream = diagEntry.Open())
    {
        var diagContent = $@"{{
    ""outageId"": {outageId},
    ""startTime"": ""{DateTime.Now.AddHours(-2):yyyy-MM-ddTHH:mm:ssZ}"",
    ""endTime"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ssZ}"",
    ""affectedSystems"": [""data_collection"", ""reporting""],
    ""resolution"": ""maintenance_complete""
}}";
        var diagBytes = System.Text.Encoding.UTF8.GetBytes(diagContent);
        await diagStream.WriteAsync(diagBytes, 0, diagBytes.Length);
    } // ✅ Stream chiuso automaticamente
}