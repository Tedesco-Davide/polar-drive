using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.Helpers;
using System.IO.Compression;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileManagerController(PolarDriveDbContext db, PolarDriveLogger logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<AdminFileManager>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        try
        {
            var query = db.AdminFileManager.AsQueryable();

            // Filtro ricerca
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(j =>
                    j.Status.Contains(search) ||
                    (j.RequestedBy != null && j.RequestedBy.Contains(search)) ||
                    j.CompanyList.Any(c => c.Contains(search)) ||
                    j.VinList.Any(v => v.Contains(search)) ||
                    j.BrandList.Any(b => b.Contains(search)));
            }

            var totalCount = await query.CountAsync();

            // ✅ FIX: Projection per ESCLUDERE ZipContent dal caricamento
            // Questo previene il timeout HTTP 500 quando ci sono ZIP da 1GB+ nel database
            var items = await query
                .OrderByDescending(j => j.RequestedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new AdminFileManager
                {
                    Id = j.Id,
                    RequestedAt = j.RequestedAt,
                    PeriodStart = j.PeriodStart,
                    PeriodEnd = j.PeriodEnd,
                    CompanyList = j.CompanyList,
                    VinList = j.VinList,
                    BrandList = j.BrandList,
                    Status = j.Status,
                    RequestedBy = j.RequestedBy,
                    StartedAt = j.StartedAt,
                    CompletedAt = j.CompletedAt,
                    TotalPdfCount = j.TotalPdfCount,
                    IncludedPdfCount = j.IncludedPdfCount,
                    ZipFileSizeMB = j.ZipFileSizeMB,
                    ZipHash = j.ZipHash,
                    Notes = j.Notes,
                })
                .ToListAsync();

            return Ok(new PaginatedResponse<AdminFileManager>
            {
                Data = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _ = logger.Error(ex.ToString(), "Error retrieving file manager jobs");
            return StatusCode(500, new { error = "Errore interno server", details = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AdminFileManager>> GetById(int id)
    {
        var job = await db.AdminFileManager.FindAsync(id);
        if (job == null)
            return NotFound();

        return job;
    }

    [HttpPost("filemanager-download")]
    public async Task<ActionResult<AdminFileManager>> AdminFileManagerRequest(AdminFileManagerRequest request)
    {
        var job = new AdminFileManager
        {
            RequestedAt = DateTime.Now,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            CompanyList = request.Companies ?? [],
            VinList = request.Vins ?? [],
            BrandList = request.Brands ?? [],
            Status = "PENDING",
            RequestedBy = request.RequestedBy,
            Notes = null
        };

        db.AdminFileManager.Add(job);
        await db.SaveChangesAsync();

        _ = logger.Info("FileManagerController.AdminFileManagerRequest", $"Job {job.Id} queued");

        return CreatedAtAction(nameof(GetById), new { id = job.Id }, job);
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadZip(int id)
    {
        var job = await db.AdminFileManager.FindAsync(id);
        if (job == null)
            return NotFound();

        if (!job.IsCompleted || !job.HasZipFile)
            return BadRequest("Il file ZIP non è ancora pronto per il download");

        await db.SaveChangesAsync();

        var fileName = $"PDF_Reports_{job.PeriodStart:yyyyMMdd}_{job.PeriodEnd:yyyyMMdd}_{job.Id}.zip";

        return File(job.ZipContent!, "application/zip", fileName);
    }

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> UpdateNotes(int id, [FromBody] UpdateNotesRequest request)
    {
        var job = await db.AdminFileManager.FindAsync(id);
        if (job == null)
            return NotFound();

        job.Notes = request.Notes;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteJob(int id)
    {
        var job = await db.AdminFileManager.FindAsync(id);
        if (job == null)
            return NotFound();

        db.AdminFileManager.Remove(job);
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("available-companies")]
    public async Task<ActionResult<IEnumerable<string>>> GetAvailableCompanies()
    {
        var companies = await db.PdfReports
            .Include(p => p.ClientCompany)
            .Select(p => p.ClientCompany!.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        return Ok(companies);
    }

    [HttpGet("available-brands")]
    public async Task<ActionResult<IEnumerable<string>>> GetAvailableBrands()
    {
        var brands = await db.PdfReports
            .Include(p => p.ClientVehicle)
            .Where(p => !string.IsNullOrEmpty(p.ClientVehicle!.Brand))
            .Select(p => p.ClientVehicle!.Brand)
            .Distinct()
            .OrderBy(brand => brand)
            .ToListAsync();

        return Ok(brands);
    }

    [HttpGet("available-vins")]
    public async Task<ActionResult<IEnumerable<string>>> GetAvailableVins(string? company = null)
    {
        var vinsQuery = db.PdfReports
            .Include(p => p.ClientCompany)
            .Include(p => p.ClientVehicle)
            .AsQueryable();

        if (!string.IsNullOrEmpty(company))
        {
            vinsQuery = vinsQuery.Where(p => p.ClientCompany!.Name == company);
        }

        var vins = await vinsQuery
            .Select(p => p.ClientVehicle!.Vin)
            .Distinct()
            .OrderBy(vin => vin)
            .ToListAsync();

        return Ok(vins);
    }

    private async Task ProcessPdfDownloadAsync(int jobId)
    {
        using var scope = HttpContext.RequestServices.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

        var job = await scopedDb.AdminFileManager.FindAsync(jobId);
        if (job == null)
        {
            _ = logger.Error("Job {JobId} not found in database", jobId.ToString());
            return;
        }

        try
        {
            job.Status = "PROCESSING";
            job.StartedAt = DateTime.Now;
            await scopedDb.SaveChangesAsync();

            var adjustedPeriodEnd = job.PeriodEnd;
            if (job.PeriodEnd.TimeOfDay == TimeSpan.Zero)
            {
                adjustedPeriodEnd = job.PeriodEnd.AddDays(1).AddSeconds(-1);
            }

            var pdfQuery = scopedDb.PdfReports
                .AsNoTracking()
                .Include(p => p.ClientCompany)
                .Include(p => p.ClientVehicle)
                .Where(p =>
                    p.ReportPeriodEnd >= job.PeriodStart &&
                    p.ReportPeriodStart <= adjustedPeriodEnd
                );

            if (job.CompanyList.Any())
            {
                pdfQuery = pdfQuery.Where(p => job.CompanyList.Contains(p.ClientCompany!.Name));
            }

            if (job.VinList.Any())
            {
                pdfQuery = pdfQuery.Where(p => job.VinList.Contains(p.ClientVehicle!.Vin));
            }

            if (job.BrandList.Any())
            {
                pdfQuery = pdfQuery.Where(p => job.BrandList.Contains(p.ClientVehicle!.Brand));
            }

            var pdfReports = await pdfQuery.ToListAsync();
            job.TotalPdfCount = pdfReports.Count;

            if (pdfReports.Count == 0)
            {
                job.Status = "COMPLETED";
                job.CompletedAt = DateTime.Now;
                job.Notes = "Nessun PDF trovato per i criteri specificati";
                job.IncludedPdfCount = 0;
                job.ZipFileSizeMB = 0;
                await scopedDb.SaveChangesAsync();

                _ = logger.Info("Job {JobId} completed with no PDFs found for specified criteria", jobId.ToString());
                return;
            }

            using var zipStream = new MemoryStream();
            using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var includedCount = 0;

                foreach (var pdf in pdfReports)
                {
                    if (pdf.PdfContent == null || pdf.PdfContent.Length == 0)
                    {
                        _ = logger.Warning("PDF content not available in DB for PDF {PdfId}", pdf.Id.ToString());
                        continue;
                    }

                    try
                    {
                        var companyName = SanitizeFileName(pdf.ClientCompany!.Name);
                        var vin = SanitizeFileName(pdf.ClientVehicle!.Vin);
                        var entryName = $"{companyName}_{vin}_{pdf.GeneratedAt:yyyyMMdd}_{pdf.Id}.pdf";

                        var entry = zipArchive.CreateEntry(entryName, CompressionLevel.Optimal);
                        using var entryStream = entry.Open();
                        await entryStream.WriteAsync(pdf.PdfContent, 0, pdf.PdfContent.Length);

                        includedCount++;
                    }
                    catch (Exception ex)
                    {
                        _ = logger.Warning(ex.ToString(), "Failed to add PDF {PdfId} to ZIP archive", pdf.Id.ToString());
                    }
                }

                job.IncludedPdfCount = includedCount;
            }

            var zipBytes = zipStream.ToArray();
            job.ZipFileSizeMB = Math.Round((decimal)zipBytes.Length / (1024 * 1024), 2);
            job.ZipHash = GenericHelpers.ComputeContentHash(zipBytes);

            // ✅ Salva metadati prima del contenuto
            job.Status = "UPLOADING";
            await scopedDb.SaveChangesAsync();

            // ✅ Poi salva il contenuto in una transazione separata
            job.ZipContent = zipBytes;
            job.Status = "COMPLETED";
            job.CompletedAt = DateTime.Now;

            await scopedDb.SaveChangesAsync();

            _ = logger.Info(
                "FileManagerDownloadService.ProcessJob",
                $"Job {jobId} completed successfully. Included {job.IncludedPdfCount}/{job.TotalPdfCount} PDFs, ZIP size: {job.ZipFileSizeMB}MB"
            );
        }
        catch (Exception ex)
        {
            _ = logger.Error(ex.ToString(), "Critical error processing job {JobId}", jobId.ToString());

            try
            {
                job.Status = "FAILED";
                job.CompletedAt = DateTime.Now;
                job.Notes = $"Errore: {ex.Message}";
                await scopedDb.SaveChangesAsync();
            }
            catch (Exception saveEx)
            {
                _ = logger.Error(saveEx.ToString(), "Failed to save FAILED status for job {JobId}", jobId.ToString());
            }
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "Unknown";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        return string.IsNullOrEmpty(sanitized) ? "Unknown" : sanitized;
    }
}

public record AdminFileManagerRequest(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    List<string>? Companies = null,
    List<string>? Vins = null,
    List<string>? Brands = null,
    string? RequestedBy = null
);

public record UpdateNotesRequest(string Notes);