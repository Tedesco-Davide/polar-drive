using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.PolarAiReports;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientProfileController(PolarDriveDbContext db, PdfGenerationService pdfService) : ControllerBase
{
    private readonly PolarDriveDbContext _db = db;
    private readonly PolarDriveLogger _logger = new();
    private readonly PdfGenerationService _pdfService = pdfService;

    /// <summary>
    /// Ottiene i dati del profilo cliente senza generare il PDF (per preview o debug)
    /// </summary>
    [HttpGet("{companyId}/profile-data")]
    public async Task<ActionResult<ClientProfileData>> GetClientProfileData(int companyId)
    {
        try
        {
            await _logger.Info("ClientProfileController.GetClientProfileData",
                "Requested client profile data", $"ClientCompanyId: {companyId}");

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

[HttpPost("{companyId}/generate-profile-pdf")]
    public async Task<IActionResult> GenerateClientProfilePdf(int companyId)
    {
        try
        {
            await _logger.Info("ClientProfileController.GenerateClientProfilePdf",
                "Starting client profile PDF generation", $"ClientCompanyId: {companyId}");

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

            var basePath = "/app/wwwroot/fonts/satoshi";
            var satoshiRegular = System.IO.File.ReadAllText(Path.Combine(basePath, "Satoshi-Regular.b64"));
            var satoshiBold = System.IO.File.ReadAllText(Path.Combine(basePath, "Satoshi-Bold.b64"));
            
            var fontStyles = $@"
                @font-face {{
                    font-family: 'Satoshi';
                    src: url(data:font/woff2;base64,{satoshiRegular}) format('woff2');
                    font-weight: 400;
                    font-style: normal;
                }}
                @font-face {{
                    font-family: 'Satoshi';
                    src: url(data:font/woff2;base64,{satoshiBold}) format('woff2');
                    font-weight: 700;
                    font-style: normal;
                }}";

            var htmlContent = GenerateProfileHtml(profileData);

            var tempReport = new PdfReport
            {
                Id = 0,
                ReportPeriodStart = DateTime.Now.AddMonths(-1),
                ReportPeriodEnd = DateTime.Now,
                GeneratedAt = DateTime.Now
            };

            var pdfOptions = new PdfConversionOptions
            {
                HeaderTemplate = $@"
                    <html>
                    <head>
                        <style>
                            {fontStyles}
                            body {{
                                margin: 0;
                                padding: 0;
                                width: 100%;
                                height: 100%;
                                display: flex;
                                align-items: center;
                                justify-content: center;
                                font-family: 'Satoshi', 'Noto Color Emoji', sans-serif;
                                letter-spacing: normal;
                                word-spacing: normal;
                            }}
                            .header-content {{
                                font-size: 10px;
                                color: #ccc;
                                text-align: center;
                                border-bottom: 1px solid #ccc;
                                padding-bottom: 5px;
                                width: 100%;
                                letter-spacing: normal;
                                word-spacing: normal;
                            }}
                        </style>
                    </head>
                    <body>
                        <div class='header-content'>Profilo Cliente - {company.Name} - {DateTime.Now:yyyy-MM-dd HH:mm}</div>
                    </body>
                    </html>",
                FooterTemplate = $@"
                    <html>
                    <head>
                        <style>
                            {fontStyles}
                            body {{
                                margin: 0;
                                padding: 0;
                                width: 100%;
                                height: 100%;
                                display: flex;
                                align-items: center;
                                justify-content: center;
                                font-family: 'Satoshi', 'Noto Color Emoji', sans-serif;
                                letter-spacing: normal;
                                word-spacing: normal;
                            }}
                            .footer-content {{
                                font-size: 10px;
                                color: #ccc;
                                text-align: center;
                                border-top: 1px solid #ccc;
                                padding-top: 5px;
                                width: 100%;
                                letter-spacing: normal;
                                word-spacing: normal;
                            }}
                        </style>
                    </head>
                    <body>
                        <div class='footer-content'>
                            Pagina <span class='pageNumber'></span> di <span class='totalPages'></span> | DataPolar Analytics
                        </div>
                    </body>
                    </html>"
            };

            var pdfBytes = await _pdfService.ConvertHtmlToPdfAsync(htmlContent, tempReport, pdfOptions);
            var fileName = GenerateProfileFileName(profileData.CompanyInfo);

            var clientProfilePdf = new ClientProfilePdf
            {
                ClientCompanyId = companyId,
                FileName = fileName,
                PdfContent = pdfBytes,
                GeneratedAt = DateTime.Now,
                FileSizeBytes = pdfBytes.Length
            };

            _db.ClientProfilePdfs.Add(clientProfilePdf);
            await _db.SaveChangesAsync();

            await _logger.Info("ClientProfileController.GenerateClientProfilePdf",
                "PDF generated and saved to DB", $"Size: {pdfBytes.Length} bytes, RecordId: {clientProfilePdf.Id}");

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientProfileController.GenerateClientProfilePdf",
                $"Error generating PDF for company {companyId}", ex.ToString());
            return StatusCode(500, new { message = "Errore generazione PDF", errorCode = "INTERNAL_SERVER_ERROR" });
        }
    }

    /// <summary>
    /// ✅ CORRETTO: Ottiene i dati aggregati del profilo cliente dalla view vw_ClientFullProfile
    /// </summary>
    private async Task<ClientProfileData?> GetClientProfileDataAsync(int companyId)
    {
        var previousTimeout = _db.Database.GetCommandTimeout();
        _db.Database.SetCommandTimeout(120); // 2 minuti

        try
        {
            // ✅ USA LA VIEW CORRETTA con tutti i campi inclusi quelli SMS
            var sql = @"
                SELECT * 
                FROM vw_ClientFullProfile 
                WHERE ClientCompanyId = @companyId 
                ORDER BY Brand, Model, Vin";

            var companyParam = new SqlParameter("@companyId", SqlDbType.Int) { Value = companyId };
            var rawData = await _db.Database.SqlQueryRaw<ClientFullProfileViewDto>(sql, companyParam)
                .ToListAsync();

            if (rawData.Count == 0)
                return null;

            var firstRow = rawData.First();

            return new ClientProfileData
            {
                CompanyInfo = new CompanyProfileInfo
                {
                    Id = firstRow.ClientCompanyId,
                    VatNumber = firstRow.VatNumber,
                    Name = firstRow.Name,
                    Address = firstRow.Address,
                    Email = firstRow.Email,
                    PecAddress = firstRow.PecAddress,
                    LandlineNumber = firstRow.LandlineNumber,
                    CompanyCreatedAt = firstRow.CompanyCreatedAt,
                    DaysRegistered = firstRow.DaysRegistered,
                    TotalVehicles = firstRow.TotalVehicles,
                    ActiveVehicles = firstRow.ActiveVehicles,
                    FetchingVehicles = firstRow.FetchingVehicles,
                    AuthorizedVehicles = firstRow.AuthorizedVehicles,
                    UniqueBrands = firstRow.UniqueBrands,

                    // ✅ CORRETTO: Mappa i dati aggregati reali dalla view
                    TotalConsentsCompany = firstRow.TotalConsentsCompany,
                    TotalOutagesCompany = firstRow.TotalOutagesCompany,
                    TotalReportsCompany = firstRow.TotalReportsCompany,

                    // ✅ NUOVO: Statistiche SMS aggregate aziendali
                    TotalSmsEventsCompany = firstRow.TotalSmsEventsCompany,
                    AdaptiveOnEventsCompany = firstRow.AdaptiveOnEventsCompany,
                    AdaptiveOffEventsCompany = firstRow.AdaptiveOffEventsCompany,
                    ActiveSessionsCompany = firstRow.ActiveSessionsCompany,
                    LastSmsReceivedCompany = firstRow.LastSmsReceivedCompany,
                    LastActiveSessionExpiresCompany = firstRow.LastActiveSessionExpiresCompany,

                    FirstVehicleActivation = firstRow.FirstVehicleActivation,
                    LastReportGeneratedCompany = firstRow.LastReportGeneratedCompany,
                },
                Vehicles = rawData
                    .Where(r => r.VehicleId.HasValue)
                    .Select(r => new VehicleProfileInfo
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

                        // ✅ CORRETTO: Statistiche reali dalla view
                        TotalConsents = r.VehicleConsents,
                        TotalOutages = r.VehicleOutages,
                        TotalReports = r.VehicleReports,

                        // ✅ NUOVO: Statistiche SMS dettagliate per veicolo
                        TotalSmsEvents = r.VehicleSmsEvents,
                        AdaptiveOnEvents = r.VehicleAdaptiveOn,
                        AdaptiveOffEvents = r.VehicleAdaptiveOff,
                        ActiveSessions = r.VehicleActiveSessions,
                        LastSmsReceived = r.VehicleLastSms,
                        ActiveSessionExpires = r.VehicleActiveSessionExpires,

                        LastConsentDate = r.VehicleLastConsent,
                        LastOutageStart = r.VehicleLastOutage,
                        LastReportGenerated = r.VehicleLastReport,
                        DaysSinceFirstActivation = r.DaysSinceFirstActivation,
                        VehicleOutageDays = r.VehicleOutageDays,
                        ReferentName = r.ReferentName,
                        VehicleMobileNumber = r.VehicleMobileNumber,
                        ReferentEmail = r.ReferentEmail
                    })
                    .ToList()
            };
        }
        finally
        {
            // Ripristina il timeout originale
            _db.Database.SetCommandTimeout(previousTimeout);
        }
    }

    /// <summary>
    /// Genera l'HTML per il PDF del profilo cliente
    /// </summary>
    private string GenerateProfileHtml(ClientProfileData data)
    {
        var generationDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        // ✅ Path assoluto nel container Docker
        var basePath = "/app/wwwroot/fonts/satoshi";
        
        var satoshiRegular = System.IO.File.ReadAllText(Path.Combine(basePath, "Satoshi-Regular.b64"));
        var satoshiBold = System.IO.File.ReadAllText(Path.Combine(basePath, "Satoshi-Bold.b64"));
        var satoshiBlack = System.IO.File.ReadAllText(Path.Combine(basePath, "Satoshi-Black.b64"));

        return $@"
    <!DOCTYPE html>
    <html lang='it'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <title>Profilo Cliente - {data.CompanyInfo.Name}</title>
            <style>
                @font-face {{
                    font-family: 'Satoshi';
                    src: url(data:font/woff2;base64,{satoshiRegular}) format('woff2');
                    font-weight: 400;
                    font-style: normal;
                    font-display: swap;
                }}
                @font-face {{
                    font-family: 'Satoshi';
                    src: url(data:font/woff2;base64,{satoshiBold}) format('woff2');
                    font-weight: 700;
                    font-style: normal;
                    font-display: swap;
                }}
                @font-face {{
                    font-family: 'Satoshi';
                    src: url(data:font/woff2;base64,{satoshiBlack}) format('woff2');
                    font-weight: 800;
                    font-style: normal;
                    font-display: swap;
                }}

                body {{
                    font-family: 'Satoshi', 'Noto Color Emoji', 'Apple Color Emoji', 'Segoe UI Emoji', sans-serif;
                    margin: 0;
                    padding: 20px;
                    color: #333;
                    line-height: 1.4;
                    letter-spacing: normal;
                    word-spacing: normal;
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
                    margin-bottom: 20px;
                    border-left: 5px solid #667eea;
                    break-inside: avoid;
                    page-break-inside: avoid;
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
                    gap: 15px;
                    margin-bottom: 15px;
                }}
                .info-item {{
                    background: white;
                    padding: 15px;
                    border-radius: 6px;
                    border: 1px solid #e9ecef;
                    height: auto;
                    min-height: auto;
                }}
                .info-label {{
                    font-weight: 600;
                    color: #495057;
                    margin-bottom: 5px;
                }}
                .info-value {{
                    color: #212529;
                    font-size: 1.1em;
                    word-wrap: break-word;
                    overflow-wrap: break-word;
                }}
                .stats-grid {{
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
                    gap: 15px;
                    margin: 15px 0;
                    align-items: start;
                }}
                .stat-card {{
                    background: white;
                    padding: 20px;
                    border-radius: 8px;
                    border: 2px solid #e9ecef;
                    text-align: center;
                    transition: all 0.3s;
                    height: auto;
                }}
                .stat-number {{
                    font-size: 2.5em;
                    font-weight: 700;
                    color: #667eea;
                    margin-bottom: 5px;
                }}
                .stat-label {{
                    color: #6c757d;
                    font-size: 0.95em;
                    text-transform: uppercase;
                    letter-spacing: 1px;
                }}
                .vehicle-card {{
                    background: white;
                    border: 1px solid #dee2e6;
                    border-radius: 8px;
                    padding: 20px;
                    margin-bottom: 20px;
                    break-inside: avoid;
                    page-break-inside: avoid;
                }}
                .vehicle-header {{
                    border-bottom: 2px solid #667eea;
                    padding-bottom: 15px;
                    margin-bottom: 20px;
                }}
                .vehicle-title {{
                    font-size: 1.6em;
                    font-weight: 600;
                    color: #212529;
                }}
                .vehicle-vin {{
                    font-family: 'Courier New', monospace;
                    color: #6c757d;
                    font-size: 0.95em;
                    margin-top: 5px;
                }}
                .status-badges {{
                    display: flex;
                    flex-wrap: wrap;
                    gap: 8px;
                    margin: 15px 0;
                }}
                .badge {{
                    padding: 6px 12px;
                    border-radius: 6px;
                    font-size: 0.85em;
                    font-weight: 600;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                }}
                .badge.active {{
                    background: #d4edda;
                    color: #155724;
                    border: 1px solid #c3e6cb;
                }}
                .badge.inactive {{
                    background: #f8d7da;
                    color: #721c24;
                    border: 1px solid #f5c6cb;
                }}
                .badge.fetching {{
                    background: #d1ecf1;
                    color: #0c5460;
                    border: 1px solid #bee5eb;
                }}
                .badge.authorized {{
                    background: #fff3cd;
                    color: #856404;
                    border: 1px solid #ffeeba;
                }}
                .footer {{
                    margin-top: 40px;
                    padding-top: 20px;
                    border-top: 2px solid #dee2e6;
                    text-align: center;
                    color: #6c757d;
                    font-size: 0.9em;
                }}
            </style>
        </head>
            <body>
                <div class='header'>
                    <h1>📊 Profilo Cliente Completo</h1>
                    <div class='subtitle'>{data.CompanyInfo.Name}</div>
                    <div style='margin-top: 10px; font-size: 0.9em; opacity: 0.8;'>
                        Generato il {generationDate}
                    </div>
                </div>

                {GenerateCompanySection(data.CompanyInfo)}
                {GenerateVehiclesSection(data.Vehicles)}

                <div class='footer'>
                    <p>© {DateTime.Now.Year} DataPolar Analytics - Documento Riservato</p>
                    <p>P.IVA: {data.CompanyInfo.VatNumber}</p>
                </div>
            </body>
        </html>";
    }

    /// <summary>
    /// Genera la sezione aziendale
    /// </summary>
    private string GenerateCompanySection(CompanyProfileInfo company)
    {
        // ✅ NUOVO: Mostra statistiche SMS aggregate aziendali
        var smsStats = company.TotalSmsEventsCompany > 0 ? $@"
        <div class='stat-card'>
            <div class='stat-number'>{company.TotalSmsEventsCompany}</div>
            <div class='stat-label'>SMS Totali</div>
            <div style='font-size: 11px; margin-top: 8px; color: #666;'>
                ON: {company.AdaptiveOnEventsCompany} | OFF: {company.AdaptiveOffEventsCompany}
            </div>
        </div>" : "";

        // ✅ NUOVO: Mostra sessioni attive aggregate
        var activeSessions = company.ActiveSessionsCompany > 0 ? $@"
        <div class='stat-card' style='border-color: #10b981; background: #f0fdf4;'>
            <div class='stat-number' style='color: #10b981;'>{company.ActiveSessionsCompany}</div>
            <div class='stat-label' style='color: #10b981;'>Sessioni SMS Attive</div>
            {(company.LastActiveSessionExpiresCompany.HasValue ? $@"
            <div style='font-size: 10px; margin-top: 5px; color: #059669;'>
                Ultima scadenza: {company.LastActiveSessionExpiresCompany:dd/MM/yyyy HH:mm}
            </div>" : "")}
        </div>" : "";

        return $@"
        <div class='section'>
            <h2>🏢 Informazioni Azienda</h2>
            <div class='info-grid'>
                <div class='info-item'>
                    <div class='info-label'>Ragione Sociale</div>
                    <div class='info-value'>{company.Name}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Partita IVA</div>
                    <div class='info-value'>{company.VatNumber}</div>
                </div>
                {(!string.IsNullOrWhiteSpace(company.Address) ? $@"
                <div class='info-item'>
                    <div class='info-label'>Indirizzo</div>
                    <div class='info-value'>{company.Address}</div>
                </div>" : "")}
                <div class='info-item'>
                    <div class='info-label'>Email</div>
                    <div class='info-value'>{company.Email}</div>
                </div>
                {(!string.IsNullOrWhiteSpace(company.PecAddress) ? $@"
                <div class='info-item'>
                    <div class='info-label'>PEC</div>
                    <div class='info-value'>{company.PecAddress}</div>
                </div>" : "")}
                {(!string.IsNullOrWhiteSpace(company.LandlineNumber) ? $@"
                <div class='info-item'>
                    <div class='info-label'>Telefono</div>
                    <div class='info-value'>{company.LandlineNumber}</div>
                </div>" : "")}
                <div class='info-item'>
                    <div class='info-label'>Data Registrazione</div>
                    <div class='info-value'>{company.CompanyCreatedAt:dd/MM/yyyy} ({company.DaysRegistered} giorni fa)</div>
                </div>
                {(company.FirstVehicleActivation.HasValue ? $@"
                <div class='info-item'>
                    <div class='info-label'>Prima Attivazione Veicolo</div>
                    <div class='info-value'>{company.FirstVehicleActivation:dd/MM/yyyy HH:mm}</div>
                </div>" : "")}
            </div>

            <h3 style='margin-top: 30px; margin-bottom: 15px; color: #495057;'>📈 Statistiche Flotta</h3>
            <div class='stats-grid'>
                <div class='stat-card'>
                    <div class='stat-number'>{company.TotalVehicles}</div>
                    <div class='stat-label'>Veicoli Totali</div>
                </div>
                <div class='stat-card'>
                    <div class='stat-number'>{company.ActiveVehicles}</div>
                    <div class='stat-label'>Veicoli Attivi</div>
                </div>
                <div class='stat-card'>
                    <div class='stat-number'>{company.FetchingVehicles}</div>
                    <div class='stat-label'>In Acquisizione</div>
                </div>
                <div class='stat-card'>
                    <div class='stat-number'>{company.AuthorizedVehicles}</div>
                    <div class='stat-label'>Autorizzati OAuth</div>
                </div>
                <div class='stat-card'>
                    <div class='stat-number'>{company.UniqueBrands}</div>
                    <div class='stat-label'>Brand Unici</div>
                </div>
                <div class='stat-card'>
                    <div class='stat-number'>{company.TotalConsentsCompany}</div>
                    <div class='stat-label'>Consensi Totali</div>
                </div>
                <div class='stat-card'>
                    <div class='stat-number'>{company.TotalOutagesCompany}</div>
                    <div class='stat-label'>Outage Totali</div>
                </div>
                <div class='stat-card'>
                    <div class='stat-number'>{company.TotalReportsCompany}</div>
                    <div class='stat-label'>Report Generati</div>
                </div>
                {smsStats}
                {activeSessions}
            </div>
        </div>";
    }

    /// <summary>
    /// Genera la sezione veicoli
    /// </summary>
    private string GenerateVehiclesSection(List<VehicleProfileInfo> vehicles)
    {
        if (!vehicles.Any())
        {
            return @"
            <div class='section'>
                <h2>🚗 Veicoli</h2>
                <p style='color: #6c757d; font-style: italic;'>Nessun veicolo registrato per questa azienda.</p>
            </div>";
        }

        var vehicleCards = string.Join("", vehicles.Select(GenerateVehicleCard));

        return $@"
        <div class='section'>
            <h2>🚗 Veicoli ({vehicles.Count})</h2>
            {vehicleCards}
        </div>";
    }

    /// <summary>
    /// Genera la card per un singolo veicolo
    /// </summary>
    private string GenerateVehicleCard(VehicleProfileInfo vehicle)
    {
        var activationInfo = GenerateActivationInfo(vehicle);
        var statusBadges = GenerateStatusBadges(vehicle);
        var statisticsGrid = GenerateVehicleStatistics(vehicle);

        // ✅ Sezione Referente (se presente)
        var referentSection = !string.IsNullOrWhiteSpace(vehicle.ReferentName) ||
                            !string.IsNullOrWhiteSpace(vehicle.VehicleMobileNumber) ||
                            !string.IsNullOrWhiteSpace(vehicle.ReferentEmail) ? $@"
        <div class='info-grid' style='margin-top: 20px;'>
            <h4 style='grid-column: 1 / -1; margin: 10px 0;'>👤 Referente Veicolo</h4>
            {(!string.IsNullOrWhiteSpace(vehicle.ReferentName) ? $@"
            <div class='info-item'>
                <div class='info-label'>Nome Referente</div>
                <div class='info-value'>{vehicle.ReferentName}</div>
            </div>" : "")}
            {(!string.IsNullOrWhiteSpace(vehicle.VehicleMobileNumber) ? $@"
            <div class='info-item'>
                <div class='info-label'>Cellulare Operativo Autorizzato</div>
                <div class='info-value'>{vehicle.VehicleMobileNumber}</div>
            </div>" : "")}
            {(!string.IsNullOrWhiteSpace(vehicle.ReferentEmail) ? $@"
            <div class='info-item'>
                <div class='info-label'>Email Referente</div>
                <div class='info-value'>{vehicle.ReferentEmail}</div>
            </div>" : "")}
        </div>" : "";

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

            {referentSection}
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
            var daysText = vehicle.DaysSinceFirstActivation.HasValue && vehicle.DaysSinceFirstActivation > 0
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
        // ✅ Mostra solo statistiche non-zero o significative
        var hasStats = vehicle.TotalConsents > 0 || vehicle.TotalOutages > 0 ||
                    vehicle.TotalReports > 0 || vehicle.TotalSmsEvents > 0 ||
                    vehicle.VehicleOutageDays > 0;

        if (!hasStats)
        {
            return $@"
        <h4>📈 Statistiche Veicolo</h4>
        <div class='info-item'>
            <div class='info-label'>Stato</div>
            <div class='info-value'>Nessuna attività registrata</div>
        </div>";
        }

        var outageCard = vehicle.VehicleOutageDays > 0 ? $@"
        <div class='stat-card'>
            <div class='stat-number'>{vehicle.VehicleOutageDays}</div>
            <div class='stat-label'>Giorni Outage</div>
        </div>" : "";

        // ✅ NUOVO: Card SMS dettagliata
        var smsCard = vehicle.TotalSmsEvents > 0 ? $@"
        <div class='stat-card'>
            <div class='stat-number'>{vehicle.TotalSmsEvents}</div>
            <div class='stat-label'>SMS Eventi</div>
            <div style='font-size: 10px; margin-top: 5px; color: #666;'>
                ON: {vehicle.AdaptiveOnEvents} | OFF: {vehicle.AdaptiveOffEvents}
            </div>
        </div>" : "";

        // ✅ NUOVO: Card Sessioni Attive
        var activeSessionCard = vehicle.ActiveSessions > 0 ? $@"
        <div class='stat-card' style='border: 2px solid #10b981;'>
            <div class='stat-number' style='color: #10b981;'>{vehicle.ActiveSessions}</div>
            <div class='stat-label'>Sessioni SMS Attive</div>
            {(vehicle.ActiveSessionExpires.HasValue ? $@"
            <div style='font-size: 10px; margin-top: 5px; color: #10b981;'>
                Scade: {vehicle.ActiveSessionExpires:dd/MM/yyyy HH:mm}
            </div>" : "")}
        </div>" : "";

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
            {smsCard}
            {activeSessionCard}
            {outageCard}
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

        // ✅ NUOVO: Ultimo SMS ricevuto
        if (vehicle.LastSmsReceived.HasValue)
            activities.Add($"Ultimo SMS: {vehicle.LastSmsReceived:dd/MM/yyyy HH:mm}");

        if (!activities.Any())
            return "";

        return $@"
        <div class='info-item' style='margin-top: 15px;'>
            <div class='info-label'>Ultima Attività</div>
            <div class='info-value'>{string.Join(" • ", activities)}</div>
        </div>";
    }

    /// <summary>
    /// Genera il nome del file PDF
    /// </summary>
    private static string GenerateProfileFileName(CompanyProfileInfo company)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var sanitizedCompanyName = System.Text.RegularExpressions.Regex.Replace(
            company.Name, @"[^a-zA-Z0-9]", "_");

        return $"Profilo_Cliente_{sanitizedCompanyName}_{company.VatNumber}_{timestamp}.pdf";
    }
}