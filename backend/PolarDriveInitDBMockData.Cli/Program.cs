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

// Pulizia iniziale
db.ClientConsents.RemoveRange(db.ClientConsents);
db.ClientTeslaVehicles.RemoveRange(db.ClientTeslaVehicles);
db.ClientCompanies.RemoveRange(db.ClientCompanies);
await db.SaveChangesAsync();

Console.WriteLine("Popolamento DB con mock reali...");

// Inserimento aziende
var companyList = new[]
{
    new ClientCompany { VatNumber = "63123456789", Name = "Paninoteca Rossi S.r.l.", Address = "Via dei Panini 42, Milano", Email = "info@paninotecarossi.it", PecAddress = "pec@paninotecarossi.it", LandlineNumber = "02123456788", ReferentName = "Luca Rossi", ReferentMobileNumber = "3201234567", ReferentEmail = "luca@paninotecarossi.it", ReferentPecAddress = "" },
    new ClientCompany { VatNumber = "78987654321", Name = "Studio Legale Verdi", Address = "Corso Venezia 12, Roma", Email = "contatti@studioverdi.it", PecAddress = "studioverdi@pec.it", LandlineNumber = "06445566714", ReferentName = "Chiara Verdi", ReferentMobileNumber = "3471122334", ReferentEmail = "chiara.verdi@studioverdi.it", ReferentPecAddress = "c.verdi@pec.studioverdi.it" },
    new ClientCompany { VatNumber = "44543216789", Name = "TechZone S.p.A.", Address = "Viale Innovazione 8, Torino", Email = "support@techzone.it", PecAddress = "pec@techzone.it", LandlineNumber = "01177889904", ReferentName = "Marco Bianchi", ReferentMobileNumber = "3359988776", ReferentEmail = "marco.b@techzone.it", ReferentPecAddress = "m.bianchi@pec.techzone.it" },
    new ClientCompany { VatNumber = "56135792468", Name = "Farmacia Centrale S.n.c.", Address = "Piazza della Salute 2, Firenze", Email = "info@farmaciacentrale.it", PecAddress = "farmaciacentrale@pec.it", LandlineNumber = "05522334456", ReferentName = "Giulia Neri", ReferentMobileNumber = "3281239874", ReferentEmail = "g.neri@farmaciacentrale.it", ReferentPecAddress = "giulia.neri@pec.farmacia.it" },
    new ClientCompany { VatNumber = "75112358132", Name = "Alfa Costruzioni S.r.l.", Address = "Via del Mattone 100, Bologna", Email = "segreteria@alfacostruzioni.it", PecAddress = "pec@alfacostruzioni.it", LandlineNumber = "05199887783", ReferentName = "Federico Costa", ReferentMobileNumber = "3294455667", ReferentEmail = "f.costa@alfacostruzioni.it", ReferentPecAddress = "federico.costa@pec.alfacostruzioni.it" },
    new ClientCompany { VatNumber = "72192837465", Name = "Gamma Energia s.n.c.", Address = "Via Solare 15, Napoli", Email = "contatti@gammaenergia.it", PecAddress = "pec@gammaenergia.it", LandlineNumber = "08144556677", ReferentName = "Anna Romano", ReferentMobileNumber = "3402211334", ReferentEmail = "anna@gammaenergia.it", ReferentPecAddress = "a.romano@pec.gammaenergia.it" },
    new ClientCompany { VatNumber = "92314159265", Name = "Studio Architetti Bassi e Longhi", Address = "Via Architettura 7, Bari", Email = "info@bassilonghi.it", PecAddress = "pec@bassilonghi.it", LandlineNumber = "08033221138", ReferentName = "Leonardo Longhi", ReferentMobileNumber = "3387766554", ReferentEmail = "l.longhi@bassilonghi.it", ReferentPecAddress = "leo.longhi@pec.bassilonghi.it" },
    new ClientCompany { VatNumber = "82010203040", Name = "Bottega del Caffè di Mario e C.", Address = "Via Cavour 50, Palermo", Email = "bottega@marioecaffe.it", PecAddress = "pec@bottegacaffe.it", LandlineNumber = "09122334401", ReferentName = "Mario Grillo", ReferentMobileNumber = "3315566778", ReferentEmail = "mario@marioecaffe.it", ReferentPecAddress = "m.grillo@pec.bottegacaffe.it" },
    new ClientCompany { VatNumber = "46001100110", Name = "NextData Analytics S.r.l.", Address = "Via Digitale 3, Trento", Email = "contact@nextdata.it", PecAddress = "pec@nextdata.it", LandlineNumber = "04618877664", ReferentName = "Serena Bellini", ReferentMobileNumber = "3391122445", ReferentEmail = "serena.bellini@nextdata.it", ReferentPecAddress = "s.bellini@pec.nextdata.it" },
    new ClientCompany { VatNumber = "83999888776", Name = "Autofficina Turbo S.a.s.", Address = "Via del Motore 21, Genova", Email = "turbo@autofficina.it", PecAddress = "pec@autofficina.it", LandlineNumber = "01033445577", ReferentName = "Gabriele Ferro", ReferentMobileNumber = "3489988771", ReferentEmail = "g.ferro@autofficina.it", ReferentPecAddress = "gabriele.ferro@pec.autofficina.it" }
};

db.ClientCompanies.AddRange(companyList);
await db.SaveChangesAsync();

// Inserimento veicoli
var teslaList = new[]
{
    ("5YJ3E1EA7KF317001", "Model 3"),
    ("5YJSA1E26HF000199", "Model S"),
    ("5YJXCDE45GF011123", "Model X"),
    ("5YJYGDEE0MF005555", "Model 3"),
    ("7SAYGDEE9PF123999", "Model Y"),
    ("5YJ3E1EA2HF001234", "Model Y"),
    ("5YJSA1E20FF000777", "Model 3"),
    ("LRWYGDEE7PC888888", "Model S"),
    ("5YJXCDE23JF055432", "Model Y"),
    ("5YJSA1E26HF000101", "Model X")
};

var vehicleList = new List<ClientTeslaVehicle>();

for (int i = 0; i < teslaList.Length; i++)
{
    var vehicle = new ClientTeslaVehicle
    {
        ClientCompanyId = companyList[i].Id,
        Vin = teslaList[i].Item1,
        Model = teslaList[i].Item2,
        IsActiveFlag = true,
        IsFetchingDataFlag = true,
        FirstActivationAt = DateTime.UtcNow
    };
    vehicleList.Add(vehicle);
}

db.ClientTeslaVehicles.AddRange(vehicleList);
await db.SaveChangesAsync();

// Inserimento consensi mock
var consentTypes = new[] {
    "Consent Activation",
    "Consent Deactivation",
    "Consent Stop Data Fetching",
    "Consent Reactivation"
};

var consentList = new List<ClientConsent>();
for (int i = 0; i < companyList.Length; i++)
{
    consentList.Add(new ClientConsent
    {
        ClientCompanyId = companyList[i].Id,
        TeslaVehicleId = vehicleList[i].Id,
        UploadDate = new DateTime(2024, 3, 12).AddDays(i * 2),
        ZipFilePath = $"pdfs/consents/{vehicleList[i].Vin}.zip",
        ConsentHash = $"mockhash{i + 1:D2}abcdef0123456789{i + 1:D2}",
        ConsentType = consentTypes[i % consentTypes.Length]
    });
}

db.ClientConsents.AddRange(consentList);
await db.SaveChangesAsync();

Console.WriteLine("✅ Mock inseriti con successo!");
