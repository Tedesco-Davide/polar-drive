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
var logger = new PolarDriveLogger(db);

try
{
    await logger.Info("PolarDriveInitDBMockData.Cli", "Starting mock DB cleanup");

    // ───────────────────────────────
    // 1. Pulisce tabelle
    // ───────────────────────────────
    await DbMockDataHelper.ClearMockDataAsync(db);
    await logger.Info("PolarDriveInitDBMockData.Cli", "Cleared all client-related tables");

    // ───────────────────────────────
    // 2. Mock aziende
    // ───────────────────────────────
    var companies = new[]
    {
        new ClientCompany { Name = "Paninoteca Rossi", VatNumber = "00000000001", ReferentName = "Luca Rossi", ReferentEmail = "luca@paninotecarossi.com", ReferentMobileNumber = "3201234567" },
    };

    db.ClientCompanies.AddRange(companies);
    await db.SaveChangesAsync();
    await logger.Info("PolarDriveInitDBMockData.Cli", $"Inserted {companies.Length} mock companies");

    // ───────────────────────────────
    // 3. Setup directories + (opz.) ZIP mock
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

        // Toggle ZIP generation
        bool generaZipMock = false;

        if (generaZipMock)
        {
            await logger.Info("PolarDriveInitDBMockData.Cli", $"ZIP generation enabled for company ID {company.Id}");

            for (int i = 1; i <= 2; i++)
            {
                var historyPdfPath = Path.Combine(historyDir, $"history_{i}.pdf");
                var reportPdfPath = Path.Combine(reportsDir, $"report_{i}.pdf");
                var zipPath = Path.Combine(consentsDir, $"consent_{i}.zip");

                await File.WriteAllTextAsync(historyPdfPath, "%PDF-1.4\n%empty history\n%%EOF");
                await File.WriteAllTextAsync(reportPdfPath, "%PDF-1.4\n%empty report\n%%EOF");

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                    await logger.Info("PolarDriveInitDBMockData.Cli", $"Existing ZIP deleted: {zipPath}");
                }

                using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                var entry = zip.CreateEntry($"consent_{i}.pdf");
                using var stream = entry.Open();
                using var writer = new StreamWriter(stream);
                await writer.WriteAsync("%PDF-1.4\n%empty consent\n%%EOF");

                await logger.Info("PolarDriveInitDBMockData.Cli", $"ZIP mock created: {zipPath}");
            }

            await logger.Info("PolarDriveInitDBMockData.Cli", $"ZIP generation completed for company ID {company.Id}");
        }
    }

    // ───────────────────────────────
    // 4. 🚘 Mock Vehicles
    // ───────────────────────────────
    var allVehicles = new List<ClientVehicle>();
    var vehicles = new[]
    {
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
    };

    db.ClientVehicles.AddRange(vehicles);
    await db.SaveChangesAsync();
    allVehicles.AddRange(vehicles);

    await logger.Info(
        "PolarDriveInitDBMockData.Cli",
        "Inserted mock vehicles",
        string.Join(", ", vehicles.Select(v => $"{v.Brand} {v.Model} (VIN: {v.Vin})"))
    );

    // ───────────────────────────────
    // 6. 🔑🔐 Consent for vehicles that are authorized
    // ───────────────────────────────
    for (int i = 0; i < vehicles.Length; i++)
    {
        var vehicle = vehicles[i];
        var currentCompany = companies[i];

        // ✅ Inserisco SEMPRE il consenso ZIP
        var consent = new ClientConsent
        {
            ClientCompanyId = currentCompany.Id,
            VehicleId = vehicle.Id,
            ConsentType = "Consent Activation",
            UploadDate = DateTime.Today,
            ZipFilePath = Path.Combine("companies", $"company-{currentCompany.Id}", "consents-zip", "consent_1.zip").Replace("\\", "/"),
            ConsentHash = $"mockhash-{currentCompany.Id}-abc123",
            Notes = "Mock consent automatico"
        };
        db.ClientConsents.Add(consent);
        await db.SaveChangesAsync();
    }

    // ───────────────────────────────
    // 7. Create mock ZIPs used in outages
    // ───────────────────────────────
    var zipsDir = Path.Combine(wwwRoot, "zips-outages");
    Directory.CreateDirectory(zipsDir);
    await logger.Info("PolarDriveInitDBMockData.Cli", $"ZIP outage directory ensured: {zipsDir}");

    // 7.1 zips-outages/20250418-rossi-vehicle01.zip
    var zip1Path = Path.Combine(zipsDir, "20250418-rossi-vehicle01.zip");
    if (File.Exists(zip1Path))
    {
        File.Delete(zip1Path);
        await logger.Info("PolarDriveInitDBMockData.Cli", $"Deleted existing ZIP file: {zip1Path}");
    }
    using (var zip = ZipFile.Open(zip1Path, ZipArchiveMode.Create))
    {
        var entry = zip.CreateEntry("mock_outage_rossi.pdf");
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync("%PDF-1.4\n%OUTAGE ZIP Rossi\n%%EOF");
        await logger.Info("PolarDriveInitDBMockData.Cli", $"Created new ZIP for outage Rossi: {zip1Path}");
    }

    // 7.2 zips-outages/20250425-manual-fleetapi.zip
    var zip2Path = Path.Combine(zipsDir, "20250425-manual-fleetapi.zip");
    if (File.Exists(zip2Path))
    {
        File.Delete(zip2Path);
        await logger.Info("PolarDriveInitDBMockData.Cli", $"Deleted existing ZIP file: {zip2Path}");
    }
    using (var zip = ZipFile.Open(zip2Path, ZipArchiveMode.Create))
    {
        var entry = zip.CreateEntry("manual_fleetapi.pdf");
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync("%PDF-1.4\n%OUTAGE ZIP FleetApi\n%%EOF");
        await logger.Info("PolarDriveInitDBMockData.Cli", $"Created new ZIP for outage FleetApi: {zip2Path}");
    }

    // ───────────────────────────────
    // 8. Insert 5 consistent mock outage records
    // ───────────────────────────────
    var rand = new Random();
    int offset = rand.Next(1, 10000);
    var outages = new List<OutagePeriod>
    {
        new()
        {
            VehicleId = allVehicles[0].Id,
            ClientCompanyId = allVehicles[0].ClientCompanyId,
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
            VehicleId = allVehicles[0].Id,
            ClientCompanyId = allVehicles[1].ClientCompanyId,
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
            VehicleId = allVehicles[0].Id,
            ClientCompanyId = allVehicles[2].ClientCompanyId,
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
    await logger.Info("PolarDriveInitDBMockData.Cli", $"Inserted {outages.Count} mock outage records.");

    await logger.Info("PolarDriveInitDBMockData.Cli", "Mock VehicleData successfully inserted and change tracker cleared.");
    await logger.Info("PolarDriveInitDBMockData.Cli", "✅ Mock data initialization completed successfully!");

    Console.WriteLine("🏁 Mock data initialization completed successfully!");
}
catch (Exception ex)
{
    await logger.Error("PolarDriveInitDBMockData.Cli", "Exception during mock setup", ex.ToString());
    Console.WriteLine($"❌ FATAL ERROR: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"🔍 Inner: {ex.InnerException.Message}");
    }
}