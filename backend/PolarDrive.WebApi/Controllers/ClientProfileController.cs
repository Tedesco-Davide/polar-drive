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
                return NotFound(new { errorCode = "COMPANY_NOT_FOUND" });
            }

            var profileData = await GetClientProfileDataAsync(companyId);
            if (profileData == null)
            {
                return NotFound(new { errorCode = "PROFILE_DATA_NOT_FOUND" });
            }

            return Ok(profileData);
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientProfileController.GetClientProfileData",
                $"Error retrieving client profile data for company {companyId}", ex.ToString());
            return StatusCode(500, new { errorCode = "INTERNAL_SERVER_ERROR" });
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
                return NotFound(new { errorCode = "COMPANY_NOT_FOUND" });
            }

            var profileData = await GetClientProfileDataAsync(companyId);
            if (profileData == null)
            {
                return NotFound(new { errorCode = "PROFILE_DATA_NOT_FOUND" });
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

            // ✅ NUOVO: Recupera dati ADAPTIVE_GDPR, ADAPTIVE_PROFILE e OUTAGES
            var vehicles = rawData
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
                    TotalConsents = r.VehicleConsents,
                    TotalOutages = r.VehicleOutages,
                    TotalReports = r.VehicleReports,
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
                .ToList();

            var vehicleIds = vehicles.Select(v => v.Id).ToList();

            var adaptiveGdprConsents = await GetAdaptiveGdprConsentsAsync(companyId);
            var adaptiveProfileUsers = await GetAdaptiveProfileUsersAsync(companyId);
            var vehicleAdaptiveProfiles = await GetVehicleAdaptiveProfilesAsync(vehicleIds);
            var (outagesSummary, outages) = await GetOutagesForCompanyAsync(companyId);

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
                    ActiveSessionsCompany = firstRow.ActiveSessionsCompany,
                    LastSmsReceivedCompany = firstRow.LastSmsReceivedCompany,
                    LastActiveSessionExpiresCompany = firstRow.LastActiveSessionExpiresCompany,

                    FirstVehicleActivation = firstRow.FirstVehicleActivation,
                    LastReportGeneratedCompany = firstRow.LastReportGeneratedCompany,
                },
                Vehicles = vehicles,

                // ✅ NUOVO: Dati ADAPTIVE_GDPR, ADAPTIVE_PROFILE e OUTAGES
                AdaptiveGdprConsents = adaptiveGdprConsents,
                AdaptiveProfileUsers = adaptiveProfileUsers,
                VehicleAdaptiveProfiles = vehicleAdaptiveProfiles,
                OutagesSummary = outagesSummary,
                Outages = outages
            };
        }
        finally
        {
            // Ripristina il timeout originale
            _db.Database.SetCommandTimeout(previousTimeout);
        }
    }

    /// <summary>
    /// Recupera tutti i consensi ADAPTIVE_GDPR per l'azienda (storici)
    /// </summary>
    private async Task<List<AdaptiveGdprConsentDto>> GetAdaptiveGdprConsentsAsync(int companyId)
    {
        var consents = await _db.SmsAdaptiveGdpr
            .Where(g => g.ClientCompanyId == companyId)
            .OrderByDescending(g => g.RequestedAt)
            .Select(g => new AdaptiveGdprConsentDto
            {
                Id = g.Id,
                AdaptiveNumber = g.AdaptiveNumber,
                AdaptiveSurnameName = g.AdaptiveSurnameName,
                Brand = g.Brand,
                RequestedAt = g.RequestedAt,
                ConsentGivenAt = g.ConsentGivenAt,
                ConsentAccepted = g.ConsentAccepted,
                Status = g.ConsentAccepted ? "Attivo" : "Revocato"
            })
            .ToListAsync();

        return consents;
    }

    /// <summary>
    /// Recupera tutti gli utilizzatori ADAPTIVE_PROFILE per l'azienda (aggregati per utente)
    /// </summary>
    private async Task<List<AdaptiveProfileUserDto>> GetAdaptiveProfileUsersAsync(int companyId)
    {
        // Get all vehicles for the company
        var vehicleIds = await _db.ClientVehicles
            .Where(v => v.ClientCompanyId == companyId)
            .Select(v => v.Id)
            .ToListAsync();

        if (!vehicleIds.Any())
            return new List<AdaptiveProfileUserDto>();

        // Get all ADAPTIVE_PROFILE sessions for company vehicles
        var sessions = await _db.SmsAdaptiveProfile
            .Include(s => s.ClientVehicle)
            .Where(s => vehicleIds.Contains(s.VehicleId)
                    && s.ParsedCommand == "ADAPTIVE_PROFILE_ON")
            .OrderBy(s => s.ReceivedAt)
            .ToListAsync();

        // Group by user (AdaptiveNumber + AdaptiveSurnameName)
        var userGroups = sessions
            .GroupBy(s => new { s.AdaptiveNumber, s.AdaptiveSurnameName })
            .Select(g => new AdaptiveProfileUserDto
            {
                AdaptiveNumber = g.Key.AdaptiveNumber,
                AdaptiveSurnameName = g.Key.AdaptiveSurnameName,
                FirstActivation = g.Min(s => s.ReceivedAt),
                LastActivation = g.Max(s => s.ReceivedAt),
                LastExpiry = g.OrderByDescending(s => s.ReceivedAt).First().ExpiresAt,
                TotalSessions = g.Count(),
                HasRevokedConsent = !g.OrderByDescending(s => s.ReceivedAt).First().ConsentAccepted,
                VehicleIds = g.Select(s => s.VehicleId).Distinct().ToList(),
                VehicleVins = g.Select(s => s.ClientVehicle!.Vin).Distinct().ToList()
            })
            .OrderBy(u => u.AdaptiveSurnameName)
            .ToList();

        return userGroups;
    }

    /// <summary>
    /// Recupera dettagli ADAPTIVE_PROFILE per ogni veicolo
    /// </summary>
    private async Task<Dictionary<int, VehicleAdaptiveProfileDto>> GetVehicleAdaptiveProfilesAsync(List<int> vehicleIds)
    {
        var result = new Dictionary<int, VehicleAdaptiveProfileDto>();

        foreach (var vehicleId in vehicleIds)
        {
            var vehicle = await _db.ClientVehicles.FindAsync(vehicleId);
            if (vehicle == null) continue;

            // Get all sessions for this vehicle
            var sessions = await _db.SmsAdaptiveProfile
                .Where(s => s.VehicleId == vehicleId
                        && s.ParsedCommand == "ADAPTIVE_PROFILE_ON")
                .OrderBy(s => s.ReceivedAt)
                .ToListAsync();

            // Count certified records (VehiclesData with IsSmsAdaptiveProfile = true)
            var certifiedCount = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId && vd.IsSmsAdaptiveProfile)
                .CountAsync();

            // Group by user
            var users = sessions
                .GroupBy(s => new { s.AdaptiveNumber, s.AdaptiveSurnameName })
                .Select(g => new AdaptiveProfileUserDto
                {
                    AdaptiveNumber = g.Key.AdaptiveNumber,
                    AdaptiveSurnameName = g.Key.AdaptiveSurnameName,
                    FirstActivation = g.Min(s => s.ReceivedAt),
                    LastActivation = g.Max(s => s.ReceivedAt),
                    LastExpiry = g.OrderByDescending(s => s.ReceivedAt).First().ExpiresAt,
                    TotalSessions = g.Count(),
                    HasRevokedConsent = !g.OrderByDescending(s => s.ReceivedAt).First().ConsentAccepted
                })
                .ToList();

            result[vehicleId] = new VehicleAdaptiveProfileDto
            {
                VehicleId = vehicleId,
                Vin = vehicle.Vin,
                Users = users,
                TotalSessionsCount = sessions.Count,
                CertifiedRecordsCount = certifiedCount
            };
        }

        return result;
    }

    /// <summary>
    /// Helper: Calcola la durata di un outage
    /// </summary>
    private (int Days, int Hours) CalculateDuration(DateTime start, DateTime? end)
    {
        var endTime = end ?? DateTime.Now;
        var span = endTime - start;
        return (span.Days, span.Hours % 24);
    }

    /// <summary>
    /// Helper: Calcola il totale di veicoli impattati dagli outages
    /// </summary>
    private int CalculateTotalVehiclesAffected(List<OutageDetailDto> outages, List<dynamic> vehicles)
    {
        var affectedVehicleIds = new HashSet<int>();

        foreach (var outage in outages)
        {
            if (outage.OutageType == "Outage Vehicle" && outage.VehicleId.HasValue)
            {
                affectedVehicleIds.Add(outage.VehicleId.Value);
            }
            else if (outage.OutageType == "Outage Fleet Api")
            {
                var brandVehicles = vehicles
                    .Where(v => v.Brand.Equals(outage.OutageBrand, StringComparison.OrdinalIgnoreCase))
                    .Select(v => (int)v.Id);
                foreach (var vid in brandVehicles)
                    affectedVehicleIds.Add(vid);
            }
        }

        return affectedVehicleIds.Count;
    }

    /// <summary>
    /// Recupera tutti gli outages che hanno impattato l'azienda (brand-level + vehicle-specific)
    /// </summary>
    private async Task<(OutagesSummaryDto summary, List<OutageDetailDto> outages)> GetOutagesForCompanyAsync(int companyId)
    {
        // Step 1: Get all vehicles for this company with their brands
        var companyVehicles = await _db.ClientVehicles
            .Where(v => v.ClientCompanyId == companyId)
            .Select(v => new { v.Id, v.Vin, v.Brand, v.Model })
            .ToListAsync();

        var vehicleIds = companyVehicles.Select(v => v.Id).ToList();
        var brandsByCompany = companyVehicles.Select(v => v.Brand.ToLowerInvariant()).Distinct().ToList();

        // Step 2: Query vehicle-specific outages for this company's vehicles
        var vehicleOutages = await _db.OutagePeriods
            .Where(o => o.OutageType == "Outage Vehicle" &&
                        o.VehicleId.HasValue &&
                        vehicleIds.Contains(o.VehicleId.Value))
            .OrderByDescending(o => o.OutageStart)
            .ToListAsync();

        // Step 3: Query brand-level (Fleet API) outages affecting this company's brands
        var brandOutages = await _db.OutagePeriods
            .Where(o => o.OutageType == "Outage Fleet Api" &&
                        brandsByCompany.Contains(o.OutageBrand.ToLower()))
            .OrderByDescending(o => o.OutageStart)
            .ToListAsync();

        // Step 4: Build OutageDetailDto list
        var outageDetails = new List<OutageDetailDto>();

        // Add vehicle-specific outages
        foreach (var outage in vehicleOutages)
        {
            var vehicle = companyVehicles.FirstOrDefault(v => v.Id == outage.VehicleId);
            var duration = CalculateDuration(outage.OutageStart, outage.OutageEnd);

            outageDetails.Add(new OutageDetailDto
            {
                Id = outage.Id,
                OutageType = outage.OutageType,
                OutageBrand = outage.OutageBrand,
                OutageStart = outage.OutageStart,
                OutageEnd = outage.OutageEnd,
                DurationDays = duration.Days,
                DurationHours = duration.Hours,
                Notes = outage.Notes,
                AutoDetected = outage.AutoDetected,
                VehicleId = outage.VehicleId,
                Vin = vehicle?.Vin,
                VehicleModel = vehicle?.Model,
                AffectedVehicleVins = new List<string>(),
                AffectedVehicleCount = 0
            });
        }

        // Add brand-level outages with affected vehicles
        foreach (var outage in brandOutages)
        {
            var affectedVehicles = companyVehicles
                .Where(v => v.Brand.Equals(outage.OutageBrand, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var duration = CalculateDuration(outage.OutageStart, outage.OutageEnd);

            outageDetails.Add(new OutageDetailDto
            {
                Id = outage.Id,
                OutageType = outage.OutageType,
                OutageBrand = outage.OutageBrand,
                OutageStart = outage.OutageStart,
                OutageEnd = outage.OutageEnd,
                DurationDays = duration.Days,
                DurationHours = duration.Hours,
                Notes = outage.Notes,
                AutoDetected = outage.AutoDetected,
                VehicleId = null,
                Vin = null,
                VehicleModel = null,
                AffectedVehicleVins = affectedVehicles.Select(v => v.Vin).ToList(),
                AffectedVehicleCount = affectedVehicles.Count
            });
        }

        // Step 5: Calculate summary statistics
        var summary = new OutagesSummaryDto
        {
            TotalOutages = outageDetails.Count,
            OngoingOutages = outageDetails.Count(o => o.IsOngoing),
            ResolvedOutages = outageDetails.Count(o => !o.IsOngoing),
            TotalDowntimeDays = outageDetails.Sum(o => o.DurationDays),
            BrandLevelOutages = brandOutages.Count,
            VehicleSpecificOutages = vehicleOutages.Count,
            OutagesByBrand = outageDetails
                .GroupBy(o => o.OutageBrand)
                .ToDictionary(g => g.Key, g => g.Count()),
            FirstOutageDate = outageDetails.Any() ? outageDetails.Min(o => o.OutageStart) : null,
            LastOutageDate = outageDetails.Any() ? outageDetails.Max(o => o.OutageStart) : null,
            AverageOutageDurationDays = outageDetails.Any()
                ? Math.Round(outageDetails.Average(o => o.DurationDays + (o.DurationHours / 24.0)), 2)
                : 0,
            TotalVehiclesAffected = CalculateTotalVehiclesAffected(outageDetails, companyVehicles.Cast<dynamic>().ToList())
        };

        return (summary, outageDetails.OrderByDescending(o => o.OutageStart).ToList());
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
                .header h2 {{
                    margin: 0;
                    font-size: 2em;
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
                }}
                .section h3 {{
                    color: #667eea;
                    margin-top: 0;
                    margin-bottom: 20px;
                    font-size: 1.5em;
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
                    font-size: 2em;
                    font-weight: 700;
                    color: #667eea;
                    margin-bottom: 5px;
                }}
                .stat-label {{
                    color: #6c757d;
                    font-size: 0.75em;
                    text-transform: uppercase;
                    letter-spacing: 1px;
                }}
                .vehicle-card {{
                    background: white;
                    border: 1px solid #dee2e6;
                    border-radius: 8px;
                    padding: 20px;
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

                .certification-table {{
                    width: 100%;
                    border-collapse: collapse;
                    background: white;
                    border-radius: 8px;
                    box-shadow: 0 4px 12px rgba(139, 159, 242, 0.1);
                    border: 1px solid rgba(139, 159, 242, 0.2);
                    margin: 15px 0;
                    page-break-inside: avoid;
                }}
                .certification-table thead {{
                    background: linear-gradient(135deg, #8b9ff2 0%, #9c82c7 100%);
                }}
                .certification-table th {{
                    padding: 10px 12px;
                    text-align: left;
                    font-weight: 600;
                    font-size: 9px;
                    color: white;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                }}
                .certification-table tbody tr:nth-child(even) {{
                    background: rgba(139, 159, 242, 0.03);
                }}
                .certification-table td {{
                    padding: 8px 12px;
                    font-size: 9px;
                    color: #4a5568;
                    border-bottom: 1px solid rgba(139, 159, 242, 0.1);
                }}

                /* ✅ NUOVO: STATUS BADGES */
                .status-badge {{
                    display: inline-block;
                    padding: 4px 10px;
                    border-radius: 6px;
                    font-size: 10px;
                    font-weight: 600;
                    text-transform: uppercase;
                }}
                .status-badge.active {{
                    background: rgba(72, 187, 120, 0.15);
                    color: #276749;
                    border: 1px solid rgba(72, 187, 120, 0.4);
                }}
                .status-badge.revoked {{
                    background: rgba(229, 62, 62, 0.15);
                    color: #c53030;
                    border: 1px solid rgba(229, 62, 62, 0.4);
                }}
                .status-badge.info {{
                    background: rgba(139, 159, 242, 0.15);
                    color: #5a67d8;
                    border: 1px solid rgba(139, 159, 242, 0.4);
                }}

                /* ✅ NUOVO: LEGENDA ADAPTIVE */
                .adaptive-legend {{
                    display: flex;
                    flex-direction: column;
                    gap: 15px;
                    margin: 20px 0;
                    page-break-inside: avoid;
                }}
                .adaptive-legend-item {{
                    border-radius: 10px;
                    padding: 18px;
                    page-break-inside: avoid;
                }}
                .adaptive-legend-yes {{
                    background: linear-gradient(135deg, rgba(72, 187, 120, 0.15) 0%, rgba(72, 187, 120, 0.08) 100%);
                    border: 1px solid rgba(72, 187, 120, 0.4);
                }}
                .adaptive-legend-no {{
                    background: linear-gradient(135deg, rgba(203, 203, 203, 0.15) 0%, rgba(203, 203, 203, 0.08) 100%);
                    border: 1px solid rgba(160, 160, 160, 0.4);
                }}
                .adaptive-legend-badge {{
                    display: inline-block;
                    font-weight: 700;
                    font-size: 13px;
                    padding: 5px 12px;
                    border-radius: 20px;
                    margin-bottom: 10px;
                }}
                .adaptive-legend-yes .adaptive-legend-badge {{
                    background: linear-gradient(135deg, #48bb78 0%, #38a169 100%);
                    color: white;
                }}
                .adaptive-legend-no .adaptive-legend-badge {{
                    background: linear-gradient(135deg, #a0aec0 0%, #718096 100%);
                    color: white;
                }}
                .adaptive-legend-description {{
                    font-size: 12px;
                    line-height: 1.6;
                    color: #4a5568;
                }}

                /* ✅ NUOVO: SUMMARY BOXES */
                .summary-box {{
                    background: linear-gradient(135deg, rgba(139, 159, 242, 0.08) 0%, rgba(156, 130, 199, 0.08) 100%);
                    border: 1px solid rgba(139, 159, 242, 0.2);
                    border-radius: 10px;
                    padding: 18px;
                    margin: 15px 0;
                    page-break-inside: avoid;
                }}
                .summary-box h6 {{
                    font-size: 13px;
                    font-weight: 600;
                    margin: 0 0 12px 0;
                    color: #4a5568;
                }}
                .summary-table {{
                    width: 100%;
                    margin: 0;
                }}
                .summary-table td {{
                    padding: 8px 12px;
                    font-size: 12px;
                    border-bottom: 1px solid rgba(139, 159, 242, 0.1);
                }}
                .summary-table td:first-child {{
                    font-weight: 500;
                    color: #4a5568;
                }}
                .summary-table td:last-child {{
                    font-weight: 700;
                    color: #2d3748;
                    text-align: right;
                }}

                /* ✅ NUOVO: VEHICLE CARDS - Layout orizzontale per veicoli */
                .vehicle-card {{
                    background: white;
                    border: 1px solid rgba(139, 159, 242, 0.2);
                    border-radius: 10px;
                    padding: 20px;
                    margin: 15px 0;
                    box-shadow: 0 2px 8px rgba(139, 159, 242, 0.1);
                }}
                .vehicle-card-header {{
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    margin-bottom: 10px;
                    border-bottom: 2px solid rgba(139, 159, 242, 0.2);
                    padding-bottom: 10px;
                }}
                .vehicle-card-title {{
                    font-size: 14px;
                    font-weight: 600;
                    color: #2d3748;
                }}
                .vehicle-card-fuel {{
                    font-size: 11px;
                    color: #718096;
                    background: rgba(139, 159, 242, 0.1);
                    padding: 4px 10px;
                    border-radius: 6px;
                }}
                .vehicle-card-vin {{
                    font-size: 10px;
                    color: #718096;
                    margin-bottom: 12px;
                }}
                .vehicle-card-section {{
                    margin: 8px 0;
                    padding: 6px 0;
                    border-bottom: 1px solid rgba(139, 159, 242, 0.1);
                }}
                .vehicle-card-section:last-child {{
                    border-bottom: none;
                }}
                .vehicle-card-label {{
                    font-size: 11px;
                    color: #8b9ff2;
                    font-weight: 600;
                    margin-bottom: 4px;
                }}
                .vehicle-sms-adaptive-list {{
                    list-style: none;
                    padding-left: 0;
                    margin: 4px 0;
                }}
                .vehicle-sms-adaptive-list li {{
                    font-size: 11px;
                    color: #4a5568;
                    margin: 2px 0;
                    padding-left: 15px;
                    position: relative;
                }}
                .vehicle-sms-adaptive-list li:before {{
                    content: '•';
                    position: absolute;
                    left: 0;
                    color: #8b9ff2;
                }}
                .vehicle-adaptive-empty {{
                    font-size: 11px;
                    color: #718096;
                    font-style: italic;
                }}

                /* ✅ NUOVO: OUTAGES SECTION */
                /* ⚡ Font size ridotto per tabelle outages */
                .outages-section .certification-table th {{
                    font-size: 10px !important;
                    padding: 10px 12px;
                }}
                .outages-section .certification-table td {{
                    font-size: 10px !important;
                    padding: 8px 12px;
                }}
                .outage-ongoing {{
                    border-left: 4px solid #dc3545;
                }}
                .outage-badge {{
                    padding: 4px 10px;
                    border-radius: 4px;
                    font-weight: 600;
                    font-size: 9px;
                    display: inline-block;
                }}
                .outage-badge.ongoing {{
                    background: #f8d7da;
                    color: #721c24;
                }}
                .outage-badge.resolved {{
                    background: #d4edda;
                    color: #155724;
                }}
                .outage-badge.auto {{
                    background: #fff3cd;
                    color: #856404;
                }}
                .outage-badge.manual {{
                    background: #d1ecf1;
                    color: #0c5460;
                }}
                .outage-impact {{
                    background: #e7f3ff;
                    color: #0066cc;
                    padding: 4px 10px;
                    border-radius: 4px;
                    font-weight: 600;
                    font-size: 9px;
                }}
            </style>
        </head>
            <body>
                <div class='header'>
                    <h2>📊 Profilo Cliente Certificato</h2>
                    <div class='subtitle'>{data.CompanyInfo.Name}</div>
                    <div style='margin-top: 10px; font-size: 0.9em; opacity: 0.8;'>
                        Generato il {generationDate}
                    </div>
                </div>

                {GenerateCompanySection(data.CompanyInfo)}
                {GenerateAdaptiveGdprSection(data.AdaptiveGdprConsents)}
                {GenerateAdaptiveProfileLegend()}
                {GenerateAdaptiveProfileSection(data.AdaptiveProfileUsers, data)}
                {GenerateVehiclesSection(data.Vehicles, data.VehicleAdaptiveProfiles)}
                {GenerateOutagesSection(data.OutagesSummary, data.Outages)}

                <!-- ⚠️ NO FOOTER: Rimosso per evitare pagina extra sprecata -->
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
        </div>" : "";

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
            <h3>🏢 Informazioni Azienda</h3>
            <div class='info-grid'>
                <div class='info-item'>
                    <div class='info-label'>Ragione Sociale</div>
                    <div class='info-value'>{(string.IsNullOrWhiteSpace(company.Name) ? "-" : company.Name)}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Partita IVA</div>
                    <div class='info-value'>{(string.IsNullOrWhiteSpace(company.VatNumber) ? "-" : company.VatNumber)}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Indirizzo</div>
                    <div class='info-value'>{(string.IsNullOrWhiteSpace(company.Address) ? "-" : company.Address)}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Email</div>
                    <div class='info-value'>{(string.IsNullOrWhiteSpace(company.Email) ? "-" : company.Email)}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>PEC</div>
                    <div class='info-value'>{(string.IsNullOrWhiteSpace(company.PecAddress) ? "-" : company.PecAddress)}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Telefono</div>
                    <div class='info-value'>{(string.IsNullOrWhiteSpace(company.LandlineNumber) ? "-" : company.LandlineNumber)}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Data Registrazione</div>
                    <div class='info-value'>{company.CompanyCreatedAt:dd/MM/yyyy} ({company.DaysRegistered} giorni fa)</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Prima Attivazione Veicolo</div>
                    <div class='info-value'>{(company.FirstVehicleActivation.HasValue ? $"{company.FirstVehicleActivation:dd/MM/yyyy HH:mm}" : "-")}</div>
                </div>
            </div>
        </div>
        <div class='section' style='page-break-before: always;'>
            <h3>📈 Statistiche Flotta</h3>
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
                    <div class='stat-label'>Acquisizione Dati</div>
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
    private string GenerateVehiclesSection(List<VehicleProfileInfo> vehicles, Dictionary<int, VehicleAdaptiveProfileDto> adaptiveProfiles)
    {
        if (!vehicles.Any())
        {
            return @"
            <div class='section'>
                <h3>🚗 Veicoli</h3>
                <p style='color: #6c757d; font-style: italic;'>Nessun veicolo registrato per questa azienda.</p>
            </div>";
        }

        var vehicleCards = string.Join("", vehicles.Select(v => GenerateVehicleCard(v, adaptiveProfiles)));

        return $@"
        <div class='section'>
            <h3>🚗 Veicoli ({vehicles.Count})</h3>
            {vehicleCards}
        </div>";
    }

    /// <summary>
    /// Genera la card per un singolo veicolo (con dati adaptive)
    /// </summary>
    private string GenerateVehicleCard(VehicleProfileInfo vehicle, Dictionary<int, VehicleAdaptiveProfileDto> adaptiveProfiles)
    {
        var statusBadges = GenerateStatusBadges(vehicle);

        // ✅ Sezione SMS & Adaptive
        var smsAdaptiveSection = GenerateSmsAdaptiveSection(vehicle, adaptiveProfiles);

        return $@"
        <div class='vehicle-card'>
            <div class='vehicle-card-header'>
                <div class='vehicle-card-title'>🚗 {vehicle.Brand} {vehicle.Model} ➜ VIN: {vehicle.Vin}</div>
                <div class='vehicle-card-fuel'>{vehicle.FuelType}</div>
            </div>

            <div class='vehicle-card-section'>
                <div class='vehicle-card-label'>Prima Attivazione: {(vehicle.FirstActivationAt.HasValue ? vehicle.FirstActivationAt.Value.ToString("dd/MM/yyyy") : "N/A")} ➜ Stato attuale:</div>
                {statusBadges}
            </div>

            <div class='vehicle-card-section'>
                <div class='vehicle-card-label'>📊 Attività: {vehicle.TotalConsents} Consensi | {vehicle.TotalOutages} Outages | {vehicle.TotalReports} Report</div>
            </div>

            {smsAdaptiveSection}
        </div>";
    }

    /// <summary>
    /// Genera la sezione SMS & Adaptive per un veicolo
    /// </summary>
    private static string GenerateSmsAdaptiveSection(VehicleProfileInfo vehicle, Dictionary<int, VehicleAdaptiveProfileDto> adaptiveProfiles)
    {
        var hasAdaptive = adaptiveProfiles.ContainsKey(vehicle.Id) && adaptiveProfiles[vehicle.Id].Users.Any();

        if (!hasAdaptive && vehicle.TotalSmsEvents == 0)
        {
            // Caso: nessun SMS e nessun adaptive
            return $@"
            <div class='vehicle-card-section'>
                <div class='vehicle-card-label'>📱 SMS & Adaptive:</div>
                <div class='vehicle-adaptive-empty'>Nessuna procedura adaptive presente</div>
            </div>";
        }

        var adaptiveData = hasAdaptive ? adaptiveProfiles[vehicle.Id] : null;

        return $@"
        <div class='vehicle-card-section'>
            <div class='vehicle-card-label'>📱 SMS & Adaptive:</div>
            <ul class='vehicle-sms-adaptive-list'>
                <li>SMS Totali: {vehicle.TotalSmsEvents} (ON: {vehicle.AdaptiveOnEvents} | OFF: {vehicle.AdaptiveOffEvents})</li>
                {(hasAdaptive && adaptiveData != null ? $@"
                <li>👥 Utilizzatori terzi: {adaptiveData.Users.Count}</li>
                <li>📊 Sessioni totali: {adaptiveData.TotalSessionsCount}</li>
                <li>✅ Records certificati: {adaptiveData.CertifiedRecordsCount}</li>" : "")}
            </ul>
        </div>";
    }

    /// <summary>
    /// Genera i badge di stato per il veicolo
    /// </summary>
    private static string GenerateStatusBadges(VehicleProfileInfo vehicle)
    {
        var badges = new List<string>
        {
            vehicle.IsActive
            ? "<span class='status-badge active'>ATTIVO</span>"
            : "<span class='status-badge revoked'>INATTIVO</span>",
            vehicle.IsFetching
            ? "<span class='status-badge active'>FETCHING</span>"
            : "<span class='status-badge info'>NO FETCH</span>",
            vehicle.IsAuthorized
            ? "<span class='status-badge active'>OAUTH</span>"
            : "<span class='status-badge info'>NO OAUTH</span>"
        };

        return string.Join(" ", badges);
    }

    /// <summary>
    /// Genera le informazioni di attivazione
    /// </summary>
    private static string GenerateActivationInfo(VehicleProfileInfo vehicle)
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
        <h5>📈 Statistiche Veicolo</h5>
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
        <h5>📈 Statistiche Veicolo</h5>
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
    /// Genera la sezione ADAPTIVE_GDPR con tutti i consensi storici
    /// </summary>
    private string GenerateAdaptiveGdprSection(List<AdaptiveGdprConsentDto> consents)
    {
        if (consents.Count == 0)
        {
            return @"
            <div class='section' style='page-break-before: always;'>
                <h3>🔒 Procedure ADAPTIVE_GDPR</h3>
                <div class='summary-box'>
                    <p style='color: #6c757d; font-style: italic;'>
                        Nessun consenso ADAPTIVE_GDPR registrato per questa azienda.
                    </p>
                </div>
            </div>";
        }

        var activeCount = consents.Count(c => c.ConsentAccepted);
        var revokedCount = consents.Count(c => !c.ConsentAccepted);

        var rows = string.Join("", consents.Select(c => $@"
            <tr>
                <td>{c.AdaptiveSurnameName}</td>
                <td>{c.AdaptiveNumber}</td>
                <td>{c.Brand}</td>
                <td>{c.RequestedAt:dd/MM/yyyy HH:mm}</td>
                <td>{(c.ConsentGivenAt.HasValue ? c.ConsentGivenAt.Value.ToString("dd/MM/yyyy HH:mm") : "-")}</td>
                <td>
                    <span class='status-badge {(c.ConsentAccepted ? "active" : "revoked")}'>
                        {c.Status}
                    </span>
                </td>
            </tr>"));

        return $@"
        <div class='section' style='page-break-before: always;'>
            <h3>🔒 Procedure ADAPTIVE_GDPR</h3>
            <div class='summary-box'>
                <p style='margin-bottom: 10px;'>
                    Questa sezione certifica tutti i consensi GDPR richiesti per utilizzatori terzi autorizzati
                    ad operare sui veicoli dell'azienda tramite procedura ADAPTIVE_GDPR
                </p>
                <table class='summary-table'>
                    <tr>
                        <td>Consensi totali richiesti</td>
                        <td>{consents.Count}</td>
                    </tr>
                    <tr>
                        <td>Consensi attivi</td>
                        <td>{activeCount}</td>
                    </tr>
                    <tr>
                        <td>Consensi revocati</td>
                        <td>{revokedCount}</td>
                    </tr>
                </table>
            </div>

            <table class='certification-table'>
                <thead>
                    <tr>
                        <th>Nome Utilizzatore</th>
                        <th>Telefono</th>
                        <th>Brand</th>
                        <th>Data Richiesta</th>
                        <th>Data Consenso</th>
                        <th>Stato</th>
                    </tr>
                </thead>
                <tbody>
                    {rows}
                </tbody>
            </table>
        </div>";
    }

    /// <summary>
    /// Genera la legenda ADAPTIVE_PROFILE (come nel PDF normale)
    /// </summary>
    private static string GenerateAdaptiveProfileLegend()
    {
        return @"
        <div class='section' style='page-break-before: always;'>
            <h3>📖 Legenda Certificazione Utilizzi</h3>
            <div class='adaptive-legend'>
                <div class='adaptive-legend-item adaptive-legend-yes'>
                    <div class='adaptive-legend-badge'>Adaptive = Si</div>
                    <div class='adaptive-legend-description'>
                        <p>I dati di utilizzo del laboratorio mobile sono certificati anche in relazione all'identità degli utilizzatori terzi, secondo le procedure <strong>ADAPTIVE_GDPR</strong> ed <strong>ADAPTIVE_PROFILE</strong> descritte nel Contratto Principale e nei relativi allegati.</p>
                        <p>In questo caso:</p>
                        <ul style='margin: 10px 0; padding-left: 25px;'>
                            <li>È tracciato l'utilizzatore (o gli utilizzatori) che hanno usato il laboratorio mobile</li>
                            <li>È disponibile la documentazione associata alle procedure eseguite</li>
                        </ul>
                    </div>
                </div>

                <div class='adaptive-legend-item adaptive-legend-no'>
                    <div class='adaptive-legend-badge'>Adaptive = No</div>
                    <div class='adaptive-legend-description'>
                        <p>I dati di utilizzo del laboratorio mobile sono raccolti e certificati a livello tecnico-operativo, in modalità standard.</p>
                        <p>In assenza delle procedure <strong>ADAPTIVE_GDPR</strong> ed <strong>ADAPTIVE_PROFILE</strong>:</p>
                        <ul style='margin: 10px 0; padding-left: 25px;'>
                            <li>Non è certificabile l'uso da parte di utilizzatori terzi specifici</li>
                            <li>Non è disponibile documentazione nominativa riferita a quella specifica finestra temporale</li>
                        </ul>
                    </div>
                </div>
            </div>
        </div>";
    }

    /// <summary>
    /// Genera la sezione ADAPTIVE_PROFILE company-wide
    /// </summary>
    private string GenerateAdaptiveProfileSection(List<AdaptiveProfileUserDto> users, ClientProfileData data)
    {
        if (users.Count == 0)
        {
            return @"
            <div class='section'>
                <h3>👥 Utilizzatori Terzi Autorizzati (ADAPTIVE_PROFILE)</h3>
                <div class='summary-box'>
                    <p style='color: #6c757d; font-style: italic;'>
                        Nel periodo analizzato non risultano sessioni ADAPTIVE_PROFILE attive per questa azienda.<br/>
                        I laboratori mobili sono stati utilizzati esclusivamente da soggetti interni all'azienda oppure
                        non sono state eseguite le procedure ADAPTIVE_GDPR ed ADAPTIVE_PROFILE per eventuali utilizzatori terzi.
                    </p>
                </div>
            </div>";
        }

        var totalSessions = users.Sum(u => u.TotalSessions);
        var totalCertifiedRecords = data.VehicleAdaptiveProfiles.Values.Sum(v => v.CertifiedRecordsCount);

        var rows = string.Join("", users.Select(u => $@"
            <tr>
                <td>
                    {u.AdaptiveSurnameName}
                    {(u.HasRevokedConsent ? "<br/><span class='status-badge revoked' style='margin-top: 5px;'>⛔ Revocato</span>" : "")}
                </td>
                <td>{u.AdaptiveNumber}</td>
                <td>{u.FirstActivation:dd/MM/yyyy HH:mm}</td>
                <td>{u.LastActivation:dd/MM/yyyy HH:mm}</td>
                <td>{u.LastExpiry:dd/MM/yyyy HH:mm}</td>
                <td style='text-align: center; font-weight: 600;'>{u.TotalSessions}</td>
                <td style='font-size: 10px;'>{string.Join(", ", u.VehicleVins)}</td>
            </tr>"));

        return $@"
        <div class='section'>
            <h3>👥 Utilizzatori Terzi Autorizzati (ADAPTIVE_PROFILE)</h3>

            <div class='summary-box'>
                <h6>📊 Riepilogo Generale</h6>
                <table class='summary-table'>
                    <tr>
                        <td>Utilizzatori terzi profilati</td>
                        <td>{users.Count}</td>
                    </tr>
                    <tr>
                        <td>Sessioni totali</td>
                        <td>{totalSessions}</td>
                    </tr>
                    <tr>
                        <td>Records certificati con procedura attiva</td>
                        <td>{totalCertifiedRecords}</td>
                    </tr>
                </table>
            </div>

            <table class='certification-table'>
                <thead>
                    <tr>
                        <th>Nome Utilizzatore</th>
                        <th>Telefono</th>
                        <th>Prima Attivazione</th>
                        <th>Ultima Attivazione</th>
                        <th>Scadenza Profilo</th>
                        <th style='text-align: center;'>Sessioni</th>
                        <th>Veicoli Utilizzati</th>
                    </tr>
                </thead>
                <tbody>
                    {rows}
                </tbody>
            </table>
        </div>";
    }

    /// <summary>
    /// Genera la nuova sezione veicoli in formato tabella (non cards)
    /// </summary>
    private string GenerateVehiclesSectionTable(List<VehicleProfileInfo> vehicles, Dictionary<int, VehicleAdaptiveProfileDto> adaptiveProfiles)
    {
        if (vehicles.Count == 0)
        {
            return @"
            <div class='section' style='page-break-before: always;'>
                <h3>🚗 Flotta Veicoli</h3>
                <p style='color: #6c757d; font-style: italic;'>Nessun veicolo registrato per questa azienda.</p>
            </div>";
        }

        var rows = string.Join("", vehicles.Select(v =>
        {
            var statusBadges = new List<string>();
            if (v.IsActive) statusBadges.Add("<span class='status-badge active'>Attivo</span>");
            else statusBadges.Add("<span class='status-badge revoked'>Non Attivo</span>");
            if (v.IsFetching) statusBadges.Add("<span class='status-badge active'>Fetching</span>");
            if (v.IsAuthorized) statusBadges.Add("<span class='status-badge info'>OAuth</span>");

            var adaptiveInfo = "";
            if (adaptiveProfiles.TryGetValue(v.Id, out var profile) && profile.Users.Count > 0)
            {
                adaptiveInfo = $@"
                    <br/><strong>👥 Utilizzatori terzi:</strong> {profile.Users.Count}
                    <br/><strong>📊 Sessioni totali:</strong> {profile.TotalSessionsCount}
                    <br/><strong>✅ Records certificati:</strong> {profile.CertifiedRecordsCount}";
            }

            return $@"
            <tr>
                <td style='font-weight: 600;'>{v.Brand} {v.Model}<br/><span style='font-family: monospace; font-size: 10px; color: #718096;'>{v.Vin}</span></td>
                <td>{v.FuelType}</td>
                <td>{string.Join(" ", statusBadges.Where(b => !string.IsNullOrEmpty(b)))}</td>
                <td style='font-size: 10px;'>
                    {(v.FirstActivationAt.HasValue ? $"Prima: {v.FirstActivationAt:dd/MM/yyyy}<br/>" : "")}
                    {(v.LastDeactivationAt.HasValue ? $"Ultima: {v.LastDeactivationAt:dd/MM/yyyy}" : "")}
                </td>
                <td style='text-align: center;'>{v.TotalConsents}</td>
                <td style='text-align: center;'>{v.TotalOutages}</td>
                <td style='text-align: center;'>{v.TotalReports}</td>
                <td style='font-size: 10px;'>
                    <strong>SMS:</strong> {v.TotalSmsEvents}
                    <br/><strong>ON:</strong> {v.AdaptiveOnEvents} | <strong>OFF:</strong> {v.AdaptiveOffEvents}
                    {(v.ActiveSessions > 0 ? $"<br/><span class='status-badge active'>Sessioni: {v.ActiveSessions}</span>" : "")}
                    {adaptiveInfo}
                </td>
            </tr>";
        }));

        return $@"
        <div class='section' style='page-break-before: always;'>
            <h3>🚗 Flotta Veicoli ({vehicles.Count})</h3>
            <table class='certification-table'>
                <thead>
                    <tr>
                        <th>Veicolo</th>
                        <th>Alimentazione</th>
                        <th>Status</th>
                        <th>Attivazioni</th>
                        <th style='text-align: center;'>Consensi</th>
                        <th style='text-align: center;'>Outages</th>
                        <th style='text-align: center;'>Report</th>
                        <th>SMS & Adaptive</th>
                    </tr>
                </thead>
                <tbody>
                    {rows}
                </tbody>
            </table>
        </div>";
    }

    /// <summary>
    /// Genera la sezione OUTAGES completa
    /// </summary>
    private string GenerateOutagesSection(OutagesSummaryDto summary, List<OutageDetailDto> outages)
    {
        if (summary.TotalOutages == 0)
        {
            return @"
            <div class='section' style='page-break-before: always;'>
                <h3>⚡ Interruzioni di Servizio (Outages)</h3>
                <p style='color: #6c757d; font-style: italic;'>Nessuna interruzione di servizio registrata per questa azienda.</p>
            </div>";
        }

        var summaryCards = GenerateOutagesSummaryCards(summary);
        var brandOutages = outages.Where(o => o.OutageType == "Outage Fleet Api").ToList();
        var vehicleOutages = outages.Where(o => o.OutageType == "Outage Vehicle").ToList();

        var brandOutagesTable = brandOutages.Count > 0 ? $@"
            <h4 style='margin-top: 30px; color: #667eea; page-break-before: avoid;'>🌐 Outages a Livello Brand (Fleet API)</h4>
            <p style='font-size: 0.9em; color: #6c757d; margin-bottom: 15px;'>
                Interruzioni delle API del costruttore che hanno impattato tutti i veicoli del brand.
            </p>
            {GenerateBrandOutagesTable(brandOutages)}" : "";

        var vehicleOutagesTable = vehicleOutages.Count > 0 ? $@"
            <div style='page-break-before: always;'>
                <h4 style='margin-top: 30px; color: #667eea;'>🚗 Outages Specifici per Veicolo</h4>
                <p style='font-size: 0.9em; color: #6c757d; margin-bottom: 15px;'>
                    Interruzioni specifiche per singoli veicoli della flotta.
                </p>
                {GenerateVehicleOutagesTable(vehicleOutages)}
            </div>" : "";

        return $@"
        <div class='section' style='page-break-before: always;'>
            <h3>⚡ Interruzioni di Servizio (Outages)</h3>

            <h4 style='margin-top: 25px; color: #667eea;'>📊 Riepilogo Generale</h4>
            {summaryCards}

            {brandOutagesTable}

            {vehicleOutagesTable}
        </div>";
    }

    /// <summary>
    /// Genera le card di riepilogo statistiche outages
    /// </summary>
    private string GenerateOutagesSummaryCards(OutagesSummaryDto summary)
    {
        return $@"
        <div class='stats-grid' style='margin-top: 15px;'>
            <div class='stat-card'>
                <div class='stat-number'>{summary.TotalOutages}</div>
                <div class='stat-label'>Outages Totali</div>
            </div>
            <div class='stat-card' style='border-color: #dc3545;'>
                <div class='stat-number' style='color: #dc3545;'>{summary.OngoingOutages}</div>
                <div class='stat-label'>In Corso</div>
            </div>
            <div class='stat-card' style='border-color: #28a745;'>
                <div class='stat-number' style='color: #28a745;'>{summary.ResolvedOutages}</div>
                <div class='stat-label'>Risolti</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{summary.TotalDowntimeDays}</div>
                <div class='stat-label'>Giorni Downtime Totali</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{summary.BrandLevelOutages}</div>
                <div class='stat-label'>Outages Brand</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{summary.VehicleSpecificOutages}</div>
                <div class='stat-label'>Outages Veicolo</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{summary.TotalVehiclesAffected}</div>
                <div class='stat-label'>Veicoli Impattati</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{summary.AverageOutageDurationDays:F1}</div>
                <div class='stat-label'>Durata Media (giorni)</div>
            </div>
        </div>";
    }

    /// <summary>
    /// Genera la tabella per outages brand-level
    /// </summary>
    private static string GenerateBrandOutagesTable(List<OutageDetailDto> brandOutages)
    {
        var rows = string.Join("", brandOutages.Select((outage, index) => $@"
            <tr style='{(outage.IsOngoing ? "border-left: 4px solid #dc3545;" : "")}'>
                <td style='padding: 6px 8px; border-bottom: 1px solid #dee2e6; font-size: 9px;'>
                    <span style='font-weight: 600; text-transform: uppercase;'>{outage.OutageBrand}</span>
                </td>
                <td style='padding: 6px 8px; border-bottom: 1px solid #dee2e6; font-size: 9px;'>
                    {outage.OutageStart:dd/MM/yyyy HH:mm}
                </td>
                <td style='padding: 6px 8px; border-bottom: 1px solid #dee2e6; font-size: 9px;'>
                    {(outage.OutageEnd.HasValue ? outage.OutageEnd.Value.ToString("dd/MM/yyyy HH:mm") : "<span style='color: #dc3545; font-weight: 600;'>IN CORSO</span>")}
                </td>
                <td style='padding: 6px 8px; border-bottom: 1px solid #dee2e6; text-align: center; font-size: 9px;'>
                    <span style='font-weight: 600;'>{outage.DurationDays}g {outage.DurationHours}h</span>
                </td>
                <td style='padding: 6px 8px; border-bottom: 1px solid #dee2e6; text-align: center; font-size: 9px;'>
                    <span class='outage-impact'>
                        {outage.AffectedVehicleCount} veicoli
                    </span>
                </td>
                <td style='padding: 6px 8px; border-bottom: 1px solid #dee2e6; font-size: 8px;'>
                    {(outage.AffectedVehicleCount <= 5
                        ? string.Join(", ", outage.AffectedVehicleVins)
                        : $"{string.Join(", ", outage.AffectedVehicleVins.Take(3))}... (+{outage.AffectedVehicleCount - 3})")}
                </td>
                <td style='padding: 6px 8px; border-bottom: 1px solid #dee2e6; font-size: 8px; color: #666;'>
                    {(string.IsNullOrWhiteSpace(outage.Notes) ? "-" : outage.Notes)}
                </td>
            </tr>"));

        return $@"
        <div style='overflow-x: auto; margin-top: 15px;'>
            <table style='width: 100%; border-collapse: collapse; background: white; border: 1px solid #dee2e6; border-radius: 8px;'>
                <thead style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white;'>
                    <tr>
                        <th style='padding: 8px; text-align: left; font-weight: 600; font-size: 9px;'>Brand</th>
                        <th style='padding: 8px; text-align: left; font-weight: 600; font-size: 9px;'>Inizio</th>
                        <th style='padding: 8px; text-align: left; font-weight: 600; font-size: 9px;'>Fine</th>
                        <th style='padding: 8px; text-align: center; font-weight: 600; font-size: 9px;'>Durata</th>
                        <th style='padding: 8px; text-align: center; font-weight: 600; font-size: 9px;'>Veicoli Impattati</th>
                        <th style='padding: 8px; text-align: left; font-weight: 600; font-size: 9px;'>VIN Impattati</th>
                        <th style='padding: 8px; text-align: left; font-weight: 600; font-size: 9px;'>Note</th>
                    </tr>
                </thead>
                <tbody>
                    {rows}
                </tbody>
            </table>
        </div>";
    }

    /// <summary>
    /// Genera la tabella per outages vehicle-specific
    /// </summary>
    private string GenerateVehicleOutagesTable(List<OutageDetailDto> vehicleOutages)
    {
        var rows = string.Join("", vehicleOutages.Select((outage, index) => $@"
            <tr style='{(outage.IsOngoing ? "border-left: 4px solid #dc3545;" : "")}'>
                <td style='padding: 8px 10px; border-bottom: 1px solid #dee2e6; font-size: 10px;'>
                    <span style='font-weight: 600;'>{outage.VehicleModel}</span><br/>
                    <span style='font-family: monospace; font-size: 9px; color: #666;'>{outage.Vin}</span>
                </td>
                <td style='padding: 8px 10px; border-bottom: 1px solid #dee2e6; text-transform: uppercase; font-weight: 600; font-size: 10px;'>
                    {outage.OutageBrand}
                </td>
                <td style='padding: 8px 10px; border-bottom: 1px solid #dee2e6; font-size: 10px;'>
                    {outage.OutageStart:dd/MM/yyyy HH:mm}
                </td>
                <td style='padding: 8px 10px; border-bottom: 1px solid #dee2e6; font-size: 10px;'>
                    {(outage.OutageEnd.HasValue ? outage.OutageEnd.Value.ToString("dd/MM/yyyy HH:mm") : "<span style='color: #dc3545; font-weight: 600;'>IN CORSO</span>")}
                </td>
                <td style='padding: 8px 10px; border-bottom: 1px solid #dee2e6; text-align: center; font-size: 10px;'>
                    <span style='font-weight: 600;'>{outage.DurationDays}g {outage.DurationHours}h</span>
                </td>
                <td style='padding: 8px 10px; border-bottom: 1px solid #dee2e6; font-size: 10px;'>
                    <span class='outage-badge {(outage.AutoDetected ? "auto" : "manual")}'>
                        {(outage.AutoDetected ? "Auto-rilevato" : "Manuale")}
                    </span>
                </td>
                <td style='padding: 8px 10px; border-bottom: 1px solid #dee2e6; font-size: 9px; color: #666;'>
                    {(string.IsNullOrWhiteSpace(outage.Notes) ? "-" : outage.Notes)}
                </td>
            </tr>"));

        return $@"
        <div style='overflow-x: auto; margin-top: 15px;'>
            <table style='width: 100%; border-collapse: collapse; background: white; border: 1px solid #dee2e6; border-radius: 8px; font-size: 10px;'>
                <thead style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white;'>
                    <tr>
                        <th style='padding: 10px; text-align: left; font-weight: 600; font-size: 10px;'>Veicolo</th>
                        <th style='padding: 10px; text-align: left; font-weight: 600; font-size: 10px;'>Brand</th>
                        <th style='padding: 10px; text-align: left; font-weight: 600; font-size: 10px;'>Inizio</th>
                        <th style='padding: 10px; text-align: left; font-weight: 600; font-size: 10px;'>Fine</th>
                        <th style='padding: 10px; text-align: center; font-weight: 600; font-size: 10px;'>Durata</th>
                        <th style='padding: 10px; text-align: left; font-weight: 600; font-size: 10px;'>Tipo</th>
                        <th style='padding: 10px; text-align: left; font-weight: 600; font-size: 10px;'>Note</th>
                    </tr>
                </thead>
                <tbody>
                    {rows}
                </tbody>
            </table>
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