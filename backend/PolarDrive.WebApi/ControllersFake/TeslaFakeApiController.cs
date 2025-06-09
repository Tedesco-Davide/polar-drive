using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Jobs;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeslaFakeApiController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly PolarDriveLogger _logger;

    public TeslaFakeApiController(PolarDriveDbContext db)
    {
        _db = db;
        _logger = new PolarDriveLogger(_db);
    }

    /// <summary>
    /// Forza la generazione di un report di test (ultimi 5 minuti - 4-5 records)
    /// </summary>
    [HttpPost("GenerateTestReport")]
    public async Task<IActionResult> GenerateTestReport()
    {
        const string source = "TestController.GenerateTestReport";

        try
        {
            await _logger.Info(source, "Manual test report generation triggered (5 min period)");

            var reportJob = new ReportGeneratorJob(_db);
            await reportJob.RunTestAsync();

            await _logger.Info(source, "Manual test report generation completed");

            return Ok(new
            {
                success = true,
                message = "Test report generation completed (last 5 minutes)",
                timestamp = DateTime.UtcNow,
                period = "5 minutes"
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error in manual test report generation", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Forza la generazione di un report rapido (ultimi 2 minuti - 1-2 records)
    /// </summary>
    [HttpPost("GenerateQuickReport")]
    public async Task<IActionResult> GenerateQuickReport()
    {
        const string source = "TestController.GenerateQuickReport";

        try
        {
            await _logger.Info(source, "Manual quick report generation triggered (2 min period)");

            var reportJob = new ReportGeneratorJob(_db);
            await reportJob.RunQuickTestAsync();

            await _logger.Info(source, "Manual quick report generation completed");

            return Ok(new
            {
                success = true,
                message = "Quick report generation completed (last 2 minutes)",
                timestamp = DateTime.UtcNow,
                period = "2 minutes"
            });
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Error in manual quick report generation", ex.ToString());
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Controlla quanti dati ci sono per ogni veicolo
    /// </summary>
    [HttpGet("DataStatus")]
    public async Task<IActionResult> GetDataStatus()
    {
        var vehicles = await _db.ClientVehicles
            .Where(v => v.ClientOAuthAuthorized && v.IsActiveFlag && v.IsFetchingDataFlag)
            .ToListAsync();

        var result = new List<object>();

        foreach (var vehicle in vehicles)
        {
            var dataCount = await _db.VehiclesData.CountAsync(vd => vd.VehicleId == vehicle.Id);
            var latestData = await _db.VehiclesData
                .Where(vd => vd.VehicleId == vehicle.Id)
                .OrderByDescending(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            result.Add(new
            {
                vin = vehicle.Vin,
                dataRecords = dataCount,
                latestData = latestData?.Timestamp,
                lastUpdate = vehicle.LastDataUpdate,
                isActive = vehicle.IsActiveFlag,
                isFetching = vehicle.IsFetchingDataFlag
            });
        }

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            vehicles = result,
            summary = new
            {
                totalVehicles = vehicles.Count,
                totalDataRecords = await _db.VehiclesData.CountAsync()
            }
        });
    }

    /// <summary>
    /// Controlla lo stato dei report generati
    /// </summary>
    [HttpGet("ReportStatus")]
    public async Task<IActionResult> GetReportStatus()
    {
        var reports = await _db.PdfReports
            .Include(r => r.ClientVehicle)
            .OrderByDescending(r => r.GeneratedAt)
            .Take(20)
            .ToListAsync();

        var result = reports.Select(r => new
        {
            reportId = r.Id,
            vin = r.ClientVehicle?.Vin,
            periodStart = r.ReportPeriodStart,
            periodEnd = r.ReportPeriodEnd,
            generatedAt = r.GeneratedAt,
            notes = r.Notes
        });

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            recentReports = result,
            totalReports = await _db.PdfReports.CountAsync()
        });
    }
}