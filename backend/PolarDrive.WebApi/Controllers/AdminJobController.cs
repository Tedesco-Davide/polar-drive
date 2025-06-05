using Microsoft.AspNetCore.Mvc;
using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Jobs;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/admin/jobs")]
public class AdminJobController(PolarDriveDbContext db) : ControllerBase
{
    private readonly PolarDriveLogger _logger = new(db);

    [HttpPost("monthly-report")]
    public async Task<IActionResult> RunMonthlyReportJob()
    {
        const string source = "AdminJobController.RunMonthlyReportJob";

        await _logger.Info(source, "Monthly report job triggered manually from API.");

        try
        {
            var job = new MonthlyReportGeneratorJob(db);
            await job.RunAsync();

            await _logger.Info(source, "Monthly report job completed successfully.");
            return Ok("✅ Monthly report generation completed.");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Monthly report job execution failed.", ex.ToString());
            return StatusCode(500, "❌ Server error during monthly report generation.");
        }
    }
}
