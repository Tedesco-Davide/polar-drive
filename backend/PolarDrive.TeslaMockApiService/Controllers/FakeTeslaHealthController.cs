using Microsoft.AspNetCore.Mvc;

namespace PolarDrive.TeslaMockApiService.Controllers;

/// <summary>
/// Controller per gli endpoint di health check che mimano l'API Tesla ufficiale
/// </summary>
[ApiController]
[Route("api/tesla")]
public class TeslaHealthController(ILogger<TeslaHealthController> logger) : ControllerBase
{
    private readonly ILogger<TeslaHealthController> _logger = logger;

    /// <summary>
    /// GET /api/tesla/health - Health check per OutageDetectionService
    /// </summary>
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        _logger.LogInformation("Tesla Fleet API health check requested from OutageDetectionService");
        
        var healthResponse = new 
        { 
            status = "healthy",
            timestamp = DateTime.Now,
            service = "Tesla Fleet API Mock",
            version = "1.0.0",
            uptime = DateTime.Now.Subtract(System.Diagnostics.Process.GetCurrentProcess().StartTime),
            message = "Tesla Mock API Service is running correctly"
        };

        return Ok(healthResponse);
    }

    /// <summary>
    /// GET /api/tesla/ping - Endpoint alternativo per ping
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        _logger.LogDebug("Tesla Fleet API ping requested");
        
        return Ok(new 
        { 
            status = "pong",
            timestamp = DateTime.Now
        });
    }
}