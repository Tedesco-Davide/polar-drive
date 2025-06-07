using Microsoft.AspNetCore.Mvc;

namespace PolarDrive.TeslaMockApiService.Controllers;

[ApiController]
[Route("oauth2/v3")]
public class FakeOAuthController : ControllerBase
{
    [HttpGet("authorize")]
    public IActionResult Authorize(
        [FromQuery] string redirect_uri,
        [FromQuery] string state
    )
    {
        // Genera un fake code per il mock
        var fakeCode = "FAKECODE_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var redirectUrl = $"{redirect_uri}?code={fakeCode}&state={state}";

        return Redirect(redirectUrl);
    }

    [HttpPost("token")]
    public IActionResult Token([FromForm] TokenRequest request)
    {
        // Valida il grant type
        if (string.IsNullOrEmpty(request.GrantType))
        {
            return BadRequest(new { error = "invalid_request", error_description = "grant_type is required" });
        }

        var response = request.GrantType switch
        {
            "authorization_code" => HandleAuthorizationCode(request),
            "refresh_token" => HandleRefreshToken(request),
            _ => BadRequest(new { error = "unsupported_grant_type" })
        };

        return response;
    }

    private IActionResult HandleAuthorizationCode(TokenRequest request)
    {
        if (string.IsNullOrEmpty(request.Code))
        {
            return BadRequest(new { error = "invalid_request", error_description = "code is required" });
        }

        // Simula la risposta Tesla OAuth
        var tokenResponse = new
        {
            access_token = "TESLA_ACCESS_TOKEN_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            refresh_token = "TESLA_REFRESH_TOKEN_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            id_token = "TESLA_ID_TOKEN_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            expires_in = 28800, // 8 ore
            token_type = "Bearer",
            created_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        return Ok(tokenResponse);
    }

    private IActionResult HandleRefreshToken(TokenRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest(new { error = "invalid_request", error_description = "refresh_token is required" });
        }

        // Simula il refresh del token
        var tokenResponse = new
        {
            access_token = "TESLA_REFRESHED_ACCESS_TOKEN_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            refresh_token = request.RefreshToken, // Mantieni lo stesso refresh token
            expires_in = 28800, // 8 ore
            token_type = "Bearer",
            created_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        return Ok(tokenResponse);
    }
}

public class TokenRequest
{
    [FromForm(Name = "grant_type")]
    public string? GrantType { get; set; }

    [FromForm(Name = "code")]
    public string? Code { get; set; }

    [FromForm(Name = "refresh_token")]
    public string? RefreshToken { get; set; }

    [FromForm(Name = "client_id")]
    public string? ClientId { get; set; }

    [FromForm(Name = "redirect_uri")]
    public string? RedirectUri { get; set; }
}