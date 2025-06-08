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
    public async Task<ActionResult<IEnumerable<ClientCompanyDTO>>> Get()
    {
        await _logger.Info("ClientCompaniesController.Get", "Requested list of client companies.");

        var items = await db.ClientCompanies
            .Select(c => new ClientCompanyDTO
            {
                Id = c.Id,
                VatNumber = c.VatNumber,
                Name = c.Name,
                Address = c.Address,
                Email = c.Email,
                PecAddress = c.PecAddress,
                LandlineNumber = c.LandlineNumber,
                ReferentName = c.ReferentName,
                ReferentMobileNumber = c.ReferentMobileNumber,
                ReferentEmail = c.ReferentEmail,
                ReferentPecAddress = c.ReferentPecAddress
            }).ToListAsync();

        return Ok(items);
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
            ReferentName = dto.ReferentName,
            ReferentMobileNumber = dto.ReferentMobileNumber,
            ReferentEmail = dto.ReferentEmail,
            ReferentPecAddress = dto.ReferentPecAddress,
            CreatedAt = DateTime.UtcNow
        };

        db.ClientCompanies.Add(entity);
        await db.SaveChangesAsync();

        await _logger.Info("ClientCompaniesController.Post", "New client company inserted successfully.", $"CompanyId: {entity.Id}, VAT: {entity.VatNumber}");

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
            await _logger.Warning("ClientCompaniesController.Put", "Attempt to update a non-existing company.", $"CompanyId: {id}");
            return NotFound();
        }

        existing.Name = updated.Name;
        existing.VatNumber = updated.VatNumber;
        existing.Address = updated.Address;
        existing.Email = updated.Email;
        existing.PecAddress = updated.PecAddress;
        existing.ReferentName = updated.ReferentName;
        existing.ReferentEmail = updated.ReferentEmail;
        existing.ReferentMobileNumber = updated.ReferentMobileNumber;
        existing.ReferentPecAddress = updated.ReferentPecAddress;
        existing.LandlineNumber = updated.LandlineNumber;

        await db.SaveChangesAsync();

        await _logger.Info("ClientCompaniesController.Put", "Client company updated successfully.", $"CompanyId: {id}");

        return Ok();
    }
}