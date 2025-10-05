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
public class ClientProfileController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly PolarDriveLogger _logger;
    private readonly string _reportsStoragePath;
    private readonly PdfGenerationService _pdfService;

    public ClientProfileController(PolarDriveDbContext db, PdfGenerationService pdfService)
    {
        _db = db;
        _logger = new PolarDriveLogger(db);
        _reportsStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "storage", "client-profiles");
        _pdfService = pdfService;
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

    /// <summary>
    /// Genera un PDF completo del profilo cliente con tutte le informazioni aggregate
    /// </summary>
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

            var htmlContent = GenerateProfileHtml(profileData);

            var tempReport = new PdfReport
            {
                Id = 0,
                ReportPeriodStart = DateTime.UtcNow.AddMonths(-1),
                ReportPeriodEnd = DateTime.UtcNow,
                GeneratedAt = DateTime.UtcNow
            };

            // OPZIONI PERSONALIZZATE PER HEADER E FOOTER
            var pdfOptions = new PdfConversionOptions
            {
                HeaderTemplate = $@"
                    <html>
                    <head>
                        <style>
                            body {{
                                margin: 0;
                                padding: 0;
                                width: 100%;
                                height: 100%;
                                display: flex;
                                align-items: center;
                                justify-content: center;
                            }}
                            .header-content {{
                                font-size: 10px;
                                color: #ccc;
                                text-align: center;
                                border-bottom: 1px solid #ccc;
                                padding-bottom: 5px;
                                width: 100%;
                            }}
                        </style>
                    </head>
                    <body>
                        <div class='header-content'>Profilo Cliente - {company.Name} - {DateTime.Now:yyyy-MM-dd HH:mm}</div>
                    </body>
                    </html>",
                FooterTemplate = @"
                    <html>
                    <head>
                        <style>
                            body {
                                margin: 0;
                                padding: 0;
                                width: 100%;
                                height: 100%;
                                display: flex;
                                align-items: center;
                                justify-content: center;
                            }
                            .footer-content {
                                font-size: 10px;
                                color: #ccc;
                                text-align: center;
                                border-top: 1px solid #ccc;
                                padding-top: 5px;
                                width: 100%;
                            }
                        </style>
                    </head>
                    <body>
                        <div class='footer-content'>
                            Pagina <span class='pageNumber'></span> di <span class='totalPages'></span> | DataPolar Analytics
                        </div>
                    </body>
                    </html>"
            };

            // PASSA LE OPZIONI AL SERVIZIO PDF
            var pdfBytes = await _pdfService.ConvertHtmlToPdfAsync(htmlContent, tempReport, pdfOptions);

            var contentType = "application/pdf";
            var fileName = GenerateProfileFileName(profileData.CompanyInfo);

            if (System.Text.Encoding.UTF8.GetString(pdfBytes).StartsWith("<!DOCTYPE html") ||
                System.Text.Encoding.UTF8.GetString(pdfBytes).StartsWith("<html"))
            {
                contentType = "text/html";
                fileName = fileName.Replace(".pdf", ".html");
            }

            await _logger.Info("ClientProfileController.GenerateClientProfilePdf",
                "PDF generated successfully", $"Size: {pdfBytes.Length} bytes");

            return File(pdfBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientProfileController.GenerateClientProfilePdf",
                $"Error generating PDF for company {companyId}", ex.ToString());
            return StatusCode(500, new { message = "Errore generazione PDF", errorCode = "INTERNAL_SERVER_ERROR" });
        }
    }

    /// <summary>
    /// Ottiene i dati aggregati del profilo cliente dalla view
    /// </summary>
    private async Task<ClientProfileData?> GetClientProfileDataAsync(int companyId)
    {
        var previousTimeout = _db.Database.GetCommandTimeout();
        _db.Database.SetCommandTimeout(120); // 2 minuti

        try
        {
            var sql = @"
            SELECT 
                -- Dati azienda (SENZA referente)
                c.Id as ClientCompanyId, c.VatNumber, c.Name, c.Address, c.Email, c.PecAddress, c.LandlineNumber,
                c.CreatedAt as CompanyCreatedAt,
                -- Calcoli statistici azienda
                (SELECT COUNT(*) FROM ClientVehicles WHERE ClientCompanyId = c.Id) as TotalVehicles,
                (SELECT COUNT(*) FROM ClientVehicles WHERE ClientCompanyId = c.Id AND IsActiveFlag = 1) as ActiveVehicles,
                (SELECT COUNT(*) FROM ClientVehicles WHERE ClientCompanyId = c.Id AND IsFetchingDataFlag = 1) as FetchingVehicles,
                (SELECT COUNT(*) FROM ClientVehicles WHERE ClientCompanyId = c.Id AND ClientOAuthAuthorized = 1) as AuthorizedVehicles,
                (SELECT COUNT(DISTINCT Brand) FROM ClientVehicles WHERE ClientCompanyId = c.Id) as UniqueBrands,
                0 as TotalConsentsCompany, 0 as TotalOutagesCompany, 0 as TotalReportsCompany, 0 as TotalSmsEventsCompany,
                DATEDIFF(day, c.CreatedAt, GETDATE()) as DaysRegistered,
                NULL as FirstVehicleActivation, NULL as LastReportGeneratedCompany,
                NULL as LandlineNumbers, NULL as MobileNumbers, NULL as AssociatedPhones,
                
                -- Dati veicolo CON referente
                v.Id as VehicleId, v.Vin, v.Brand, v.Model, v.FuelType,
                CAST(ISNULL(v.IsActiveFlag, 0) AS BIT) as VehicleIsActive, 
                CAST(ISNULL(v.IsFetchingDataFlag, 0) AS BIT) as VehicleIsFetching, 
                CAST(ISNULL(v.ClientOAuthAuthorized, 0) AS BIT) as VehicleIsAuthorized,
                v.CreatedAt as VehicleCreatedAt, v.FirstActivationAt as VehicleFirstActivation, 
                v.LastDeactivationAt as VehicleLastDeactivation,
                
                -- Referenti dal veicolo
                v.ReferentName, v.ReferentMobileNumber, v.ReferentEmail,
                
                -- Statistiche veicolo
                0 as VehicleConsents, 0 as VehicleOutages, 0 as VehicleReports, 0 as VehicleSmsEvents,
                NULL as VehicleLastConsent, NULL as VehicleLastOutage, NULL as VehicleLastReport,
                CASE WHEN v.FirstActivationAt IS NOT NULL 
                    THEN DATEDIFF(day, v.FirstActivationAt, GETDATE())
                    ELSE NULL END as DaysSinceFirstActivation,
                0 as VehicleOutageDays
                
            FROM ClientCompanies c
            LEFT JOIN ClientVehicles v ON c.Id = v.ClientCompanyId
            WHERE c.Id = @companyId 
            ORDER BY v.Brand, v.Model, v.Vin";

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
                Vehicles = [.. rawData.Where(r => r.VehicleId.HasValue).Select(r => new VehicleProfileInfo
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
                    VehicleOutageDays = r.VehicleOutageDays,
                    ReferentName = r.ReferentName,
                    ReferentMobileNumber = r.ReferentMobileNumber,
                    ReferentEmail = r.ReferentEmail
                })]
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

                    /* ✅ SEZIONI COMPATTE */
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

                    /* ✅ GRIGLIE COMPATTE */
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

                    /* ✅ STATISTICHE COMPATTE */
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
                        text-align: center;
                        border: 1px solid #e9ecef;
                        box-shadow: 0 2px 4px rgba(0,0,0,0.1);
                        height: auto;
                        min-height: auto;
                    }}
                    .stat-number {{
                        font-size: 2.5em;
                        font-weight: 700;
                        color: #667eea;
                        margin-bottom: 5px;
                        line-height: 1;
                    }}
                    .stat-label {{
                        color: #6c757d;
                        font-weight: 500;
                        text-transform: uppercase;
                        font-size: 0.9em;
                        margin: 0;
                    }}

                    /* ✅ VEICOLI COMPATTI - ELIMINA SPAZI VUOTI */
                    .vehicle-card {{
                        background: white;
                        border: 1px solid #e9ecef;
                        border-radius: 8px;
                        padding: 20px;
                        margin-bottom: 15px;
                        box-shadow: 0 2px 4px rgba(0,0,0,0.1);
                        height: auto;
                        min-height: auto;
                        break-inside: avoid;
                        page-break-inside: avoid;
                    }}
                    .vehicle-header {{
                        display: flex;
                        align-items: center;
                        margin-bottom: 15px;
                        padding-bottom: 15px;
                        border-bottom: 2px solid #f8f9fa;
                        flex-wrap: wrap;
                        gap: 10px;
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
                        font-size: 0.9em;
                    }}

                    /* ✅ BADGE COMPATTI */
                    .status-badges {{
                        display: flex;
                        gap: 10px;
                        margin: 15px 0;
                        flex-wrap: wrap;
                    }}
                    .badge {{
                        padding: 6px 12px;
                        border-radius: 20px;
                        font-size: 0.85em;
                        font-weight: 600;
                        text-transform: uppercase;
                        white-space: nowrap;
                    }}
                    .badge.active {{ background: #d4edda; color: #155724; }}
                    .badge.inactive {{ background: #f8d7da; color: #721c24; }}
                    .badge.fetching {{ background: #d1ecf1; color: #0c5460; }}
                    .badge.authorized {{ background: #fff3cd; color: #856404; }}

                    .footer {{
                        text-align: center;
                        margin-top: 30px;
                        padding: 20px;
                        color: #6c757d;
                        border-top: 1px solid #e9ecef;
                    }}
                    .company-info {{
                        font-style: italic;
                    }}
                    .no-vehicles {{
                        text-align: center;
                        padding: 20px;
                        color: #6c757d;
                        font-style: italic;
                        background: white;
                        border-radius: 8px;
                        border: 1px solid #e9ecef;
                    }}

                    /* ✅ TITOLO VEICOLI SEPARATO */
                    .vehicles-title {{
                        color: #667eea;
                        margin: 30px 0 20px 0;
                        font-size: 1.8em;
                        font-weight: 600;
                        padding: 20px 25px;
                        background: #f8f9fa;
                        border-radius: 8px;
                        border-left: 5px solid #667eea;
                        /* ✅ PERMETTI IL BREAK DOPO IL TITOLO */
                        break-after: auto;
                        page-break-after: auto;
                    }}

                    /* ✅ RIMUOVI break-inside: avoid DALLA CLASSE .section PER I VEICOLI */
                    .section {{
                        background: #f8f9fa;
                        border-radius: 8px;
                        padding: 25px;
                        margin-bottom: 20px;
                        border-left: 5px solid #667eea;
                        /* ✅ RIMUOVI QUESTE RIGHE CHE CAUSANO PROBLEMI:
                        break-inside: avoid;
                        page-break-inside: avoid;
                        */
                    }}

                    /* ✅ VEHICLE CARD OTTIMIZZATE */
                    .vehicle-card {{
                        background: white;
                        border: 1px solid #e9ecef;
                        border-radius: 8px;
                        padding: 20px;
                        margin-bottom: 15px;
                        box-shadow: 0 2px 4px rgba(0,0,0,0.1);
                        height: auto;
                        min-height: auto;
                        /* ✅ MANTIENI BREAK SOLO PER LE CARD */
                        break-inside: avoid;
                        page-break-inside: avoid;
                    }}

                    /* ✅ PRINT OTTIMIZZATO */
                    @media print {{
                        body {{ margin: 0; padding: 10px; }}
                        
                        /* ✅ TITOLO VEICOLI IN STAMPA */
                        .vehicles-title {{
                            margin: 20px 0 15px 0;
                            break-after: auto;
                            page-break-after: auto;
                        }}
                        
                        /* ✅ SEZIONI NORMALI */
                        .section {{ 
                            margin-bottom: 10px;
                            /* ✅ NON USARE break-inside: avoid */
                        }}
                        
                        /* ✅ VEHICLE CARD */
                        .vehicle-card {{ 
                            break-inside: avoid; 
                            page-break-inside: avoid;
                            margin-bottom: 10px;
                        }}
                        
                        .stats-grid {{ gap: 8px; }}
                        .info-grid {{ gap: 8px; }}
                        
                        .section:last-child {{ margin-bottom: 0; }}
                        .vehicle-card:last-child {{ margin-bottom: 0; }}
                    }}

                    /* ✅ RESPONSIVE */
                    @media (max-width: 768px) {{
                        .info-grid {{
                            grid-template-columns: 1fr;
                        }}
                        .stats-grid {{
                            grid-template-columns: repeat(2, 1fr);
                        }}
                        .vehicle-header {{
                            flex-direction: column;
                            align-items: flex-start;
                        }}
                    }}

                    /* ✅ PRINT OTTIMIZZATO - ELIMINA PAGINE VUOTE */
                    @media print {{
                        body {{ margin: 0; padding: 10px; }}
                        .section {{ 
                            break-inside: avoid; 
                            page-break-inside: avoid;
                            margin-bottom: 10px;
                        }}
                        .vehicle-card {{ 
                            break-inside: avoid; 
                            page-break-inside: avoid;
                            margin-bottom: 10px;
                        }}
                        .stats-grid {{ gap: 8px; }}
                        .info-grid {{ gap: 8px; }}
                        /* ✅ ELIMINA MARGIN FINALE */
                        .section:last-child {{
                            margin-bottom: 0;
                        }}
                        .vehicle-card:last-child {{
                            margin-bottom: 0;
                        }}
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
                            <div class='info-label'>Email Aziendale</div>
                            <div class='info-value'>{data.CompanyInfo.Email}</div>
                        </div>
                        <div class='info-item'>
                            <div class='info-label'>PEC Aziendale</div>
                            <div class='info-value'>{data.CompanyInfo.PecAddress ?? "Non specificata"}</div>
                        </div>
                        <div class='info-item'>
                            <div class='info-label'>Data Registrazione</div>
                            <div class='info-value'>{data.CompanyInfo.CompanyCreatedAt:dd/MM/yyyy} ({data.CompanyInfo.DaysRegistered} giorni fa)</div>
                        </div>
                        <div class='info-item'>
                            <div class='info-label'>Telefono Fisso</div>
                            <div class='info-value'>{data.CompanyInfo.LandlineNumber ?? "Non specificato"}</div>
                        </div>
                    </div>

                    {GeneratePhoneNumbersSection(data.CompanyInfo)}
                </div>

                <div class='section'>
                    <h2>📊 Altre Statistiche</h2>
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
                    <p>Report generato da PolarDrive™ • {generationDate}</p>
                    <p class='company-info'>DataPolar - The future of AI</p>
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
    /// Genera la sezione dei veicoli SENZA WRAPPER SECTION
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

        // ✅ GENERA DIRETTAMENTE LE CARD SENZA IL DIV SECTION WRAPPER
        var vehiclesHtml = string.Join("", vehicles.Select(GenerateVehicleCard));

        return $@"
            <h2 class='vehicles-title'>🚗 Veicoli Associati ({vehicles.Count})</h2>
            {vehiclesHtml}";
    }

    /// <summary>
    /// Genera la card per un singolo veicolo
    /// </summary>
    private string GenerateVehicleCard(VehicleProfileInfo vehicle)
    {
        var statusBadges = GenerateStatusBadges(vehicle);
        var activationInfo = GenerateActivationInfo(vehicle);
        var statisticsGrid = GenerateVehicleStatistics(vehicle);

        // ✅ Referente solo se presente
        var referentSection = !string.IsNullOrEmpty(vehicle.ReferentName) ? $@"
        <h4>👤 Referente Veicolo</h4>
        <div class='info-grid'>
            <div class='info-item'>
                <div class='info-label'>Nome Referente</div>
                <div class='info-value'>{vehicle.ReferentName}</div>
            </div>
            {(!string.IsNullOrEmpty(vehicle.ReferentMobileNumber) ? $@"
            <div class='info-item'>
                <div class='info-label'>Cellulare Referente</div>
                <div class='info-value'>{vehicle.ReferentMobileNumber}</div>
            </div>" : "")}
            {(!string.IsNullOrEmpty(vehicle.ReferentEmail) ? $@"
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