using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

var basePath = AppContext.BaseDirectory;
var dbPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "PolarDriveInitDB.Cli", "datapolar.db"));

var wwwRoot = Path.Combine(basePath, "..", "..", "..", "..", "PolarDrive.WebApi", "wwwroot");

var options = new DbContextOptionsBuilder<PolarDriveDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;

using var db = new PolarDriveDbContext(options);

// ─────────────────────────────────────
// 1. Pulisce tabelle
// ─────────────────────────────────────
db.ClientConsents.RemoveRange(db.ClientConsents);
db.ClientTokens.RemoveRange(db.ClientTokens);
db.ClientVehicles.RemoveRange(db.ClientVehicles);
db.ClientCompanies.RemoveRange(db.ClientCompanies);
await db.SaveChangesAsync();

// ─────────────────────────────────────
// 2. Mock aziende complete
// ─────────────────────────────────────
var companies = new[]
{
    new ClientCompany
    {
        Name = "Paninoteca Rossi",
        VatNumber = "00000000001",
        ReferentName = "Luca Rossi",
        ReferentEmail = "luca@paninotecarossi.com",
        ReferentMobileNumber = "3201234567",
    },
    new ClientCompany
    {
        Name = "TechZone",
        VatNumber = "00000000002",
        ReferentName = "Marco Bianchi",
        ReferentEmail = "marco.b@techzone.it",
        ReferentMobileNumber = "3351234567",
    },
    new ClientCompany
    {
        Name = "DataPolar",
        VatNumber = "00000000003",
        ReferentName = "Tedesco Davide",
        ReferentEmail = "support@datapolar.dev",
        ReferentMobileNumber = "3289876543",
    }
};

db.ClientCompanies.AddRange(companies);
await db.SaveChangesAsync();

// ─────────────────────────────────────
// 3. Per ogni azienda: crea cartelle + mock vehicle + token + consenso
// ─────────────────────────────────────
foreach (var company in companies)
{
    var companyDir = Path.Combine(wwwRoot, $"company-{company.Id}");
    var consentsDir = Path.Combine(companyDir, "consents-zip");
    var historyDir = Path.Combine(companyDir, "history-pdf");
    var reportsDir = Path.Combine(companyDir, "reports-pdf");

    Directory.CreateDirectory(consentsDir);
    Directory.CreateDirectory(historyDir);
    Directory.CreateDirectory(reportsDir);

    // 🔧 File PDF mock
    for (int i = 1; i <= 2; i++)
    {
        await File.WriteAllTextAsync(Path.Combine(historyDir, $"history_{i}.pdf"), "%PDF-1.4\n%empty history\n%%EOF");
        await File.WriteAllTextAsync(Path.Combine(reportsDir, $"report_{i}.pdf"), "%PDF-1.4\n%empty report\n%%EOF");

        var zipPath = Path.Combine(consentsDir, $"consent_{i}.zip");

        // Se il file esiste, lo elimino
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        // Crea ZIP nuovo
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var entry = zip.CreateEntry($"consent_{i}.pdf");
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync("%PDF-1.4\n%empty consent\n%%EOF");
    }
    await db.SaveChangesAsync();
}

// 🚘 Mock Veicoli
var vehicles = new[]
{
        new ClientVehicle
        {
            ClientCompanyId = companies[0].Id,
            Vin = "5YJJ6677544845943",
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
            Vin = "5YJW5451356531830",
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
            Vin = "5YJT8233374058256",
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

// 🔑🔐 Inserisce token + consenso per ciascun veicolo
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
}

// ─────────────────────────────────────
// 3.b Crea ZIP mock usati negli outage
// ─────────────────────────────────────
var zipsDir = Path.Combine(wwwRoot, "zips-outages");
Directory.CreateDirectory(zipsDir);

// 1. zips-outages/20250418-rossi-vehicle01.zip
var zip1Path = Path.Combine(zipsDir, "20250418-rossi-vehicle01.zip");
if (File.Exists(zip1Path)) File.Delete(zip1Path);
using (var zip = ZipFile.Open(zip1Path, ZipArchiveMode.Create))
{
    var entry = zip.CreateEntry("mock_outage_rossi.pdf");
    using var stream = entry.Open();
    using var writer = new StreamWriter(stream);
    await writer.WriteAsync("%PDF-1.4\n%OUTAGE ZIP Rossi\n%%EOF");
}

// 2. zips-outages/20250425-manual-fleetapi.zip
var zip2Path = Path.Combine(zipsDir, "20250425-manual-fleetapi.zip");
if (File.Exists(zip2Path)) File.Delete(zip2Path);
using (var zip = ZipFile.Open(zip2Path, ZipArchiveMode.Create))
{
    var entry = zip.CreateEntry("manual_fleetapi.pdf");
    using var stream = entry.Open();
    using var writer = new StreamWriter(stream);
    await writer.WriteAsync("%PDF-1.4\n%OUTAGE ZIP FleetApi\n%%EOF");
}

// ─────────────────────────────────────
// 4. Inserisce 5 outage mock coerenti
// ─────────────────────────────────────
var outages = new List<OutagePeriod>
{
    new()
    {
        VehicleId = 1,
        ClientCompanyId = 1,
        AutoDetected = true,
        OutageType = "Outage Vehicle",
        OutageBrand = "Tesla",
        CreatedAt = DateTime.Parse("2025-04-20T10:30:00Z"),
        OutageStart = DateTime.Parse("2025-04-18T04:00:00Z"),
        OutageEnd = DateTime.Parse("2025-04-19T18:00:00Z"),
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
        CreatedAt = DateTime.Parse("2025-04-21T09:45:00Z"),
        OutageStart = DateTime.Parse("2025-04-15T00:00:00Z"),
        OutageEnd = DateTime.Parse("2025-04-20T23:50:00Z"),
        Notes = "Outage manuale: veicolo in assistenza per aggiornamento batteria."
    },
    new()
    {
        VehicleId = 3,
        ClientCompanyId = 3,
        AutoDetected = true,
        OutageType = "Outage Vehicle",
        OutageBrand = "Porsche",
        CreatedAt = DateTime.Parse("2025-04-24T07:10:00Z"),
        OutageStart = DateTime.Parse("2025-04-23T22:00:00Z"),
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
        CreatedAt = DateTime.Parse("2025-04-22T12:00:00Z"),
        OutageStart = DateTime.Parse("2025-04-22T08:00:00Z"),
        OutageEnd = DateTime.Parse("2025-04-22T11:30:00Z"),
        Notes = "API ufficiale non rispondeva: HTTP 503 da tutte le richieste."
    },
    new()
    {
        VehicleId = null,
        ClientCompanyId = null,
        AutoDetected = false,
        OutageType = "Outage Fleet Api",
        OutageBrand = "Polestar",
        CreatedAt = DateTime.Parse("2025-04-25T09:00:00Z"),
        OutageStart = DateTime.Parse("2025-04-25T07:00:00Z"),
        OutageEnd = null,
        ZipFilePath = "zips-outages/20250425-manual-fleetapi.zip",
        Notes = "Inserito manualmente: timeout frequenti da client."
    },
};

db.OutagePeriods.AddRange(outages);
await db.SaveChangesAsync();
db.ChangeTracker.Clear();

Console.WriteLine("✅ Dati mock completi e file fisici generati con successo.");