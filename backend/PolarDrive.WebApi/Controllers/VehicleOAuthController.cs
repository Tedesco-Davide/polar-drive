using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.Data.Constants;
using PolarDrive.Data.Helpers;
using PolarDrive.WebApi.Helpers;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehicleOAuthController(PolarDriveDbContext db, IWebHostEnvironment env, IConfiguration cfg) : ControllerBase
{
    private readonly PolarDriveDbContext _db = db;
    private readonly IWebHostEnvironment _env = env;
    private readonly IConfiguration _cfg = cfg;
    private readonly PolarDriveLogger _logger = new();

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
        string scopes;
        string authBaseUrl;

        var publicBase = _cfg["PublicBaseUrl"]?.TrimEnd('/');
        string redirectUri = $"{publicBase}/api/VehicleOAuth/OAuthCallback";

        try
        {
            switch (brand)
            {
            case VehicleConstants.VehicleBrand.TESLA:
                // Fleet API V2: ClientId e Scopes da configurazione (PROD) o default (DEV mock)
                clientId = _cfg["TeslaApi:ClientId"] ?? throw new InvalidOperationException("TeslaApi:ClientId not configured");
                scopes = _cfg["TeslaApi:Scopes"] ?? "openid offline_access vehicle_device_data vehicle_location vehicle_cmds vehicle_charging_cmds";

                // In dev usa il mock-api pubblico (porta 9090), altrimenti Tesla reale
                var teslaPublicBase = _cfg["TeslaApi:PublicBaseUrl"];
                var authBase = _cfg["TeslaApi:AuthBaseUrl"];
                authBaseUrl = _env.IsDevelopment() && !string.IsNullOrWhiteSpace(teslaPublicBase)
                    ? $"{teslaPublicBase.TrimEnd('/')}/oauth2/v3/authorize"
                    : $"{(authBase ?? "https://auth.tesla.com").TrimEnd('/')}/oauth2/v3/authorize";
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

            // Fleet API V2: rimosso audience=ownerapi (non usato)
            var url = $"{authBaseUrl}?" +
                      $"client_id={clientId}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&response_type=code" +
                      $"&scope={Uri.EscapeDataString(scopes)}" +
                      $"&state={state}" +
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

        var stateHash = GdprHelpers.GdprComputeLookupHash(state);
        var vehicle = await _db.ClientVehicles.FirstOrDefaultAsync(v => v.VinHash == stateHash);
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
                VehicleConstants.VehicleBrand.TESLA => await TeslaOAuthService.ExchangeCodeForTokens(code, _cfg, _env),
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
                AccessTokenExpiresAt = DateTime.Now.AddHours(8),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });

            vehicle.ClientOAuthAuthorized = true;
            vehicle.IsActiveFlag = true;
            vehicle.IsFetchingDataFlag = true;
            vehicle.FirstActivationAt = DateTime.Now;

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
        public static async Task<(string AccessToken, string RefreshToken)> ExchangeCodeForTokens(
            string code,
            IConfiguration configuration,
            IWebHostEnvironment env)
        {
            using var client = new HttpClient();

            // 🔹 Legge la base URL configurata (es. http://mock-api:9090 in dev)
            var baseUrl = configuration["TeslaApi:BaseUrl"];

            // 🔹 Determina endpoint corretto
            var tokenUrl = !string.IsNullOrWhiteSpace(baseUrl)
                ? $"{baseUrl.TrimEnd('/')}/oauth2/v3/token"
                : env.IsDevelopment()
                    ? "http://mock-api:9090/oauth2/v3/token"
                    : "https://auth.tesla.com/oauth2/v3/token";

            // 🔹 Parametri comuni
            var parameters = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code
            };

            // 🔹 Se siamo in produzione (Tesla Fleet API V2)
            if (!env.IsDevelopment())
            {
                parameters["client_id"] = configuration["TeslaApi:ClientId"] ?? throw new InvalidOperationException("TeslaApi:ClientId not configured");
                parameters["client_secret"] = configuration["TeslaApi:ClientSecret"] ?? throw new InvalidOperationException("TeslaApi:ClientSecret not configured");
                parameters["redirect_uri"] =
                configuration["PublicBaseUrl"]?.TrimEnd('/') + "/api/VehicleOAuth/OAuthCallback";
            }

            var content = new FormUrlEncodedContent(parameters);

            // 🔹 Esegue la POST al token endpoint
            var response = await client.PostAsync(tokenUrl, content);
            response.EnsureSuccessStatusCode();

            // 🔹 Legge e parse il JSON di risposta
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            return (
                AccessToken: data.GetProperty("access_token").GetString()!,
                RefreshToken: data.GetProperty("refresh_token").GetString()!
            );
        }

        public static async Task<string> RefreshAccessToken(
            string refreshToken,
            IConfiguration configuration,
            IWebHostEnvironment env)
        {
            using var client = new HttpClient();

            // Leggi la base URL della tua WebAPI (mock/proxy) da config
            var baseUrl = configuration["WebAPI:BaseUrl"];

            string tokenUrl;
            FormUrlEncodedContent content;

            if (env.IsDevelopment())
            {
                // ✅ CASO MOCK → chiama il backend tramite proxy /api/
                tokenUrl = $"{GenericHelpers.EnsureTrailingSlash(baseUrl)}api/oauth2/v3/token";
                content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken
                });
            }
            else
            {
                // ✅ CASO REALE → Tesla Fleet API V2
                var authBase = configuration["TeslaApi:AuthBaseUrl"] ?? "https://auth.tesla.com";
                tokenUrl = $"{authBase.TrimEnd('/')}/oauth2/v3/token";
                content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"]    = "refresh_token",
                    ["client_id"]     = configuration["TeslaApi:ClientId"] ?? throw new InvalidOperationException("TeslaApi:ClientId not configured"),
                    ["client_secret"] = configuration["TeslaApi:ClientSecret"] ?? throw new InvalidOperationException("TeslaApi:ClientSecret not configured"),
                    ["refresh_token"] = refreshToken
                });
            }

            var response = await client.PostAsync(tokenUrl, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            return data.GetProperty("access_token").GetString()!;
        }

        public static async Task<bool> ValidateToken(
            string accessToken,
            IConfiguration configuration,
            IWebHostEnvironment env)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                string apiUrl;
                if (env.IsDevelopment())
                {
                    var baseUrl = configuration["WebAPI:BaseUrl"];
                    // ✅ CASO MOCK → chiama il backend tramite proxy /api/
                    apiUrl = $"{GenericHelpers.EnsureTrailingSlash(baseUrl)}api/1/vehicles";
                }
                else
                {
                    // ✅ CASO REALE → Tesla Fleet API V2
                    var teslaApiBase = configuration["TeslaApi:BaseUrl"] ?? "https://fleet-api.prd.eu.vn.cloud.tesla.com";
                    apiUrl = $"{teslaApiBase.TrimEnd('/')}/api/1/vehicles";
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
}