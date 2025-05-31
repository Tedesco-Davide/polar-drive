using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDriveInitDBMockData.Cli;
using System.IO.Compression;
using static PolarDrive.Data.DbContexts.DbInitHelper;

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
        new ClientCompany { Name = "TechZone", VatNumber = "00000000002", ReferentName = "Marco Bianchi", ReferentEmail = "marco.b@techzone.it", ReferentMobileNumber = "3351234567" },
        new ClientCompany { Name = "DataPolar", VatNumber = "00000000003", ReferentName = "Tedesco Davide", ReferentEmail = "support@datapolar.dev", ReferentMobileNumber = "3289876543" }
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

        await db.SaveChangesAsync();

        // 🚘 Mock Vehicles
        var suffix = DateTime.UtcNow.Ticks.ToString()[10..];
        var vehicles = new[]
        {
            new ClientVehicle
            {
                ClientCompanyId = companies[0].Id,
                Vin = $"5YJJ667754484{suffix}",
                FuelType = "Electric",
                Brand = "Tesla",
                Model = "Model 3",
                Trim = "Long Range",
                Color = "Ultra Red",
                IsActiveFlag = true,
                IsFetchingDataFlag = true,
                FirstActivationAt = DateTime.Today,
                CreatedAt = DateTime.UtcNow
            },
            new ClientVehicle
            {
                ClientCompanyId = companies[1].Id,
                Vin = $"5YJW545135653{suffix}",
                FuelType = "Electric",
                Brand = "Polestar",
                Model = "Polestar 4",
                Trim = "Long range Single motor",
                Color = "Snow",
                IsActiveFlag = true,
                IsFetchingDataFlag = true,
                FirstActivationAt = DateTime.Today,
                CreatedAt = DateTime.UtcNow
            },
            new ClientVehicle
            {
                ClientCompanyId = companies[2].Id,
                Vin = $"5YJT823337405{suffix}",
                FuelType = "Combustion",
                Brand = "Porsche",
                Model = "718 Cayman",
                Trim = "GT4RS",
                Color = "Racing Yellow",
                IsActiveFlag = true,
                IsFetchingDataFlag = true,
                FirstActivationAt = DateTime.Today,
                CreatedAt = DateTime.UtcNow
            }
        };

        db.ClientVehicles.AddRange(vehicles);
        await db.SaveChangesAsync();

        await logger.Info(
            "PolarDriveInitDBMockData.Cli",
            "Inserted mock vehicles",
            string.Join(", ", vehicles.Select(v => $"{v.Brand} {v.Model} (VIN: {v.Vin})"))
        );

        // 📄 Mock PDF Reports
        var reports = new List<PdfReport>
        {
            new()
            {
                ClientCompanyId = companies[0].Id,
                ClientVehicleId = vehicles[0].Id,
                ReportPeriodStart = new DateTime(2025, 4, 1),
                ReportPeriodEnd = new DateTime(2025, 4, 30),
                GeneratedAt = DateTime.UtcNow,
                Notes = "Report mensile standard aprile 2025"
            },
            new()
            {
                ClientCompanyId = companies[1].Id,
                ClientVehicleId = vehicles[1].Id,
                ReportPeriodStart = new DateTime(2025, 4, 1),
                ReportPeriodEnd = new DateTime(2025, 4, 30),
                GeneratedAt = DateTime.UtcNow,
                Notes = "Primo report per veicolo Polestar"
            },
            new()
            {
                ClientCompanyId = companies[2].Id,
                ClientVehicleId = vehicles[2].Id,
                ReportPeriodStart = new DateTime(2025, 4, 1),
                ReportPeriodEnd = new DateTime(2025, 4, 30),
                GeneratedAt = DateTime.UtcNow,
                Notes = "Monitoraggio uso esclusivo veicolo sportivo"
            }
        };

        db.PdfReports.AddRange(reports);
        await db.SaveChangesAsync();

        await logger.Info(
            "PolarDriveInitDBMockData.Cli",
            "Inserted mock PDF reports",
            string.Join(", ", reports.Select(r =>
                $"CompanyId={r.ClientCompanyId}, VehicleId={r.ClientVehicleId}, Period={r.ReportPeriodStart:yyyy-MM-dd}→{r.ReportPeriodEnd:yyyy-MM-dd}"
            ))
        );

        // 🔑🔐 Insert token + consent for each vehicle
        for (int i = 0; i < vehicles.Length; i++)
        {
            var vehicle = vehicles[i];
            var currentCompany = companies[i];

            var token = new ClientToken
            {
                VehicleId = vehicle.Id,
                AccessToken = $"access_token_{vehicle.Id}",
                RefreshToken = $"refresh_token_{vehicle.Id}",
                AccessTokenExpiresAt = DateTime.UtcNow.AddHours(8),
                RefreshTokenExpiresAt = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.ClientTokens.Add(token);

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

            await logger.Info(
                "PolarDriveInitDBMockData.Cli",
                "Inserted mock token and consent for vehicle",
                $"CompanyId={currentCompany.Id}, VehicleId={vehicle.Id}, VIN={vehicle.Vin}, ConsentHash={consent.ConsentHash}"
            );
        }

        // ───────────────────────────────────────────────────────────────────────────────────────────────────────────────
        // 3.b Create mock ZIPs used in outages
        // ───────────────────────────────────────────────────────────────────────────────────────────────────────────────
        var zipsDir = Path.Combine(wwwRoot, "zips-outages");
        Directory.CreateDirectory(zipsDir);
        await logger.Info("PolarDriveInitDBMockData.Cli", $"ZIP outage directory ensured: {zipsDir}");

        // 1. zips-outages/20250418-rossi-vehicle01.zip
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

        // 2. zips-outages/20250425-manual-fleetapi.zip
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

        // ───────────────────────────────────────────────────────────────────────────────────────────────────────────────
        // 4. Insert 5 consistent mock outage records
        // ───────────────────────────────────────────────────────────────────────────────────────────────────────────────
        var rand = new Random();
        int offset = rand.Next(1, 10000);
        var outages = new List<OutagePeriod>
        {
            new()
            {
                VehicleId = 1,
                ClientCompanyId = 1,
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
                VehicleId = 2,
                ClientCompanyId = 2,
                AutoDetected = false,
                OutageType = "Outage Vehicle",
                OutageBrand = "Polestar",
                CreatedAt = DateTime.Parse("2025-04-21T09:45:00Z").AddSeconds(offset),
                OutageStart = DateTime.Parse("2025-04-15T00:00:00Z").AddSeconds(offset),
                OutageEnd = DateTime.Parse("2025-04-20T23:50:00Z").AddSeconds(offset),
                Notes = "Outage manuale: veicolo in assistenza per aggiornamento batteria."
            },
            new()
            {
                VehicleId = 3,
                ClientCompanyId = 3,
                AutoDetected = true,
                OutageType = "Outage Vehicle",
                OutageBrand = "Porsche",
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

        // ───────────────────────────────────────────────────────────────────────────────────────────────────────────────
        // 5. Insert mock VehicleData successfully for all vehicles
        // ───────────────────────────────────────────────────────────────────────────────────────────────────────────────

        var random = new Random();
        var startDate = DateTime.Today.AddDays(-30);
        var totalHours = 30 * 24;

        foreach (var vehicle in vehicles)
        {
            for (int i = 0; i < totalHours; i++)
            {
                var ts = startDate.AddHours(i);
                var rawJson = FakeTeslaJsonDataFetch.GenerateRawVehicleJson(ts, random);

                db.VehiclesData.Add(new VehicleData
                {
                    VehicleId = vehicle.Id,
                    Timestamp = ts,
                    RawJson = rawJson
                });
            }

            await logger.Info("PolarDriveInitDBMockData.Cli", $"Mock VehicleData generated for vehicle VIN: {vehicle.Vin}, total hours: {totalHours}");
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await logger.Info("PolarDriveInitDBMockData.Cli", "Mock VehicleData successfully inserted and change tracker cleared.");
    }
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