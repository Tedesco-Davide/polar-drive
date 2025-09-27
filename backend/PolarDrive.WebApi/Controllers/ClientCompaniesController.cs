using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientCompaniesController(PolarDriveDbContext db) : ControllerBase
{
    private readonly PolarDriveLogger _logger = new(db);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> Get()
    {
        await _logger.Info("ClientCompaniesController.Get", "Requested list of client companies.");

        try
        {
            var result = await (from company in db.ClientCompanies
                                join vehicle in db.ClientVehicles on company.Id equals vehicle.ClientCompanyId
                                select new
                                {
                                    Id = company.Id,
                                    VatNumber = company.VatNumber,
                                    Name = company.Name,
                                    Address = company.Address ?? "",
                                    Email = company.Email ?? "",
                                    PecAddress = company.PecAddress ?? "",
                                    LandlineNumber = company.LandlineNumber ?? "",
                                    DisplayReferentName = vehicle.ReferentName ?? "—",
                                    DisplayReferentMobile = vehicle.ReferentMobileNumber ?? "—",
                                    DisplayReferentEmail = vehicle.ReferentEmail ?? "—",
                                    CorrespondingVehicleId = vehicle.Id,
                                    CorrespondingVehicleVin = vehicle.Vin
                                }).ToListAsync();

            await _logger.Info("ClientCompaniesController.Get", $"Successfully returned {result.Count} company-vehicle combinations");
            return Ok(result);
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientCompaniesController.Get", "Error retrieving companies with vehicles", ex.ToString());
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult> Post([FromBody] ClientCompanyDTO dto)
    {
        if (await db.ClientCompanies.AnyAsync(c => c.VatNumber == dto.VatNumber))
        {
            await _logger.Warning("ClientCompaniesController.Post", "Attempt to insert duplicate company.", $"VAT: {dto.VatNumber}");
            return Conflict("CONFLICT - SERVER ERROR: This company has already been saved, VAT number already existing!");
        }

        var entity = new ClientCompany
        {
            VatNumber = dto.VatNumber,
            Name = dto.Name,
            Address = dto.Address,
            Email = dto.Email,
            PecAddress = dto.PecAddress,
            LandlineNumber = dto.LandlineNumber,
            CreatedAt = DateTime.Now
        };

        db.ClientCompanies.Add(entity);
        await db.SaveChangesAsync();

        await _logger.Info("ClientCompaniesController.Post", "New client company inserted successfully.", $"ClientCompanyId: {entity.Id}, VAT: {entity.VatNumber}");

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity.Id);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] ClientCompanyDTO updated)
    {
        if (id != updated.Id)
        {
            await _logger.Error("ClientCompaniesController.Put", "ID mismatch in update request.", $"Route ID: {id}, Body ID: {updated.Id}");
            return BadRequest("SERVER ERROR → BAD REQUEST: ID mismatch!");
        }

        var existing = await db.ClientCompanies.FindAsync(id);
        if (existing == null)
        {
            await _logger.Warning("ClientCompaniesController.Put", "Attempt to update a non-existing company.", $"ClientCompanyId: {id}");
            return NotFound();
        }

        existing.Name = updated.Name;
        existing.VatNumber = updated.VatNumber;
        existing.Address = updated.Address;
        existing.Email = updated.Email;
        existing.PecAddress = updated.PecAddress;
        existing.LandlineNumber = updated.LandlineNumber;

        await db.SaveChangesAsync();

        await _logger.Info("ClientCompaniesController.Put", "Client company updated successfully.", $"ClientCompanyId: {id}");

        return Ok();
    }
}