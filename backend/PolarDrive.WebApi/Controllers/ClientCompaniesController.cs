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
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClientCompanyDTO>>> Get()
    {
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
            return Conflict("Azienda già presente con la stessa partita IVA.");

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

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity.Id);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await db.ClientCompanies.FindAsync(id);
        if (entity == null)
            return NotFound();

        db.ClientCompanies.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }
}