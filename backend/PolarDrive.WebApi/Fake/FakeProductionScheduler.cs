using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.PolarAiReports;
using PolarDrive.WebApi.Production;

namespace PolarDrive.WebApi.Fake;

/// <summary>
/// Scheduler FAKE per development - genera report molto frequentemente per testing
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

        _logger.LogInformation("🚀 FakeProductionScheduler: Starting DEVELOPMENT scheduler");
        _logger.LogInformation("📋 Report generation: Every 5 minutes for testing");
        _logger.LogInformation("🧠 Using PolarAi Analysis for all reports");
        _logger.LogInformation("🔄 Retry logic: Up to {MaxRetries} retries per vehicle with {RetryDelay}min delays",
            MAX_RETRIES_PER_VEHICLE, RETRY_DELAY_MINUTES);

        // Verifica servizi disponibili all'avvio
        await CheckServicesAvailability();

        // Avvia solo il report scheduler (non il data fetch, lo fa già TeslaMockApiService)
        await RunFakeReportScheduler(stoppingToken);
    }

    /// <summary>
    /// Scheduler FAKE per report
    /// </summary>
    private async Task RunFakeReportScheduler(CancellationToken stoppingToken)
    {
        // Aspetta 1 minuto all'avvio per permettere accumulo dati
        _logger.LogInformation("⏳ Waiting 1 minute for data accumulation before first report...");
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

                _logger.LogInformation("📊 FakeProductionScheduler: Starting report generation cycle");

                // ✅ Processo di generazione con risultati dettagliati
                var results = await ProcessReportGeneration(db);

                // Log statistiche sui report generati
                await LogReportStatistics(db, results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ FakeProductionScheduler: Error in report generation cycle");
            }

            // ⚡ FREQUENZA VELOCE: Controlla ogni 1 minuto per retry, genera nuovi ogni 1 minuti
            _logger.LogInformation("⏰ Next check in 1 minute...");
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    /// <summary>
    /// Processo di generazione con risultati tracciati
    /// </summary>
    private async Task<FakeSchedulerResults> ProcessReportGeneration(PolarDriveDbContext db)
    {
        var results = new FakeSchedulerResults();
        var now = DateTime.UtcNow;

        // 1. 🆕 NUOVI REPORT: Ogni 5 minuti con analisi
        var needsNewReport = !_lastReportAttempts.Any() ||
                           _lastReportAttempts.Values.All(lastTime => now - lastTime >= TimeSpan.FromMinutes(5));

        if (needsNewReport)
        {
            _logger.LogInformation("🧠 Generating new reports (5-minute cycle)");

            try
            {
                var generationResults = await GenerateReportsForAllVehicles(db, now);
                results.NewReportsGenerated = generationResults.SuccessCount;
                results.NewReportsErrors = generationResults.ErrorCount;

                // Aggiorna il timestamp per tutti i veicoli attivi
                var fetchingVehicles = await db.ClientVehicles
                    .Where(v => v.IsFetchingDataFlag)  // ← Solo fetching per includere grace period
                    .Select(v => v.Id)
                    .ToListAsync();

                foreach (var vehicleId in fetchingVehicles)
                {
                    _lastReportAttempts[vehicleId] = now;
                    if (generationResults.SuccessfulVehicles.Contains(vehicleId))
                    {
                        _retryCount[vehicleId] = 0;
                    }
                }

                _logger.LogInformation("✅ Report generation cycle completed: {Success}/{Total}",
                    generationResults.SuccessCount, generationResults.TotalProcessed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in report generation");

                // Marca come fallito ma non resetta il timer per permettere retry
                var fetchingVehicles = await db.ClientVehicles
                    .Where(v => v.IsFetchingDataFlag)  // ← Solo fetching per includere grace period
                    .Select(v => v.Id)
                    .ToListAsync();

                foreach (var vehicleId in fetchingVehicles)
                {
                    if (!_lastReportAttempts.ContainsKey(vehicleId))
                    {
                        _lastReportAttempts[vehicleId] = now;
                    }
                    _retryCount[vehicleId] = _retryCount.GetValueOrDefault(vehicleId, 0) + 1;
                }

                results.NewReportsErrors = fetchingVehicles.Count;
            }
        }

        // 2. 🔄 RETRY LOGIC
        var retryResults = await ProcessRetries(db, now);
        results.RetriesProcessed = retryResults.ProcessedCount;
        results.RetriesSuccessful = retryResults.SuccessCount;
        results.RetriesFailed = retryResults.ErrorCount;

        return results;
    }

    /// <summary>
    /// Generazione con tracking dei risultati
    /// </summary>
    private async Task<GenerationResults> GenerateReportsForAllVehicles(PolarDriveDbContext db, DateTime now)
    {
        var results = new GenerationResults();

        var fetchingVehicles = await db.ClientVehicles
            .Where(v => v.IsFetchingDataFlag)  // ← Solo fetching per includere grace period
            .ToListAsync();

        results.TotalProcessed = fetchingVehicles.Count;
        _logger.LogInformation("🧠 Processing {VehicleCount} vehicles for analysis", fetchingVehicles.Count);

        foreach (var vehicle in fetchingVehicles)
        {
            try
            {
                await GenerateReportForVehicle(db, vehicle.Id, now);
                results.SuccessCount++;
                results.SuccessfulVehicles.Add(vehicle.Id);
            }
            catch (Exception ex)
            {
                results.ErrorCount++;
                results.FailedVehicles.Add(vehicle.Id);
                _logger.LogError(ex, "❌ Error generating report for vehicle {VIN}", vehicle.Vin);
                // Non re-throw per permettere continuazione con altri veicoli
            }
        }

        return results;
    }

    /// <summary>
    /// Genera report con controlli di sicurezza
    /// </summary>
    public async Task GenerateReportForVehicle(PolarDriveDbContext db, int vehicleId, DateTime now)
    {
        var vehicle = await db.ClientVehicles
            .Include(v => v.ClientCompany)
            .FirstOrDefaultAsync(v => v.Id == vehicleId);

        if (vehicle == null) return;

        if (!vehicle.IsActiveFlag && vehicle.IsFetchingDataFlag)
        {
            _logger.LogInformation("⏳ Grace Period - Generating report for {VIN} with terminated contract - awaiting token revocation",
                vehicle.Vin);
        }

        // Determina il periodo del report
        var reportPeriod = await DetermineReportPeriod(db, vehicleId);

        _logger.LogInformation("🧠 Generating {AnalysisLevel} for vehicle {VIN} ({DataHours}h data, {MonitoringDays:F1}d monitoring)",
            reportPeriod.AnalysisLevel, vehicle.Vin, reportPeriod.DataHours, reportPeriod.MonitoringDays);

        // Genera insights usando PolarAiReportGenerator
        var aiGenerator = new PolarAiReportGenerator(db);
        var insights = await aiGenerator.GenerateInsightsAsync(vehicleId);

        if (string.IsNullOrWhiteSpace(insights))
        {
            _logger.LogWarning("⚠️ No insights generated for vehicle {VIN}", vehicle.Vin);
            return;
        }

        // Crea record del report con metadati
        var report = new Data.Entities.PdfReport
        {
            ClientVehicleId = vehicleId,
            ClientCompanyId = vehicle.ClientCompanyId,
            ReportPeriodStart = reportPeriod.Start,
            ReportPeriodEnd = reportPeriod.End,
            GeneratedAt = now,
            Notes = $"[FAKE-{reportPeriod.AnalysisLevel.Replace(" ", "")}] " +
                   $"DataHours: {reportPeriod.DataHours}, MonitoringDays: {reportPeriod.MonitoringDays:F1}"
        };

        db.PdfReports.Add(report);
        await db.SaveChangesAsync();

        var shouldGenerateFiles = await CheckFileGenerationServices();

        if (shouldGenerateFiles)
        {
            try
            {
                await GenerateHtmlAndPdfFiles(db, report, insights, reportPeriod, vehicle, now);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ File generation failed for vehicle {VIN}, but database record saved", vehicle.Vin);
                // Non fallire tutto il processo se solo la generazione file fallisce
            }
        }
        else
        {
            _logger.LogInformation("ℹ️ File generation services not available, saving only database record for {VIN}", vehicle.Vin);
        }

        _logger.LogInformation("✅ Report generated for {VIN}: ReportId {ReportId}, Level: {Level}",
            vehicle.Vin, report.Id, reportPeriod.AnalysisLevel);
    }

    /// <summary>
    /// Generazione HTML e PDF separata con gestione errori
    /// </summary>
    public async Task GenerateHtmlAndPdfFiles(PolarDriveDbContext db, Data.Entities.PdfReport report,
        string insights, ReportPeriodInfo reportPeriod, Data.Entities.ClientVehicle vehicle, DateTime now)
    {
        // Genera HTML con insights
        var htmlService = new HtmlReportService(db);
        var htmlOptions = new HtmlReportOptions
        {
            ShowDetailedStats = true,
            ShowRawData = false,
            ReportType = $"🧠 {reportPeriod.AnalysisLevel} - Development Cycle",
            AdditionalCss = PolarAiReports.Templates.DefaultCssTemplate.Value
        };

        var htmlContent = await htmlService.GenerateHtmlReportAsync(report, insights, htmlOptions);

        // Salva HTML
        var htmlPath = GetReportFilePath(report, "html");
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
                <span>🧪 DEV | 🧠 PolarDrive {reportPeriod.AnalysisLevel} - {vehicle.Vin} - {reportPeriod.MonitoringDays:F1}d monitoring - {now:yyyy-MM-dd HH:mm}</span>
            </div>",
            FooterTemplate = @"
            <div style='font-size: 10px; width: 100%; text-align: center; color: #666; border-top: 1px solid #ccc; padding-top: 5px;'>
                <span>Pagina <span class='pageNumber'></span> di <span class='totalPages'></span> | Development Testing</span>
            </div>"
        };

        var pdfBytes = await pdfService.ConvertHtmlToPdfAsync(htmlContent, report, pdfOptions);

        // Salva PDF solo nella directory "reports"
        var pdfPath = Path.Combine("storage", "reports",
            report.ReportPeriodStart.Year.ToString(),
            report.ReportPeriodStart.Month.ToString("D2"),
            $"PolarDrive_Report_{report.Id}.pdf");

        Directory.CreateDirectory(Path.GetDirectoryName(pdfPath)!);
        await System.IO.File.WriteAllBytesAsync(pdfPath, pdfBytes);

        _logger.LogDebug("📄 Generated HTML and PDF files for report {ReportId}, PDF size: {Size} bytes",
            report.Id, pdfBytes.Length);
    }

    /// <summary>
    /// Retry con tracking risultati
    /// </summary>
    private async Task<RetryResults> ProcessRetries(PolarDriveDbContext db, DateTime now)
    {
        var results = new RetryResults();
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

        results.ProcessedCount = vehiclesToRetry.Count;

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

                    // ✅ USA ANALISI anche nei retry
                    await GenerateReportForVehicle(db, vehicleId, now);

                    _logger.LogInformation("✅ Retry successful for vehicle {VIN}", vehicle.Vin);
                    _retryCount[vehicleId] = 0; // Reset su successo
                    _lastReportAttempts[vehicleId] = now;
                    results.SuccessCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in retry for vehicle {VehicleId}", vehicleId);
                    _retryCount[vehicleId]++;
                    _lastReportAttempts[vehicleId] = now;
                    results.ErrorCount++;
                }

                // Verifica se ha superato il limite di retry
                if (_retryCount[vehicleId] > MAX_RETRIES_PER_VEHICLE)
                {
                    _logger.LogWarning("🚫 Vehicle {VehicleId} exceeded max retries ({MaxRetries}), stopping attempts",
                        vehicleId, MAX_RETRIES_PER_VEHICLE);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Usa ReportPeriodInfo condivisa dal namespace Production
    /// </summary>
    public async Task<ReportPeriodInfo> DetermineReportPeriod(PolarDriveDbContext db, int vehicleId)
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

        return new ReportPeriodInfo
        {
            Start = now.AddHours(-dataHours),
            End = now,
            DataHours = dataHours,
            AnalysisLevel = analysisLevel,
            MonitoringDays = monitoringPeriod.TotalDays
        };
    }

    #region Helper Methods

    /// <summary>
    /// Verifica disponibilità servizi all'avvio
    /// </summary>
    private async Task CheckServicesAvailability()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            // Test AI service
            var aiAvailable = await CheckAiServiceAvailability();
            _logger.LogInformation("🧠 PolarAi Service availability: {Available}", aiAvailable ? "Available" : "Not Available");

            // Test PDF services
            var pdfAvailable = await CheckFileGenerationServices();
            _logger.LogInformation("📄 PDF Generation services: {Available}", pdfAvailable ? "Available" : "Not Available");

            // Test VehicleDataService
            try
            {
                var vehicleDataService = scope.ServiceProvider.GetService<VehicleDataService>();
                _logger.LogInformation("🚗 VehicleDataService: {Available}", vehicleDataService != null ? "Available" : "Not Available");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ VehicleDataService not available");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Error checking services availability");
        }
    }

    /// <summary>
    /// Verifica se AI service è disponibile
    /// </summary>
    private async Task<bool> CheckAiServiceAvailability()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            var response = await client.GetAsync("http://localhost:11434/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifica se servizi di generazione file sono disponibili
    /// </summary>
    public async Task<bool> CheckFileGenerationServices()
    {
        try
        {
            // Test se Node.js/npx è disponibile per PDF generation
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            var npxPath = Path.Combine(programFiles, "nodejs", "npx.cmd");
            var nodeAvailable = File.Exists(npxPath);

            // Test AI service per HTML generation
            var aiAvailable = await CheckAiServiceAvailability();

            return nodeAvailable && aiAvailable;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Path per report development
    /// </summary>
    private string GetReportFilePath(Data.Entities.PdfReport report, string extension)
    {
        var fileName = $"PolarDrive_Report_{report.Id}.{extension}";

        if (extension == "html")
        {
            // HTML va in dev-reports
            return Path.Combine("storage", "dev-reports",
                report.ReportPeriodStart.Year.ToString(),
                report.ReportPeriodStart.Month.ToString("D2"),
                fileName);
        }
        else
        {
            // PDF e altri formati solo in reports
            return Path.Combine("storage", "reports",
                report.ReportPeriodStart.Year.ToString(),
                report.ReportPeriodStart.Month.ToString("D2"),
                fileName);
        }
    }

    /// <summary>
    /// Statistics con risultati dettagliati
    /// </summary>
    private async Task LogReportStatistics(PolarDriveDbContext db, FakeSchedulerResults results)
    {
        try
        {
            var totalReports = await db.PdfReports.CountAsync();
            var recentReports = await db.PdfReports
                .Where(r => r.GeneratedAt >= DateTime.UtcNow.AddHours(-1))
                .CountAsync();

            // Conta report fake
            var fakeReports = await db.PdfReports.CountAsync();
            var recentFakeReports = await db.PdfReports.CountAsync();
            var totalVehicleData = await db.VehiclesData.CountAsync();
            var recentData = await db.VehiclesData.CountAsync();

            var vehiclesWithRetries = _retryCount.Count(kv => kv.Value > 0);
            var vehiclesExceededRetries = _retryCount.Count(kv => kv.Value > MAX_RETRIES_PER_VEHICLE);

            var fetchingVehicles = await db.ClientVehicles.CountAsync(v => v.IsFetchingDataFlag);
            var activeContracts = await db.ClientVehicles.CountAsync(v => v.IsActiveFlag && v.IsFetchingDataFlag);
            var gracePeriodVehicles = await db.ClientVehicles.CountAsync(v => !v.IsActiveFlag && v.IsFetchingDataFlag);

            _logger.LogInformation("📈 FakeProductionScheduler Statistics:");
            _logger.LogInformation($"   Vehicles - Fetching: {fetchingVehicles}, Active Contracts: {activeContracts}, Grace Period: {gracePeriodVehicles}");
            _logger.LogInformation($"   Cycle Results - New: {results.NewReportsGenerated} success, {results.NewReportsErrors} errors");
            _logger.LogInformation($"   Cycle Results - Retries: {results.RetriesSuccessful}/{results.RetriesProcessed} successful");
            _logger.LogInformation($"   Total Reports: {totalReports} (Fake: {fakeReports})");
            _logger.LogInformation($"   Last Hour: {recentReports} total ({recentFakeReports} fake)");
            _logger.LogInformation($"   Total Vehicle Data: {totalVehicleData} (Last 10min: {recentData})");
            _logger.LogInformation($"   Vehicles with active retries: {vehiclesWithRetries}");
            _logger.LogInformation($"   Vehicles exceeded max retries: {vehiclesExceededRetries}");

            // Alert per grace period
            if (gracePeriodVehicles > 0)
            {
                _logger.LogWarning("⏳ Grace Period Alert: {GracePeriodCount} vehicles with terminated contracts still sending data",
                    gracePeriodVehicles);
            }

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

    #endregion

    #region Public Methods

    /// <summary>
    /// Metodo pubblico per forzare un reset dei retry (utile per testing)
    /// </summary>
    public void ResetRetryCounters()
    {
        _retryCount.Clear();
        _lastReportAttempts.Clear();
        _logger.LogInformation("🔄 Fake scheduler retry counters reset manually");
    }

    /// <summary>
    /// ✅ NUOVO: Metodo per forzare generazione report manuale
    /// </summary>
    public async Task<bool> ForceReportGenerationAsync(int? vehicleId = null)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            if (vehicleId.HasValue)
            {
                await GenerateReportForVehicle(db, vehicleId.Value, DateTime.UtcNow);
                _logger.LogInformation("✅ Manual report generated for vehicle {VehicleId}", vehicleId.Value);
            }
            else
            {
                var results = await GenerateReportsForAllVehicles(db, DateTime.UtcNow);
                _logger.LogInformation("✅ Manual reports generated for all vehicles: {Success}/{Total}",
                    results.SuccessCount, results.TotalProcessed);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to force report generation");
            return false;
        }
    }

    /// <summary>
    /// ✅ NUOVO: Ottieni statistiche del fake scheduler
    /// </summary>
    public async Task<FakeSchedulerStatistics> GetStatisticsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            var stats = new FakeSchedulerStatistics
            {
                CheckTime = DateTime.UtcNow,
                IsRunning = !_env.IsDevelopment() ? false : true,
                VehiclesWithRetries = _retryCount.Count(kv => kv.Value > 0),
                VehiclesExceededRetries = _retryCount.Count(kv => kv.Value > MAX_RETRIES_PER_VEHICLE),
                TotalReportsGenerated = await db.PdfReports.CountAsync(),
                RecentReportsGenerated = await db.PdfReports.CountAsync(r => r.GeneratedAt >= DateTime.UtcNow.AddHours(-1)),

                FetchingVehicles = await db.ClientVehicles
                    .CountAsync(v => v.IsFetchingDataFlag),
                ActiveContracts = await db.ClientVehicles
                    .CountAsync(v => v.IsActiveFlag && v.IsFetchingDataFlag),
                GracePeriodVehicles = await db.ClientVehicles
                    .CountAsync(v => !v.IsActiveFlag && v.IsFetchingDataFlag),

                LastReportAttempts = _lastReportAttempts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                RetryCounters = _retryCount.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error getting fake scheduler statistics");
            return new FakeSchedulerStatistics
            {
                CheckTime = DateTime.UtcNow,
                IsRunning = false,
                ErrorMessage = ex.Message
            };
        }
    }
    #endregion
}

// Tracking risultati

/// <summary>
/// Risultati di un ciclo completo del fake scheduler
/// </summary>
public class FakeSchedulerResults
{
    public int NewReportsGenerated { get; set; }
    public int NewReportsErrors { get; set; }
    public int RetriesProcessed { get; set; }
    public int RetriesSuccessful { get; set; }
    public int RetriesFailed { get; set; }
}

/// <summary>
/// Risultati della generazione report per tutti i veicoli
/// </summary>
public class GenerationResults
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public List<int> SuccessfulVehicles { get; set; } = [];
    public List<int> FailedVehicles { get; set; } = [];
}

/// <summary>
/// Risultati dei retry
/// </summary>
public class RetryResults
{
    public int ProcessedCount { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
}

/// <summary>
/// Statistiche del fake scheduler
/// </summary>
public class FakeSchedulerStatistics
{
    public DateTime CheckTime { get; set; }
    public bool IsRunning { get; set; }
    public int VehiclesWithRetries { get; set; }
    public int VehiclesExceededRetries { get; set; }
    public int TotalReportsGenerated { get; set; }
    public int RecentReportsGenerated { get; set; }
    public int FetchingVehicles { get; set; }
    public int ActiveContracts { get; set; }
    public int GracePeriodVehicles { get; set; }
    public Dictionary<int, DateTime> LastReportAttempts { get; set; } = [];
    public Dictionary<int, int> RetryCounters { get; set; } = [];
    public string? ErrorMessage { get; set; }
}