using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

var basePath = AppContext.BaseDirectory;
var relativePath = Path.Combine("..", "..", "..", "..", "PolarDriveInitDB.Cli", "datapolar.db");
var dbPath = Path.GetFullPath(Path.Combine(basePath, relativePath));
Console.WriteLine("Sto usando il DB: " + dbPath);

var options = new DbContextOptionsBuilder<PolarDriveDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;

using var db = new PolarDriveDbContext(options);

// Pulisce prima di inserire
db.ClientTeslaVehicles.RemoveRange(db.ClientTeslaVehicles);
db.ClientCompanies.RemoveRange(db.ClientCompanies);
await db.SaveChangesAsync();

Console.WriteLine("Popolamento DB con mock reali...");

// Lista mock
var companies = new[]
{
    new { Name = "Paninoteca Rossi S.r.l.", PIVA = "63123456789", Email = "info@paninotecarossi.it", VIN = "5YJ3E1EA7KF317001", Model = "Model 3", IsActive = true, IsFetching = true },
    new { Name = "Studio Legale Verdi", PIVA = "78987654321", Email = "contatti@studioverdi.it", VIN = "5YJSA1E26HF000199", Model = "Model S", IsActive = true, IsFetching = true },
    new { Name = "TechZone S.p.A.", PIVA = "44543216789", Email = "support@techzone.it", VIN = "5YJXCDE45GF011123", Model = "Model X", IsActive = true, IsFetching = true },
    new { Name = "Farmacia Centrale S.n.c.", PIVA = "56135792468", Email = "info@farmaciacentrale.it", VIN = "5YJYGDEE0MF005555", Model = "Model 3", IsActive = true, IsFetching = true },
    new { Name = "Alfa Costruzioni S.r.l.", PIVA = "75112358132", Email = "segreteria@alfacostruzioni.it", VIN = "7SAYGDEE9PF123999", Model = "Model Y", IsActive = true, IsFetching = true },
    new { Name = "Gamma Energia s.n.c.", PIVA = "72192837465", Email = "contatti@gammaenergia.it", VIN = "5YJ3E1EA2HF001234", Model = "Model Y", IsActive = true, IsFetching = true },
    new { Name = "Studio Architetti Bassi e Longhi", PIVA = "92314159265", Email = "info@bassilonghi.it", VIN = "5YJSA1E20FF000777", Model = "Model 3", IsActive = true, IsFetching = true },
    new { Name = "Bottega del Caffè di Mario e C.", PIVA = "82010203040", Email = "bottega@marioecaffe.it", VIN = "LRWYGDEE7PC888888", Model = "Model S", IsActive = true, IsFetching = true },
    new { Name = "NextData Analytics S.r.l.", PIVA = "46001100110", Email = "contact@nextdata.it", VIN = "5YJXCDE23JF055432", Model = "Model Y", IsActive = true, IsFetching = true },
    new { Name = "Autofficina Turbo S.a.s.", PIVA = "83999888776", Email = "turbo@autofficina.it", VIN = "5YJSA1E26HF000101", Model = "Model X", IsActive = true, IsFetching = true }
};

// Popola
foreach (var c in companies)
{
    var company = new ClientCompany
    {
        Name = c.Name,
        VatNumber = c.PIVA,
        Email = c.Email
    };

    db.ClientCompanies.Add(company);
    await db.SaveChangesAsync(); // necessario per ottenere l'ID

    // ✅ Verifica difensiva per evitare VIN duplicati
    if (!db.ClientTeslaVehicles.Any(v => v.Vin == c.VIN))
    {
        db.ClientTeslaVehicles.Add(new ClientTeslaVehicle
        {
            ClientCompanyId = company.Id,
            Vin = c.VIN,
            Model = c.Model,
            IsActiveFlag = c.IsActive,
            IsFetchingDataFlag = c.IsFetching,
            FirstActivationAt = DateTime.UtcNow
        });
    }
    else
    {
        Console.WriteLine($"⚠️ VIN già presente, salto: {c.VIN}");
    }
}

await db.SaveChangesAsync();
Console.WriteLine("✅ Mock inseriti con successo!");