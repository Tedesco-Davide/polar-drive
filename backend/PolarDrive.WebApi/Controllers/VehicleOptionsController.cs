using Microsoft.AspNetCore.Mvc;
using PolarDrive.Data.Constants;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehicleOptionsController : ControllerBase
{
    private readonly ILogger<VehicleOptionsController> _logger;

    public VehicleOptionsController(ILogger<VehicleOptionsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get all vehicle options (brands, models, trims, colors)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetVehicleOptions()
    {
        try
        {
            var options = VehicleConstants.Options;
            
            var response = new Dictionary<string, object>
            {
                ["options"] = options.ToDictionary(
                    brand => brand.Key,
                    brand => new
                    {
                        models = brand.Value.ToDictionary(
                            model => model.Key,
                            model => new
                            {
                                fuelType = model.Value.FuelType.ToString(),
                                trims = model.Value.Trims,
                                colors = model.Value.Colors
                            }
                        )
                    }
                )
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle options");
            return StatusCode(500, "Error retrieving vehicle options");
        }
    }

    /// <summary>
    /// Get all valid brands
    /// </summary>
    [HttpGet("brands")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetBrands()
    {
        return Ok(VehicleConstants.ValidBrands);
    }

    /// <summary>
    /// Get all valid models
    /// </summary>
    [HttpGet("models")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetModels()
    {
        return Ok(VehicleConstants.ValidModels);
    }

    /// <summary>
    /// Get models for a specific brand
    /// </summary>
    [HttpGet("brands/{brand}/models")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetModelsByBrand(string brand)
    {
        if (!VehicleConstants.Options.TryGetValue(brand, out var models))
        {
            return NotFound($"Brand '{brand}' not found");
        }

        return Ok(models.Keys);
    }

    /// <summary>
    /// Reload vehicle options from configuration file (admin use)
    /// </summary>
    [HttpPost("reload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ReloadOptions()
    {
        try
        {
            VehicleConstants.ReloadOptions();
            _logger.LogInformation("Vehicle options reloaded successfully");
            return Ok(new { message = "Vehicle options reloaded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading vehicle options");
            return StatusCode(500, "Error reloading vehicle options");
        }
    }
}
