using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PdfReportsController(PolarDriveDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PdfReportDTO>>> Get()
    {
        var reports = await db.PdfReports
            .Include(r => r.ClientCompany)
            .Include(r => r.ClientVehicle)
            .ToListAsync();

        var result = reports.Select(r => new PdfReportDTO
        {
            Id = r.Id,
            ReportPeriodStart = r.ReportPeriodStart.ToString("dd/MM/yyyy"),
            ReportPeriodEnd = r.ReportPeriodEnd.ToString("dd/MM/yyyy"),
            GeneratedAt = r.GeneratedAt?.ToString("dd/MM/yyyy"),
            CompanyVatNumber = r.ClientCompany?.VatNumber ?? "",
            CompanyName = r.ClientCompany?.Name ?? "",
            VehicleVin = r.ClientVehicle?.Vin ?? "",
            VehicleModel = r.ClientVehicle?.Model ?? "",
            Notes = r.Notes
        });

        return Ok(result);
    }

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> PatchNotes(int id, [FromBody] JsonElement body)
    {
        var entity = await db.PdfReports.FindAsync(id);
        if (entity == null)
            return NotFound("SERVER ERROR → NOT FOUND: PDF report not found!");

        if (!body.TryGetProperty("notes", out var notesProp))
            return BadRequest("SERVER ERROR → BAD REQUEST: Notes field missing!");

        entity.Notes = notesProp.GetString() ?? string.Empty;
        await db.SaveChangesAsync();

        return NoContent();
    }


}