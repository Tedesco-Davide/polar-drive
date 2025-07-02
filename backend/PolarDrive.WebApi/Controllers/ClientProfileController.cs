using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientProfileController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly PolarDriveLogger _logger;
    private readonly string _reportsStoragePath;

    public ClientProfileController(PolarDriveDbContext db)
    {
        _db = db;
        _logger = new PolarDriveLogger(db);
        _reportsStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "storage", "client-profiles");
    }

    /// <summary>
    /// Ottiene i dati del profilo cliente senza generare il PDF (per preview o debug)
    /// </summary>
    [HttpGet("{companyId}/profile-data")]
    public async Task<ActionResult<ClientProfileData>> GetClientProfileData(int companyId)
    {
        try
        {
            await _logger.Info("ClientProfileController.GetClientProfileData",
                "Requested client profile data", $"CompanyId: {companyId}");

            var company = await _db.ClientCompanies.FindAsync(companyId);
            if (company == null)
            {
                return NotFound(new { message = "Azienda non trovata", errorCode = "COMPANY_NOT_FOUND" });
            }

            var profileData = await GetClientProfileDataAsync(companyId);
            if (profileData == null)
            {
                return NotFound(new { message = "Dati del profilo non disponibili", errorCode = "PROFILE_DATA_NOT_FOUND" });
            }

            return Ok(profileData);
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientProfileController.GetClientProfileData",
                $"Error retrieving client profile data for company {companyId}", ex.ToString());
            return StatusCode(500, new { message = "Errore interno del server", errorCode = "INTERNAL_SERVER_ERROR" });
        }
    }

    /// <summary>
    /// Genera un PDF completo del profilo cliente con tutte le informazioni aggregate
    /// </summary>
    [HttpPost("{companyId}/generate-profile-pdf")]
    public async Task<IActionResult> GenerateClientProfilePdf(int companyId)
    {
        try
        {
            await _logger.Info("ClientProfileController.GenerateClientProfilePdf",
                "Starting client profile PDF generation", $"CompanyId: {companyId}");

            // Verifica che l'azienda esista
            var company = await _db.ClientCompanies.FindAsync(companyId);
            if (company == null)
            {
                return NotFound(new { message = "Azienda non trovata", errorCode = "COMPANY_NOT_FOUND" });
            }

            // Recupera i dati del profilo completo dalla view
            var profileData = await GetClientProfileDataAsync(companyId);
            if (profileData == null)
            {
                return NotFound(new { message = "Dati del profilo non disponibili", errorCode = "PROFILE_DATA_NOT_FOUND" });
            }

            // Genera l'HTML per il PDF
            var htmlContent = GenerateProfileHtml(profileData);

            // Genera il PDF usando Puppeteer
            var pdfBytes = await GeneratePdfFromHtmlAsync(htmlContent);

            // Salva il PDF nel filesystem
            var fileName = GenerateProfileFileName(profileData.CompanyInfo);
            var filePath = await SavePdfFileAsync(pdfBytes, fileName);

            await _logger.Info("ClientProfileController.GenerateClientProfilePdf",
                "Client profile PDF generated successfully",
                $"CompanyId: {companyId}, FileName: {fileName}, Size: {pdfBytes.Length} bytes");

            // Determina il content type e estensione del file in base al contenuto
            var contentType = "application/pdf";
            var actualFileName = fileName;

            // Se è HTML fallback, aggiusta il content type e nome file
            if (System.Text.Encoding.UTF8.GetString(pdfBytes).StartsWith("<!DOCTYPE html") ||
                System.Text.Encoding.UTF8.GetString(pdfBytes).StartsWith("<html"))
            {
                contentType = "text/html";
                actualFileName = fileName.Replace(".pdf", ".html");

                await _logger.Info("ClientProfileController.GenerateClientProfilePdf",
                    "Returning HTML fallback instead of PDF", $"FileName: {actualFileName}");
            }

            // Ritorna il file per il download immediato
            return File(pdfBytes, contentType, actualFileName);
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientProfileController.GenerateClientProfilePdf",
                $"Error generating client profile PDF for company {companyId}", ex.ToString());
            return StatusCode(500, new { message = "Errore interno durante la generazione del PDF", errorCode = "INTERNAL_SERVER_ERROR" });
        }
    }

    /// <summary>
    /// Ottiene i dati aggregati del profilo cliente dalla view
    /// </summary>
    private async Task<ClientProfileData?> GetClientProfileDataAsync(int companyId)
    {
        // Query SQL per recuperare i dati dalla view
        var sql = @"
            SELECT * FROM vw_ClientFullProfile 
            WHERE CompanyId = @companyId 
            ORDER BY Brand, Model, Vin";

        var rawData = await _db.Database.SqlQueryRaw<ClientFullProfileViewDto>(sql,
            new Microsoft.Data.Sqlite.SqliteParameter("@companyId", companyId))
            .ToListAsync();

        if (!rawData.Any())
        {
            return null;
        }

        // Raggruppa i dati per azienda e veicoli
        var firstRow = rawData.First();

        return new ClientProfileData
        {
            CompanyInfo = new CompanyProfileInfo
            {
                Id = firstRow.CompanyId,
                VatNumber = firstRow.VatNumber,
                Name = firstRow.Name,
                Address = firstRow.Address,
                Email = firstRow.Email,
                PecAddress = firstRow.PecAddress,
                LandlineNumber = firstRow.LandlineNumber,
                ReferentName = firstRow.ReferentName,
                ReferentMobileNumber = firstRow.ReferentMobileNumber,
                ReferentEmail = firstRow.ReferentEmail,
                CompanyCreatedAt = firstRow.CompanyCreatedAt,
                DaysRegistered = firstRow.DaysRegistered,
                TotalVehicles = firstRow.TotalVehicles,
                ActiveVehicles = firstRow.ActiveVehicles,
                FetchingVehicles = firstRow.FetchingVehicles,
                AuthorizedVehicles = firstRow.AuthorizedVehicles,
                UniqueBrands = firstRow.UniqueBrands,
                TotalConsentsCompany = firstRow.TotalConsentsCompany,
                TotalOutagesCompany = firstRow.TotalOutagesCompany,
                TotalReportsCompany = firstRow.TotalReportsCompany,
                TotalSmsEventsCompany = firstRow.TotalSmsEventsCompany,
                FirstVehicleActivation = firstRow.FirstVehicleActivation,
                LastReportGeneratedCompany = firstRow.LastReportGeneratedCompany,
                LandlineNumbers = firstRow.LandlineNumbers,
                MobileNumbers = firstRow.MobileNumbers,
                AssociatedPhones = firstRow.AssociatedPhones
            },
            Vehicles = rawData.Where(r => r.VehicleId.HasValue).Select(r => new VehicleProfileInfo
            {
                Id = r.VehicleId!.Value,
                Vin = r.Vin ?? "",
                Brand = r.Brand ?? "",
                Model = r.Model ?? "",
                FuelType = r.FuelType ?? "",
                IsActive = r.VehicleIsActive,
                IsFetching = r.VehicleIsFetching,
                IsAuthorized = r.VehicleIsAuthorized,
                VehicleCreatedAt = r.VehicleCreatedAt,
                FirstActivationAt = r.VehicleFirstActivation,
                LastDeactivationAt = r.VehicleLastDeactivation,
                TotalConsents = r.VehicleConsents,
                TotalOutages = r.VehicleOutages,
                TotalReports = r.VehicleReports,
                TotalSmsEvents = r.VehicleSmsEvents,
                LastConsentDate = r.VehicleLastConsent,
                LastOutageStart = r.VehicleLastOutage,
                LastReportGenerated = r.VehicleLastReport,
                DaysSinceFirstActivation = r.DaysSinceFirstActivation,
                VehicleOutageDays = r.VehicleOutageDays
            }).ToList()
        };
    }

    /// <summary>
    /// Genera l'HTML per il PDF del profilo cliente
    /// </summary>
    private string GenerateProfileHtml(ClientProfileData data)
    {
        var generationDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        return $@"
<!DOCTYPE html>
<html lang='it'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Profilo Cliente - {data.CompanyInfo.Name}</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 20px;
            color: #333;
            line-height: 1.4;
        }}
        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px;
            border-radius: 10px;
            margin-bottom: 30px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 2.5em;
            font-weight: 300;
        }}
        .header .subtitle {{
            margin: 10px 0 0 0;
            font-size: 1.2em;
            opacity: 0.9;
        }}
        .section {{
            background: #f8f9fa;
            border-radius: 8px;
            padding: 25px;
            margin-bottom: 25px;
            border-left: 5px solid #667eea;
        }}
        .section h2 {{
            color: #667eea;
            margin-top: 0;
            margin-bottom: 20px;
            font-size: 1.8em;
            font-weight: 600;
        }}
        .info-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
            gap: 20px;
            margin-bottom: 20px;
        }}
        .info-item {{
            background: white;
            padding: 15px;
            border-radius: 6px;
            border: 1px solid #e9ecef;
        }}
        .info-label {{
            font-weight: 600;
            color: #495057;
            margin-bottom: 5px;
        }}
        .info-value {{
            color: #212529;
            font-size: 1.1em;
        }}
        .stats-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin: 20px 0;
        }}
        .stat-card {{
            background: white;
            padding: 20px;
            border-radius: 8px;
            text-align: center;
            border: 1px solid #e9ecef;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .stat-number {{
            font-size: 2.5em;
            font-weight: 700;
            color: #667eea;
            margin-bottom: 5px;
        }}
        .stat-label {{
            color: #6c757d;
            font-weight: 500;
            text-transform: uppercase;
            font-size: 0.9em;
        }}
        .vehicle-card {{
            background: white;
            border: 1px solid #e9ecef;
            border-radius: 8px;
            padding: 20px;
            margin-bottom: 20px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .vehicle-header {{
            display: flex;
            align-items: center;
            margin-bottom: 15px;
            padding-bottom: 15px;
            border-bottom: 2px solid #f8f9fa;
        }}
        .vehicle-title {{
            font-size: 1.4em;
            font-weight: 600;
            color: #495057;
        }}
        .vehicle-vin {{
            font-family: 'Courier New', monospace;
            background: #f8f9fa;
            padding: 5px 10px;
            border-radius: 4px;
            margin-left: 15px;
            font-size: 0.9em;
        }}
        .status-badges {{
            display: flex;
            gap: 10px;
            margin: 15px 0;
        }}
        .badge {{
            padding: 6px 12px;
            border-radius: 20px;
            font-size: 0.85em;
            font-weight: 600;
            text-transform: uppercase;
        }}
        .badge.active {{ background: #d4edda; color: #155724; }}
        .badge.inactive {{ background: #f8d7da; color: #721c24; }}
        .badge.fetching {{ background: #d1ecf1; color: #0c5460; }}
        .badge.authorized {{ background: #fff3cd; color: #856404; }}
        .footer {{
            text-align: center;
            margin-top: 40px;
            padding: 20px;
            color: #6c757d;
            border-top: 1px solid #e9ecef;
        }}
        .no-vehicles {{
            text-align: center;
            padding: 40px;
            color: #6c757d;
            font-style: italic;
        }}
        @media print {{
            body {{ margin: 0; }}
            .section {{ break-inside: avoid; }}
            .vehicle-card {{ break-inside: avoid; }}
        }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>Profilo Cliente Completo</h1>
        <div class='subtitle'>{data.CompanyInfo.Name} • P.IVA: {data.CompanyInfo.VatNumber}</div>
    </div>

    <div class='section'>
        <h2>📋 Informazioni Azienda</h2>
        <div class='info-grid'>
            <div class='info-item'>
                <div class='info-label'>Ragione Sociale</div>
                <div class='info-value'>{data.CompanyInfo.Name}</div>
            </div>
            <div class='info-item'>
                <div class='info-label'>Partita IVA</div>
                <div class='info-value'>{data.CompanyInfo.VatNumber}</div>
            </div>
            <div class='info-item'>
                <div class='info-label'>Indirizzo</div>
                <div class='info-value'>{data.CompanyInfo.Address ?? "Non specificato"}</div>
            </div>
            <div class='info-item'>
                <div class='info-label'>Email</div>
                <div class='info-value'>{data.CompanyInfo.Email}</div>
            </div>
            <div class='info-item'>
                <div class='info-label'>PEC</div>
                <div class='info-value'>{data.CompanyInfo.PecAddress ?? "Non specificata"}</div>
            </div>
            <div class='info-item'>
                <div class='info-label'>Data Registrazione</div>
                <div class='info-value'>{data.CompanyInfo.CompanyCreatedAt:dd/MM/yyyy} ({data.CompanyInfo.DaysRegistered} giorni fa)</div>
            </div>
        </div>

        <h3>👤 Referente</h3>
        <div class='info-grid'>
            <div class='info-item'>
                <div class='info-label'>Nome Referente</div>
                <div class='info-value'>{data.CompanyInfo.ReferentName ?? "Non specificato"}</div>
            </div>
            <div class='info-item'>
                <div class='info-label'>Email Referente</div>
                <div class='info-value'>{data.CompanyInfo.ReferentEmail ?? "Non specificata"}</div>
            </div>
            <div class='info-item'>
                <div class='info-label'>Cellulare Referente</div>
                <div class='info-value'>{data.CompanyInfo.ReferentMobileNumber ?? "Non specificato"}</div>
            </div>
            <div class='info-item'>
                <div class='info-label'>Telefono Fisso</div>
                <div class='info-value'>{data.CompanyInfo.LandlineNumber ?? "Non specificato"}</div>
            </div>
        </div>

        {GeneratePhoneNumbersSection(data.CompanyInfo)}
    </div>

    <div class='section'>
        <h2>📊 Statistiche Generali</h2>
        <div class='stats-grid'>
            <div class='stat-card'>
                <div class='stat-number'>{data.CompanyInfo.TotalVehicles}</div>
                <div class='stat-label'>Veicoli Totali</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{data.CompanyInfo.ActiveVehicles}</div>
                <div class='stat-label'>Veicoli Attivi</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{data.CompanyInfo.AuthorizedVehicles}</div>
                <div class='stat-label'>Autorizzati</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{data.CompanyInfo.UniqueBrands}</div>
                <div class='stat-label'>Brand Diversi</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{data.CompanyInfo.TotalConsentsCompany}</div>
                <div class='stat-label'>Consensi Totali</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{data.CompanyInfo.TotalOutagesCompany}</div>
                <div class='stat-label'>Outages Totali</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{data.CompanyInfo.TotalReportsCompany}</div>
                <div class='stat-label'>Report Generati</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{data.CompanyInfo.TotalSmsEventsCompany}</div>
                <div class='stat-label'>Eventi SMS</div>
            </div>
        </div>
    </div>

    {GenerateVehiclesSection(data.Vehicles)}

    <div class='footer'>
        <p>Report generato automaticamente da DataPolar • {generationDate}</p>
        <p>Documento riservato e confidenziale - Proprietà di DataPolar SRLS</p>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// Genera la sezione dei numeri di telefono
    /// </summary>
    private string GeneratePhoneNumbersSection(CompanyProfileInfo company)
    {
        var phones = new List<string>();

        if (!string.IsNullOrWhiteSpace(company.LandlineNumbers))
            phones.Add(company.LandlineNumbers);
        if (!string.IsNullOrWhiteSpace(company.MobileNumbers))
            phones.Add(company.MobileNumbers);
        if (!string.IsNullOrWhiteSpace(company.AssociatedPhones))
            phones.Add(company.AssociatedPhones);

        if (!phones.Any())
            return "";

        return $@"
        <h3>📞 Numeri di Telefono Associati</h3>
        <div class='info-item'>
            <div class='info-label'>Contatti Registrati</div>
            <div class='info-value'>{string.Join(" • ", phones)}</div>
        </div>";
    }

    /// <summary>
    /// Genera la sezione dei veicoli
    /// </summary>
    private string GenerateVehiclesSection(List<VehicleProfileInfo> vehicles)
    {
        if (!vehicles.Any())
        {
            return @"
            <div class='section'>
                <h2>🚗 Veicoli Associati</h2>
                <div class='no-vehicles'>
                    <p>Nessun veicolo associato a questa azienda</p>
                </div>
            </div>";
        }

        var vehiclesHtml = string.Join("", vehicles.Select(GenerateVehicleCard));

        return $@"
        <div class='section'>
            <h2>🚗 Veicoli Associati ({vehicles.Count})</h2>
            {vehiclesHtml}
        </div>";
    }

    /// <summary>
    /// Genera la card per un singolo veicolo
    /// </summary>
    private string GenerateVehicleCard(VehicleProfileInfo vehicle)
    {
        var statusBadges = GenerateStatusBadges(vehicle);
        var activationInfo = GenerateActivationInfo(vehicle);
        var statisticsGrid = GenerateVehicleStatistics(vehicle);

        return $@"
        <div class='vehicle-card'>
            <div class='vehicle-header'>
                <div class='vehicle-title'>{vehicle.Brand} {vehicle.Model}</div>
                <div class='vehicle-vin'>{vehicle.Vin}</div>
            </div>
            
            <div class='info-grid'>
                <div class='info-item'>
                    <div class='info-label'>Alimentazione</div>
                    <div class='info-value'>{vehicle.FuelType}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Data Registrazione</div>
                    <div class='info-value'>{vehicle.VehicleCreatedAt:dd/MM/yyyy}</div>
                </div>
                {activationInfo}
            </div>

            {statusBadges}
            {statisticsGrid}
        </div>";
    }

    /// <summary>
    /// Genera i badge di stato per il veicolo
    /// </summary>
    private string GenerateStatusBadges(VehicleProfileInfo vehicle)
    {
        var badges = new List<string>();

        badges.Add(vehicle.IsActive
            ? "<span class='badge active'>Attivo</span>"
            : "<span class='badge inactive'>Non Attivo</span>");

        badges.Add(vehicle.IsFetching
            ? "<span class='badge fetching'>In Acquisizione</span>"
            : "<span class='badge inactive'>Acquisizione Ferma</span>");

        badges.Add(vehicle.IsAuthorized
            ? "<span class='badge authorized'>Autorizzato OAuth</span>"
            : "<span class='badge inactive'>Non Autorizzato</span>");

        return $@"
        <div class='status-badges'>
            {string.Join("", badges)}
        </div>";
    }

    /// <summary>
    /// Genera le informazioni di attivazione
    /// </summary>
    private string GenerateActivationInfo(VehicleProfileInfo vehicle)
    {
        var items = new List<string>();

        if (vehicle.FirstActivationAt.HasValue)
        {
            var daysText = vehicle.DaysSinceFirstActivation.HasValue
                ? $" ({vehicle.DaysSinceFirstActivation} giorni fa)"
                : "";
            items.Add($@"
                <div class='info-item'>
                    <div class='info-label'>Prima Attivazione</div>
                    <div class='info-value'>{vehicle.FirstActivationAt:dd/MM/yyyy HH:mm}{daysText}</div>
                </div>");
        }

        if (vehicle.LastDeactivationAt.HasValue)
        {
            items.Add($@"
                <div class='info-item'>
                    <div class='info-label'>Ultima Disattivazione</div>
                    <div class='info-value'>{vehicle.LastDeactivationAt:dd/MM/yyyy HH:mm}</div>
                </div>");
        }

        return string.Join("", items);
    }

    /// <summary>
    /// Genera la griglia delle statistiche per il veicolo
    /// </summary>
    private string GenerateVehicleStatistics(VehicleProfileInfo vehicle)
    {
        return $@"
        <h4>📈 Statistiche Veicolo</h4>
        <div class='stats-grid'>
            <div class='stat-card'>
                <div class='stat-number'>{vehicle.TotalConsents}</div>
                <div class='stat-label'>Consensi</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{vehicle.TotalOutages}</div>
                <div class='stat-label'>Outages</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{vehicle.TotalReports}</div>
                <div class='stat-label'>Report</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{vehicle.TotalSmsEvents}</div>
                <div class='stat-label'>SMS Eventi</div>
            </div>
            {(vehicle.VehicleOutageDays > 0 ? $@"
            <div class='stat-card'>
                <div class='stat-number'>{vehicle.VehicleOutageDays}</div>
                <div class='stat-label'>Giorni Outage</div>
            </div>" : "")}
        </div>
        {GenerateLastActivityInfo(vehicle)}";
    }

    /// <summary>
    /// Genera le informazioni sull'ultima attività
    /// </summary>
    private string GenerateLastActivityInfo(VehicleProfileInfo vehicle)
    {
        var activities = new List<string>();

        if (vehicle.LastConsentDate.HasValue)
            activities.Add($"Ultimo consenso: {vehicle.LastConsentDate:dd/MM/yyyy}");

        if (vehicle.LastOutageStart.HasValue)
            activities.Add($"Ultimo outage: {vehicle.LastOutageStart:dd/MM/yyyy}");

        if (vehicle.LastReportGenerated.HasValue)
            activities.Add($"Ultimo report: {vehicle.LastReportGenerated:dd/MM/yyyy}");

        if (!activities.Any())
            return "";

        return $@"
        <div class='info-item' style='margin-top: 15px;'>
            <div class='info-label'>Ultima Attività</div>
            <div class='info-value'>{string.Join(" • ", activities)}</div>
        </div>";
    }

    /// <summary>
    /// Genera il PDF usando Puppeteer tramite Node.js script (come il sistema esistente)
    /// </summary>
    private async Task<byte[]> GeneratePdfFromHtmlAsync(string htmlContent)
    {
        try
        {
            await _logger.Info("ClientProfileController.GeneratePdfFromHtml",
                "Starting PDF generation using Node.js Puppeteer script");

            // 1. Salva HTML temporaneo
            var tempHtmlPath = await SaveTemporaryHtmlAsync(htmlContent);

            // 2. Prepara path di output temporaneo
            var tempPdfPath = Path.Combine(Path.GetTempPath(), $"ClientProfile_{DateTime.UtcNow.Ticks}.pdf");

            // 3. Controlla disponibilità Node.js
            if (!IsNodeJsAvailable())
            {
                await _logger.Warning("ClientProfileController.GeneratePdfFromHtml",
                    "Node.js not available, falling back to HTML content");
                return await ConvertToHtmlFallback(htmlContent);
            }

            // 4. Genera script Puppeteer
            var scriptPath = await CreatePuppeteerScriptAsync();

            // 5. Esegui conversione
            var pdfBytes = await ExecutePuppeteerConversion(scriptPath, tempHtmlPath, tempPdfPath);

            // 6. Cleanup
            await CleanupTemporaryFiles(tempHtmlPath, scriptPath);
            if (System.IO.File.Exists(tempPdfPath))
                System.IO.File.Delete(tempPdfPath);

            await _logger.Info("ClientProfileController.GeneratePdfFromHtml",
                "PDF generation completed successfully", $"Size: {pdfBytes.Length} bytes");

            return pdfBytes;
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientProfileController.GeneratePdfFromHtml",
                "Error during PDF generation", ex.ToString());

            // Fallback: restituisci HTML come bytes
            return await ConvertToHtmlFallback(htmlContent);
        }
    }

    /// <summary>
    /// Salva HTML in file temporaneo
    /// </summary>
    private async Task<string> SaveTemporaryHtmlAsync(string htmlContent)
    {
        var tempDir = Path.GetTempPath();
        var htmlPath = Path.Combine(tempDir, $"ClientProfile_{DateTime.UtcNow.Ticks}.html");
        await System.IO.File.WriteAllTextAsync(htmlPath, htmlContent);
        return htmlPath;
    }

    /// <summary>
    /// Controlla se Node.js è disponibile
    /// </summary>
    private bool IsNodeJsAvailable()
    {
        try
        {
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            var nodePath = Path.Combine(programFiles, "nodejs", "node.exe");
            return System.IO.File.Exists(nodePath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Crea lo script Puppeteer temporaneo (basato sul tuo esempio funzionante)
    /// </summary>
    private async Task<string> CreatePuppeteerScriptAsync()
    {
        var scriptContent = @"
import { readFileSync } from 'fs';
import { launch } from 'puppeteer';

(async () => {
  const htmlPath = process.argv[2];
  const pdfPath = process.argv[3];
  
  if (!htmlPath || !pdfPath) {
    console.error('Usage: node script.js <htmlPath> <pdfPath>');
    process.exit(1);
  }
  
  console.log(`Starting PDF conversion:`);
  console.log(`  HTML: ${htmlPath}`);
  console.log(`  PDF:  ${pdfPath}`);
  
  try {
    const html = readFileSync(htmlPath, 'utf8');
    console.log(`HTML content loaded (${html.length} chars)`);
    
    const browser = await launch({ 
      headless: true,
      args: [
        '--no-sandbox',
        '--disable-dev-shm-usage',
        '--disable-gpu',
        '--disable-web-security'
      ]
    });
    
    const page = await browser.newPage();
    await page.setContent(html, { waitUntil: 'networkidle0' });
    
    await page.pdf({
      path: pdfPath,
      format: 'A4',
      printBackground: true,
      preferCSSPageSize: true,
      margin: {
        top: '30px',
        bottom: '30px',
        left: '20px',
        right: '20px',
      },
    });
    
    await browser.close();
    console.log('✅ PDF generated successfully');
  } catch (error) {
    console.error('❌ PDF generation failed:', error.message);
    process.exit(1);
  }
})();";

        var projectDirectory = FindProjectDirectory();
        var scriptPath = Path.Combine(projectDirectory, $"temp_client_profile_script_{DateTime.UtcNow.Ticks}.mjs");
        await System.IO.File.WriteAllTextAsync(scriptPath, scriptContent);
        return scriptPath;
    }

    /// <summary>
    /// Trova la directory del progetto
    /// </summary>
    private string FindProjectDirectory()
    {
        var currentDir = Directory.GetCurrentDirectory();

        while (!string.IsNullOrEmpty(currentDir))
        {
            var packageJsonPath = Path.Combine(currentDir, "package.json");
            if (System.IO.File.Exists(packageJsonPath))
            {
                return currentDir;
            }

            var csprojFiles = Directory.GetFiles(currentDir, "*.csproj");
            if (csprojFiles.Length > 0)
            {
                var nodeModulesPath = Path.Combine(currentDir, "node_modules");
                if (Directory.Exists(nodeModulesPath))
                {
                    return currentDir;
                }
            }

            var parentDir = Directory.GetParent(currentDir);
            if (parentDir == null) break;
            currentDir = parentDir.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Esegue la conversione PDF tramite Node.js script
    /// </summary>
    private async Task<byte[]> ExecutePuppeteerConversion(string scriptPath, string htmlPath, string pdfPath)
    {
        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        var nodePath = Path.Combine(programFiles, "nodejs", "node.exe");
        var projectDirectory = FindProjectDirectory();

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = nodePath,
                Arguments = $"\"{scriptPath}\" \"{htmlPath}\" \"{pdfPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = projectDirectory
            }
        };

        await _logger.Info("ClientProfileController.ExecutePuppeteerConversion",
            "Starting Node.js PDF conversion", $"Script: {scriptPath}");

        process.Start();

        // Timeout di 60 secondi
        var processTask = Task.Run(() => process.WaitForExit());
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(60));
        var completedTask = await Task.WhenAny(processTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            await _logger.Warning("ClientProfileController.ExecutePuppeteerConversion",
                "PDF conversion timeout (60s)");

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    await process.WaitForExitAsync();
                }
            }
            catch { }

            throw new TimeoutException("PDF conversion timeout after 60 seconds");
        }

        // Leggi output per debug
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await _logger.Debug("ClientProfileController.ExecutePuppeteerConversion",
            "Node.js output", $"Stdout\n\n{stdout}");

        if (!string.IsNullOrEmpty(stderr))
        {
            await _logger.Debug("ClientProfileController.ExecutePuppeteerConversion",
                "Node.js stderr", stderr);
        }

        // Verifica che il PDF sia stato creato
        if (!System.IO.File.Exists(pdfPath))
        {
            throw new InvalidOperationException($"PDF file not created. Exit code: {process.ExitCode}, Stderr: {stderr}");
        }

        var pdfBytes = await System.IO.File.ReadAllBytesAsync(pdfPath);

        await _logger.Info("ClientProfileController.ExecutePuppeteerConversion",
            "PDF conversion successful", $"Size: {pdfBytes.Length} bytes");

        return pdfBytes;
    }

    /// <summary>
    /// Fallback: converte HTML in bytes per download diretto
    /// </summary>
    private async Task<byte[]> ConvertToHtmlFallback(string htmlContent)
    {
        await _logger.Warning("ClientProfileController.ConvertToHtmlFallback",
            "Using HTML fallback instead of PDF");

        return System.Text.Encoding.UTF8.GetBytes(htmlContent);
    }

    /// <summary>
    /// Cleanup file temporanei
    /// </summary>
    private async Task CleanupTemporaryFiles(params string[] filePaths)
    {
        foreach (var path in filePaths)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                    await _logger.Debug("ClientProfileController.CleanupTemporaryFiles",
                        "Temporary file deleted", path);
                }
            }
            catch (Exception ex)
            {
                await _logger.Debug("ClientProfileController.CleanupTemporaryFiles",
                    "Error deleting temporary file", $"Path: {path}, Error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Salva il PDF nel filesystem
    /// </summary>
    private async Task<string> SavePdfFileAsync(byte[] pdfBytes, string fileName)
    {
        if (!Directory.Exists(_reportsStoragePath))
        {
            Directory.CreateDirectory(_reportsStoragePath);
        }

        var filePath = Path.Combine(_reportsStoragePath, fileName);
        await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);

        return filePath;
    }

    /// <summary>
    /// Genera il nome del file PDF
    /// </summary>
    private static string GenerateProfileFileName(CompanyProfileInfo company)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var sanitizedCompanyName = System.Text.RegularExpressions.Regex.Replace(
            company.Name, @"[^a-zA-Z0-9]", "_");

        return $"profilo_cliente_{sanitizedCompanyName}_{company.VatNumber}_{timestamp}.pdf";
    }
}