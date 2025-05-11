using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientTeslaVehiclesController(PolarDriveDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminWorkflowExtendedDTO>>> Get()
    {
        var rawItems = await db.ClientTeslaVehicles
            .Include(v => v.ClientCompany)
            .ToListAsync();

        var items = rawItems.Select(v => new AdminWorkflowExtendedDTO
        {
            Id = v.Id,
            Vin = v.Vin,
            Model = v.Model,
            Trim = v.Trim ?? "",
            Color = v.Color ?? "",
            IsActive = v.IsActiveFlag,
            IsFetching = v.IsFetchingDataFlag,
            FirstActivationAt = v.FirstActivationAt?.ToString("o"),
            LastDeactivationAt = v.LastDeactivationAt?.ToString("o"),
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
    public async Task<ActionResult> Post([FromBody] ClientTeslaVehicleDTO dto)
    {
        // Validazione logica: se è attiva, deve anche fare fetch
        if (dto.IsActive && !dto.IsFetching)
            return BadRequest("SERVER ERROR → BAD REQUEST: An Active vehicle must also be in Data Acquisition state (IsFetching)!");

        if (!await db.ClientCompanies.AnyAsync(c => c.Id == dto.ClientCompanyId))
            return NotFound("SERVER ERROR → NOT FOUND: Client Company not found!");

        var entity = new ClientTeslaVehicle
        {
            ClientCompanyId = dto.ClientCompanyId,
            Vin = dto.Vin,
            Model = dto.Model,
            Trim = dto.Trim,
            Color = dto.Color,
            IsActiveFlag = dto.IsActive,
            IsFetchingDataFlag = dto.IsFetching,
            FirstActivationAt = ParseDate(dto.FirstActivationAt),
            LastDeactivationAt = ParseDate(dto.LastDeactivationAt),
            CreatedAt = DateTime.UtcNow
        };

        db.ClientTeslaVehicles.Add(entity);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity.Id);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] TeslaVehicleStatusUpdateDTO dto)
    {
        if (dto.IsActive && !dto.IsFetching)
            return BadRequest("SERVER ERROR → BAD REQUEST: An Active vehicle must also be in Data Acquisition state (IsFetching)!");

        var vehicle = await db.ClientTeslaVehicles.FindAsync(id);
        if (vehicle == null)
            return NotFound("SERVER ERROR → NOT FOUND: Tesla vehicle not found!");

        vehicle.IsActiveFlag = dto.IsActive;
        vehicle.IsFetchingDataFlag = dto.IsFetching;

        if (dto.IsFetching)
            vehicle.LastFetchingDataAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return NoContent();
    }

    private static DateTime? ParseDate(string? date)
    {
        return DateTime.TryParseExact(date, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var d)
            ? d : null;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] ClientTeslaVehicleDTO dto)
    {
        var vehicle = await db.ClientTeslaVehicles.FindAsync(id);
        if (vehicle == null)
            return NotFound("SERVER ERROR → NOT FOUND: Tesla vehicle not found!");

        if (dto.IsActive && !dto.IsFetching)
            return BadRequest("SERVER ERROR → BAD REQUEST: An Active vehicle must also be in Data Acquisition state (IsFetching)!");

        if (!await db.ClientCompanies.AnyAsync(c => c.Id == dto.ClientCompanyId))
            return NotFound("SERVER ERROR → NOT FOUND: Client Company not found!");

        vehicle.Vin = dto.Vin;
        vehicle.Model = dto.Model;
        vehicle.Trim = dto.Trim;
        vehicle.Color = dto.Color;
        vehicle.IsActiveFlag = dto.IsActive;
        vehicle.IsFetchingDataFlag = dto.IsFetching;
        vehicle.FirstActivationAt = ParseDate(dto.FirstActivationAt);
        vehicle.LastDeactivationAt = ParseDate(dto.LastDeactivationAt);

        await db.SaveChangesAsync();
        return NoContent();
    }
}