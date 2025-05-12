using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

var basePath = AppContext.BaseDirectory;
var dbPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "PolarDriveInitDB.Cli", "datapolar.db"));

var wwwRoot = Path.Combine(basePath, "..", "..", "..", "..", "PolarDrive.WebApi", "wwwroot", "companies");

var options = new DbContextOptionsBuilder<PolarDriveDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;

using var db = new PolarDriveDbContext(options);

// ─────────────────────────────────────
// 1. Pulisce tabelle
// ─────────────────────────────────────
db.ClientConsents.RemoveRange(db.ClientConsents);
db.ClientTeslaTokens.RemoveRange(db.ClientTeslaTokens);
db.ClientTeslaVehicles.RemoveRange(db.ClientTeslaVehicles);
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
        Name = "Gamma Energia",
        VatNumber = "00000000003",
        ReferentName = "Sara Verdi",
        ReferentEmail = "s.verdi@gammaenergia.it",
        ReferentMobileNumber = "3289876543",
    }
};

db.ClientCompanies.AddRange(companies);
await db.SaveChangesAsync();

// ─────────────────────────────────────
// 3. Per ogni azienda: crea cartelle + mock Tesla + token + consenso
// ─────────────────────────────────────
int vinCounter = 1;
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

    // 🚘 Mock Tesla
    var vin = $"5YJ3E1EA7KF31{vinCounter++:0000}";
    var vehicle = new ClientTeslaVehicle
    {
        ClientCompanyId = company.Id,
        Vin = vin,
        Model = "Model 3",
        IsActiveFlag = true,
        IsFetchingDataFlag = true,
        FirstActivationAt = DateTime.Today
    };
    db.ClientTeslaVehicles.Add(vehicle);
    await db.SaveChangesAsync();

    // 🔑 Mock Token associato alla Tesla
    var token = new ClientTeslaToken
    {
        TeslaVehicleId = vehicle.Id,
        AccessToken = $"access_token_{vehicle.Id}",
        RefreshToken = $"refresh_token_{vehicle.Id}",
        AccessTokenExpiresAt = DateTime.UtcNow.AddHours(8),
        RefreshTokenExpiresAt = null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    db.ClientTeslaTokens.Add(token);
    await db.SaveChangesAsync();

    // 📝 Mock consenso associato alla Tesla
    var consent = new ClientConsent
    {
        ClientCompanyId = company.Id,
        TeslaVehicleId = vehicle.Id,
        ConsentType = "Consent Activation",
        UploadDate = DateTime.Today,
        ZipFilePath = Path.Combine("companies", $"company-{company.Id}", "consents-zip", "consent_1.zip").Replace("\\", "/"),
        ConsentHash = $"mockhash-{company.Id}-abc123",
        Notes = "Mock consent automatico"
    };
    db.ClientConsents.Add(consent);
    await db.SaveChangesAsync();
}

Console.WriteLine("✅ Dati mock completi e file fisici generati con successo.");