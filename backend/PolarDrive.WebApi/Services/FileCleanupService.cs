using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;

namespace PolarDrive.WebApi.Services;

public class FileCleanupService(IServiceProvider serviceProvider, PolarDriveLogger logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly PolarDriveLogger _logger = logger;
    private readonly string _fileManagerZipStoragePath = Path.Combine("storage", "filemanager-zips");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldFileManagerFiles();

                // Esegui ogni 24 ore
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (Exception ex)
            {
                _ = _logger.Error(ex.ToString(), "Errore durante il cleanup dei file ZIP");
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
        var cutoffDate = DateTime.Now.AddDays(-30);

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
                    _ = _logger.Info(
                        "FileCleanupService.CleanupOldFileManagerFiles",
                        $"File Manager ZIP eliminato: {job.ResultZipPath}"
                    );
                }
                catch (Exception ex)
                {
                    _ = _logger.Warning(ex.ToString(), $"Impossibile eliminare il file ZIP del File Manager: {job.ResultZipPath}");
                }
            }

            // Rimuovi il record dal database
            db.AdminFileManager.Remove(job);
        }

        if (oldJobs.Any())
        {
            await db.SaveChangesAsync();
            _ = _logger.Info(
                "FileCleanupService.CleanupOldFileManagerFiles",
                $"File Manager cleanup completato: {oldJobs.Count} job vecchi rimossi"
            );
        }

        // Rimuovi file ZIP orfani del File Manager
        await CleanupOrphanedFileManagerFiles(db);
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
                    _ = _logger.Info(
                        "FileCleanupService.CleanupOldFileManagerFiles",
                        $"File Manager ZIP orfano eliminato: {orphanedFile}"
                    );
                }
            }
            catch (Exception ex)
            {
                _ = _logger.Warning(ex.ToString(), $"Impossibile eliminare il file ZIP orfano del File Manager: {orphanedFile}");
            }
        }
    }
}