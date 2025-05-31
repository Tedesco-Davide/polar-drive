using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

            var state = vin;

            var url = $"{authBaseUrl}?" +
                      $"client_id={clientId}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&response_type=code" +
                      $"&scope={Uri.EscapeDataString(scopes)}" +
                      $"&state={state}" +
                      $"&audience=ownerapi" +
                      $"&brand={brand}";

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

    [HttpGet("OAuthCallback")]
    public async Task<IActionResult> OAuthCallback([FromQuery] string code, [FromQuery] string state, [FromQuery] string brand)
    {
        const string source = "VehicleOAuthController.OAuthCallback";

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(brand))
        {
            await _logger.Error(source, "Missing one or more required parameters");
            return BadRequest("Missing parameters");
        }

        try
        {
            var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Vin == state);
            if (vehicle == null)
            {
                await _logger.Warning(source, "Vehicle not found for OAuth callback", $"State (VIN): {state}");
                return BadRequest("Invalid VIN");
            }

            brand = brand.Trim().ToLowerInvariant();

            var tokens = brand switch
            {
                "tesla" => await TeslaOAuthService.ExchangeCodeForTokens(code),
                "polestar" => await PolestarOAuthService.ExchangeCodeForTokens(code),
                "porsche" => await PorscheOAuthService.ExchangeCodeForTokens(code),
                _ => throw new NotSupportedException($"Brand '{brand}' not supported")
            };

            await _logger.Debug(source, "Token received from external service", $"Brand: {brand}, VIN: {vehicle.Vin}");

            _db.ClientTokens.Add(new ClientToken
            {
                VehicleId = vehicle.Id,
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                AccessTokenExpiresAt = DateTime.UtcNow.AddHours(8),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            vehicle.ClientOAuthAuthorized = true;
            await _db.SaveChangesAsync();

            await _logger.Info(source, "OAuth authorization completed", $"Brand: {brand}, VIN: {vehicle.Vin}");

            return Redirect("https://datapolar.dev/admin");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Exception in OAuthCallback", ex.ToString());
            return StatusCode(500, $"OAuth error: {ex.Message}");
        }
    }

    public static class TeslaOAuthService
    {
        public static Task<(string AccessToken, string RefreshToken)> ExchangeCodeForTokens(string code)
        {
            return Task.FromResult((
                AccessToken: "TESLA_TOKEN_" + code,
                RefreshToken: "TESLA_REFRESH_" + code
            ));
        }
    }

    public static class PolestarOAuthService
    {
        public static Task<(string AccessToken, string RefreshToken)> ExchangeCodeForTokens(string code)
        {
            return Task.FromResult((
                AccessToken: "POLESTAR_TOKEN_" + code,
                RefreshToken: "POLESTAR_REFRESH_" + code
            ));
        }
    }

    public static class PorscheOAuthService
    {
        public static Task<(string AccessToken, string RefreshToken)> ExchangeCodeForTokens(string code)
        {
            return Task.FromResult((
                AccessToken: "PORSCHE_TOKEN_" + code,
                RefreshToken: "PORSCHE_REFRESH_" + code
            ));
        }
    }

}