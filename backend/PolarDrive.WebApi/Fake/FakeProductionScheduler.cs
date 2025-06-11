using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.PolarAiReports;
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
        // Aspetta 1 minuto all'avvio per permettere accumulo dati
        _logger.LogInformation("⏳ Waiting 1 minutes for data accumulation before first report...");
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

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
    /// Riga nel metodo ProcessReportGeneration
    /// </summary>
    private async Task ProcessReportGeneration(PolarDriveDbContext db)
    {
        var now = DateTime.UtcNow;
        // ✅ RIMUOVI questa riga - reportJob non serve più
        // var reportJob = new ReportGeneratorJob(db);

        // 1. 🆕 NUOVI REPORT: Ogni 5 minuti con analisi progressiva
        var needsNewReport = !_lastReportAttempts.Any() ||
                           _lastReportAttempts.Values.All(lastTime => now - lastTime >= TimeSpan.FromMinutes(5));

        if (needsNewReport)
        {
            _logger.LogInformation("🧠 Generating new PROGRESSIVE reports (5-minute cycle)");

            try
            {
                // ✅ CORREGGI: rimuovi reportJob parameter
                await GenerateProgressiveReportsForAllVehicles(db, now);

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

                _logger.LogInformation("✅ Progressive report generation cycle completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in progressive report generation");

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

        // 2. 🔄 RETRY LOGIC: ✅ CORREGGI - rimuovi reportJob parameter
        await ProcessProgressiveRetries(db, now);
    }

    /// <summary>
    /// ✅ CORREGGI anche questo metodo signature
    /// </summary>
    private async Task GenerateProgressiveReportsForAllVehicles(PolarDriveDbContext db, DateTime now)
    {
        var activeVehicles = await db.ClientVehicles
            .Where(v => v.IsActiveFlag && v.IsFetchingDataFlag)
            .ToListAsync();

        _logger.LogInformation("🧠 Processing {VehicleCount} vehicles for progressive analysis", activeVehicles.Count);

        foreach (var vehicle in activeVehicles)
        {
            try
            {
                await GenerateProgressiveReportForVehicle(db, vehicle.Id, now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generating progressive report for vehicle {VIN}", vehicle.Vin);
                throw; // Re-throw per essere gestito dal livello superiore
            }
        }
    }

    /// <summary>
    /// ✅ NUOVO: Genera report progressivo per un singolo veicolo
    /// </summary>
    private async Task GenerateProgressiveReportForVehicle(PolarDriveDbContext db, int vehicleId, DateTime now)
    {
        var vehicle = await db.ClientVehicles.FindAsync(vehicleId);
        if (vehicle == null) return;

        // Determina il periodo del report progressivo
        var reportPeriod = await DetermineProgressiveReportPeriod(db, vehicleId);

        _logger.LogInformation("🧠 Generating {AnalysisLevel} for vehicle {VIN} ({DataHours}h of data)",
            reportPeriod.AnalysisLevel, vehicle.Vin, reportPeriod.DataHours);

        // Genera insights progressivi usando PolarAiReportGenerator
        var aiGenerator = new PolarAiReportGenerator(db);
        var progressiveInsights = await aiGenerator.GenerateProgressiveInsightsAsync(vehicleId);

        if (string.IsNullOrWhiteSpace(progressiveInsights))
        {
            _logger.LogWarning("⚠️ No progressive insights generated for vehicle {VIN}", vehicle.Vin);
            return;
        }

        // Crea record del report con metadati progressivi
        var progressiveReport = new Data.Entities.PdfReport
        {
            ClientVehicleId = vehicleId,
            ClientCompanyId = vehicle.ClientCompanyId,
            ReportPeriodStart = reportPeriod.Start,
            ReportPeriodEnd = reportPeriod.End,
            GeneratedAt = now,
            Notes = $"[PROGRESSIVE-{reportPeriod.AnalysisLevel.Replace(" ", "")}] Generated with {reportPeriod.DataHours}h historical data - Monitoring: {reportPeriod.MonitoringDays:F1} days"
        };

        db.PdfReports.Add(progressiveReport);
        await db.SaveChangesAsync();

        // Genera HTML con insights progressivi
        var htmlService = new HtmlReportService(db);
        var htmlOptions = new HtmlReportOptions
        {
            ShowDetailedStats = true,
            ShowRawData = false,
            ReportType = $"🧠 {reportPeriod.AnalysisLevel} - Development Cycle",
            AdditionalCss = GetFakeSchedulerProgressiveStyles()
        };

        var htmlContent = await htmlService.GenerateHtmlReportAsync(progressiveReport, progressiveInsights, htmlOptions);

        // Salva HTML
        var htmlPath = GetProgressiveReportFilePath(progressiveReport, "html");
        var htmlDirectory = Path.GetDirectoryName(htmlPath);
        if (!string.IsNullOrEmpty(htmlDirectory))
        {
            Directory.CreateDirectory(htmlDirectory);
        }
        await File.WriteAllTextAsync(htmlPath, htmlContent);

        // Genera PDF con stili development
        var pdfService = new PdfGenerationService(db);
        var pdfOptions = new PdfConversionOptions
        {
            PageFormat = "A4",
            MarginTop = "2cm",
            MarginBottom = "2cm",
            MarginLeft = "1.5cm",
            MarginRight = "1.5cm",
            DisplayHeaderFooter = true,
            HeaderTemplate = $@"
            <div style='font-size: 10px; width: 100%; text-align: center; color: #667eea; border-bottom: 1px solid #667eea; padding-bottom: 5px;'>
                <span>🧪 DEV | 🧠 PolarDrive {reportPeriod.AnalysisLevel} - {vehicle.Vin} - {now:yyyy-MM-dd HH:mm}</span>
            </div>",
            FooterTemplate = @"
            <div style='font-size: 10px; width: 100%; text-align: center; color: #666; border-top: 1px solid #ccc; padding-top: 5px;'>
                <span>Pagina <span class='pageNumber'></span> di <span class='totalPages'></span> | Development Testing</span>
            </div>"
        };

        var pdfBytes = await pdfService.ConvertHtmlToPdfAsync(htmlContent, progressiveReport, pdfOptions);

        // Salva PDF
        var pdfPath = GetProgressiveReportFilePath(progressiveReport, "pdf");
        var pdfDirectory = Path.GetDirectoryName(pdfPath);
        if (!string.IsNullOrEmpty(pdfDirectory))
        {
            Directory.CreateDirectory(pdfDirectory);
        }
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);

        _logger.LogInformation("✅ Progressive report generated for {VIN}: ReportId {ReportId}, Level: {Level}, Size: {Size} bytes",
            vehicle.Vin, progressiveReport.Id, reportPeriod.AnalysisLevel, pdfBytes.Length);
    }

    /// <summary>
    /// ✅ AGGIORNATO: Retry con analisi progressiva
    /// </summary>
    private async Task ProcessProgressiveRetries(PolarDriveDbContext db, DateTime now)
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
            _logger.LogInformation("🔄 Processing {RetryCount} vehicle PROGRESSIVE retries", vehiclesToRetry.Count);

            foreach (var vehicleId in vehiclesToRetry)
            {
                try
                {
                    var vehicle = await db.ClientVehicles.FindAsync(vehicleId);
                    if (vehicle == null) continue;

                    _logger.LogInformation("🔄 Progressive retry #{RetryNum} for vehicle {VIN}",
                        _retryCount[vehicleId], vehicle.Vin);

                    // ✅ USA ANALISI PROGRESSIVA anche nei retry
                    await GenerateProgressiveReportForVehicle(db, vehicleId, now);

                    _logger.LogInformation("✅ Progressive retry successful for vehicle {VIN}", vehicle.Vin);
                    _retryCount[vehicleId] = 0; // Reset su successo

                    _lastReportAttempts[vehicleId] = now;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in progressive retry for vehicle {VehicleId}", vehicleId);
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
    /// ✅ NUOVO: Determina periodo del report progressivo per fake scheduler
    /// </summary>
    private async Task<ProgressiveReportPeriodInfo> DetermineProgressiveReportPeriod(PolarDriveDbContext db, int vehicleId)
    {
        var firstRecord = await db.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId)
            .OrderBy(vd => vd.Timestamp)
            .Select(vd => vd.Timestamp)
            .FirstOrDefaultAsync();

        if (firstRecord == default)
        {
            // Fallback per development - usa timestamp recente
            firstRecord = DateTime.UtcNow.AddHours(-1);
        }

        var now = DateTime.UtcNow;
        var monitoringPeriod = now - firstRecord;

        // In development, acceleriamo i livelli per testare velocemente
        var dataHours = monitoringPeriod.TotalMinutes switch
        {
            < 5 => 1,        // Primi 5 minuti: 1 ora di dati (simulato)
            < 15 => 6,       // Primi 15 minuti: 6 ore di dati  
            < 30 => 24,      // Primi 30 minuti: 1 giorno di dati
            < 60 => 168,     // Prima ora: 1 settimana di dati
            _ => 720         // Oltre 1 ora: 1 mese di dati
        };

        var analysisLevel = monitoringPeriod.TotalMinutes switch
        {
            < 5 => "Valutazione Iniziale",
            < 15 => "Analisi Rapida",
            < 30 => "Pattern Recognition",
            < 60 => "Behavioral Analysis",
            _ => "Deep Dive Analysis"
        };

        return new ProgressiveReportPeriodInfo
        {
            Start = now.AddHours(-dataHours),
            End = now,
            DataHours = dataHours,
            AnalysisLevel = analysisLevel,
            MonitoringDays = monitoringPeriod.TotalDays
        };
    }

    /// <summary>
    /// ✅ NUOVO: Stili CSS per report fake scheduler
    /// </summary>
    private string GetFakeSchedulerProgressiveStyles()
    {
        return @"
        .development-badge {
            background: linear-gradient(135deg, #ff6b6b 0%, #ee5a24 100%);
            color: white;
            padding: 8px 16px;
            border-radius: 25px;
            font-size: 12px;
            font-weight: bold;
            display: inline-block;
            margin: 10px 15px 10px 0;
            box-shadow: 0 4px 8px rgba(0,0,0,0.2);
        }
        
        .development-badge::before {
            content: '🧪 DEVELOPMENT • ';
        }
        
        .progressive-development {
            background: linear-gradient(135deg, rgba(255, 107, 107, 0.1) 0%, rgba(102, 126, 234, 0.1) 100%);
            border: 2px dashed #ff6b6b;
            padding: 15px;
            margin: 20px 0;
            border-radius: 8px;
        }
        
        .progressive-development::before {
            content: '🧪 Development Testing • 🧠 Progressive AI • ';
            color: #ff6b6b;
            font-weight: bold;
            font-size: 14px;
        }
        
        .fake-scheduler-info {
            background: #fff3cd;
            border: 1px solid #ffeaa7;
            padding: 12px;
            border-radius: 6px;
            margin: 15px 0;
            font-size: 12px;
            color: #856404;
        }
        
        .fake-scheduler-info::before {
            content: '⚡ Fast Development Cycle: ';
            font-weight: bold;
        }";
    }

    /// <summary>
    /// ✅ NUOVO: Path per report progressivi development
    /// </summary>
    private string GetProgressiveReportFilePath(Data.Entities.PdfReport report, string extension)
    {
        var outputDir = Path.Combine("storage", "dev-reports", // ✅ Directory separata per development
            report.ReportPeriodStart.Year.ToString(),
            report.ReportPeriodStart.Month.ToString("D2"));

        return Path.Combine(outputDir, $"PolarDrive_Progressive_Dev_{report.Id}.{extension}");
    }

    /// <summary>
    /// ✅ CLASSE HELPER per info periodo report progressivo
    /// </summary>
    public class ProgressiveReportPeriodInfo
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int DataHours { get; set; }
        public string AnalysisLevel { get; set; } = "";
        public double MonitoringDays { get; set; }
    }

    /// <summary>
    /// ✅ AGGIORNATO: Statistics con info progressive
    /// </summary>
    private async Task LogReportStatistics(PolarDriveDbContext db)
    {
        try
        {
            var totalReports = await db.PdfReports.CountAsync();
            var recentReports = await db.PdfReports
                .Where(r => r.GeneratedAt >= DateTime.UtcNow.AddHours(-1))
                .CountAsync();

            // ✅ NUOVO: Conta report progressivi
            var progressiveReports = await db.PdfReports
                .Where(r => r.Notes != null && r.Notes.Contains("[PROGRESSIVE"))
                .CountAsync();

            var recentProgressiveReports = await db.PdfReports
                .Where(r => r.GeneratedAt >= DateTime.UtcNow.AddHours(-1) &&
                           r.Notes != null && r.Notes.Contains("[PROGRESSIVE"))
                .CountAsync();

            var totalVehicleData = await db.VehiclesData.CountAsync();
            var recentData = await db.VehiclesData
                .Where(d => d.Timestamp >= DateTime.UtcNow.AddMinutes(-10))
                .CountAsync();

            var vehiclesWithRetries = _retryCount.Count(kv => kv.Value > 0);
            var vehiclesExceededRetries = _retryCount.Count(kv => kv.Value > MAX_RETRIES_PER_VEHICLE);

            _logger.LogInformation("📈 FakeProductionScheduler Progressive Statistics:");
            _logger.LogInformation($"   Total Reports: {totalReports} (Progressive: {progressiveReports})");
            _logger.LogInformation($"   Last Hour: {recentReports} total ({recentProgressiveReports} progressive)");
            _logger.LogInformation($"   Total Vehicle Data: {totalVehicleData} (Last 10min: {recentData})");
            _logger.LogInformation($"   Vehicles with active retries: {vehiclesWithRetries}");
            _logger.LogInformation($"   Vehicles exceeded max retries: {vehiclesExceededRetries}");

            // Log dettagli retry se presenti
            if (vehiclesWithRetries > 0)
            {
                _logger.LogInformation("🔄 Progressive retry details:");
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
            _logger.LogWarning(ex, "⚠️ FakeProductionScheduler: Failed to log progressive statistics");
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