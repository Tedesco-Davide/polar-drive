using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.Helpers;
using System.IO.Compression;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Services;

public class FileManagerBackgroundService(IServiceProvider serviceProvider, PolarDriveLogger logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly PolarDriveLogger _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextJobAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _ = _logger.Error(ex.ToString(), "Error in FileManager background service");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task ProcessNextJobAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

        var processingJob = await db.AdminFileManager
            .FirstOrDefaultAsync(j => j.Status == ReportStatus.PROCESSING, stoppingToken);

        if (processingJob != null)
        {
            _ = _logger.Debug("FileManagerBackgroundService", $"Job {processingJob.Id} already processing, waiting");
            return;
        }

        var nextJob = await db.AdminFileManager
            .Where(j => j.Status == ReportStatus.PENDING)
            .OrderBy(j => j.RequestedAt)
            .FirstOrDefaultAsync(stoppingToken);

        if (nextJob == null) return;

        await ProcessJobAsync(db, nextJob, stoppingToken);
    }

    private async Task ProcessJobAsync(PolarDriveDbContext db, AdminFileManager job, CancellationToken stoppingToken)
    {
        await db.Entry(job).ReloadAsync(stoppingToken);

        try
        {
            job.Status = ReportStatus.PROCESSING;
            job.StartedAt = DateTime.Now;
            await db.SaveChangesAsync(stoppingToken);

            _ = _logger.Info("FileManagerBackgroundService", $"Starting job {job.Id}");

            var adjustedPeriodEnd = job.PeriodEnd.TimeOfDay == TimeSpan.Zero
                ? job.PeriodEnd.AddDays(1).AddSeconds(-1)
                : job.PeriodEnd;

            // 🔍 DEBUG: Log dei periodi
            _ = _logger.Info("FileManagerBackgroundService",
                $"Job {job.Id} - PeriodStart: {job.PeriodStart:yyyy-MM-dd HH:mm:ss}, PeriodEnd: {adjustedPeriodEnd:yyyy-MM-dd HH:mm:ss}");

            // ✅ QUERY BASE (senza filtri) - USA VAR
            var baseQuery = db.PdfReports
                .Include(p => p.ClientCompany)
                .Include(p => p.ClientVehicle);

            // 🔍 DEBUG: Conta totale PDF
            var totalPdfs = await baseQuery.CountAsync(stoppingToken);
            _ = _logger.Info("FileManagerBackgroundService", $"Total PDFs in database: {totalPdfs}");

            // ✅ FILTRO COMPANY (con case-insensitive) - USA VAR
            var pdfQuery = baseQuery.AsQueryable();

            if (job.CompanyList.Any())
            {
                var companyNames = job.CompanyList.Select(c => c.ToUpper()).ToList();
                pdfQuery = pdfQuery.Where(p => companyNames.Contains(p.ClientCompany!.Name.ToUpper()));

                var countAfterCompany = await pdfQuery.CountAsync(stoppingToken);
                _ = _logger.Info("FileManagerBackgroundService",
                    $"Job {job.Id} - After company filter: {countAfterCompany} PDFs (filters: {string.Join(", ", job.CompanyList)})");
            }

            // ✅ FILTRO BRAND
            if (job.BrandList.Any())
            {
                var brandNames = job.BrandList.Select(b => b.ToUpper()).ToList();
                pdfQuery = pdfQuery.Where(p => brandNames.Contains(p.ClientVehicle!.Brand.ToUpper()));

                var countAfterBrand = await pdfQuery.CountAsync(stoppingToken);
                _ = _logger.Info("FileManagerBackgroundService",
                    $"Job {job.Id} - After brand filter: {countAfterBrand} PDFs");
            }

            // ✅ FILTRO VIN
            if (job.VinList.Any())
            {
                pdfQuery = pdfQuery.Where(p => job.VinList.Contains(p.ClientVehicle!.Vin));

                var countAfterVin = await pdfQuery.CountAsync(stoppingToken);
                _ = _logger.Info("FileManagerBackgroundService",
                    $"Job {job.Id} - After VIN filter: {countAfterVin} PDFs");
            }

            // ✅ FILTRO PERIODO (LOGICA CORRETTA DI OVERLAP)
            pdfQuery = pdfQuery.Where(p =>
                p.ReportPeriodStart <= adjustedPeriodEnd &&
                p.ReportPeriodEnd >= job.PeriodStart
            );

            var countAfterPeriod = await pdfQuery.CountAsync(stoppingToken);
            _ = _logger.Info("FileManagerBackgroundService",
                $"Job {job.Id} - After period filter: {countAfterPeriod} PDFs");

            // 🔍 DEBUG: Mostra alcuni esempi di PDF trovati
            var samplePdfs = await pdfQuery
                .Take(5)
                .Select(p => new
                {
                    p.Id,
                    Company = p.ClientCompany!.Name,
                    Vin = p.ClientVehicle!.Vin,
                    p.ReportPeriodStart,
                    p.ReportPeriodEnd
                })
                .ToListAsync(stoppingToken);

            if (samplePdfs.Any())
            {
                _ = _logger.Info("FileManagerBackgroundService",
                    $"Sample PDFs found: {string.Join(", ", samplePdfs.Select(p => $"ID={p.Id} Company={p.Company} VIN={p.Vin} Period={p.ReportPeriodStart:yyyy-MM-dd}→{p.ReportPeriodEnd:yyyy-MM-dd}"))}");
            }

            // ✅ CARICAMENTO IN BATCH per evitare SQL timeout
            _ = _logger.Info("FileManagerBackgroundService",
                $"Job {job.Id} - Loading PDF IDs...");

            // Prima carica SOLO gli ID (velocissimo)
            var pdfIds = await pdfQuery
                .Select(p => p.Id)
                .ToListAsync(stoppingToken);

            _ = _logger.Info("FileManagerBackgroundService",
                $"Job {job.Id} - Found {pdfIds.Count} PDF IDs, loading content in batches...");

            job.TotalPdfCount = pdfIds.Count;

            if (pdfIds.Count == 0)
            {
                job.Status = ReportStatus.COMPLETED;
                job.CompletedAt = DateTime.Now;
                job.Notes = "Nessun PDF trovato";
                job.IncludedPdfCount = 0;
                job.ZipFileSizeMB = 0;
                await db.SaveChangesAsync(stoppingToken);

                _ = _logger.Warning("FileManagerBackgroundService",
                    $"Job {job.Id} completed with 0 PDFs - Check filters!");
                return;
            }

            using var zipStream = new MemoryStream();
            using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var includedCount = 0;
                const int batchSize = 50;
                var totalBatches = (int)Math.Ceiling(pdfIds.Count / (double)batchSize);

                for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
                {
                    var batchIds = pdfIds.Skip(batchIndex * batchSize).Take(batchSize).ToList();

                    _ = _logger.Info("FileManagerBackgroundService",
                        $"Job {job.Id} - Loading batch {batchIndex + 1}/{totalBatches} ({batchIds.Count} PDFs)...");

                    // Carica batch con PdfContent
                    var batchPdfs = await db.PdfReports
                        .AsNoTracking()
                        .Include(p => p.ClientCompany)
                        .Include(p => p.ClientVehicle)
                        .Where(p => batchIds.Contains(p.Id))
                        .Select(p => new
                        {
                            p.Id,
                            p.PdfContent,
                            p.GeneratedAt,
                            CompanyName = p.ClientCompany!.Name,
                            VehicleVin = p.ClientVehicle!.Vin
                        })
                        .ToListAsync(stoppingToken);

                    foreach (var pdf in batchPdfs)
                    {
                        if (pdf.PdfContent == null || pdf.PdfContent.Length == 0)
                        {
                            _ = _logger.Debug("FileManagerBackgroundService",
                                $"Skipping PDF {pdf.Id} - no content");
                            continue;
                        }

                        try
                        {
                            var companyName = SanitizeFileName(pdf.CompanyName);
                            var vin = SanitizeFileName(pdf.VehicleVin);
                            var entryName = $"{companyName}_{vin}_{pdf.GeneratedAt:yyyyMMdd}_{pdf.Id}.pdf";

                            var entry = zipArchive.CreateEntry(entryName, CompressionLevel.Optimal);
                            using var entryStream = entry.Open();
                            await entryStream.WriteAsync(pdf.PdfContent, 0, pdf.PdfContent.Length, stoppingToken);
                            includedCount++;
                        }
                        catch (Exception ex)
                        {
                            _ = _logger.Warning(ex.ToString(), $"Failed to add PDF {pdf.Id}");
                        }
                    }

                    _ = _logger.Info("FileManagerBackgroundService",
                        $"Job {job.Id} - Batch {batchIndex + 1}/{totalBatches} completed. Total PDFs in ZIP: {includedCount}");

                    // ✅ FORZA GARBAGE COLLECTION dopo ogni batch per liberare memoria
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                job.IncludedPdfCount = includedCount;

                _ = _logger.Info("FileManagerBackgroundService",
                    $"Job {job.Id} - All batches completed. Total PDFs in ZIP: {includedCount}/{pdfIds.Count}");
            }

            // ✅ Crea ZIP
            var zipBytes = zipStream.ToArray();
            job.ZipFileSizeMB = Math.Round((decimal)zipBytes.Length / (1024 * 1024), 2);
            job.ZipHash = GenericHelpers.ComputeContentHash(zipBytes);

            _ = _logger.Info("FileManagerBackgroundService",
                $"Job {job.Id} - ZIP created: {job.ZipFileSizeMB}MB, Hash: {job.ZipHash}, PDFs: {job.IncludedPdfCount}/{job.TotalPdfCount}");

            // ✅ SALVA METADATI (NO RELOAD!)
            job.Status = "UPLOADING";
            await db.SaveChangesAsync(stoppingToken);

            _ = _logger.Info("FileManagerBackgroundService",
                $"Job {job.Id} - Metadata saved. Now saving ZIP content ({job.ZipFileSizeMB}MB)...");

            // ✅ SALVA BLOB (NO RELOAD!)
            job.ZipContent = zipBytes;
            job.Status = ReportStatus.COMPLETED;
            job.CompletedAt = DateTime.Now;
            await db.AdminFileManager
            .Where(j => j.Id == job.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.ZipContent, zipBytes)
                .SetProperty(j => j.Status, ReportStatus.COMPLETED)
                .SetProperty(j => j.CompletedAt, DateTime.Now)
                .SetProperty(j => j.HasZipFile, true)
                .SetProperty(j => j.ZipFileSizeMB, job.ZipFileSizeMB)
                .SetProperty(j => j.ZipHash, job.ZipHash)
                .SetProperty(j => j.TotalPdfCount, job.TotalPdfCount)
                .SetProperty(j => j.IncludedPdfCount, job.IncludedPdfCount), stoppingToken);


            _ = _logger.Info("FileManagerBackgroundService",
                $"Job {job.Id} completed: {job.IncludedPdfCount}/{job.TotalPdfCount} PDFs, {job.ZipFileSizeMB}MB");
        }
        catch (Exception ex)
        {
            _ = _logger.Error(ex.ToString(), $"Error processing job {job.Id}");
            try { await db.Entry(job).ReloadAsync(stoppingToken); } catch { }
            job.Status = "FAILED";
            job.CompletedAt = DateTime.Now;
            job.Notes = $"Errore: {ex.Message}";
            await db.SaveChangesAsync(stoppingToken);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "Unknown";
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrEmpty(sanitized) ? "Unknown" : sanitized;
    }
}