using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Jobs;

namespace PolarDrive.WebApi.Fake;

/// <summary>
/// Scheduler FAKE per development - genera report molto frequentemente per testing
/// MIGLIORATO con retry logic e better error handling
/// </summary>
public class FakeProductionScheduler(IServiceProvider serviceProvider, ILogger<FakeProductionScheduler> logger, IWebHostEnvironment env) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<FakeProductionScheduler> _logger = logger;
    private readonly IWebHostEnvironment _env = env;
    private readonly Dictionary<int, DateTime> _lastReportAttempts = new();
    private readonly Dictionary<int, int> _retryCount = new();
    private const int MAX_RETRIES_PER_VEHICLE = 3;
    private const int RETRY_DELAY_MINUTES = 2;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // FUNZIONA SOLO in Development
        if (!_env.IsDevelopment())
        {
            _logger.LogInformation("FakeProductionScheduler: Only runs in Development mode");
            return;
        }

        _logger.LogInformation("🚀 FakeProductionScheduler: Starting DEVELOPMENT report scheduler");
        _logger.LogInformation("📋 Report generation frequency: Every 5 minutes for testing");
        _logger.LogInformation("🔄 Retry logic: Up to {MaxRetries} retries per vehicle with {RetryDelay}min delays",
            MAX_RETRIES_PER_VEHICLE, RETRY_DELAY_MINUTES);

        // Avvia solo il report scheduler (non il data fetch, lo fa già TeslaMockApiService)
        await RunFakeReportScheduler(stoppingToken);
    }

    /// <summary>
    /// Scheduler FAKE per report - genera report ogni 5 minuti per testing rapido
    /// CON RETRY LOGIC per gestire fallimenti
    /// </summary>
    private async Task RunFakeReportScheduler(CancellationToken stoppingToken)
    {
        // Aspetta 2 minuti all'avvio per permettere accumulo dati
        _logger.LogInformation("⏳ Waiting 2 minutes for data accumulation before first report...");
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

                _logger.LogInformation("📊 FakeProductionScheduler: Starting report generation cycle");

                // 🔄 NUOVA LOGICA: Controlla sia report nuovi che retry
                await ProcessReportGeneration(db);

                // Log statistiche sui report generati
                await LogReportStatistics(db);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ FakeProductionScheduler: Error in report generation cycle");
            }

            // ⚡ FREQUENZA VELOCE: Controlla ogni 1 minuto per retry, genera nuovi ogni 5 minuti
            _logger.LogInformation("⏰ Next check in 1 minute...");
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    /// <summary>
    /// Processa sia la generazione di nuovi report che i retry di quelli falliti
    /// </summary>
    private async Task ProcessReportGeneration(PolarDriveDbContext db)
    {
        var now = DateTime.UtcNow;
        var reportJob = new ReportGeneratorJob(db);

        // 1. 🆕 NUOVI REPORT: Ogni 5 minuti
        var needsNewReport = !_lastReportAttempts.Any() ||
                           _lastReportAttempts.Values.All(lastTime => now - lastTime >= TimeSpan.FromMinutes(5));

        if (needsNewReport)
        {
            _logger.LogInformation("🆕 Generating new reports (5-minute cycle)");

            try
            {
                await reportJob.RunTestAsync();

                // Aggiorna il timestamp per tutti i veicoli attivi
                var activeVehicles = await db.ClientVehicles
                    .Where(v => v.IsActiveFlag && v.IsFetchingDataFlag)
                    .Select(v => v.Id)
                    .ToListAsync();

                foreach (var vehicleId in activeVehicles)
                {
                    _lastReportAttempts[vehicleId] = now;
                    _retryCount[vehicleId] = 0; // Reset retry count on successful cycle
                }

                _logger.LogInformation("✅ New report generation cycle completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in new report generation");

                // Marca come fallito ma non resetta il timer per permettere retry
                var activeVehicles = await db.ClientVehicles
                    .Where(v => v.IsActiveFlag && v.IsFetchingDataFlag)
                    .Select(v => v.Id)
                    .ToListAsync();

                foreach (var vehicleId in activeVehicles)
                {
                    if (!_lastReportAttempts.ContainsKey(vehicleId))
                    {
                        _lastReportAttempts[vehicleId] = now;
                    }
                    _retryCount[vehicleId] = _retryCount.GetValueOrDefault(vehicleId, 0) + 1;
                }
            }
        }

        // 2. 🔄 RETRY LOGIC: Controlla se ci sono report da riprovare
        await ProcessRetries(db, reportJob, now);
    }

    /// <summary>
    /// Gestisce i retry per i report falliti
    /// </summary>
    private async Task ProcessRetries(PolarDriveDbContext db, ReportGeneratorJob reportJob, DateTime now)
    {
        var vehiclesToRetry = new List<int>();

        foreach (var (vehicleId, retryCount) in _retryCount.ToList())
        {
            if (retryCount > 0 && retryCount <= MAX_RETRIES_PER_VEHICLE)
            {
                var lastAttempt = _lastReportAttempts.GetValueOrDefault(vehicleId, DateTime.MinValue);
                var timeSinceLastAttempt = now - lastAttempt;

                if (timeSinceLastAttempt >= TimeSpan.FromMinutes(RETRY_DELAY_MINUTES))
                {
                    vehiclesToRetry.Add(vehicleId);
                }
            }
        }

        if (vehiclesToRetry.Any())
        {
            _logger.LogInformation("🔄 Processing {RetryCount} vehicle retries", vehiclesToRetry.Count);

            foreach (var vehicleId in vehiclesToRetry)
            {
                try
                {
                    var vehicle = await db.ClientVehicles.FindAsync(vehicleId);
                    if (vehicle == null) continue;

                    _logger.LogInformation("🔄 Retry #{RetryNum} for vehicle {VIN}",
                        _retryCount[vehicleId], vehicle.Vin);

                    // Genera report per questo specifico veicolo
                    var endTime = now;
                    var startTime = endTime.AddMinutes(-5); // Ultimi 5 minuti

                    var report = await reportJob.GenerateForVehicleAsync(vehicleId, startTime, endTime);

                    if (report != null)
                    {
                        _logger.LogInformation("✅ Retry successful for vehicle {VIN}", vehicle.Vin);
                        _retryCount[vehicleId] = 0; // Reset su successo
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
            }
        }
    }

    /// <summary>
    /// Log statistiche sui report per il development
    /// CON INFORMAZIONI SUI RETRY
    /// </summary>
    private async Task LogReportStatistics(PolarDriveDbContext db)
    {
        try
        {
            var totalReports = await db.PdfReports.CountAsync();
            var recentReports = await db.PdfReports
                .Where(r => r.GeneratedAt >= DateTime.UtcNow.AddHours(-1))
                .CountAsync();

            var totalVehicleData = await db.VehiclesData.CountAsync();
            var recentData = await db.VehiclesData
                .Where(d => d.Timestamp >= DateTime.UtcNow.AddMinutes(-10))
                .CountAsync();

            var vehiclesWithRetries = _retryCount.Count(kv => kv.Value > 0);
            var vehiclesExceededRetries = _retryCount.Count(kv => kv.Value > MAX_RETRIES_PER_VEHICLE);

            _logger.LogInformation("📈 FakeProductionScheduler Statistics:");
            _logger.LogInformation($"   Total Reports: {totalReports} (Last hour: {recentReports})");
            _logger.LogInformation($"   Total Vehicle Data: {totalVehicleData} (Last 10min: {recentData})");
            _logger.LogInformation($"   Vehicles with active retries: {vehiclesWithRetries}");
            _logger.LogInformation($"   Vehicles exceeded max retries: {vehiclesExceededRetries}");

            // Log dettagli retry se presenti
            if (vehiclesWithRetries > 0)
            {
                _logger.LogInformation("🔄 Retry details:");
                foreach (var (vehicleId, retryCount) in _retryCount.Where(kv => kv.Value > 0))
                {
                    var lastAttempt = _lastReportAttempts.GetValueOrDefault(vehicleId, DateTime.MinValue);
                    var status = retryCount > MAX_RETRIES_PER_VEHICLE ? "EXCEEDED" : "PENDING";
                    _logger.LogInformation($"   Vehicle {vehicleId}: {retryCount} retries, last attempt: {lastAttempt:HH:mm:ss}, status: {status}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ FakeProductionScheduler: Failed to log statistics");
        }
    }

    /// <summary>
    /// Metodo pubblico per forzare un reset dei retry (utile per testing)
    /// </summary>
    public void ResetRetryCounters()
    {
        _retryCount.Clear();
        _lastReportAttempts.Clear();
        _logger.LogInformation("🔄 Retry counters reset manually");
    }
}