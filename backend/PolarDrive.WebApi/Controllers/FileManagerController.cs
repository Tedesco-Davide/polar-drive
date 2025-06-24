using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using System.IO.Compression;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileManagerController(PolarDriveDbContext db, ILogger<FileManagerController> logger) : ControllerBase
{
    private readonly string _zipStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "storage", "filemanager-zips");

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminFileManager>>> GetAll()
    {
        return await db.AdminFileManager
            .OrderByDescending(j => j.RequestedAt)
            .ToListAsync();
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

        // Avvia processamento in background
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessPdfDownloadAsync(job.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Errore critico nel processamento del job {job.Id}");

                try
                {
                    using var scope = HttpContext.RequestServices.CreateScope();
                    var scopedDb = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();
                    var failedJob = await scopedDb.AdminFileManager.FindAsync(job.Id);
                    if (failedJob != null)
                    {
                        failedJob.Status = "FAILED";
                        failedJob.CompletedAt = DateTime.Now;
                        failedJob.Notes = $"Errore critico: {ex.Message}";
                        await scopedDb.SaveChangesAsync();
                    }
                }
                catch (Exception dbEx)
                {
                    logger.LogError(dbEx, $"Impossibile aggiornare stato FAILED per job {job.Id}");
                }
            }
        });

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

        if (!System.IO.File.Exists(job.ResultZipPath))
            return NotFound("Il file ZIP non è più disponibile");

        await db.SaveChangesAsync();

        var zipBytes = await System.IO.File.ReadAllBytesAsync(job.ResultZipPath);
        var fileName = $"PDF_Reports_{job.PeriodStart:yyyyMMdd}_{job.PeriodEnd:yyyyMMdd}_{job.Id}.zip";

        return File(zipBytes, "application/zip", fileName);
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

        // Rimuovi il file ZIP se esiste
        if (job.HasZipFile && System.IO.File.Exists(job.ResultZipPath))
        {
            try
            {
                System.IO.File.Delete(job.ResultZipPath);
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Impossibile eliminare il file ZIP {job.ResultZipPath}: {ex.Message}");
            }
        }

        db.AdminFileManager.Remove(job);
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("available-companies")]
    public async Task<ActionResult<IEnumerable<string>>> GetAvailableCompanies()
    {
        var companies = await db.PdfReports
            .Include(p => p.ClientCompany)
            .Select(selector: p => p.ClientCompany!.Name)
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
            logger.LogError($"Job {jobId} non trovato nel database");
            return;
        }

        try
        {
            job.Status = "PROCESSING";
            job.StartedAt = DateTime.Now;
            await scopedDb.SaveChangesAsync();

            // Crea directory se non esiste
            if (!Directory.Exists(_zipStoragePath))
            {
                Directory.CreateDirectory(_zipStoragePath);
            }

            // Aggiusta la data di fine se è mezzanotte
            var adjustedPeriodEnd = job.PeriodEnd;
            if (job.PeriodEnd.TimeOfDay == TimeSpan.Zero)
            {
                adjustedPeriodEnd = job.PeriodEnd.AddDays(1).AddSeconds(-1);
            }

            // Converti in UTC
            var jobPeriodStartUtc = job.PeriodStart.Kind == DateTimeKind.Utc
                ? job.PeriodStart
                : job.PeriodStart.ToUniversalTime();

            var jobPeriodEndUtc = adjustedPeriodEnd.Kind == DateTimeKind.Utc
                ? adjustedPeriodEnd
                : adjustedPeriodEnd.ToUniversalTime();

            // Query per i PDF nel periodo specificato
            var pdfQuery = scopedDb.PdfReports
                .Include(p => p.ClientCompany)
                .Include(p => p.ClientVehicle)
                .Where(p =>
                    p.ReportPeriodStart <= jobPeriodEndUtc &&
                    p.ReportPeriodEnd >= jobPeriodStartUtc
                );

            // Applica filtri se specificati
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
                await scopedDb.SaveChangesAsync();
                return;
            }

            // Crea il file ZIP
            var zipFileName = $"pdf_reports_{jobId}_{DateTime.Now:yyyyMMddHHmmss}.zip";
            var zipFilePath = Path.Combine(_zipStoragePath, zipFileName);

            using (var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                var includedCount = 0;

                foreach (var pdf in pdfReports)
                {
                    var pdfFilePath = GetReportPdfPath(pdf);

                    if (System.IO.File.Exists(pdfFilePath))
                    {
                        try
                        {
                            var companyName = SanitizeFileName(pdf.ClientCompany!.Name);
                            var vin = SanitizeFileName(pdf.ClientVehicle!.Vin);
                            var entryName = $"{companyName}_{vin}_{pdf.GeneratedAt:yyyyMMdd}_{pdf.Id}.pdf";

                            zipArchive.CreateEntryFromFile(pdfFilePath, entryName);
                            includedCount++;
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning($"Errore aggiunta PDF {pdf.Id} al ZIP: {ex.Message}");
                        }
                    }
                }

                job.IncludedPdfCount = includedCount;
            }

            // Calcola la dimensione del file ZIP e completa il job
            var zipFileInfo = new FileInfo(zipFilePath);
            job.ZipFileSizeMB = Math.Round((decimal)zipFileInfo.Length / (1024 * 1024), 2);
            job.ResultZipPath = zipFilePath;
            job.Status = "COMPLETED";
            job.CompletedAt = DateTime.Now;

            await scopedDb.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Errore nel processamento del job {jobId}");

            job.Status = "FAILED";
            job.CompletedAt = DateTime.Now;
            job.Notes = $"Errore: {ex.Message}";
            await scopedDb.SaveChangesAsync();
        }
    }

    private static string GetReportPdfPath(PdfReport report)
    {
        var generationDate = report.GeneratedAt ?? DateTime.UtcNow;
        var basePath = Directory.GetCurrentDirectory();

        return Path.Combine(basePath, "storage", "reports",
            generationDate.Year.ToString(),
            generationDate.Month.ToString("D2"),
            $"PolarDrive_Report_{report.Id}.pdf");
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