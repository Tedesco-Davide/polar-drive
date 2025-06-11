using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Jobs;

namespace PolarDrive.WebApi.Production;

/// <summary>
/// Scheduler per produzione - gestisce i task automatici con retry logic
/// AGGIORNATO per usare il nuovo sistema modulare
/// </summary>
public class ProductionScheduler(IServiceProvider serviceProvider, ILogger<ProductionScheduler> logger, IWebHostEnvironment env) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ProductionScheduler> _logger = logger;
    private readonly IWebHostEnvironment _env = env;
    private readonly Dictionary<int, DateTime> _lastReportAttempts = [];
    private readonly Dictionary<int, int> _retryCount = [];
    private const int MAX_RETRIES_PER_VEHICLE = 5; // Più retry in produzione
    private const int RETRY_DELAY_HOURS = 2; // Retry ogni 2 ore

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_env.IsDevelopment())
        {
            _logger.LogInformation("ProductionScheduler: Skipping in Development mode");
            return;
        }

        _logger.LogInformation("🏭 ProductionScheduler: Starting PRODUCTION schedulers");
        _logger.LogInformation("📋 Report generation: Monthly on 1st day");
        _logger.LogInformation("🔄 Retry logic: Up to {MaxRetries} retries per vehicle with {RetryDelay}h delays",
            MAX_RETRIES_PER_VEHICLE, RETRY_DELAY_HOURS);

        // Avvia i task paralleli
        var reportTask = RunReportScheduler(stoppingToken);
        var retryTask = RunRetryScheduler(stoppingToken);

        await Task.WhenAll(reportTask, retryTask);
    }

    /// <summary>
    /// Scheduler per i report mensili (controlla ogni giorno alle 02:00)
    /// </summary>
    private async Task RunReportScheduler(CancellationToken stoppingToken)
    {
        // Aspetta fino alle 2:00 del mattino del primo avvio
        await WaitUntilTargetTime(2, 0);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Genera report solo il primo giorno del mese alle 02:00
                if (now.Day == 1)
                {
                    _logger.LogInformation("📊 ProductionScheduler: Starting monthly report generation");

                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

                    await ProcessMonthlyReports(db);

                    _logger.LogInformation("✅ ProductionScheduler: Completed monthly report generation");
                }
                else
                {
                    _logger.LogInformation("⏭️ ProductionScheduler: Not first day of month, skipping report generation");
                }

                // Log statistiche
                await LogProductionStatistics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ProductionScheduler: Error in report generation cycle");
            }

            // Aspetta 24 ore (alle 02:00 del giorno successivo)
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    /// <summary>
    /// Scheduler per i retry (controlla ogni 2 ore)
    /// </summary>
    private async Task RunRetryScheduler(CancellationToken stoppingToken)
    {
        // Offset per non coincidere con il report scheduler
        await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

                await ProcessRetries(db);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ProductionScheduler: Error in retry cycle");
            }

            // Controllo retry ogni 2 ore
            await Task.Delay(TimeSpan.FromHours(2), stoppingToken);
        }
    }

    /// <summary>
    /// Processa la generazione di report mensili per tutti i veicoli attivi
    /// </summary>
    private async Task ProcessMonthlyReports(PolarDriveDbContext db)
    {
        var now = DateTime.UtcNow;
        var startOfLastMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
        var endOfLastMonth = startOfLastMonth.AddMonths(1).AddDays(-1);

        _logger.LogInformation("📅 Generating monthly reports for period: {Start} to {End}",
            startOfLastMonth.ToString("yyyy-MM-dd"), endOfLastMonth.ToString("yyyy-MM-dd"));

        // Trova tutti i veicoli attivi con dati nel periodo
        var activeVehicles = await db.ClientVehicles
            .Include(v => v.ClientCompany)
            .Where(v => v.IsActiveFlag)
            .Where(v => db.VehiclesData.Any(vd =>
                vd.VehicleId == v.Id &&
                vd.Timestamp >= startOfLastMonth &&
                vd.Timestamp <= endOfLastMonth))
            .ToListAsync();

        if (!activeVehicles.Any())
        {
            _logger.LogInformation("ℹ️ No vehicles with data for monthly report period");
            return;
        }

        _logger.LogInformation("🚗 Found {Count} vehicles for monthly reports", activeVehicles.Count);

        var reportJob = new ReportGeneratorJob(db);
        var successCount = 0;
        var errorCount = 0;

        foreach (var vehicle in activeVehicles)
        {
            try
            {
                _logger.LogInformation("📄 Generating monthly report for vehicle {VIN}", vehicle.Vin);

                var report = await reportJob.GenerateForVehicleAsync(
                    vehicle.Id,
                    startOfLastMonth,
                    endOfLastMonth,
                    "Monthly-Production");

                if (report != null)
                {
                    successCount++;
                    _lastReportAttempts[vehicle.Id] = DateTime.UtcNow;
                    _retryCount[vehicle.Id] = 0; // Reset retry count on success

                    _logger.LogInformation("✅ Monthly report generated for vehicle {VIN} - ReportId: {ReportId}",
                        vehicle.Vin, report.Id);
                }
                else
                {
                    errorCount++;
                    _retryCount[vehicle.Id] = _retryCount.GetValueOrDefault(vehicle.Id, 0) + 1;
                    _lastReportAttempts[vehicle.Id] = DateTime.UtcNow;

                    _logger.LogWarning("⚠️ Failed to generate monthly report for vehicle {VIN}", vehicle.Vin);
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                _retryCount[vehicle.Id] = _retryCount.GetValueOrDefault(vehicle.Id, 0) + 1;
                _lastReportAttempts[vehicle.Id] = DateTime.UtcNow;

                _logger.LogError(ex, "❌ Error generating monthly report for vehicle {VIN}", vehicle.Vin);
            }

            // Pausa tra veicoli per non sovraccaricare il sistema
            await Task.Delay(TimeSpan.FromSeconds(30));
        }

        _logger.LogInformation("📊 Monthly report generation completed: {Success} success, {Errors} errors",
            successCount, errorCount);
    }

    /// <summary>
    /// Gestisce i retry per i report falliti
    /// </summary>
    private async Task ProcessRetries(PolarDriveDbContext db)
    {
        var now = DateTime.UtcNow;
        var vehiclesToRetry = new List<int>();

        foreach (var (vehicleId, retryCount) in _retryCount.ToList())
        {
            if (retryCount > 0 && retryCount <= MAX_RETRIES_PER_VEHICLE)
            {
                var lastAttempt = _lastReportAttempts.GetValueOrDefault(vehicleId, DateTime.MinValue);
                var timeSinceLastAttempt = now - lastAttempt;

                if (timeSinceLastAttempt >= TimeSpan.FromHours(RETRY_DELAY_HOURS))
                {
                    vehiclesToRetry.Add(vehicleId);
                }
            }
        }

        if (!vehiclesToRetry.Any())
        {
            _logger.LogInformation("🔄 No vehicles need retry at this time");
            return;
        }

        _logger.LogInformation("🔄 Processing {Count} vehicle retries", vehiclesToRetry.Count);

        var reportJob = new ReportGeneratorJob(db);

        foreach (var vehicleId in vehiclesToRetry)
        {
            try
            {
                var vehicle = await db.ClientVehicles.FindAsync(vehicleId);
                if (vehicle == null) continue;

                _logger.LogInformation("🔄 Retry #{RetryNum} for vehicle {VIN}",
                    _retryCount[vehicleId], vehicle.Vin);

                // Per i retry, usa l'ultimo mese completo
                var now2 = DateTime.UtcNow;
                var startOfLastMonth = new DateTime(now2.Year, now2.Month, 1).AddMonths(-1);
                var endOfLastMonth = startOfLastMonth.AddMonths(1).AddDays(-1);

                var report = await reportJob.GenerateForVehicleAsync(
                    vehicleId,
                    startOfLastMonth,
                    endOfLastMonth,
                    $"Retry-{_retryCount[vehicleId]}");

                if (report != null)
                {
                    _logger.LogInformation("✅ Retry successful for vehicle {VIN} - ReportId: {ReportId}",
                        vehicle.Vin, report.Id);
                    _retryCount[vehicleId] = 0; // Reset on success
                }
                else
                {
                    _logger.LogWarning("⚠️ Retry failed for vehicle {VIN}", vehicle.Vin);
                    _retryCount[vehicleId]++;
                }

                _lastReportAttempts[vehicleId] = now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in retry for vehicle {VehicleId}", vehicleId);
                _retryCount[vehicleId]++;
                _lastReportAttempts[vehicleId] = now;
            }

            // Verifica se ha superato il limite di retry
            if (_retryCount[vehicleId] > MAX_RETRIES_PER_VEHICLE)
            {
                _logger.LogWarning("🚫 Vehicle {VehicleId} exceeded max retries ({MaxRetries}), stopping attempts",
                    vehicleId, MAX_RETRIES_PER_VEHICLE);
            }

            // Pausa tra retry per non sovraccaricare
            await Task.Delay(TimeSpan.FromMinutes(5));
        }
    }

    /// <summary>
    /// Aspetta fino all'orario target (es. 02:00)
    /// </summary>
    private async Task WaitUntilTargetTime(int targetHour, int targetMinute)
    {
        var now = DateTime.UtcNow;
        var target = new DateTime(now.Year, now.Month, now.Day, targetHour, targetMinute, 0);

        if (target <= now)
            target = target.AddDays(1);

        var delay = target - now;

        _logger.LogInformation("⏰ ProductionScheduler: Waiting until {Target} (in {Delay})",
            target.ToString("yyyy-MM-dd HH:mm"), delay.ToString(@"hh\:mm"));

        await Task.Delay(delay);
    }

    /// <summary>
    /// Log statistiche di produzione
    /// </summary>
    private async Task LogProductionStatistics()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            var totalReports = await db.PdfReports.CountAsync();
            var monthlyReports = await db.PdfReports
                .Where(r => r.GeneratedAt >= DateTime.UtcNow.AddDays(-30))
                .CountAsync();

            var totalVehicles = await db.ClientVehicles
                .Where(v => v.IsActiveFlag)
                .CountAsync();

            var totalData = await db.VehiclesData.CountAsync();
            var recentData = await db.VehiclesData
                .Where(d => d.Timestamp >= DateTime.UtcNow.AddDays(-1))
                .CountAsync();

            var vehiclesWithRetries = _retryCount.Count(kv => kv.Value > 0);
            var vehiclesExceededRetries = _retryCount.Count(kv => kv.Value > MAX_RETRIES_PER_VEHICLE);

            _logger.LogInformation("📈 ProductionScheduler Statistics:");
            _logger.LogInformation($"   Total Reports: {totalReports} (Last 30 days: {monthlyReports})");
            _logger.LogInformation($"   Active Vehicles: {totalVehicles}");
            _logger.LogInformation($"   Vehicle Data: {totalData} total (Last 24h: {recentData})");
            _logger.LogInformation($"   Vehicles with retries: {vehiclesWithRetries}");
            _logger.LogInformation($"   Vehicles exceeded retries: {vehiclesExceededRetries}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ ProductionScheduler: Failed to log statistics");
        }
    }
}