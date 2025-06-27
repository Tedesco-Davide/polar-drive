using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;

namespace PolarDrive.WebApi.Services;

public class FileCleanupService(IServiceProvider serviceProvider, ILogger<FileCleanupService> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<FileCleanupService> _logger = logger;
    private readonly string _fileManagerZipStoragePath = Path.Combine("storage", "filemanager-zips");
    private readonly string _outageZipStoragePath = Path.Combine("storage", "outages-zips");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldFileManagerFiles();
                await CleanupOldOutageFiles();

                // Esegui ogni 24 ore
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il cleanup dei file ZIP");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Cleanup dei file del File Manager (logica esistente)
    /// </summary>
    private async Task CleanupOldFileManagerFiles()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

        // Rimuovi job più vecchi di 30 giorni
        var cutoffDate = DateTime.UtcNow.AddDays(-30);

        var oldJobs = await db.AdminFileManager
            .Where(j => j.RequestedAt < cutoffDate && j.Status == "COMPLETED")
            .ToListAsync();

        foreach (var job in oldJobs)
        {
            // Rimuovi il file ZIP se esiste
            if (!string.IsNullOrEmpty(job.ResultZipPath) && File.Exists(job.ResultZipPath))
            {
                try
                {
                    File.Delete(job.ResultZipPath);
                    _logger.LogInformation($"File Manager ZIP eliminato: {job.ResultZipPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Impossibile eliminare il file ZIP del File Manager: {job.ResultZipPath}");
                }
            }

            // Rimuovi il record dal database
            db.AdminFileManager.Remove(job);
        }

        if (oldJobs.Any())
        {
            await db.SaveChangesAsync();
            _logger.LogInformation($"File Manager cleanup completato: {oldJobs.Count} job vecchi rimossi");
        }

        // Rimuovi file ZIP orfani del File Manager
        await CleanupOrphanedFileManagerFiles(db);
    }

    /// <summary>
    /// Cleanup degli outage ZIP files
    /// </summary>
    private async Task CleanupOldOutageFiles()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

        // ✅ Rimuovi outage più vecchi di 60 giorni (retention più lunga per gli outages)
        var outagesCutoffDate = DateTime.UtcNow.AddDays(-60);

        var oldOutages = await db.OutagePeriods
            .Where(o => o.CreatedAt < outagesCutoffDate && !string.IsNullOrEmpty(o.ZipFilePath))
            .ToListAsync();

        foreach (var outage in oldOutages)
        {
            // Rimuovi il file ZIP se esiste
            if (!string.IsNullOrEmpty(outage.ZipFilePath) && File.Exists(outage.ZipFilePath))
            {
                try
                {
                    File.Delete(outage.ZipFilePath);
                    _logger.LogInformation($"Outage ZIP eliminato: {outage.ZipFilePath}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Impossibile eliminare il file ZIP dell'outage: {outage.ZipFilePath}");
                }
            }

            // ✅ Rimuovi solo il riferimento al file ZIP, non l'outage stesso
            outage.ZipFilePath = null;
        }

        if (oldOutages.Any())
        {
            await db.SaveChangesAsync();
            _logger.LogInformation($"Outage ZIP cleanup completato: {oldOutages.Count} file ZIP rimossi");
        }

        // Rimuovi file ZIP orfani degli outages
        await CleanupOrphanedOutageFiles(db);
    }

    /// <summary>
    /// Cleanup dei file ZIP orfani del File Manager
    /// </summary>
    private async Task CleanupOrphanedFileManagerFiles(PolarDriveDbContext db)
    {
        if (!Directory.Exists(_fileManagerZipStoragePath)) return;

        var zipFiles = Directory.GetFiles(_fileManagerZipStoragePath, "*.zip");
        var dbZipPaths = await db.AdminFileManager
            .Where(j => !string.IsNullOrEmpty(j.ResultZipPath))
            .Select(j => j.ResultZipPath)
            .ToListAsync();

        var orphanedFiles = zipFiles.Where(file => !dbZipPaths.Contains(file)).ToList();

        foreach (var orphanedFile in orphanedFiles)
        {
            try
            {
                // Elimina solo se più vecchio di 7 giorni
                var fileInfo = new FileInfo(orphanedFile);
                if (fileInfo.CreationTime < DateTime.Now.AddDays(-7))
                {
                    File.Delete(orphanedFile);
                    _logger.LogInformation($"File Manager ZIP orfano eliminato: {orphanedFile}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Impossibile eliminare il file ZIP orfano del File Manager: {orphanedFile}");
            }
        }
    }

    /// <summary>
    /// Cleanup dei file ZIP orfani degli outages
    /// </summary>
    private async Task CleanupOrphanedOutageFiles(PolarDriveDbContext db)
    {
        if (!Directory.Exists(_outageZipStoragePath)) return;

        var zipFiles = Directory.GetFiles(_outageZipStoragePath, "*.zip");
        var dbZipPaths = await db.OutagePeriods
            .Where(o => !string.IsNullOrEmpty(o.ZipFilePath))
            .Select(o => o.ZipFilePath!)
            .ToListAsync();

        var orphanedFiles = zipFiles.Where(file => !dbZipPaths.Contains(file)).ToList();

        foreach (var orphanedFile in orphanedFiles)
        {
            try
            {
                // Elimina solo se più vecchio di 7 giorni
                var fileInfo = new FileInfo(orphanedFile);
                if (fileInfo.CreationTime < DateTime.Now.AddDays(-7))
                {
                    File.Delete(orphanedFile);
                    _logger.LogInformation($"Outage ZIP orfano eliminato: {orphanedFile}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Impossibile eliminare il file ZIP orfano dell'outage: {orphanedFile}");
            }
        }
    }
}