using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Constants;
using PolarDrive.WebApi.Services;
using PolarDrive.WebApi.PolarAiReports;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GapAlertsController(
    PolarDriveDbContext db,
    IGapMonitoringService monitoringService,
    GapValidationPdfService pdfService) : ControllerBase
{
    private readonly PolarDriveDbContext _db = db;
    private readonly IGapMonitoringService _monitoringService = monitoringService;
    private readonly GapValidationPdfService _pdfService = pdfService;
    private readonly PolarDriveLogger _logger = new();

    /// <summary>
    /// Lista alert con filtri e paginazione
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<object>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? alertType = null,
        [FromQuery] string? severity = null,
        [FromQuery] int? vehicleId = null)
    {
        try
        {
            await _logger.Info("GapAlertsController.Get", "Requested filtered list of gap alerts",
                $"Page: {page}, PageSize: {pageSize}, Status: {status}, AlertType: {alertType}");

            var query = _db.GapAlerts
                .AsNoTracking()
                .AsQueryable();

            // Filtri
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(a => a.Status == status);

            if (!string.IsNullOrWhiteSpace(alertType))
                query = query.Where(a => a.AlertType == alertType);

            if (!string.IsNullOrWhiteSpace(severity))
                query = query.Where(a => a.Severity == severity);

            if (vehicleId.HasValue)
                query = query.Where(a => a.VehicleId == vehicleId);

            var totalCount = await query.CountAsync();

            var alerts = await query
                .OrderByDescending(a => a.DetectedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.VehicleId,
                    a.PdfReportId,
                    a.AlertType,
                    a.Severity,
                    a.DetectedAt,
                    a.Description,
                    a.MetricsJson,
                    a.Status,
                    a.ResolvedAt,
                    a.ResolutionNotes,
                    Vin = a.ClientVehicle != null ? a.ClientVehicle.Vin : null,
                    Brand = a.ClientVehicle != null ? a.ClientVehicle.Brand : null,
                    CompanyName = a.ClientVehicle != null && a.ClientVehicle.ClientCompany != null
                        ? a.ClientVehicle.ClientCompany.Name : null
                })
                .ToListAsync();

            return Ok(new PaginatedResponse<object>
            {
                Data = alerts.Cast<object>().ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("GapAlertsController.Get", "Error retrieving gap alerts", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
    }

    /// <summary>
    /// Dettaglio singolo alert
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var alert = await _db.GapAlerts
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Select(a => new
                {
                    a.Id,
                    a.VehicleId,
                    a.PdfReportId,
                    a.AlertType,
                    a.Severity,
                    a.DetectedAt,
                    a.Description,
                    a.MetricsJson,
                    a.Status,
                    a.ResolvedAt,
                    a.ResolutionNotes,
                    Vin = a.ClientVehicle != null ? a.ClientVehicle.Vin : null,
                    Brand = a.ClientVehicle != null ? a.ClientVehicle.Brand : null,
                    CompanyName = a.ClientVehicle != null && a.ClientVehicle.ClientCompany != null
                        ? a.ClientVehicle.ClientCompany.Name : null
                })
                .FirstOrDefaultAsync();

            if (alert == null)
                return NotFound("Alert not found");

            return Ok(alert);
        }
        catch (Exception ex)
        {
            await _logger.Error("GapAlertsController.GetById", $"Error retrieving alert {id}", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
    }

    /// <summary>
    /// Statistiche aggregate degli alert
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var stats = await _monitoringService.GetAlertStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            await _logger.Error("GapAlertsController.GetStats", "Error retrieving stats", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
    }

    /// <summary>
    /// Certifica alert - genera PDF CERTIFICATION, status → COMPLETED
    /// </summary>
    [HttpPost("{id}/certify")]
    public async Task<IActionResult> Certify(int id, [FromBody] AlertActionRequest request)
    {
        const string source = "GapAlertsController.Certify";

        try
        {
            var alert = await _db.GapAlerts.FindAsync(id);
            if (alert == null)
                return NotFound("Alert not found");

            if (alert.Status == GapAlertStatus.COMPLETED || alert.Status == GapAlertStatus.CONTRACT_BREACH)
                return BadRequest($"Alert is already in final status: {alert.Status}");

            await _logger.Info(source, $"Certifying alert {id}");

            // Trova il report PDF associato al veicolo
            var report = await _db.PdfReports
                .Where(r => r.VehicleId == alert.VehicleId && r.Status == "COMPLETED")
                .OrderByDescending(r => r.GeneratedAt)
                .FirstOrDefaultAsync();

            if (report == null)
            {
                await _logger.Warning(source, $"No completed PDF report found for vehicle {alert.VehicleId}");
                return BadRequest("No completed PDF report found for this vehicle");
            }

            // Genera PDF di certificazione
            var success = await _pdfService.StartGapValidationPdfAsync(report.Id, GapValidationDocumentTypes.CERTIFICATION, id);

            if (!success)
            {
                return StatusCode(500, "Error generating certification PDF");
            }

            // Aggiorna alert
            alert.Status = GapAlertStatus.COMPLETED;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.ResolutionNotes = request.Notes;
            alert.PdfReportId = report.Id;

            // Crea audit log
            var auditLog = new GapAuditLog
            {
                GapAlertId = id,
                VehicleId = alert.VehicleId,
                ActionAt = DateTime.UtcNow,
                ActionType = GapAuditActionTypes.CERTIFIED,
                ActionBy = request.ActionBy,
                ActionNotes = request.Notes,
                VerificationOutcome = "VALID",
                FinalDecision = "ACCEPTED"
            };

            _db.GapAuditLogs.Add(auditLog);
            await _db.SaveChangesAsync();

            await _logger.Info(source, $"Alert {id} certified successfully");

            return Ok(new
            {
                AlertId = id,
                NewStatus = alert.Status,
                PdfReportId = report.Id,
                DocumentType = GapValidationDocumentTypes.CERTIFICATION
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error certifying alert {id}", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
    }

    /// <summary>
    /// Escalate alert - genera PDF ESCALATION, status → ESCALATED
    /// </summary>
    [HttpPost("{id}/escalate")]
    public async Task<IActionResult> Escalate(int id, [FromBody] AlertActionRequest request)
    {
        const string source = "GapAlertsController.Escalate";

        try
        {
            var alert = await _db.GapAlerts.FindAsync(id);
            if (alert == null)
                return NotFound("Alert not found");

            if (alert.Status != GapAlertStatus.OPEN)
                return BadRequest($"Can only escalate OPEN alerts. Current status: {alert.Status}");

            await _logger.Info(source, $"Escalating alert {id}");

            // Trova il report PDF associato al veicolo
            var report = await _db.PdfReports
                .Where(r => r.VehicleId == alert.VehicleId && r.Status == "COMPLETED")
                .OrderByDescending(r => r.GeneratedAt)
                .FirstOrDefaultAsync();

            if (report == null)
            {
                await _logger.Warning(source, $"No completed PDF report found for vehicle {alert.VehicleId}");
                return BadRequest("No completed PDF report found for this vehicle");
            }

            // Genera PDF di escalation
            var success = await _pdfService.StartGapValidationPdfAsync(report.Id, GapValidationDocumentTypes.ESCALATION, id);

            if (!success)
            {
                return StatusCode(500, "Error generating escalation PDF");
            }

            // Aggiorna alert - NON è stato finale
            alert.Status = GapAlertStatus.ESCALATED;
            alert.PdfReportId = report.Id;

            // Crea audit log
            var auditLog = new GapAuditLog
            {
                GapAlertId = id,
                VehicleId = alert.VehicleId,
                ActionAt = DateTime.UtcNow,
                ActionType = GapAuditActionTypes.ESCALATED,
                ActionBy = request.ActionBy,
                ActionNotes = request.Notes,
                VerificationOutcome = "NEEDS_REVIEW"
            };

            _db.GapAuditLogs.Add(auditLog);
            await _db.SaveChangesAsync();

            await _logger.Info(source, $"Alert {id} escalated successfully");

            return Ok(new
            {
                AlertId = id,
                NewStatus = alert.Status,
                PdfReportId = report.Id,
                DocumentType = GapValidationDocumentTypes.ESCALATION
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error escalating alert {id}", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
    }

    /// <summary>
    /// Contract Breach - genera PDF CONTRACT_BREACH, status → CONTRACT_BREACH
    /// </summary>
    [HttpPost("{id}/breach")]
    public async Task<IActionResult> Breach(int id, [FromBody] AlertActionRequest request)
    {
        const string source = "GapAlertsController.Breach";

        try
        {
            var alert = await _db.GapAlerts.FindAsync(id);
            if (alert == null)
                return NotFound("Alert not found");

            if (alert.Status == GapAlertStatus.COMPLETED || alert.Status == GapAlertStatus.CONTRACT_BREACH)
                return BadRequest($"Alert is already in final status: {alert.Status}");

            await _logger.Info(source, $"Marking alert {id} as contract breach");

            // Trova il report PDF associato al veicolo
            var report = await _db.PdfReports
                .Where(r => r.VehicleId == alert.VehicleId && r.Status == "COMPLETED")
                .OrderByDescending(r => r.GeneratedAt)
                .FirstOrDefaultAsync();

            if (report == null)
            {
                await _logger.Warning(source, $"No completed PDF report found for vehicle {alert.VehicleId}");
                return BadRequest("No completed PDF report found for this vehicle");
            }

            // Genera PDF di contract breach
            var success = await _pdfService.StartGapValidationPdfAsync(report.Id, GapValidationDocumentTypes.CONTRACT_BREACH, id);

            if (!success)
            {
                return StatusCode(500, "Error generating contract breach PDF");
            }

            // Aggiorna alert
            alert.Status = GapAlertStatus.CONTRACT_BREACH;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.ResolutionNotes = request.Notes;
            alert.PdfReportId = report.Id;

            // Crea audit log
            var auditLog = new GapAuditLog
            {
                GapAlertId = id,
                VehicleId = alert.VehicleId,
                ActionAt = DateTime.UtcNow,
                ActionType = GapAuditActionTypes.CONTRACT_BREACH,
                ActionBy = request.ActionBy,
                ActionNotes = request.Notes,
                VerificationOutcome = "INVALID",
                FinalDecision = "REJECTED"
            };

            _db.GapAuditLogs.Add(auditLog);
            await _db.SaveChangesAsync();

            await _logger.Info(source, $"Alert {id} marked as contract breach");

            return Ok(new
            {
                AlertId = id,
                NewStatus = alert.Status,
                PdfReportId = report.Id,
                DocumentType = GapValidationDocumentTypes.CONTRACT_BREACH
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error marking alert {id} as breach", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
    }

    /// <summary>
    /// Ottiene l'intervallo di monitoraggio configurato (per frontend auto-refresh)
    /// </summary>
    [HttpGet("monitoring-interval")]
    public IActionResult GetMonitoringInterval()
    {
        return Ok(new
        {
            CheckIntervalMinutes = AppConfig.GAP_MONITORING_CHECK_INTERVAL_MINUTES
        });
    }

    /// <summary>
    /// Forza un controllo immediato su un veicolo specifico
    /// </summary>
    [HttpPost("check-vehicle/{vehicleId}")]
    public async Task<IActionResult> CheckVehicle(int vehicleId)
    {
        const string source = "GapAlertsController.CheckVehicle";

        try
        {
            await _logger.Info(source, $"Manual check requested for vehicle {vehicleId}");

            await _monitoringService.CheckVehicleAsync(vehicleId);

            return Ok(new { Message = $"Check completed for vehicle {vehicleId}" });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, $"Error checking vehicle {vehicleId}", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
    }

    /// <summary>
    /// Audit log per un alert specifico
    /// </summary>
    [HttpGet("{id}/audit")]
    public async Task<IActionResult> GetAuditLog(int id)
    {
        try
        {
            var logs = await _db.GapAuditLogs
                .AsNoTracking()
                .Where(l => l.GapAlertId == id)
                .OrderByDescending(l => l.ActionAt)
                .Select(l => new
                {
                    l.Id,
                    l.ActionAt,
                    l.ActionType,
                    l.ActionBy,
                    l.ActionNotes,
                    l.VerificationOutcome,
                    l.FinalDecision
                })
                .ToListAsync();

            return Ok(logs);
        }
        catch (Exception ex)
        {
            await _logger.Error("GapAlertsController.GetAuditLog", $"Error retrieving audit log for alert {id}", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
    }
}

/// <summary>
/// Request per azioni sugli alert
/// </summary>
public class AlertActionRequest
{
    public string? Notes { get; set; }
    public string? ActionBy { get; set; }
}
