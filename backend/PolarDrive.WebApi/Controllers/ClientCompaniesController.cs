using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Entities;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientCompaniesController(PolarDriveDbContext db) : ControllerBase
{
    private readonly PolarDriveLogger _logger = new();

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<object>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? search = null,
        [FromQuery] string? searchType = "vat")
    {
        try
        {
            await _logger.Info("ClientCompaniesController.Get", "Requested filtered list of client companies",
                $"Page: {page}, PageSize: {pageSize}");

            var query = from company in db.ClientCompanies
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
                            DisplayVehicleMobileNumber = vehicle.VehicleMobileNumber ?? "—",
                            DisplayReferentEmail = vehicle.ReferentEmail ?? "—",
                            CorrespondingVehicleId = vehicle.Id,
                            CorrespondingVehicleVin = vehicle.Vin
                        };

            // Filtro ricerca
            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmed = search.Trim();
                var pattern = $"%{trimmed}%";

                if (searchType == SearchType.VAT)
                {
                    query = query.Where(c => EF.Functions.Like(c.VatNumber, pattern));
                }
                else if (searchType == SearchType.NAME)
                {
                    query = query.Where(c => EF.Functions.Like(c.Name, pattern));
                }
            }

            var totalCount = await query.CountAsync();

            var result = await query
                .OrderBy(c => c.Name)
                .ThenBy(c => c.VatNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            await _logger.Info("ClientCompaniesController.Get",
                $"Successfully returned {result.Count} company-vehicle combinations");

            return Ok(new PaginatedResponse<object>
            {
                Data = result.Cast<object>().ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientCompaniesController.Get", "Error retrieving companies with vehicles", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
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

    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetStats()
    {
        try
        {
            await _logger.Info("ClientCompaniesController.GetStats", "Requested dashboard stats");

            // Query aggregate SQL - singola query per tutte le statistiche
            var totalCompanies = await db.ClientCompanies.CountAsync();
            var totalVehicles = await db.ClientVehicles.CountAsync();
            var activeVehicles = await db.ClientVehicles.CountAsync(v => v.IsActiveFlag);
            var totalConsents = await db.ClientConsents.CountAsync();

            // Conteggio consensi per tipo con singola query GROUP BY
            var consentsByType = await db.ClientConsents
                .GroupBy(c => c.ConsentType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            var activationConsents = consentsByType.FirstOrDefault(c => c.Type == "Consent Activation")?.Count ?? 0;
            var deactivationConsents = consentsByType.FirstOrDefault(c => c.Type == "Consent Deactivation")?.Count ?? 0;
            var stopDataConsents = consentsByType.FirstOrDefault(c => c.Type == "Consent Stop Data Fetching")?.Count ?? 0;
            var reactivationConsents = consentsByType.FirstOrDefault(c => c.Type == "Consent Reactivation")?.Count ?? 0;

            var stats = new
            {
                totalCompanies,
                totalVehicles,
                activeVehicles,
                totalConsents,
                activationConsents,
                deactivationConsents,
                stopDataConsents,
                reactivationConsents
            };

            await _logger.Info("ClientCompaniesController.GetStats", "Stats returned successfully",
                $"Companies: {totalCompanies}, Vehicles: {totalVehicles}, Consents: {totalConsents}");

            return Ok(stats);
        }
        catch (Exception ex)
        {
            await _logger.Error("ClientCompaniesController.GetStats", "Error retrieving stats", ex.ToString());
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
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