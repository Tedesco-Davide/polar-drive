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
            FirstActivationAt = v.FirstActivationAt?.ToString("dd/MM/yyyy"),
            LastDeactivationAt = v.LastDeactivationAt?.ToString("dd/MM/yyyy"),
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
            return BadRequest("Un veicolo attivo deve anche essere in stato di acquisizione dati (IsFetching).");

        if (!await db.ClientCompanies.AnyAsync(c => c.Id == dto.ClientCompanyId))
            return NotFound("Azienda cliente non trovata.");

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
            LastDeactivationAt = ParseDate(dto.LastDeactivationAt)
        };

        db.ClientTeslaVehicles.Add(entity);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity.Id);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] TeslaVehicleStatusUpdateDTO dto)
    {
        if (dto.IsActive && !dto.IsFetching)
            return BadRequest("Un veicolo attivo deve anche essere in stato di acquisizione dati.");

        var vehicle = await db.ClientTeslaVehicles.FindAsync(id);
        if (vehicle == null)
            return NotFound("Veicolo non trovato.");

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
}