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
    private readonly string _zipStoragePath = Path.Combine("storage", "filemanager-zips");

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminFileManager>>> GetAll()
    {
        return await db.AdminFileManagers
            .OrderByDescending(j => j.RequestedAt)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AdminFileManager>> GetById(int id)
    {
        var job = await db.AdminFileManagers.FindAsync(id);
        if (job == null)
            return NotFound();

        return job;
    }

    [HttpPost("filemanager-download")]
    public async Task<ActionResult<AdminFileManager>> AdminFileManagerRequest(AdminFileManagerRequest request)
    {
        var job = new AdminFileManager
        {
            RequestedAt = DateTime.UtcNow,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            CompanyList = request.Companies ?? [],
            VinList = request.Vins ?? [],
            BrandList = request.Brands ?? [],
            Status = "PENDING",
            RequestedBy = request.RequestedBy,
            InfoMessage = $"PDF download richiesto per periodo {request.PeriodStart:yyyy-MM-dd} - {request.PeriodEnd:yyyy-MM-dd}"
        };

        db.AdminFileManagers.Add(job);
        await db.SaveChangesAsync();

        // Avvia il processo di generazione ZIP in background
        _ = Task.Run(() => ProcessPdfDownloadAsync(job.Id));

        return CreatedAtAction(nameof(GetById), new { id = job.Id }, job);
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadZip(int id)
    {
        var job = await db.AdminFileManagers.FindAsync(id);
        if (job == null)
            return NotFound();

        if (!job.IsCompleted || !job.HasZipFile)
            return BadRequest("Il file ZIP non è ancora pronto per il download");

        if (!System.IO.File.Exists(job.ResultZipPath))
            return NotFound("Il file ZIP non è più disponibile");

        // Incrementa il contatore di download
        job.DownloadCount++;
        await db.SaveChangesAsync();

        var zipBytes = await System.IO.File.ReadAllBytesAsync(job.ResultZipPath);
        var fileName = $"PDF_Reports_{job.PeriodStart:yyyyMMdd}_{job.PeriodEnd:yyyyMMdd}_{job.Id}.zip";

        return File(zipBytes, "application/zip", fileName);
    }

    [HttpPatch("{id}/notes")]
    public async Task<IActionResult> UpdateNotes(int id, [FromBody] UpdateNotesRequest request)
    {
        var job = await db.AdminFileManagers.FindAsync(id);
        if (job == null)
            return NotFound();

        job.InfoMessage = request.InfoMessage;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteJob(int id)
    {
        var job = await db.AdminFileManagers.FindAsync(id);
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

        db.AdminFileManagers.Remove(job);
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
        var job = await db.AdminFileManagers.FindAsync(jobId);
        if (job == null) return;

        try
        {
            job.Status = "PROCESSING";
            job.StartedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // ✅ Crea directory per i ZIP
            Directory.CreateDirectory(_zipStoragePath);

            // Cerca tutti i PDF nel periodo specificato - CON INCLUDE
            var pdfQuery = db.PdfReports
                .Include(p => p.ClientCompany)
                .Include(p => p.ClientVehicle)
                .Where(p => p.GeneratedAt >= job.PeriodStart && p.GeneratedAt <= job.PeriodEnd);

            // Applica filtri se specificati
            if (job.CompanyList.Any())
                pdfQuery = pdfQuery.Where(p => job.CompanyList.Contains(p.ClientCompany!.Name));

            if (job.VinList.Any())
                pdfQuery = pdfQuery.Where(p => job.VinList.Contains(p.ClientVehicle!.Vin));

            if (job.BrandList.Any())
                pdfQuery = pdfQuery.Where(p => job.BrandList.Contains(p.ClientVehicle!.Brand));

            var pdfReports = await pdfQuery.ToListAsync();
            job.TotalPdfCount = pdfReports.Count;

            if (pdfReports.Count == 0)
            {
                job.Status = "COMPLETED";
                job.CompletedAt = DateTime.UtcNow;
                job.InfoMessage = "Nessun PDF trovato nel periodo specificato con i filtri applicati";
                await db.SaveChangesAsync();
                return;
            }

            // Crea il file ZIP
            var zipFileName = $"pdf_reports_{jobId}_{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
            var zipFilePath = Path.Combine(_zipStoragePath, zipFileName);

            using (var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                var includedCount = 0;

                foreach (var pdf in pdfReports)
                {
                    // ✅ CORRETTO: Usa la logica del path PDF esistente
                    var pdfFilePath = GetReportPdfPath(pdf);

                    if (System.IO.File.Exists(pdfFilePath))
                    {
                        // Sanitize del nome file per evitare caratteri problematici
                        var companyName = SanitizeFileName(pdf.ClientCompany!.Name);
                        var vin = SanitizeFileName(pdf.ClientVehicle!.Vin);

                        var entryName = $"{companyName}_{vin}_{pdf.GeneratedAt:yyyyMMdd}_{pdf.Id}.pdf";
                        zipArchive.CreateEntryFromFile(pdfFilePath, entryName);
                        includedCount++;
                    }
                    else
                    {
                        logger.LogWarning($"PDF file not found: {pdfFilePath} for report ID {pdf.Id}");
                    }
                }

                job.IncludedPdfCount = includedCount;
            }

            // Calcola la dimensione del file ZIP
            var zipFileInfo = new FileInfo(zipFilePath);
            job.ZipFileSizeMB = Math.Round((decimal)zipFileInfo.Length / (1024 * 1024), 2);
            job.ResultZipPath = zipFilePath;

            job.Status = "COMPLETED";
            job.CompletedAt = DateTime.UtcNow;
            job.InfoMessage = $"ZIP generato con successo: {job.IncludedPdfCount} PDF inclusi su {job.TotalPdfCount} trovati";

            await db.SaveChangesAsync();

            logger.LogInformation($"PDF download job {jobId} completato: {job.IncludedPdfCount}/{job.TotalPdfCount} PDF inclusi in {job.ZipFileSizeMB} MB");
        }
        catch (Exception ex)
        {
            job.Status = "FAILED";
            job.CompletedAt = DateTime.UtcNow;
            job.InfoMessage = $"Errore durante la generazione del ZIP: {ex.Message}";
            await db.SaveChangesAsync();

            logger.LogError(ex, $"Errore nel processamento del job PDF download {jobId}");
        }
    }

    // ✅ Metodo per ottenere il path del PDF usando la stessa logica del sistema
    private static string GetReportPdfPath(PdfReport report)
    {
        var generationDate = report.GeneratedAt ?? DateTime.UtcNow;

        return Path.Combine("storage", "reports",
            generationDate.Year.ToString(),
            generationDate.Month.ToString("D2"),
            $"PolarDrive_Report_{report.Id}.pdf");
    }

    // ✅ Metodo helper per sanitizzare i nomi file
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

public record UpdateNotesRequest(string InfoMessage);