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

// Pulisce tabelle
db.ClientConsents.RemoveRange(db.ClientConsents);
db.ClientTeslaVehicles.RemoveRange(db.ClientTeslaVehicles);
db.ClientCompanies.RemoveRange(db.ClientCompanies);
await db.SaveChangesAsync();

// Mock aziende
var companies = new[]
{
    new ClientCompany { Name = "Paninoteca Rossi", VatNumber = "IT00000000001" },
    new ClientCompany { Name = "TechZone", VatNumber = "IT00000000002" },
    new ClientCompany { Name = "Gamma Energia", VatNumber = "IT00000000003" }
};

db.ClientCompanies.AddRange(companies);
await db.SaveChangesAsync();

// Mock Tesla e consensi
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

    // Crea PDF finti
    for (int i = 1; i <= 2; i++)
    {
        await File.WriteAllTextAsync(Path.Combine(historyDir, $"history_{i}.pdf"), "%PDF-1.4\n%empty history\n%%EOF");
        await File.WriteAllTextAsync(Path.Combine(reportsDir, $"report_{i}.pdf"), "%PDF-1.4\n%empty report\n%%EOF");

        var consentZipPath = Path.Combine(consentsDir, $"consent_{i}.zip");
        using (var zip = ZipFile.Open(consentZipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry($"consent_{i}.pdf");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            await writer.WriteAsync("%PDF-1.4\n%empty consent\n%%EOF");
        }
    }

    // Mock Tesla associata
    var vin = $"5YJMOCKEDVIN{vinCounter++:000}";
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

    // Mock consenso
    var consent = new ClientConsent
    {
        ClientCompanyId = company.Id,
        TeslaVehicleId = vehicle.Id,
        ConsentType = "Consent Activation",
        UploadDate = DateTime.Today,
        ZipFilePath = Path.Combine(consentsDir, "consent_1.zip"),
        ConsentHash = $"mockhash-{company.Id}-abc123",
        Notes = "Mock consent"
    };
    db.ClientConsents.Add(consent);
    await db.SaveChangesAsync();
}

Console.WriteLine("✅ Dati mock e file fisici generati con successo.");