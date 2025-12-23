using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Services;

public class VehiclesDataArchiveService(IServiceScopeFactory scopeFactory, ILogger<VehiclesDataArchiveService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ArchiveOldDataAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Errore durante archiviazione VehiclesData");
            }
            
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task ArchiveOldDataAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

        var rowsArchived = await db.Database.ExecuteSqlRawAsync($@"
            INSERT INTO dbo.VehiclesDataArchive (IsSmsAdaptiveProfile, VehicleId, Timestamp, RawJsonAnonymized)
            SELECT IsSmsAdaptiveProfile, VehicleId, Timestamp, RawJsonAnonymized 
            FROM dbo.VehiclesData 
            WHERE Timestamp < DATEADD(HOUR, -{MONTHLY_HOURS_THRESHOLD}, GETDATE());

            DELETE FROM dbo.VehiclesData 
            WHERE Timestamp < DATEADD(HOUR, -{MONTHLY_HOURS_THRESHOLD}, GETDATE());
        ");

        if (rowsArchived > 0)
        {
            await db.Database.ExecuteSqlRawAsync("ALTER INDEX ALL ON dbo.VehiclesData REBUILD;");
            logger.LogInformation("VehiclesData archiviazione completata: {Rows} righe spostate, indici ricostruiti", rowsArchived);
        }
        else
        {
            logger.LogInformation("VehiclesData archiviazione: nessuna riga da archiviare");
        }
    }
}