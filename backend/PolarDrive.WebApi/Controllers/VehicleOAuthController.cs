using Microsoft.AspNetCore.Mvc;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehicleOAuthController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly PolarDriveLogger _logger;

    public VehicleOAuthController(PolarDriveDbContext db)
    {
        _db = db;
        _logger = new PolarDriveLogger(_db);
    }

    [HttpGet("GenerateUrl")]
    public async Task<IActionResult> GenerateOAuthUrl([FromQuery] string brand, [FromQuery] string vin)
    {
        const string source = "VehicleOAuthController.GenerateOAuthUrl";

        if (string.IsNullOrWhiteSpace(brand))
        {
            await _logger.Error(source, "Missing required parameter: brand");
            return BadRequest("Missing required parameter: brand");
        }

        brand = brand.Trim().ToLowerInvariant();

        string clientId;
        string redirectUri = "https://datapolar.dev/api/OAuthCallback";
        string scopes;
        string authBaseUrl;

        try
        {
            switch (brand)
            {
                case "tesla":
                    clientId = "ownerapi";
                    scopes = "openid offline_access vehicle_read vehicle_telemetry vehicle_charging_cmds";
                    authBaseUrl = "https://auth.tesla.com/oauth2/v3/authorize";
                    break;

                case "polestar":
                    clientId = "<polestar_client_id>";
                    scopes = "openid vehicles:read";
                    authBaseUrl = "https://auth.polestar.com/oauth2/authorize";
                    break;

                default:
                    await _logger.Warning(
                        source,
                        "Unsupported brand received",
                        $"Brand: {brand}, VIN: {vin}"
                    );
                    return BadRequest($"Unsupported brand received: {brand}");
            }

            var state = Guid.NewGuid().ToString("N");

            var url = $"{authBaseUrl}?" +
                      $"client_id={clientId}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&response_type=code" +
                      $"&scope={Uri.EscapeDataString(scopes)}" +
                      $"&state={state}" +
                      $"&audience=ownerapi";

            await _logger.Info(
                source,
                $"Generated OAuth URL for brand",
                $"Brand: {brand}, VIN: {vin}, URL: {url}"
            );

            return Ok(new { url });
        }
        catch (Exception ex)
        {
            await _logger.Error(
                source,
                "Exception while generating OAuth URL",
                ex.ToString()
            );

            return StatusCode(500, "An error occurred while generating the URL.");
        }
    }
}