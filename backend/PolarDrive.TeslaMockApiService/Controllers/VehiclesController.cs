using Microsoft.AspNetCore.Mvc;

namespace PolarDrive.TeslaMockApiService.Controllers;

[ApiController]
[Route("api/1")]
public class VehiclesController : ControllerBase
{
    [HttpGet("vehicles")]
    public IActionResult GetVehicles()
    {
        // ✅ Simula un controllo sul token (opzionale)
        if (!Request.Headers.ContainsKey("Authorization"))
            return Unauthorized(new { error = "missing_token" });

        var authHeader = Request.Headers["Authorization"].ToString();
        if (!authHeader.StartsWith("Bearer "))
            return Unauthorized(new { error = "invalid_token_format" });

        var accessToken = authHeader.Substring("Bearer ".Length);

        // ✅ Controllo più flessibile per includere anche i token refreshed
        if (string.IsNullOrWhiteSpace(accessToken) ||
            (!accessToken.StartsWith("TESLA_ACCESS_TOKEN_") &&
             !accessToken.StartsWith("TESLA_REFRESHED_ACCESS_TOKEN_")))
        {
            return Unauthorized(new { error = "invalid_token" });
        }

        // ✅ Risposta identica al formato Tesla Owner API reale
        var response = new
        {
            response = new[]
            {
                new
                {
                    id = 12345678901234567L, // Usa long per ID grandi
                    vehicle_id = 987654321,
                    vin = "5YJ3E1EA7KF317XXX",
                    display_name = "Model 3 Mock",
                    option_codes = "AD15,MDL3,RSF1,APBS,DV2W,IBB1",
                    color = "Midnight Silver Metallic",
                    access_type = "OWNER", // Campo che Tesla include sempre
                    tokens = new[] { "abc123", "def456" },
                    state = "online",
                    in_service = false,
                    id_s = "12345678901234567",
                    calendar_enabled = true,
                    api_version = 54, // Versione più recente
                    backseat_token = (string?)null,
                    backseat_token_updated_at = (string?)null,
                    // ✅ Aggiungi altri campi che Tesla include
                    granular_access = new { hide_private = false }
                },
                // ✅ Opzionale: aggiungi un secondo veicolo per testare array
                new
                {
                    id = 98765432109876543L,
                    vehicle_id = 123456789,
                    vin = "5YJ3000000NEXUS01", // Coerente con il tuo FakeTeslaJsonDataFetch
                    display_name = "Model Y Mock",
                    option_codes = "MDL3,W41B,MT322,CPF0,RSF1",
                    color = "Pearl White Multi-Coat",
                    access_type = "OWNER",
                    tokens = new[] { "xyz789", "uvw012" },
                    state = "online",
                    in_service = false,
                    id_s = "98765432109876543",
                    calendar_enabled = true,
                    api_version = 54,
                    backseat_token = (string?)null,
                    backseat_token_updated_at = (string?)null,
                    granular_access = new { hide_private = false }
                }
            },
            count = 2 // Aggiorna il count
        };

        return Ok(response);
    }

    // ✅ Bonus: Endpoint per vehicle data specifico
    [HttpGet("vehicles/{id}/vehicle_data")]
    public IActionResult GetVehicleData(long id)
    {
        // ✅ Stesso controllo auth
        if (!Request.Headers.ContainsKey("Authorization"))
            return Unauthorized(new { error = "missing_token" });

        var authHeader = Request.Headers["Authorization"].ToString();
        if (!authHeader.StartsWith("Bearer "))
            return Unauthorized(new { error = "invalid_token_format" });

        var accessToken = authHeader.Substring("Bearer ".Length);

        if (string.IsNullOrWhiteSpace(accessToken) ||
            (!accessToken.StartsWith("TESLA_ACCESS_TOKEN_") &&
             !accessToken.StartsWith("TESLA_REFRESHED_ACCESS_TOKEN_")))
        {
            return Unauthorized(new { error = "invalid_token" });
        }

        // ✅ Qui potresti usare il tuo FakeTeslaJsonDataFetch per dati completi
        // Per ora una risposta semplificata
        var vehicleData = new
        {
            response = new
            {
                id = id,
                vehicle_id = 987654321,
                vin = "5YJ3E1EA7KF317XXX",
                display_name = "Model 3 Mock",
                state = "online",
                charge_state = new
                {
                    battery_level = 75,
                    charging_state = "Complete",
                    battery_range = 250.5
                },
                climate_state = new
                {
                    inside_temp = 22.0,
                    outside_temp = 18.5,
                    is_climate_on = false
                }
                // ✅ Potresti espandere con tutti i dati del tuo DTO completo
            }
        };

        return Ok(vehicleData);
    }
}