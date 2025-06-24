// Nel tuo FileCleanupService.cs, aggiorna il costruttore:

using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;

namespace PolarDrive.WebApi.Services;

public class FileCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FileCleanupService> _logger;
    private readonly string _zipStoragePath;

    public FileCleanupService(IServiceProvider serviceProvider, ILogger<FileCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // ✅ AGGIORNATO: usa la directory corretta
        _zipStoragePath = Path.Combine("storage", "filemanager-zips");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldFiles();

                // Esegui ogni 24 ore
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il cleanup dei file ZIP del File Manager");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private async Task CleanupOldFiles()
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

        // Rimuovi file ZIP orfani (senza record nel database)
        await CleanupOrphanedFiles(db);
    }

    private async Task CleanupOrphanedFiles(PolarDriveDbContext db)
    {
        if (!Directory.Exists(_zipStoragePath)) return;

        var zipFiles = Directory.GetFiles(_zipStoragePath, "*.zip");
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
}