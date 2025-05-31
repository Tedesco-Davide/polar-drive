using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientVehiclesController(PolarDriveDbContext db) : ControllerBase
{
    private readonly PolarDriveLogger _logger = new(db);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminWorkflowExtendedDTO>>> Get()
    {
        await _logger.Info("ClientVehiclesController.Get", "Requested list of client vehicles.");

        var rawItems = await db.ClientVehicles
            .Include(v => v.ClientCompany)
            .ToListAsync();

        var items = rawItems.Select(v => new AdminWorkflowExtendedDTO
        {
            Id = v.Id,
            Vin = v.Vin,
            FuelType = v.FuelType,
            Brand = v.Brand,
            Model = v.Model,
            Trim = v.Trim ?? "",
            Color = v.Color ?? "",
            IsActive = v.IsActiveFlag,
            IsFetching = v.IsFetchingDataFlag,
            FirstActivationAt = v.FirstActivationAt?.ToString("o"),
            LastDeactivationAt = v.LastDeactivationAt?.ToString("o"),
            LastFetchingDataAt = v.LastFetchingDataAt?.ToString("o"),
            ClientOAuthAuthorized = v.ClientOAuthAuthorized,
            ClientCompany = new ClientCompanyDTO
            {
                Id = v.ClientCompany!.Id,
                VatNumber = v.ClientCompany.VatNumber,
                Name = v.ClientCompany.Name,
                ReferentName = v.ClientCompany.ReferentName,
                ReferentMobileNumber = v.ClientCompany.ReferentMobileNumber,
                ReferentEmail = v.ClientCompany.ReferentEmail
            }
        }).ToList();

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult> Post([FromBody] ClientVehicleDTO dto)
    {
        if (dto.IsActive && !dto.IsFetching)
        {
            await _logger.Warning("ClientVehiclesController.Post", "Invalid vehicle state: active without fetching.");
            return BadRequest("SERVER ERROR → BAD REQUEST: An Active vehicle must also be in Data Acquisition state (IsFetching)!");
        }

        if (!await db.ClientCompanies.AnyAsync(c => c.Id == dto.ClientCompanyId))
        {
            await _logger.Warning("ClientVehiclesController.Post", "Client company not found.", $"CompanyId: {dto.ClientCompanyId}");
            return NotFound("SERVER ERROR → NOT FOUND: Client Company not found!");
        }

        var entity = new ClientVehicle
        {
            ClientCompanyId = dto.ClientCompanyId,
            Vin = dto.Vin,
            FuelType = dto.FuelType,
            Brand = dto.Brand,
            Model = dto.Model,
            Trim = dto.Trim,
            Color = dto.Color,
            IsActiveFlag = false,
            IsFetchingDataFlag = false,
            ClientOAuthAuthorized = false,
            FirstActivationAt = ParseDate(dto.FirstActivationAt),
            LastDeactivationAt = ParseDate(dto.LastDeactivationAt),
            CreatedAt = DateTime.UtcNow
        };

        await _logger.Info("ClientVehiclesController.Post", "Vehicle inserted via POST and awaits OAuth authorization.", $"VIN: {dto.Vin}");

        db.ClientVehicles.Add(entity);
        await db.SaveChangesAsync();

        await _logger.Info("ClientVehiclesController.Post", "New client vehicle created.", $"VehicleId: {entity.Id}, VIN: {entity.Vin}");

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity.Id);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] VehicleStatusUpdateDTO dto)
    {
        if (dto.IsActive && !dto.IsFetching)
        {
            await _logger.Warning("ClientVehiclesController.UpdateStatus", "Invalid vehicle state update: active without fetching.", $"VehicleId: {id}");
            return BadRequest("SERVER ERROR → BAD REQUEST: An Active vehicle must also be in Data Acquisition state (IsFetching)!");
        }

        var vehicle = await db.ClientVehicles.FindAsync(id);
        if (vehicle == null)
        {
            await _logger.Warning("ClientVehiclesController.UpdateStatus", "Vehicle not found.", $"VehicleId: {id}");
            return NotFound("SERVER ERROR → NOT FOUND: Vehicle not found!");
        }

        if (!vehicle.ClientOAuthAuthorized)
        {
            await _logger.Warning("ClientVehiclesController.Put", "Attempt to change vehicle status without OAuth authorization.", $"VehicleId: {id}");
            return BadRequest("SERVER ERROR → BAD REQUEST: Vehicle must be authorized via OAuth before activation.");
        }

        bool wasActive = vehicle.IsActiveFlag;
        bool wasFetching = vehicle.IsFetchingDataFlag;

        vehicle.IsActiveFlag = dto.IsActive;
        vehicle.IsFetchingDataFlag = dto.IsFetching;

        if (!dto.IsActive && !dto.IsFetching && vehicle.ClientOAuthAuthorized)
        {
            vehicle.ClientOAuthAuthorized = false;
            await _logger.Info("ClientVehiclesController.UpdateStatus", "OAuth authorization reset due to status fallback.", $"VehicleId: {id}");
        }

        if (wasActive && !dto.IsActive)
            vehicle.LastDeactivationAt = DateTime.UtcNow;

        if (wasFetching != dto.IsFetching)
            vehicle.LastFetchingDataAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await _logger.Info("ClientVehiclesController.UpdateStatus", "Vehicle status updated.", $"VehicleId: {id}, IsActive: {dto.IsActive}, IsFetching: {dto.IsFetching}");
        return NoContent();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] ClientVehicleDTO dto)
    {
        var vehicle = await db.ClientVehicles.FindAsync(id);
        if (vehicle == null)
        {
            await _logger.Warning("ClientVehiclesController.Put", "Vehicle not found.", $"VehicleId: {id}");
            return NotFound("SERVER ERROR → NOT FOUND: Vehicle not found!");
        }

        if (!vehicle.ClientOAuthAuthorized)
        {
            await _logger.Warning("ClientVehiclesController.Put", "Attempt to change vehicle status without OAuth authorization.", $"VehicleId: {id}");
            return BadRequest("SERVER ERROR → BAD REQUEST: Vehicle must be authorized via OAuth before activation.");
        }

        if (dto.IsActive && !dto.IsFetching)
        {
            await _logger.Warning("ClientVehiclesController.Put", "Invalid update: active without fetching.", $"VehicleId: {id}");
            return BadRequest("SERVER ERROR → BAD REQUEST: An Active vehicle must also be in Data Acquisition state (IsFetching)!");
        }

        if (!await db.ClientCompanies.AnyAsync(c => c.Id == dto.ClientCompanyId))
        {
            await _logger.Warning("ClientVehiclesController.Put", "Associated company not found.", $"CompanyId: {dto.ClientCompanyId}");
            return NotFound("SERVER ERROR → NOT FOUND: Client Company not found!");
        }

        vehicle.Vin = dto.Vin;
        vehicle.FuelType = dto.FuelType;
        vehicle.Brand = dto.Brand;
        vehicle.Model = dto.Model;
        vehicle.Trim = dto.Trim;
        vehicle.Color = dto.Color;
        vehicle.IsActiveFlag = dto.IsActive;
        vehicle.IsFetchingDataFlag = dto.IsFetching;
        vehicle.FirstActivationAt = ParseDate(dto.FirstActivationAt);
        vehicle.LastDeactivationAt = ParseDate(dto.LastDeactivationAt);

        await db.SaveChangesAsync();

        await _logger.Info("ClientVehiclesController.Put", "Client vehicle updated successfully.", $"VehicleId: {id}, VIN: {dto.Vin}");

        return NoContent();
    }

    private static DateTime? ParseDate(string? date)
    {
        return DateTime.TryParse(date, out var d) ? d : null;
    }
}