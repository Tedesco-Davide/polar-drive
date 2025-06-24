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
            RequestedBy = request.RequestedBy
        };

        db.AdminFileManager.Add(job);
        await db.SaveChangesAsync();

        logger.LogInformation($"✅ Job {job.Id} creato, avvio processamento...");

        // ✅ MIGLIORE: Usa un servizio background invece di Task.Run
        // Per ora aggiungiamo logging dettagliato
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessPdfDownloadAsync(job.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"❌ ERRORE CRITICO nel Task.Run per job {job.Id}");

                // Assicurati che il job venga marcato come FAILED anche in caso di errore del Task
                try
                {
                    using var scope = HttpContext.RequestServices.CreateScope();
                    var scopedDb = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();
                    var failedJob = await scopedDb.AdminFileManager.FindAsync(job.Id);
                    if (failedJob != null)
                    {
                        failedJob.Status = "FAILED";
                        failedJob.CompletedAt = DateTime.Now;
                        failedJob.InfoMessage = $"Errore critico: {ex.Message}";
                        await scopedDb.SaveChangesAsync();
                    }
                }
                catch (Exception dbEx)
                {
                    logger.LogError(dbEx, $"❌ Impossibile aggiornare stato FAILED per job {job.Id}");
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
        var job = await db.AdminFileManager.FindAsync(id);
        if (job == null)
            return NotFound();

        job.InfoMessage = request.InfoMessage;
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
        logger.LogInformation($"🔄 INIZIO ProcessPdfDownloadAsync per job {jobId}");

        // ✅ Usa un nuovo scope per il DB context
        using var scope = HttpContext.RequestServices.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

        var job = await scopedDb.AdminFileManager.FindAsync(jobId);
        if (job == null)
        {
            logger.LogError($"❌ Job {jobId} non trovato nel database");
            return;
        }

        try
        {
            logger.LogInformation($"📝 Aggiorno stato a PROCESSING per job {jobId}");
            job.Status = "PROCESSING";
            job.StartedAt = DateTime.Now;
            await scopedDb.SaveChangesAsync();

            // ✅ Verifica e crea directory
            logger.LogInformation($"📁 Directory ZIP: {_zipStoragePath}");
            if (!Directory.Exists(_zipStoragePath))
            {
                logger.LogInformation($"📁 Creazione directory: {_zipStoragePath}");
                Directory.CreateDirectory(_zipStoragePath);
            }
            else
            {
                logger.LogInformation($"📁 Directory già esistente: {_zipStoragePath}");
            }

            // ✅ Debug: Verifica filtri
            logger.LogInformation($"🔍 Filtri job {jobId}:");
            logger.LogInformation($"   - Periodo: {job.PeriodStart:yyyy-MM-dd} → {job.PeriodEnd:yyyy-MM-dd}");
            logger.LogInformation($"   - Companies: [{string.Join(", ", job.CompanyList)}]");
            logger.LogInformation($"   - VINs: [{string.Join(", ", job.VinList)}]");
            logger.LogInformation($"   - Brands: [{string.Join(", ", job.BrandList)}]");

            // ✅ DEBUG: Verifica TUTTI i report nel database
            var allReports = await scopedDb.PdfReports
                .Include(p => p.ClientCompany)
                .Include(p => p.ClientVehicle)
                .ToListAsync();

            logger.LogInformation($"🔍 DEBUG: Totale report nel database: {allReports.Count}");

            foreach (var r in allReports.Take(5)) // Mostra i primi 5
            {
                logger.LogInformation($"📄 Report {r.Id}: " +
                    $"GeneratedAt={r.GeneratedAt:yyyy-MM-dd HH:mm}, " +
                    $"PeriodStart={r.ReportPeriodStart:yyyy-MM-dd HH:mm}, " +
                    $"PeriodEnd={r.ReportPeriodEnd:yyyy-MM-dd HH:mm}");
            }

            // ✅ DEBUG: Verifica il periodo richiesto
            logger.LogInformation($"🎯 Periodo richiesto: {job.PeriodStart:yyyy-MM-dd HH:mm} → {job.PeriodEnd:yyyy-MM-dd HH:mm}");

            // ✅ DEBUG: Test tutte le strategie
            var byGenerated = await scopedDb.PdfReports
                .Where(p => p.GeneratedAt >= job.PeriodStart && p.GeneratedAt <= job.PeriodEnd)
                .CountAsync();
            logger.LogInformation($"📊 Report trovati per GeneratedAt: {byGenerated}");

            var byPeriodStart = await scopedDb.PdfReports
                .Where(p => p.ReportPeriodStart >= job.PeriodStart && p.ReportPeriodStart <= job.PeriodEnd)
                .CountAsync();
            logger.LogInformation($"📊 Report trovati per ReportPeriodStart: {byPeriodStart}");

            var byOverlap = await scopedDb.PdfReports
                .Where(p => p.ReportPeriodStart <= job.PeriodEnd && p.ReportPeriodEnd >= job.PeriodStart)
                .CountAsync();
            logger.LogInformation($"📊 Report trovati per Overlap: {byOverlap}");

            // ✅ FIX: Se la data di fine è "mezzanotte", estendila a fine giornata
            var adjustedPeriodEnd = job.PeriodEnd;
            if (job.PeriodEnd.TimeOfDay == TimeSpan.Zero) // Se è 00:00:00
            {
                adjustedPeriodEnd = job.PeriodEnd.AddDays(1).AddSeconds(-1); // Fino a 23:59:59
                logger.LogInformation($"🕐 Date adjusted: End extended from {job.PeriodEnd:yyyy-MM-dd HH:mm:ss} to {adjustedPeriodEnd:yyyy-MM-dd HH:mm:ss}");
            }

            // ✅ Usa la data aggiustata nella conversione UTC
            var jobPeriodStartUtc = job.PeriodStart.Kind == DateTimeKind.Utc
                ? job.PeriodStart
                : job.PeriodStart.ToUniversalTime();

            var jobPeriodEndUtc = adjustedPeriodEnd.Kind == DateTimeKind.Utc
                ? adjustedPeriodEnd
                : adjustedPeriodEnd.ToUniversalTime();

            logger.LogInformation($"🕐 Final UTC range: {jobPeriodStartUtc:yyyy-MM-dd HH:mm:ss} → {jobPeriodEndUtc:yyyy-MM-dd HH:mm:ss}");

            // ✅ Query con date UTC
            var pdfQuery = scopedDb.PdfReports
                .Include(p => p.ClientCompany)
                .Include(p => p.ClientVehicle)
                .Where(p =>
                    p.ReportPeriodStart <= jobPeriodEndUtc &&
                    p.ReportPeriodEnd >= jobPeriodStartUtc
                );

            // ✅ Debug: Conta PDF prima dei filtri
            var totalInPeriod = await pdfQuery.CountAsync();
            logger.LogInformation($"📊 PDF totali nel periodo: {totalInPeriod}");

            // Applica filtri se specificati
            if (job.CompanyList.Any())
            {
                pdfQuery = pdfQuery.Where(p => job.CompanyList.Contains(p.ClientCompany!.Name));
                var afterCompanyFilter = await pdfQuery.CountAsync();
                logger.LogInformation($"📊 PDF dopo filtro aziende: {afterCompanyFilter}");
            }

            if (job.VinList.Any())
            {
                pdfQuery = pdfQuery.Where(p => job.VinList.Contains(p.ClientVehicle!.Vin));
                var afterVinFilter = await pdfQuery.CountAsync();
                logger.LogInformation($"📊 PDF dopo filtro VIN: {afterVinFilter}");
            }

            if (job.BrandList.Any())
            {
                pdfQuery = pdfQuery.Where(p => job.BrandList.Contains(p.ClientVehicle!.Brand));
                var afterBrandFilter = await pdfQuery.CountAsync();
                logger.LogInformation($"📊 PDF dopo filtro marchi: {afterBrandFilter}");
            }

            var pdfReports = await pdfQuery.ToListAsync();
            job.TotalPdfCount = pdfReports.Count;

            logger.LogInformation($"📊 PDF finali trovati: {pdfReports.Count}");

            if (pdfReports.Count == 0)
            {
                logger.LogInformation($"⚠️ Nessun PDF trovato, completamento job {jobId}");
                job.Status = "COMPLETED";
                job.CompletedAt = DateTime.Now;
                job.InfoMessage = "Nessun PDF trovato per i criteri specificati";
                await scopedDb.SaveChangesAsync();
                return;
            }

            // Crea il file ZIP
            var zipFileName = $"pdf_reports_{jobId}_{DateTime.Now:yyyyMMddHHmmss}.zip";
            var zipFilePath = Path.Combine(_zipStoragePath, zipFileName);

            logger.LogInformation($"📦 Creazione ZIP: {zipFilePath}");

            using (var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                var includedCount = 0;
                var missingFiles = 0;

                foreach (var pdf in pdfReports)
                {
                    var pdfFilePath = GetReportPdfPath(pdf);
                    logger.LogDebug($"🔍 Controllo PDF: {pdfFilePath}");

                    if (System.IO.File.Exists(pdfFilePath))
                    {
                        try
                        {
                            var companyName = SanitizeFileName(pdf.ClientCompany!.Name);
                            var vin = SanitizeFileName(pdf.ClientVehicle!.Vin);
                            var entryName = $"{companyName}_{vin}_{pdf.GeneratedAt:yyyyMMdd}_{pdf.Id}.pdf";

                            zipArchive.CreateEntryFromFile(pdfFilePath, entryName);
                            includedCount++;

                            if (includedCount % 10 == 0)
                            {
                                logger.LogInformation($"📦 Aggiunti {includedCount} PDF al ZIP...");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning($"⚠️ Errore aggiunta PDF {pdf.Id} al ZIP: {ex.Message}");
                        }
                    }
                    else
                    {
                        logger.LogWarning($"❌ PDF non trovato: {pdfFilePath} per report ID {pdf.Id}");
                        missingFiles++;
                    }
                }

                job.IncludedPdfCount = includedCount;
                logger.LogInformation($"📦 ZIP completato: {includedCount} PDF inclusi, {missingFiles} file mancanti");
            }

            // Calcola la dimensione del file ZIP
            var zipFileInfo = new FileInfo(zipFilePath);
            job.ZipFileSizeMB = Math.Round((decimal)zipFileInfo.Length / (1024 * 1024), 2);
            job.ResultZipPath = zipFilePath;

            job.Status = "COMPLETED";
            job.CompletedAt = DateTime.Now;

            await scopedDb.SaveChangesAsync();

            logger.LogInformation($"✅ Job {jobId} completato con successo:");
            logger.LogInformation($"   - PDF inclusi: {job.IncludedPdfCount}/{job.TotalPdfCount}");
            logger.LogInformation($"   - Dimensione ZIP: {job.ZipFileSizeMB} MB");
            logger.LogInformation($"   - Path ZIP: {job.ResultZipPath}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"❌ ERRORE nel processamento del job {jobId}");

            job.Status = "FAILED";
            job.CompletedAt = DateTime.Now;
            job.InfoMessage = $"Errore: {ex.Message}";
            await scopedDb.SaveChangesAsync();
        }
    }

    // ✅ Metodo per ottenere il path del PDF
    private static string GetReportPdfPath(PdfReport report)
    {
        var generationDate = report.GeneratedAt ?? DateTime.UtcNow;

        // ✅ Usa path assoluto
        var basePath = Directory.GetCurrentDirectory();

        return Path.Combine(basePath, "storage", "reports",
            generationDate.Year.ToString(),
            generationDate.Month.ToString("D2"),
            $"PolarDrive_Report_{report.Id}.pdf");
    }

    // ✅ Test endpoint per verificare path
    [HttpGet("debug/paths")]
    public IActionResult GetDebugPaths()
    {
        return Ok(new
        {
            CurrentDirectory = Directory.GetCurrentDirectory(),
            ZipStoragePath = _zipStoragePath,
            ZipDirectoryExists = Directory.Exists(_zipStoragePath),
            ReportsPath = Path.Combine(Directory.GetCurrentDirectory(), "storage", "reports"),
            ReportsDirectoryExists = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "storage", "reports"))
        });
    }

    // ✅ Test endpoint per verificare un singolo PDF
    [HttpGet("debug/test-pdf/{reportId}")]
    public async Task<IActionResult> TestPdfPath(int reportId)
    {
        var report = await db.PdfReports
            .Include(p => p.ClientCompany)
            .Include(p => p.ClientVehicle)
            .FirstOrDefaultAsync(p => p.Id == reportId);

        if (report == null)
            return NotFound($"Report {reportId} non trovato");

        var pdfPath = GetReportPdfPath(report);

        return Ok(new
        {
            ReportId = reportId,
            CalculatedPath = pdfPath,
            FileExists = System.IO.File.Exists(pdfPath),
            Company = report.ClientCompany?.Name,
            Vin = report.ClientVehicle?.Vin,
            GeneratedAt = report.GeneratedAt
        });
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

    // Aggiungi questo endpoint nel FileManagerController per debug diretto

    [HttpGet("debug/direct-test")]
    public async Task<IActionResult> DirectDebugTest(DateTime? start = null, DateTime? end = null)
    {
        start ??= new DateTime(2025, 6, 1);
        end ??= new DateTime(2025, 6, 24);

        // Debug tutti i report
        var allReports = await db.PdfReports
            .Include(p => p.ClientCompany)
            .Include(p => p.ClientVehicle)
            .ToListAsync();

        var reportDetails = allReports.Take(5).Select(r => new
        {
            r.Id,
            GeneratedAt = r.GeneratedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
            ReportPeriodStart = r.ReportPeriodStart.ToString("yyyy-MM-dd HH:mm:ss"),
            ReportPeriodEnd = r.ReportPeriodEnd.ToString("yyyy-MM-dd HH:mm:ss"),
            Company = r.ClientCompany?.Name,
            VIN = r.ClientVehicle?.Vin
        }).ToList();

        // Test diverse strategie
        var byGenerated = await db.PdfReports
            .Where(p => p.GeneratedAt >= start && p.GeneratedAt <= end)
            .CountAsync();

        var byPeriodStart = await db.PdfReports
            .Where(p => p.ReportPeriodStart >= start && p.ReportPeriodStart <= end)
            .CountAsync();

        var byOverlap = await db.PdfReports
            .Where(p => p.ReportPeriodStart <= end && p.ReportPeriodEnd >= start)
            .CountAsync();

        // Test timezone conversion
        var startUtc = start.Value.ToUniversalTime();
        var endUtc = end.Value.ToUniversalTime();

        var byOverlapUtc = await db.PdfReports
            .Where(p => p.ReportPeriodStart <= endUtc && p.ReportPeriodEnd >= startUtc)
            .CountAsync();

        return Ok(new
        {
            RequestedPeriod = new
            {
                Start = start?.ToString("yyyy-MM-dd HH:mm:ss"),
                End = end?.ToString("yyyy-MM-dd HH:mm:ss"),
                StartUtc = startUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                EndUtc = endUtc.ToString("yyyy-MM-dd HH:mm:ss")
            },
            TotalReports = allReports.Count,
            SampleReports = reportDetails,
            TestResults = new
            {
                ByGeneratedAt = byGenerated,
                ByReportPeriodStart = byPeriodStart,
                ByOverlap = byOverlap,
                ByOverlapUtc = byOverlapUtc
            }
        });
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