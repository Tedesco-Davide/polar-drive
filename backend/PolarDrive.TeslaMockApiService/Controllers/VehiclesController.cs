using Microsoft.AspNetCore.Mvc;
using PolarDrive.TeslaMockApiService.Services;

namespace PolarDrive.TeslaMockApiService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController(MockVehicleDataGenerator generator) : ControllerBase
{
    private readonly MockVehicleDataGenerator _generator = generator;

    [HttpGet]
    public IActionResult GetVehicles()
    {
        var vehicles = _generator.GenerateVehicleList();
        return Ok(new { response = vehicles });
    }
}
