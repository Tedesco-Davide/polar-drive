using Microsoft.AspNetCore.Mvc;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehicleOAuthController : ControllerBase
{
    [HttpGet("GenerateUrl")]
    public IActionResult GenerateOAuthUrl([FromQuery] string brand, [FromQuery] string vin)
    {
        if (string.IsNullOrWhiteSpace(brand))
            return BadRequest("Missing required parameter: brand");

        // Normalizzo in lowercase per confronto robusto
        brand = brand.Trim().ToLowerInvariant();

        string clientId;
        string redirectUri;
        string scopes;
        string authBaseUrl;

        switch (brand)
        {
            case "tesla":
                clientId = "ownerapi";
                redirectUri = "https://datapolar.dev/api/OAuthCallback";
                scopes = "openid offline_access vehicle_read vehicle_telemetry vehicle_charging_cmds";
                authBaseUrl = "https://auth.tesla.com/oauth2/v3/authorize";
                break;

            case "polestar":
                clientId = "<polestar_client_id>";
                redirectUri = "https://datapolar.dev/api/OAuthCallback";
                scopes = "openid vehicles:read";
                authBaseUrl = "https://auth.polestar.com/oauth2/authorize"; // da verificare
                break;

            default:
                return BadRequest($"Unsupported brand: {brand}");
        }

        var state = Guid.NewGuid().ToString("N");

        var url = $"{authBaseUrl}?" +
                  $"client_id={clientId}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&response_type=code" +
                  $"&scope={Uri.EscapeDataString(scopes)}" +
                  $"&state={state}" +
                  $"&audience=ownerapi"; // ← opzionale, specifico Tesla

        return Ok(new { url });
    }
}