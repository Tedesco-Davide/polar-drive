using Microsoft.AspNetCore.Mvc;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController() : ControllerBase
{
    private readonly PolarDriveLogger _logger = new();

    public class LogFrontendDto
    {
        public string Source { get; set; } = string.Empty;
        public string Level { get; set; } = "INFO";
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] LogFrontendDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Source) || string.IsNullOrWhiteSpace(input.Message))
            return BadRequest("Source and Message are required.");

        if (!Enum.TryParse<PolarDriveLogLevel>(input.Level, true, out var parsedLevel))
            parsedLevel = PolarDriveLogLevel.INFO;

        await _logger.LogAsync(input.Source, parsedLevel, input.Message, input.Details);

        return Ok();
    }
}