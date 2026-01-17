using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Constants;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.PolarAiReports;
using PolarDrive.WebApi.Services;
using PolarDrive.WebApi.Services.Gdpr;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PdfReportsController(
    PolarDriveDbContext context,
    IReportGenerationService reportGenerationService,
    GapAnalysisService gapAnalysisService,
    GapValidationPdfService gapValidationPdfService,
    IGdprEncryptionService gdprService) : ControllerBase
{
    private readonly PolarDriveDbContext db = context;
    private readonly PolarDriveLogger _logger = new();
    private readonly IReportGenerationService _reportGenerationService = reportGenerationService;
    private readonly GapAnalysisService _gapAnalysisService = gapAnalysisService;
    private readonly GapValidationPdfService _gapValidationPdfService = gapValidationPdfService;
    private readonly IGdprEncryptionService _gdprService = gdprService;

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<PdfReportDTO>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? search = null)
    {
        const string source = "PdfReportsController.Get";

        try
        {
            await _logger.Info(source, "Requested filtered list of PDF reports",
                $"Page: {page}, PageSize: {pageSize}");

            var baseQuery = db.PdfReports.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmedSearch = search.Trim();
                if (int.TryParse(trimmedSearch, out int searchId))
                {
                    baseQuery = baseQuery.Where(r => r.Id == searchId);
                }
                else if (trimmedSearch.StartsWith("VIN:", StringComparison.OrdinalIgnoreCase))
                {
                    // Ricerca per VIN tramite hash (match esatto GDPR-compliant)
                    var vinSearch = trimmedSearch[4..].Trim();
                    var vinHash = _gdprService.ComputeLookupHash(vinSearch);
                    baseQuery = baseQuery.Where(r => r.ClientVehicle != null &&
                        r.ClientVehicle.VinHash == vinHash);
                }
                else
                {
                    var searchPattern = $"%{trimmedSearch}%";
                    baseQuery = baseQuery.Where(r => EF.Functions.Like(r.Status, searchPattern));
                }
            }

            var totalCount = await baseQuery.CountAsync();

            // Prima query per i report
            var reportIds = await baseQuery
                .OrderByDescending(r => r.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => r.Id)
                .ToListAsync();

            // Query separata per le validazioni gap - ottieni tutte le validazioni per ogni report
            var allGapValidations = await db.GapValidationPdfs
                .Where(c => reportIds.Contains(c.PdfReportId))
                .Select(c => new
                {
                    c.PdfReportId,
                    c.Status,
                    c.PdfHash,
                    c.DocumentType,
                    HasPdf = c.PdfContent != null
                })
                .ToListAsync();

            // Raggruppa per report: prendi lo status più recente e verifica se c'è stata escalation
            var gapValidations = allGapValidations
                .GroupBy(c => c.PdfReportId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        // Lo status attuale è quello del record finale (COMPLETED, CONTRACT_BREACH, ESCALATED o PROCESSING)
                        Status = g.OrderByDescending(x => x.Status == "COMPLETED" || x.Status == "CONTRACT_BREACH" ? 1 : 0)
                                  .ThenByDescending(x => x.Status == "ESCALATED" ? 1 : 0)
                                  .First().Status,
                        PdfHash = g.OrderByDescending(x => x.Status == "COMPLETED" || x.Status == "CONTRACT_BREACH" ? 1 : 0)
                                   .ThenByDescending(x => x.Status == "ESCALATED" ? 1 : 0)
                                   .First().PdfHash,
                        HasPdf = g.Any(x => x.HasPdf),
                        // True se esiste un record con DocumentType = ESCALATION
                        HadEscalation = g.Any(x => x.DocumentType == "ESCALATION")
                    }
                );

            var reports = await baseQuery
                .OrderByDescending(r => r.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.ReportPeriodStart,
                    r.ReportPeriodEnd,
                    r.GeneratedAt,
                    r.Notes,
                    r.Status,
                    r.PdfHash,
                    HasPdf = r.PdfContent != null,
                    PdfSize = r.PdfContent != null ? (long)r.PdfContent.Length : 0L,
                    CompanyVatNumber = r.ClientCompany != null ? r.ClientCompany.VatNumber : null,
                    CompanyName = r.ClientCompany != null ? r.ClientCompany.Name : null,
                    VehicleId = r.ClientVehicle != null ? (int?)r.ClientVehicle.Id : null,
                    VehicleVin = r.ClientVehicle != null ? r.ClientVehicle.Vin : null,
                    VehicleModel = r.ClientVehicle != null ? r.ClientVehicle.Model : null,
                    VehicleBrand = r.ClientVehicle != null ? r.ClientVehicle.Brand : null
                })
                .ToListAsync();

            // Map directly to DTOs without heavy VehiclesData queries
            var result = reports.Select(report =>
            {
                // Ottieni info Validazione Probabilistica Gap se disponibile
                gapValidations.TryGetValue(report.Id, out var gapCert);

                return new PdfReportDTO
                {
                    Id = report.Id,
                    ReportPeriodStart = report.ReportPeriodStart.ToString("o"),
                    ReportPeriodEnd = report.ReportPeriodEnd.ToString("o"),
                    GeneratedAt = report.GeneratedAt?.ToString("o"),
                    CompanyVatNumber = report.CompanyVatNumber ?? "",
                    CompanyName = report.CompanyName ?? "",
                    VehicleVin = report.VehicleVin ?? "",
                    VehicleModel = report.VehicleModel ?? "",
                    VehicleBrand = report.VehicleBrand ?? "",
                    Notes = report.Notes,
                    HasPdfFile = report.HasPdf,
                    HasHtmlFile = false,
                    DataRecordsCount = 0, // Removed: heavy query on VehiclesData
                    PdfFileSize = report.PdfSize,
                    HtmlFileSize = 0,
                    MonitoringDurationHours = 0, // Removed: heavy query on VehiclesData
                    ReportType = "Report",
                    Status = report.HasPdf ? ReportStatus.PDF_READY : (!string.IsNullOrEmpty(report.Status) ? report.Status : ReportStatus.PROCESSING),
                    PdfHash = report.PdfHash ?? "",
                    // Gap Validation info
                    GapValidationStatus = gapCert?.Status,
                    GapValidationPdfHash = gapCert?.PdfHash,
                    HasGapValidationPdf = gapCert?.HasPdf ?? false,
                    HadEscalation = gapCert?.HadEscalation ?? false
                };
            }).ToList();

            await _logger.Info(source, "Mapping DTO completato",
                $"Total: {result.Count}, Page: {page}");

            return Ok(new
            {
                Data = result,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore recupero lista report", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
    }

    private PdfReportDTO MapReportToDto(
        Data.Entities.PdfReport report,
        dynamic? stats)
    {
        var totalHistoricalRecords = stats?.TotalCount ?? 0;
        var firstRecordEver = stats?.FirstRecord ?? DateTime.Now;
        var totalMonitoringHours = (DateTime.Now - firstRecordEver).TotalHours;

        var hasPdf = report.PdfContent != null && report.PdfContent.Length > 0;
        var pdfSize = hasPdf ? report.PdfContent!.Length : 0;

        var status = hasPdf ? ReportStatus.PDF_READY : (!string.IsNullOrEmpty(report.Status) ? report.Status : ReportStatus.PROCESSING);

        return new PdfReportDTO
        {
            Id = report.Id,
            ReportPeriodStart = report.ReportPeriodStart.ToString("o"),
            ReportPeriodEnd = report.ReportPeriodEnd.ToString("o"),
            GeneratedAt = report.GeneratedAt?.ToString("o"),
            CompanyVatNumber = report.ClientCompany?.VatNumber ?? "",
            CompanyName = report.ClientCompany?.Name ?? "",
            VehicleVin = report.ClientVehicle?.Vin ?? "",
            VehicleModel = report.ClientVehicle?.Model ?? "",
            VehicleBrand = report.ClientVehicle?.Brand ?? "",
            Notes = report.Notes,
            HasPdfFile = hasPdf,
            HasHtmlFile = false,
            DataRecordsCount = totalHistoricalRecords,
            PdfFileSize = pdfSize,
            HtmlFileSize = 0,
            MonitoringDurationHours = Math.Max(0, Math.Floor(totalMonitoringHours)),
            ReportType = DetermineReportType(totalHistoricalRecords),
            Status = status,
            PdfHash = report.PdfHash ?? ""
        };
    }

    private static string DetermineReportType(int totalHistoricalRecords)
    {
        if (totalHistoricalRecords == 0) return "No_Data";
        if (totalHistoricalRecords >= AppConfig.MONTHLY_HOURS_THRESHOLD) return "Mensile";
        if (totalHistoricalRecords >= AppConfig.WEEKLY_HOURS_THRESHOLD) return "Settimanale";
        if (totalHistoricalRecords >= AppConfig.DAILY_HOURS_THRESHOLD) return "Giornaliero";
        return "Giornaliero_Parziale";
    }

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        var entity = await db.PdfReports.FindAsync(id);
        if (entity == null) return NotFound();

        if (!body.TryGetProperty("notes", out var notesProp))
            return BadRequest("Notes field missing");

        entity.Notes = notesProp.GetString() ?? string.Empty;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadPdf(int id)
    {
        try
        {
            var report = await db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null) return NotFound();

            if (report.PdfContent != null && report.PdfContent.Length > 0)
            {
                var fileName = $"PolarDrive_PolarReport_{id}_{report.ClientVehicle?.Vin}_{report.ReportPeriodStart:yyyyMMdd}.pdf";

                Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
                Response.Headers.Pragma = "no-cache";
                if (!string.IsNullOrWhiteSpace(report.PdfHash))
                    Response.Headers.ETag = $"W/\"{report.PdfHash}\"";

                return File(report.PdfContent, "application/pdf", fileName);
            }

            return NotFound("Report PDF non disponibile");
        }
        catch (Exception ex)
        {
            await _logger.Error("PdfReportsController.DownloadPdf", "Download error", ex.ToString());
            return StatusCode(500, "Download failed");
        }
    }

    [HttpPost("{id}/regenerate")]
    public async Task<IActionResult> RegenerateReport(int id)
    {
        const string source = "PdfReportsController.RegenerateReport";

        try
        {
            await _logger.Info(source, "Richiesta rigenerazione report", $"ReportId: {id}");

            var report = await db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound(new { errorCode = "REPORT_NOT_FOUND" });

            if (!string.IsNullOrWhiteSpace(report.PdfHash) &&
                report.PdfContent != null &&
                report.PdfContent.Length > 0)
            {
                return Conflict(new { errorCode = "REPORT_ALREADY_COMPLETED" });
            }

            var regenerableStatuses = new[] { ReportStatus.PROCESSING, ReportStatus.ERROR };
            if (!string.IsNullOrWhiteSpace(report.Status) &&
                !regenerableStatuses.Contains(report.Status))
            {
                return BadRequest(new { errorCode = "REPORT_NOT_REGENERABLE", status = report.Status });
            }

            // Salva parametri
            var companyId = report.ClientCompanyId;
            var vehicleId = report.VehicleId;
            var periodStart = report.ReportPeriodStart;
            var periodEnd = report.ReportPeriodEnd;

            // Reset stato
            report.Status = ReportStatus.REGENERATING;
            report.GeneratedAt = null;
            await db.SaveChangesAsync();

            await _logger.Info(source, "Report impostato per rigenerazione",
                $"ReportId: {id}, NewStatus: REGENERATING");

            // ✅ FIX: Usa IServiceScopeFactory invece del service diretto!
            var scopeFactory = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();

            _ = Task.Run(async () =>
            {
                try
                {
                    // ✅ CREA UN NUOVO SCOPE INDIPENDENTE
                    using var taskScope = scopeFactory.CreateScope();
                    var taskService = taskScope.ServiceProvider.GetRequiredService<IReportGenerationService>();

                    await _logger.Info(source, "🔄 BACKGROUND: Inizio rigenerazione",
                        $"ReportId: {id}, VehicleId: {vehicleId}, CompanyId: {companyId}");

                    var success = await taskService.GenerateSingleReportAsync(
                        companyId,
                        vehicleId,
                        periodStart,
                        periodEnd,
                        isRegeneration: true,
                        existingReportId: id
                    );

                    if (success)
                    {
                        await _logger.Info(source, "✅ BACKGROUND: Rigenerazione COMPLETATA",
                            $"ReportId: {id}");
                    }
                    else
                    {
                        await _logger.Error(source, "❌ BACKGROUND: Rigenerazione FALLITA",
                            $"ReportId: {id}");
                    }
                }
                catch (Exception bgEx)
                {
                    await _logger.Error(source, "💥 BACKGROUND: Exception durante rigenerazione",
                        $"ReportId: {id}, Error: {bgEx.Message}, StackTrace: {bgEx.StackTrace}");
                }
            });

            return Accepted(new
            {
                message = "Rigenerazione avviata in background",
                reportId = id,
                status = ReportStatus.REGENERATING
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore rigenerazione report",
                $"ReportId: {id}, Error: {ex.Message}");
            return StatusCode(500, new { error = "Errore interno server" });
        }
    }

    [HttpGet("{id}/can-regenerate")]
    public async Task<ActionResult<object>> CanRegenerate(int id)
    {
        try
        {
            var report = await db.PdfReports
                .Select(r => new
                {
                    r.Id,
                    r.PdfHash,
                    r.Status,
                    HasPdfContent = r.PdfContent != null && r.PdfContent.Length > 0
                })
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound(new { canRegenerate = false, reason = "Report non trovato" });

            var isImmutable = !string.IsNullOrWhiteSpace(report.PdfHash) && report.HasPdfContent;
            var isInErrorState = new[] { ReportStatus.PROCESSING, ReportStatus.ERROR }
                .Contains(report.Status ?? "");

            var canRegenerate = !isImmutable && isInErrorState;

            return Ok(new
            {
                canRegenerate,
                isImmutable,
                pdfHash = report.PdfHash,
                status = report.Status,
                reason = isImmutable
                    ? "Report completato e certificato - immutabile per conformità fiscale"
                    : !isInErrorState
                        ? $"Report in stato '{report.Status}' non rigenerabile"
                        : "Report eleggibile per rigenerazione"
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("PdfReportsController.CanRegenerate",
                "Errore verifica rigenerabilità", ex.ToString());
            return StatusCode(500, new { canRegenerate = false, reason = "Errore interno" });
        }
    }

    /// <summary>
    /// Verifica se esistono report in stato PROCESSING.
    /// Utilizzato per bloccare nuove rigenerazioni mentre un report è in elaborazione.
    /// </summary>
    [HttpGet("has-processing")]
    public async Task<ActionResult<object>> HasProcessingReports()
    {
        const string source = "PdfReportsController.HasProcessingReports";

        try
        {
            // Controlla sia PROCESSING che REGENERATING
            var processingReport = await db.PdfReports
                .Where(r => r.Status == ReportStatus.PROCESSING || r.Status == ReportStatus.REGENERATING)
                .Select(r => new
                {
                    r.Id,
                    r.Status,
                    CompanyName = r.ClientCompany != null ? r.ClientCompany.Name : null,
                    VehicleVin = r.ClientVehicle != null ? r.ClientVehicle.Vin : null,
                    r.GeneratedAt
                })
                .FirstOrDefaultAsync();

            if (processingReport != null)
            {
                await _logger.Info(source, "⚠️ Report in PROCESSING trovato",
                    $"ReportId: {processingReport.Id}, Company: {processingReport.CompanyName}");

                return Ok(new
                {
                    hasProcessing = true,
                    processingReportId = processingReport.Id,
                    companyName = processingReport.CompanyName,
                    vehicleVin = processingReport.VehicleVin,
                    message = "Un report è attualmente in elaborazione. Attendere il completamento prima di avviare una nuova rigenerazione."
                });
            }

            return Ok(new
            {
                hasProcessing = false,
                message = "Nessun report in elaborazione"
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore verifica report in PROCESSING", ex.ToString());
            return StatusCode(500, new { hasProcessing = false, error = "Errore interno server" });
        }
    }

    #region Gap Certification Endpoints

    /// <summary>
    /// Verifica se esiste una Validazione Probabilistica Gap in corso (PROCESSING).
    /// Utilizzato per bloccare nuove certificazioni mentre una è in elaborazione.
    /// </summary>
    [HttpGet("gap-validation-processing")]
    public async Task<ActionResult<object>> GetGapValidationProcessing()
    {
        const string source = "PdfReportsController.GetGapValidationProcessing";

        try
        {
            var processing = await db.GapValidationPdfs
                .Where(c => c.Status == ReportStatus.PROCESSING)
                .Select(c => new
                {
                    c.PdfReportId,
                    CompanyName = c.PdfReport != null && c.PdfReport.ClientCompany != null
                        ? c.PdfReport.ClientCompany.Name : null,
                    VehicleVin = c.PdfReport != null && c.PdfReport.ClientVehicle != null
                        ? c.PdfReport.ClientVehicle.Vin : null
                })
                .FirstOrDefaultAsync();

            if (processing != null)
            {
                await _logger.Info(source, "Validazione Probabilistica Gap in corso trovata",
                    $"ReportId: {processing.PdfReportId}");

                return Ok(new
                {
                    hasProcessing = true,
                    reportId = processing.PdfReportId,
                    companyName = processing.CompanyName,
                    vehicleVin = processing.VehicleVin
                });
            }

            return Ok(new
            {
                hasProcessing = false,
                reportId = (int?)null,
                companyName = (string?)null,
                vehicleVin = (string?)null
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore verifica certificazione in corso", ex.ToString());
            return StatusCode(500, new { hasProcessing = false, error = "Errore interno server" });
        }
    }

    /// <summary>
    /// Verifica lo stato di Validazione Probabilistica Gap per un report.
    /// Restituisce se ci sono gap non certificati e se è possibile generare una certificazione.
    /// </summary>
    [HttpGet("{id}/gap-status")]
    public async Task<ActionResult<object>> GetGapStatus(int id)
    {
        const string source = "PdfReportsController.GetGapStatus";

        try
        {
            var report = await db.PdfReports
                .Select(r => new
                {
                    r.Id,
                    r.PdfHash,
                    r.Status,
                    HasPdfContent = r.PdfContent != null && r.PdfContent.Length > 0
                })
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound(new { error = "Report non trovato" });

            // Verifica se il PDF del report è disponibile (prerequisito per certificare)
            var isPdfAvailable = !string.IsNullOrWhiteSpace(report.PdfHash) && report.HasPdfContent;

            // Verifica se il report è in uno stato rigenerabile (in quel caso non mostrare certificazione)
            var isRegenerable = new[] { ReportStatus.PROCESSING, ReportStatus.ERROR, ReportStatus.REGENERATING }.Contains(report.Status ?? "");

            if (!isPdfAvailable || isRegenerable)
            {
                return Ok(new
                {
                    canCertify = false,
                    isPdfAvailable,
                    isRegenerable,
                    reason = !isPdfAvailable
                        ? "PDF del report non ancora disponibile"
                        : "Report in elaborazione o in stato di errore"
                });
            }

            // Ottieni lo stato dei gap
            var gapStatus = await _gapAnalysisService.GetGapStatusForReportAsync(id);

            return Ok(new
            {
                canCertify = gapStatus.HasUncertifiedGaps && !gapStatus.HasCertificationPdf,
                isPdfAvailable,
                isRegenerable = false,
                totalGaps = gapStatus.TotalGaps,
                uncertifiedGaps = gapStatus.UncertifiedGaps,
                certifiedGaps = gapStatus.CertifiedGaps,
                hasCertificationPdf = gapStatus.HasCertificationPdf,
                reason = gapStatus.HasCertificationPdf
                    ? "Certificazione già generata"
                    : gapStatus.TotalGaps == 0
                        ? "Nessun gap da certificare"
                        : "Gap pronti per la certificazione"
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error getting gap status for report {id}", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server" });
        }
    }

    /// <summary>
    /// Preview dell'analisi gap per un report.
    /// Mostra tutti i gap identificati con la loro confidenza stimata prima di generare la certificazione.
    /// </summary>
    [HttpGet("{id}/gap-analysis")]
    public async Task<ActionResult<object>> GetGapAnalysis(int id)
    {
        const string source = "PdfReportsController.GetGapAnalysis";

        try
        {
            var report = await db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound(new { error = "Report non trovato" });

            // Esegui l'analisi e ottieni il periodo effettivo
            var (gaps, periodStart, periodEnd) = await _gapAnalysisService.AnalyzeGapsForReportWithPeriodAsync(id);

            if (gaps.Count == 0)
            {
                return Ok(new
                {
                    reportId = id,
                    vehicleVin = report.ClientVehicle?.Vin,
                    companyName = report.ClientCompany?.Name,
                    periodStart,
                    periodEnd,
                    totalGaps = 0,
                    gaps = Array.Empty<object>(),
                    message = "Nessun gap identificato nel periodo analizzato"
                });
            }

            // Calcola statistiche
            var avgConfidence = gaps.Average(g => g.ConfidencePercentage);
            var highConfidence = gaps.Count(g => g.ConfidencePercentage >= 80);
            var mediumConfidence = gaps.Count(g => g.ConfidencePercentage >= 60 && g.ConfidencePercentage < 80);
            var lowConfidence = gaps.Count(g => g.ConfidencePercentage < 60);

            // Statistiche outages
            var gapsWithOutage = gaps.Count(g => g.Factors.OutageId.HasValue);
            var uniqueOutages = gaps
                .Where(g => g.Factors.OutageId.HasValue)
                .Select(g => g.Factors.OutageId!.Value)
                .Distinct()
                .Count();
            var avgConfWithOutage = gaps.Where(g => g.Factors.OutageId.HasValue).Any()
                ? gaps.Where(g => g.Factors.OutageId.HasValue).Average(g => g.ConfidencePercentage) : 0;
            var totalDowntimeHours = gapsWithOutage;

            return Ok(new
            {
                reportId = id,
                vehicleVin = report.ClientVehicle?.Vin,
                companyName = report.ClientCompany?.Name,
                periodStart,
                periodEnd,
                totalGaps = gaps.Count,
                averageConfidence = Math.Round(avgConfidence, 1),
                summary = new
                {
                    highConfidence,
                    mediumConfidence,
                    lowConfidence
                },
                outages = new
                {
                    total = uniqueOutages,
                    gapsAffected = gapsWithOutage,
                    gapsAffectedPercentage = Math.Round((gapsWithOutage / (double)gaps.Count) * 100, 1),
                    totalDowntimeDays = totalDowntimeHours / 24,
                    totalDowntimeHours,
                    avgConfidenceWithOutage = Math.Round(avgConfWithOutage, 1)
                },
                gaps = gaps.Select(g => new
                {
                    timestamp = g.GapTimestamp,
                    confidence = g.ConfidencePercentage,
                    justification = g.Justification,
                    factors = g.Factors,
                    outageInfo = g.Factors.OutageId.HasValue ? new
                    {
                        outageType = g.Factors.OutageType,
                        outageBrand = g.Factors.OutageBrand,
                        bonusApplied = g.Factors.OutageBonusApplied
                    } : null
                }).OrderBy(g => g.timestamp)
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error analyzing gaps for report {id}", ex.ToString());
            return StatusCode(500, new { error = "Errore durante l'analisi dei gap" });
        }
    }

    /// <summary>
    /// Genera la certificazione probabilistica dei gap per un report.
    /// Crea un record PROCESSING e avvia la generazione in background.
    /// Il PDF verrà salvato nella tabella GapValidationPdfs.
    /// </summary>
    [HttpPost("{id}/validate-gaps")]
    public async Task<ActionResult<object>> ValidateGaps(int id)
    {
        const string source = "PdfReportsController.CertifyGaps";

        try
        {
            await _logger.Info(source, $"Richiesta Validazione Probabilistica Gap per report {id}");

            // 1. Verifica che non ci sia già una certificazione in corso globalmente
            var existingProcessing = await db.GapValidationPdfs
                .AnyAsync(c => c.Status == ReportStatus.PROCESSING);

            if (existingProcessing)
            {
                return Conflict(new
                {
                    error = "Una Validazione Probabilistica Gap è già in corso. Attendere il completamento.",
                    errorCode = "CERTIFICATION_IN_PROGRESS"
                });
            }

            var report = await db.PdfReports
                .Include(r => r.ClientCompany)
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound(new { error = "Report non trovato" });

            // 2. Verifica che il PDF del report sia disponibile
            if (string.IsNullOrWhiteSpace(report.PdfHash) || report.PdfContent == null || report.PdfContent.Length == 0)
            {
                return BadRequest(new { error = "Il PDF del report deve essere disponibile prima di certificare i gap" });
            }

            // 3. Verifica che non esista già una certificazione COMPLETED per questo report
            var existingCompleted = await db.GapValidationPdfs
                .AnyAsync(c => c.PdfReportId == id && c.Status == ReportStatus.COMPLETED);

            if (existingCompleted)
            {
                return Conflict(new
                {
                    error = "Una certificazione è già stata generata per questo report",
                    errorCode = "CERTIFICATION_ALREADY_EXISTS"
                });
            }

            // 4. Rimuovi eventuali certificazioni precedenti in stato ERROR per questo report
            var existingError = await db.GapValidationPdfs
                .Where(c => c.PdfReportId == id && c.Status == ReportStatus.ERROR)
                .ToListAsync();

            if (existingError.Count > 0)
            {
                db.GapValidationPdfs.RemoveRange(existingError);
                await db.SaveChangesAsync();
            }

            // 5. Crea il record GapValidationPdf con status PROCESSING
            var certPdf = new Data.Entities.GapValidationPdf
            {
                PdfReportId = id,
                Status = ReportStatus.PROCESSING,
                CreatedAt = DateTime.Now
            };

            db.GapValidationPdfs.Add(certPdf);
            await db.SaveChangesAsync();

            await _logger.Info(source, $"Record GapValidationPdf creato con status PROCESSING",
                $"ReportId: {id}, CertPdfId: {certPdf.Id}");

            // 6. Avvia il task in background
            var scopeFactory = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();

            _ = Task.Run(async () =>
            {
                try
                {
                    using var taskScope = scopeFactory.CreateScope();
                    var service = taskScope.ServiceProvider.GetRequiredService<GapValidationPdfService>();

                    await _logger.Info(source, "BACKGROUND: Inizio generazione Validazione Probabilistica Gap",
                        $"ReportId: {id}");

                    await service.GenerateAndSaveCertificationAsync(id);
                }
                catch (Exception bgEx)
                {
                    await _logger.Error(source, "BACKGROUND: Exception durante generazione certificazione",
                        $"ReportId: {id}, Error: {bgEx.Message}");
                }
            });

            // 7. Restituisci 202 Accepted
            return Accepted(new
            {
                message = "Validazione Probabilistica Gap avviata in background",
                reportId = id,
                status = ReportStatus.PROCESSING
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error certifying gaps for report {id}", ex.ToString());
            return StatusCode(500, new { error = "Errore interno durante la generazione della certificazione" });
        }
    }

    /// <summary>
    /// Download del PDF di Validazione Probabilistica Gap (CERTIFICATION) dal database.
    /// Il PDF è immutabile e salvato nella tabella GapValidationPdfs.
    /// </summary>
    [HttpGet("{id}/download-gap-validation")]
    public async Task<IActionResult> DownloadGapValidation(int id)
    {
        const string source = "PdfReportsController.DownloadGapValidation";

        try
        {
            var report = await db.PdfReports
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound("Report non trovato");

            // Recupera la certificazione PDF dal database cercando per DocumentType
            var certPdf = await db.GapValidationPdfs
                .FirstOrDefaultAsync(c => c.PdfReportId == id && c.DocumentType == GapValidationDocumentTypes.CERTIFICATION);

            if (certPdf == null)
            {
                return NotFound("Certificazione non trovata per questo report");
            }

            if (certPdf.PdfContent == null || certPdf.PdfContent.Length == 0)
            {
                return NotFound("PDF di certificazione non disponibile");
            }

            var fileName = $"PolarDrive_GapCertification_{id}_{report.ClientVehicle?.Vin}_{certPdf.GeneratedAt:yyyyMMdd}.pdf";

            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            if (!string.IsNullOrWhiteSpace(certPdf.PdfHash))
                Response.Headers.ETag = $"W/\"{certPdf.PdfHash}\"";

            await _logger.Info(source, $"Download PDF Certification per report {id}",
                $"Hash: {certPdf.PdfHash}, Size: {certPdf.PdfContent.Length} bytes");

            return File(certPdf.PdfContent, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error downloading gap certification for report {id}", ex.ToString());
            return StatusCode(500, "Errore durante il download della certificazione");
        }
    }

    /// <summary>
    /// Download del PDF di Escalation dal database.
    /// Usato quando un report è passato da ESCALATED a COMPLETED/CONTRACT_BREACH.
    /// </summary>
    [HttpGet("{id}/download-gap-escalation")]
    public async Task<IActionResult> DownloadGapEscalation(int id)
    {
        const string source = "PdfReportsController.DownloadGapEscalation";

        try
        {
            var report = await db.PdfReports
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound("Report non trovato");

            // Recupera il PDF di escalation dal database
            var escalationPdf = await db.GapValidationPdfs
                .FirstOrDefaultAsync(c => c.PdfReportId == id && c.DocumentType == GapValidationDocumentTypes.ESCALATION);

            if (escalationPdf == null)
            {
                return NotFound("PDF Escalation non trovato per questo report");
            }

            if (escalationPdf.PdfContent == null || escalationPdf.PdfContent.Length == 0)
            {
                return NotFound("PDF di escalation non disponibile");
            }

            var fileName = $"PolarDrive_GapEscalation_{id}_{report.ClientVehicle?.Vin}_{escalationPdf.GeneratedAt:yyyyMMdd}.pdf";

            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            if (!string.IsNullOrWhiteSpace(escalationPdf.PdfHash))
                Response.Headers.ETag = $"W/\"{escalationPdf.PdfHash}\"";

            await _logger.Info(source, $"Download PDF Escalation per report {id}",
                $"Hash: {escalationPdf.PdfHash}, Size: {escalationPdf.PdfContent.Length} bytes");

            return File(escalationPdf.PdfContent, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error downloading escalation PDF for report {id}", ex.ToString());
            return StatusCode(500, "Errore durante il download del PDF escalation");
        }
    }

    /// <summary>
    /// Download del PDF di Contract Breach dal database.
    /// Stato finale che indica violazione contrattuale.
    /// </summary>
    [HttpGet("{id}/download-gap-contract-breach")]
    public async Task<IActionResult> DownloadGapContractBreach(int id)
    {
        const string source = "PdfReportsController.DownloadGapContractBreach";

        try
        {
            var report = await db.PdfReports
                .Include(r => r.ClientVehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound("Report non trovato");

            // Recupera il PDF di contract breach dal database
            var breachPdf = await db.GapValidationPdfs
                .FirstOrDefaultAsync(c => c.PdfReportId == id && c.DocumentType == GapValidationDocumentTypes.CONTRACT_BREACH);

            if (breachPdf == null)
            {
                return NotFound("PDF Contract Breach non trovato per questo report");
            }

            if (breachPdf.PdfContent == null || breachPdf.PdfContent.Length == 0)
            {
                return NotFound("PDF di contract breach non disponibile");
            }

            var fileName = $"PolarDrive_GapContractBreach_{id}_{report.ClientVehicle?.Vin}_{breachPdf.GeneratedAt:yyyyMMdd}.pdf";

            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            if (!string.IsNullOrWhiteSpace(breachPdf.PdfHash))
                Response.Headers.ETag = $"W/\"{breachPdf.PdfHash}\"";

            await _logger.Info(source, $"Download PDF Contract Breach per report {id}",
                $"Hash: {breachPdf.PdfHash}, Size: {breachPdf.PdfContent.Length} bytes");

            return File(breachPdf.PdfContent, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error downloading contract breach PDF for report {id}", ex.ToString());
            return StatusCode(500, "Errore durante il download del PDF contract breach");
        }
    }

    #endregion
}