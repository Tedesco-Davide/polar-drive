using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.PolarAiReports;

namespace PolarDrive.WebApi.Production;

/// <summary>
/// Scheduler per produzione - gestisce i task automatici con analisi progressiva e retry logic
/// AGGIORNATO per usare il nuovo sistema di analisi progressiva AI
/// </summary>
public class ProductionScheduler(IServiceProvider serviceProvider, ILogger<ProductionScheduler> logger, IWebHostEnvironment env) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ProductionScheduler> _logger = logger;
    private readonly IWebHostEnvironment _env = env;
    private readonly Dictionary<int, DateTime> _lastReportAttempts = new();
    private readonly Dictionary<int, int> _retryCount = new();
    private const int MAX_RETRIES_PER_VEHICLE = 5; // Più retry in produzione
    private const int RETRY_DELAY_HOURS = 6; // Retry ogni 6 ore in produzione

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_env.IsDevelopment())
        {
            _logger.LogInformation("ProductionScheduler: Skipping in Development mode");
            return;
        }

        _logger.LogInformation("🏭 ProductionScheduler: Starting PRODUCTION progressive schedulers");
        _logger.LogInformation("📋 Progressive Report generation: Daily at 02:00, Weekly on Mondays, Monthly on 1st day");
        _logger.LogInformation("🧠 Using Progressive AI Analysis for all reports");
        _logger.LogInformation("🔄 Retry logic: Up to {MaxRetries} retries per vehicle with {RetryDelay}h delays",
            MAX_RETRIES_PER_VEHICLE, RETRY_DELAY_HOURS);

        // Avvia i task paralleli
        var dailyReportTask = RunDailyProgressiveReportScheduler(stoppingToken);
        var weeklyReportTask = RunWeeklyProgressiveReportScheduler(stoppingToken);
        var monthlyReportTask = RunMonthlyProgressiveReportScheduler(stoppingToken);
        var retryTask = RunProgressiveRetryScheduler(stoppingToken);

        await Task.WhenAll(dailyReportTask, weeklyReportTask, monthlyReportTask, retryTask);
    }

    /// <summary>
    /// Scheduler per i report giornalieri progressivi (ogni giorno alle 02:00)
    /// </summary>
    private async Task RunDailyProgressiveReportScheduler(CancellationToken stoppingToken)
    {
        // Aspetta fino alle 2:00 del mattino del primo avvio
        await WaitUntilTargetTime(2, 0);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("🌅 ProductionScheduler: Starting daily progressive report generation");

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

                await ProcessDailyProgressiveReports(db);

                _logger.LogInformation("✅ ProductionScheduler: Completed daily progressive report generation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ProductionScheduler: Error in daily report generation cycle");
            }

            // Aspetta 24 ore (alle 02:00 del giorno successivo)
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    /// <summary>
    /// Scheduler per i report settimanali progressivi (ogni lunedì alle 03:00)
    /// </summary>
    private async Task RunWeeklyProgressiveReportScheduler(CancellationToken stoppingToken)
    {
        // Aspetta fino alle 3:00 del primo lunedì
        await WaitUntilTargetDayAndTime(DayOfWeek.Monday, 3, 0);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("📅 ProductionScheduler: Starting weekly progressive report generation");

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

                await ProcessWeeklyProgressiveReports(db);

                _logger.LogInformation("✅ ProductionScheduler: Completed weekly progressive report generation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ProductionScheduler: Error in weekly report generation cycle");
            }

            // Aspetta 7 giorni
            await Task.Delay(TimeSpan.FromDays(7), stoppingToken);
        }
    }

    /// <summary>
    /// Scheduler per i report mensili progressivi (primo giorno del mese alle 04:00)
    /// </summary>
    private async Task RunMonthlyProgressiveReportScheduler(CancellationToken stoppingToken)
    {
        // Aspetta fino alle 4:00 del primo giorno del mese
        await WaitUntilFirstDayOfMonthAndTime(4, 0);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Genera report solo il primo giorno del mese alle 04:00
                if (now.Day == 1)
                {
                    _logger.LogInformation("🗓️ ProductionScheduler: Starting monthly progressive report generation");

                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

                    await ProcessMonthlyProgressiveReports(db);

                    _logger.LogInformation("✅ ProductionScheduler: Completed monthly progressive report generation");
                }
                else
                {
                    _logger.LogInformation("⏭️ ProductionScheduler: Not first day of month, skipping monthly report generation");
                }

                // Log statistiche
                await LogProductionStatistics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ProductionScheduler: Error in monthly report generation cycle");
            }

            // Aspetta fino al primo giorno del mese successivo
            await WaitUntilFirstDayOfMonthAndTime(4, 0);
        }
    }

    /// <summary>
    /// Scheduler per i retry progressivi (controlla ogni 6 ore)
    /// </summary>
    private async Task RunProgressiveRetryScheduler(CancellationToken stoppingToken)
    {
        // Offset per non coincidere con gli altri scheduler
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

                await ProcessProgressiveRetries(db);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ProductionScheduler: Error in progressive retry cycle");
            }

            // Controllo retry ogni 6 ore
            await Task.Delay(TimeSpan.FromHours(RETRY_DELAY_HOURS), stoppingToken);
        }
    }

    /// <summary>
    /// ✅ NUOVO: Processa la generazione di report giornalieri progressivi
    /// </summary>
    private async Task ProcessDailyProgressiveReports(PolarDriveDbContext db)
    {
        var now = DateTime.UtcNow;
        var yesterday = now.AddDays(-1);

        _logger.LogInformation("🌅 Generating daily progressive reports for {Date}", yesterday.ToString("yyyy-MM-dd"));

        var activeVehicles = await GetActiveVehiclesWithData(db, yesterday.AddDays(-1), now);

        if (!activeVehicles.Any())
        {
            _logger.LogInformation("ℹ️ No vehicles with data for daily progressive reports");
            return;
        }

        _logger.LogInformation("🚗 Found {Count} vehicles for daily progressive reports", activeVehicles.Count);

        var successCount = 0;
        var errorCount = 0;

        foreach (var vehicle in activeVehicles)
        {
            try
            {
                await GenerateProgressiveReportForVehicleProduction(db, vehicle.Id, "Analisi Giornaliera", 24);
                successCount++;
                _lastReportAttempts[vehicle.Id] = DateTime.UtcNow;
                _retryCount[vehicle.Id] = 0;

                _logger.LogInformation("✅ Daily progressive report generated for vehicle {VIN}", vehicle.Vin);
            }
            catch (Exception ex)
            {
                errorCount++;
                _retryCount[vehicle.Id] = _retryCount.GetValueOrDefault(vehicle.Id, 0) + 1;
                _lastReportAttempts[vehicle.Id] = DateTime.UtcNow;

                _logger.LogError(ex, "❌ Error generating daily progressive report for vehicle {VIN}", vehicle.Vin);
            }

            // Pausa tra veicoli per non sovraccaricare il sistema
            await Task.Delay(TimeSpan.FromMinutes(2));
        }

        _logger.LogInformation("📊 Daily progressive report generation completed: {Success} success, {Errors} errors",
            successCount, errorCount);
    }

    /// <summary>
    /// ✅ NUOVO: Processa la generazione di report settimanali progressivi
    /// </summary>
    private async Task ProcessWeeklyProgressiveReports(PolarDriveDbContext db)
    {
        var now = DateTime.UtcNow;
        var startOfLastWeek = now.AddDays(-7 - (int)now.DayOfWeek + 1); // Lunedì scorso
        var endOfLastWeek = startOfLastWeek.AddDays(6); // Domenica scorsa

        _logger.LogInformation("📅 Generating weekly progressive reports for period: {Start} to {End}",
            startOfLastWeek.ToString("yyyy-MM-dd"), endOfLastWeek.ToString("yyyy-MM-dd"));

        var activeVehicles = await GetActiveVehiclesWithData(db, startOfLastWeek, endOfLastWeek);

        if (!activeVehicles.Any())
        {
            _logger.LogInformation("ℹ️ No vehicles with data for weekly progressive reports");
            return;
        }

        _logger.LogInformation("🚗 Found {Count} vehicles for weekly progressive reports", activeVehicles.Count);

        var successCount = 0;
        var errorCount = 0;

        foreach (var vehicle in activeVehicles)
        {
            try
            {
                await GenerateProgressiveReportForVehicleProduction(db, vehicle.Id, "Deep Dive Settimanale", 168); // 7 giorni
                successCount++;
                _lastReportAttempts[vehicle.Id] = DateTime.UtcNow;
                _retryCount[vehicle.Id] = 0;

                _logger.LogInformation("✅ Weekly progressive report generated for vehicle {VIN}", vehicle.Vin);
            }
            catch (Exception ex)
            {
                errorCount++;
                _retryCount[vehicle.Id] = _retryCount.GetValueOrDefault(vehicle.Id, 0) + 1;
                _lastReportAttempts[vehicle.Id] = DateTime.UtcNow;

                _logger.LogError(ex, "❌ Error generating weekly progressive report for vehicle {VIN}", vehicle.Vin);
            }

            // Pausa più lunga tra veicoli per report settimanali
            await Task.Delay(TimeSpan.FromMinutes(5));
        }

        _logger.LogInformation("📊 Weekly progressive report generation completed: {Success} success, {Errors} errors",
            successCount, errorCount);
    }

    /// <summary>
    /// ✅ AGGIORNATO: Processa la generazione di report mensili con analisi progressiva
    /// </summary>
    private async Task ProcessMonthlyProgressiveReports(PolarDriveDbContext db)
    {
        var now = DateTime.UtcNow;
        var startOfLastMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
        var endOfLastMonth = startOfLastMonth.AddMonths(1).AddDays(-1);

        _logger.LogInformation("🗓️ Generating monthly progressive reports for period: {Start} to {End}",
            startOfLastMonth.ToString("yyyy-MM-dd"), endOfLastMonth.ToString("yyyy-MM-dd"));

        var activeVehicles = await GetActiveVehiclesWithData(db, startOfLastMonth, endOfLastMonth);

        if (!activeVehicles.Any())
        {
            _logger.LogInformation("ℹ️ No vehicles with data for monthly progressive reports");
            return;
        }

        _logger.LogInformation("🚗 Found {Count} vehicles for monthly progressive reports", activeVehicles.Count);

        var successCount = 0;
        var errorCount = 0;

        foreach (var vehicle in activeVehicles)
        {
            try
            {
                await GenerateProgressiveReportForVehicleProduction(db, vehicle.Id, "Analisi Comprensiva Mensile", 720); // 30 giorni
                successCount++;
                _lastReportAttempts[vehicle.Id] = DateTime.UtcNow;
                _retryCount[vehicle.Id] = 0;

                _logger.LogInformation("✅ Monthly progressive report generated for vehicle {VIN}", vehicle.Vin);
            }
            catch (Exception ex)
            {
                errorCount++;
                _retryCount[vehicle.Id] = _retryCount.GetValueOrDefault(vehicle.Id, 0) + 1;
                _lastReportAttempts[vehicle.Id] = DateTime.UtcNow;

                _logger.LogError(ex, "❌ Error generating monthly progressive report for vehicle {VIN}", vehicle.Vin);
            }

            // Pausa più lunga tra veicoli per report mensili
            await Task.Delay(TimeSpan.FromMinutes(10));
        }

        _logger.LogInformation("📊 Monthly progressive report generation completed: {Success} success, {Errors} errors",
            successCount, errorCount);
    }

    /// <summary>
    /// ✅ NUOVO: Genera report progressivo per un singolo veicolo in produzione
    /// </summary>
    private async Task GenerateProgressiveReportForVehicleProduction(PolarDriveDbContext db, int vehicleId, string analysisLevel, int dataHours)
    {
        var vehicle = await db.ClientVehicles.FindAsync(vehicleId);
        if (vehicle == null) return;

        var now = DateTime.UtcNow;

        _logger.LogInformation("🧠 Generating {AnalysisLevel} for vehicle {VIN} ({DataHours}h of data)",
            analysisLevel, vehicle.Vin, dataHours);

        // Genera insights progressivi usando PolarAiReportGenerator
        var aiGenerator = new PolarAiReportGenerator(db);
        var progressiveInsights = await aiGenerator.GenerateProgressiveInsightsAsync(vehicleId);

        if (string.IsNullOrWhiteSpace(progressiveInsights))
        {
            _logger.LogWarning("⚠️ No progressive insights generated for vehicle {VIN}", vehicle.Vin);
            throw new InvalidOperationException($"No progressive insights generated for vehicle {vehicle.Vin}");
        }

        // Determina il periodo del report
        var reportPeriod = DetermineProductionReportPeriod(analysisLevel, dataHours);

        // Crea record del report con metadati progressivi
        var progressiveReport = new Data.Entities.PdfReport
        {
            ClientVehicleId = vehicleId,
            ClientCompanyId = vehicle.ClientCompanyId,
            ReportPeriodStart = reportPeriod.Start,
            ReportPeriodEnd = reportPeriod.End,
            GeneratedAt = now,
            Notes = $"[PRODUCTION-PROGRESSIVE-{analysisLevel.Replace(" ", "")}] Generated with {dataHours}h analysis window"
        };

        db.PdfReports.Add(progressiveReport);
        await db.SaveChangesAsync();

        // Genera HTML con insights progressivi
        var htmlService = new HtmlReportService(db);
        var htmlOptions = new HtmlReportOptions
        {
            ShowDetailedStats = true,
            ShowRawData = false,
            ReportType = $"🧠 {analysisLevel} - Production",
            AdditionalCss = GetProductionProgressiveStyles()
        };

        var htmlContent = await htmlService.GenerateHtmlReportAsync(progressiveReport, progressiveInsights, htmlOptions);

        // Salva HTML
        var htmlPath = GetProductionReportFilePath(progressiveReport, "html");
        var htmlDirectory = Path.GetDirectoryName(htmlPath);
        if (!string.IsNullOrEmpty(htmlDirectory))
        {
            Directory.CreateDirectory(htmlDirectory);
        }
        await File.WriteAllTextAsync(htmlPath, htmlContent);

        // Genera PDF con stili production
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
            <div style='font-size: 10px; width: 100%; text-align: center; color: #004E92; border-bottom: 1px solid #004E92; padding-bottom: 5px;'>
                <span>🏭 PolarDrive {analysisLevel} - {vehicle.Vin} - {now:yyyy-MM-dd HH:mm}</span>
            </div>",
            FooterTemplate = @"
            <div style='font-size: 10px; width: 100%; text-align: center; color: #666; border-top: 1px solid #ccc; padding-top: 5px;'>
                <span>Pagina <span class='pageNumber'></span> di <span class='totalPages'></span> | Production Analysis</span>
            </div>"
        };

        var pdfBytes = await pdfService.ConvertHtmlToPdfAsync(htmlContent, progressiveReport, pdfOptions);

        // Salva PDF
        var pdfPath = GetProductionReportFilePath(progressiveReport, "pdf");
        var pdfDirectory = Path.GetDirectoryName(pdfPath);
        if (!string.IsNullOrEmpty(pdfDirectory))
        {
            Directory.CreateDirectory(pdfDirectory);
        }
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);

        _logger.LogInformation("✅ Production progressive report generated for {VIN}: ReportId {ReportId}, Level: {Level}, Size: {Size} bytes",
            vehicle.Vin, progressiveReport.Id, analysisLevel, pdfBytes.Length);
    }

    /// <summary>
    /// ✅ NUOVO: Gestisce i retry con analisi progressiva
    /// </summary>
    private async Task ProcessProgressiveRetries(PolarDriveDbContext db)
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
            _logger.LogInformation("🔄 No vehicles need progressive retry at this time");
            return;
        }

        _logger.LogInformation("🔄 Processing {Count} vehicle progressive retries", vehiclesToRetry.Count);

        foreach (var vehicleId in vehiclesToRetry)
        {
            try
            {
                var vehicle = await db.ClientVehicles.FindAsync(vehicleId);
                if (vehicle == null) continue;

                _logger.LogInformation("🔄 Progressive retry #{RetryNum} for vehicle {VIN}",
                    _retryCount[vehicleId], vehicle.Vin);

                // Per i retry, usa analisi settimanale (compromesso ragionevole)
                await GenerateProgressiveReportForVehicleProduction(db, vehicleId, $"Retry-{_retryCount[vehicleId]}", 168);

                _logger.LogInformation("✅ Progressive retry successful for vehicle {VIN}", vehicle.Vin);
                _retryCount[vehicleId] = 0; // Reset on success

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

            // Pausa lunga tra retry in produzione
            await Task.Delay(TimeSpan.FromMinutes(15));
        }
    }

    #region Helper Methods

    /// <summary>
    /// ✅ NUOVO: Helper per ottenere veicoli attivi con dati nel periodo specificato
    /// </summary>
    private async Task<List<Data.Entities.ClientVehicle>> GetActiveVehiclesWithData(PolarDriveDbContext db, DateTime startDate, DateTime endDate)
    {
        return await db.ClientVehicles
            .Include(v => v.ClientCompany)
            .Where(v => v.IsActiveFlag)
            .Where(v => db.VehiclesData.Any(vd =>
                vd.VehicleId == v.Id &&
                vd.Timestamp >= startDate &&
                vd.Timestamp <= endDate))
            .ToListAsync();
    }

    /// <summary>
    /// ✅ NUOVO: Determina periodo del report per produzione
    /// </summary>
    private ProductionReportPeriodInfo DetermineProductionReportPeriod(string analysisLevel, int dataHours)
    {
        var now = DateTime.UtcNow;

        return analysisLevel switch
        {
            "Analisi Giornaliera" => new ProductionReportPeriodInfo
            {
                Start = now.AddDays(-1).Date,
                End = now.Date.AddDays(-1).AddHours(23).AddMinutes(59)
            },
            "Deep Dive Settimanale" => new ProductionReportPeriodInfo
            {
                Start = now.AddDays(-7 - (int)now.DayOfWeek + 1).Date, // Lunedì scorso
                End = now.AddDays(-7 - (int)now.DayOfWeek + 1).Date.AddDays(6).AddHours(23).AddMinutes(59) // Domenica scorsa
            },
            "Analisi Comprensiva Mensile" => new ProductionReportPeriodInfo
            {
                Start = new DateTime(now.Year, now.Month, 1).AddMonths(-1),
                End = new DateTime(now.Year, now.Month, 1).AddDays(-1).AddHours(23).AddMinutes(59)
            },
            _ => new ProductionReportPeriodInfo
            {
                Start = now.AddHours(-dataHours),
                End = now
            }
        };
    }

    /// <summary>
    /// ✅ NUOVO: Stili CSS per report production
    /// </summary>
    private string GetProductionProgressiveStyles()
    {
        return @"
        .production-badge {
            background: linear-gradient(135deg, #004E92 0%, #000428 100%);
            color: white;
            padding: 8px 16px;
            border-radius: 25px;
            font-size: 12px;
            font-weight: bold;
            display: inline-block;
            margin: 10px 15px 10px 0;
            box-shadow: 0 4px 8px rgba(0,0,0,0.3);
        }
        
        .production-badge::before {
            content: '🏭 PRODUCTION • ';
        }
        
        .progressive-production {
            background: linear-gradient(135deg, rgba(0, 78, 146, 0.1) 0%, rgba(102, 126, 234, 0.1) 100%);
            border: 2px solid #004E92;
            padding: 20px;
            margin: 20px 0;
            border-radius: 12px;
        }
        
        .progressive-production::before {
            content: '🏭 Production Environment • 🧠 Progressive AI Analysis • ';
            color: #004E92;
            font-weight: bold;
            font-size: 14px;
        }
        
        .ai-insights {
            border-left: 5px solid #004E92;
            background: linear-gradient(135deg, rgba(0, 78, 146, 0.05) 0%, rgba(102, 126, 234, 0.05) 100%);
            padding: 25px;
            border-radius: 0 12px 12px 0;
        }
        
        .ai-insights::before {
            content: '🧠 Analisi Progressiva AI Production • ';
            background: linear-gradient(135deg, #004E92 0%, #667eea 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
            font-weight: bold;
            font-size: 14px;
        }
        
        .production-info {
            background: #e8f4fd;
            border: 1px solid #004E92;
            padding: 15px;
            border-radius: 8px;
            margin: 20px 0;
            font-size: 13px;
            color: #004E92;
        }
        
        .production-info::before {
            content: '🏭 Production Schedule: ';
            font-weight: bold;
        }";
    }

    /// <summary>
    /// ✅ NUOVO: Path per report production
    /// </summary>
    private string GetProductionReportFilePath(Data.Entities.PdfReport report, string extension)
    {
        var outputDir = Path.Combine("storage", "production-reports",
            report.ReportPeriodStart.Year.ToString(),
            report.ReportPeriodStart.Month.ToString("D2"));

        return Path.Combine(outputDir, $"PolarDrive_Production_Progressive_{report.Id}.{extension}");
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
    /// ✅ NUOVO: Aspetta fino al giorno specifico e orario target
    /// </summary>
    private async Task WaitUntilTargetDayAndTime(DayOfWeek targetDay, int targetHour, int targetMinute)
    {
        var now = DateTime.UtcNow;
        var daysUntilTarget = ((int)targetDay - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilTarget == 0 && now.Hour >= targetHour && now.Minute >= targetMinute)
            daysUntilTarget = 7; // Next week

        var target = now.Date.AddDays(daysUntilTarget).AddHours(targetHour).AddMinutes(targetMinute);
        var delay = target - now;

        _logger.LogInformation("⏰ ProductionScheduler: Waiting until {Day} {Target} (in {Delay})",
            targetDay, target.ToString("yyyy-MM-dd HH:mm"), delay.ToString(@"d\.hh\:mm"));

        await Task.Delay(delay);
    }

    /// <summary>
    /// ✅ NUOVO: Aspetta fino al primo giorno del mese
    /// </summary>
    private async Task WaitUntilFirstDayOfMonthAndTime(int targetHour, int targetMinute)
    {
        var now = DateTime.UtcNow;
        var target = new DateTime(now.Year, now.Month, 1).AddHours(targetHour).AddMinutes(targetMinute);

        if (target <= now)
            target = target.AddMonths(1);

        var delay = target - now;

        _logger.LogInformation("⏰ ProductionScheduler: Waiting until 1st of month {Target} (in {Delay})",
            target.ToString("yyyy-MM-dd HH:mm"), delay.ToString(@"d\.hh\:mm"));

        await Task.Delay(delay);
    }

    /// <summary>
    /// ✅ AGGIORNATO: Log statistiche con info progressive
    /// </summary>
    private async Task LogProductionStatistics()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            var totalReports = await db.PdfReports.CountAsync();

            // ✅ NUOVO: Conta report progressivi production
            var progressiveProductionReports = await db.PdfReports
                .Where(r => r.Notes != null && r.Notes.Contains("[PRODUCTION-PROGRESSIVE"))
                .CountAsync();

            var monthlyReports = await db.PdfReports
                .Where(r => r.GeneratedAt >= DateTime.UtcNow.AddDays(-30))
                .CountAsync();

            var weeklyReports = await db.PdfReports
                .Where(r => r.GeneratedAt >= DateTime.UtcNow.AddDays(-7))
                .CountAsync();

            var dailyReports = await db.PdfReports
                .Where(r => r.GeneratedAt >= DateTime.UtcNow.AddDays(-1))
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

            _logger.LogInformation("📈 ProductionScheduler Progressive Statistics:");
            _logger.LogInformation($"   Total Reports: {totalReports} (Progressive Production: {progressiveProductionReports})");
            _logger.LogInformation($"   Recent Reports - Daily: {dailyReports}, Weekly: {weeklyReports}, Monthly: {monthlyReports}");
            _logger.LogInformation($"   Active Vehicles: {totalVehicles}");
            _logger.LogInformation($"   Vehicle Data: {totalData} total (Last 24h: {recentData})");
            _logger.LogInformation($"   Vehicles with retries: {vehiclesWithRetries}");
            _logger.LogInformation($"   Vehicles exceeded retries: {vehiclesExceededRetries}");

            // ✅ NUOVO: Log dettagli retry se presenti
            if (vehiclesWithRetries > 0)
            {
                _logger.LogInformation("🔄 Production retry details:");
                foreach (var (vehicleId, retryCount) in _retryCount.Where(kv => kv.Value > 0))
                {
                    var lastAttempt = _lastReportAttempts.GetValueOrDefault(vehicleId, DateTime.MinValue);
                    var status = retryCount > MAX_RETRIES_PER_VEHICLE ? "EXCEEDED" : "PENDING";
                    var nextRetryIn = lastAttempt.AddHours(RETRY_DELAY_HOURS) - DateTime.UtcNow;

                    _logger.LogInformation($"   Vehicle {vehicleId}: {retryCount} retries, last: {lastAttempt:yyyy-MM-dd HH:mm}, status: {status}, next retry in: {nextRetryIn.TotalHours:F1}h");
                }
            }

            // ✅ NUOVO: Log prossimi eventi scheduled
            var now = DateTime.UtcNow;
            var nextDaily = new DateTime(now.Year, now.Month, now.Day, 2, 0, 0);
            if (nextDaily <= now) nextDaily = nextDaily.AddDays(1);

            var nextWeekly = now.Date.AddDays(((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7).AddHours(3);
            if (nextWeekly <= now) nextWeekly = nextWeekly.AddDays(7);

            var nextMonthly = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddHours(4);

            _logger.LogInformation("⏰ Next Scheduled Events:");
            _logger.LogInformation($"   Daily Progressive Reports: {nextDaily:yyyy-MM-dd HH:mm} (in {(nextDaily - now).TotalHours:F1}h)");
            _logger.LogInformation($"   Weekly Progressive Reports: {nextWeekly:yyyy-MM-dd HH:mm} (in {(nextWeekly - now).TotalDays:F1} days)");
            _logger.LogInformation($"   Monthly Progressive Reports: {nextMonthly:yyyy-MM-dd HH:mm} (in {(nextMonthly - now).TotalDays:F1} days)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ ProductionScheduler: Failed to log progressive statistics");
        }
    }

    /// <summary>
    /// ✅ NUOVO: Metodo pubblico per forzare un reset dei retry (utile per manutenzione)
    /// </summary>
    public void ResetRetryCounters()
    {
        _retryCount.Clear();
        _lastReportAttempts.Clear();
        _logger.LogInformation("🔄 Production retry counters reset manually");
    }

    /// <summary>
    /// ✅ NUOVO: Metodo pubblico per forzare la generazione di un report progressivo specifico
    /// </summary>
    public async Task<bool> ForceProgressiveReportAsync(int vehicleId, string analysisLevel = "Manual Generation")
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            var dataHours = analysisLevel switch
            {
                "Manual Generation" => 168, // 1 settimana
                "Quick Test" => 24,         // 1 giorno
                "Deep Analysis" => 720,     // 1 mese
                _ => 168
            };

            await GenerateProgressiveReportForVehicleProduction(db, vehicleId, analysisLevel, dataHours);

            _logger.LogInformation("✅ Manual progressive report forced for vehicle {VehicleId}", vehicleId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to force progressive report for vehicle {VehicleId}", vehicleId);
            return false;
        }
    }

    #endregion
}

/// <summary>
/// ✅ NUOVO: Classe helper per info periodo report production
/// </summary>
public class ProductionReportPeriodInfo
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}