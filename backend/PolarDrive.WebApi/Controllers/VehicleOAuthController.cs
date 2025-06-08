using System.Text.Json;
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
    private readonly IWebHostEnvironment _env;


    public VehicleOAuthController(PolarDriveDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
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
        string redirectUri = "https://localhost:5041/api/VehicleOAuth/OAuthCallback";
        string scopes;
        string authBaseUrl;

        try
        {
            switch (brand)
            {
                case "tesla":
                    clientId = "ownerapi";
                    scopes = "openid offline_access vehicle_read vehicle_telemetry vehicle_charging_cmds";
                    authBaseUrl = _env.IsDevelopment()
                        ? "http://localhost:5071/oauth2/v3/authorize"
                        : "https://auth.tesla.com/oauth2/v3/authorize"; break;

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
    public async Task<IActionResult> OAuthCallback([FromQuery] string code, [FromQuery] string state)
    {
        const string source = "VehicleOAuthController.OAuthCallback";

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            await _logger.Error(source, "Missing code or state");
            return BadRequest("Missing parameters");
        }

        var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.Vin == state);
        if (vehicle == null)
        {
            await _logger.Warning(source, "Vehicle not found for OAuth callback", $"State (VIN): {state}");
            return BadRequest("Invalid VIN");
        }

        var brand = vehicle.Brand?.Trim();
        if (string.IsNullOrWhiteSpace(brand))
        {
            await _logger.Error(source, "Brand not found on vehicle record", $"VIN: {vehicle.Vin}");
            return StatusCode(500, "Vehicle brand missing in database");
        }

        // Normalizzazione con Prima Maiuscola
        brand = char.ToUpperInvariant(brand[0]) + brand[1..].ToLowerInvariant();
        await _logger.Debug(source, "Normalized brand", $"Brand (normalized): {brand}");

        try
        {
            var tokens = brand.ToLowerInvariant() switch
            {
                "tesla" => await TeslaOAuthService.ExchangeCodeForTokens(code, _env),
                "polestar" => await PolestarOAuthService.ExchangeCodeForTokens(code),
                "porsche" => await PorscheOAuthService.ExchangeCodeForTokens(code),
                _ => throw new NotSupportedException($"Brand '{brand}' not supported")
            };

            await _logger.Debug(source, "Token received from external service", $"Brand: {brand}, VIN: {vehicle.Vin}");

            var existingToken = await _db.ClientTokens.FirstOrDefaultAsync(t => t.VehicleId == vehicle.Id);
            if (existingToken != null)
            {
                _db.ClientTokens.Remove(existingToken);
            }

            _db.ClientTokens.Add(new ClientToken
            {
                VehicleId = vehicle.Id,
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                AccessTokenExpiresAt = DateTime.UtcNow.AddHours(8),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            // ✅ AGGIUNGI QUESTE RIGHE:
            vehicle.ClientOAuthAuthorized = true;
            vehicle.IsActiveFlag = true;
            vehicle.IsFetchingDataFlag = true;
            vehicle.FirstActivationAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _logger.Info(
                source,
                "OAuth authorization completed with automatic activation",
                $"Brand: {brand}, VIN: {vehicle.Vin}, IsActive: true, IsFetching: true"
            );
            return Redirect("http://localhost:3000/admin");
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Exception in OAuthCallback", ex.ToString());
            return StatusCode(500, $"OAuth error: {ex.Message}");
        }
    }

    public static class TeslaOAuthService
    {
        public static async Task<(string AccessToken, string RefreshToken)> ExchangeCodeForTokens(string code, IWebHostEnvironment env)
        {
            using var client = new HttpClient();

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code
            });

            string tokenUrl;
            if (env.IsDevelopment())
            {
                // ✅ CASO MOCK → chiama il fake backend
                tokenUrl = "http://localhost:5071/oauth2/v3/token";
            }
            else
            {
                // ✅ CASO REALE → chiama Tesla
                tokenUrl = "https://auth.tesla.com/oauth2/v3/token";
                // Aggiungi parametri extra per Tesla reale
                content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["client_id"] = "ownerapi",
                    ["code"] = code,
                    ["redirect_uri"] = "https://localhost:5041/api/OAuthCallback"
                });
            }

            var response = await client.PostAsync(tokenUrl, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            return (
                AccessToken: data.GetProperty("access_token").GetString()!,
                RefreshToken: data.GetProperty("refresh_token").GetString()!
            );
        }

        public static async Task<string> RefreshAccessToken(string refreshToken, IWebHostEnvironment env)
        {
            using var client = new HttpClient();

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            });

            string tokenUrl;
            if (env.IsDevelopment())
            {
                // ✅ CASO MOCK → chiama il fake backend
                tokenUrl = "http://localhost:5071/oauth2/v3/token";
            }
            else
            {
                // ✅ CASO REALE → chiama Tesla
                tokenUrl = "https://auth.tesla.com/oauth2/v3/token";

                // Aggiungi client_id per Tesla reale
                content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = "ownerapi",
                    ["refresh_token"] = refreshToken
                });
            }

            var response = await client.PostAsync(tokenUrl, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            return data.GetProperty("access_token").GetString()!;
        }

        public static async Task<bool> ValidateToken(string accessToken, IWebHostEnvironment env)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                string apiUrl;
                if (env.IsDevelopment())
                {
                    // ✅ CASO MOCK → chiama il fake backend
                    apiUrl = "http://localhost:5071/api/1/vehicles";
                }
                else
                {
                    // ✅ CASO REALE → chiama Tesla
                    apiUrl = "https://owner-api.teslamotors.com/api/1/vehicles";
                }

                var response = await client.GetAsync(apiUrl);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
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