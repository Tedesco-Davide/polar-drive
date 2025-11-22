using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.PolarAiReports;
using PolarDrive.WebApi.Scheduler;
using static PolarDrive.WebApi.Constants.CommonConstants;
using Microsoft.Extensions.Options;
using PolarDrive.WebApi.Helpers;

namespace PolarDrive.WebApi.Services
{
    public interface IReportGenerationService
    {
        Task<SchedulerResults> ProcessScheduledReportsAsync(ScheduleType scheduleType, CancellationToken stoppingToken = default);
        Task<RetryResults> ProcessRetriesAsync(CancellationToken stoppingToken = default);
        Task<bool> GenerateSingleReportAsync(int companyId, int vehicleId, DateTime periodStart, DateTime periodEnd, bool isRegeneration = false, int? existingReportId = null);
        Task<int> RecoverStaleProcessingReportsAsync(CancellationToken stoppingToken = default);
    }

    public class ReportGenerationService(IServiceProvider serviceProvider, PolarDriveLogger logger, IWebHostEnvironment env) : IReportGenerationService
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly PolarDriveLogger _logger = logger;
        private readonly IWebHostEnvironment _env = env;
        private readonly Dictionary<int, DateTime> _lastReportAttempts = [];
        private readonly Dictionary<int, int> _retryCount = [];

        public async Task<SchedulerResults> ProcessScheduledReportsAsync(
            ScheduleType scheduleType,
            CancellationToken stoppingToken = default)
        {
            var results = new SchedulerResults();

            var recoveredCount = await RecoverStaleProcessingReportsAsync(stoppingToken);
            if (recoveredCount > 0) 
            {
                _ = _logger.Info(
                    "ReportGenerationService.ProcessScheduledReportsAsync", $"🔧 Recovered {recoveredCount} stale reports before starting {scheduleType}"
                );
            }

            var now = DateTime.Now;

            if (!ShouldGenerateReports(scheduleType, now))
            {
                _ = _logger.Debug("⏰ Not time for {ScheduleType} reports", scheduleType.ToString());
                return results;
            }

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            var vehicles = await GetVehiclesToProcess(db, scheduleType);
            if (!vehicles.Any())
            {
                _ = _logger.Info("📭 No vehicles to process for {ScheduleType}", scheduleType.ToString());
                return results;
            }

            var activeCount = vehicles.Count(v => v.IsActiveFlag);
            var graceCount = vehicles.Count - activeCount;

            _ = _logger.Info(
                "ReportGenerationService.ProcessScheduledReportsAsync",
                $"📊 {scheduleType}: Total={vehicles.Count}, Active={activeCount}, Grace={graceCount}"
            );

            if (graceCount > 0)
                _ = _logger.Warning("⏳ {Count} vehicles in grace period still generating reports", graceCount.ToString());

            foreach (var v in vehicles)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    // 1) Prendo la fine dell'ultimo report (se esiste)
                    var lastReportEnd = await db.PdfReports
                        .Where(r => r.VehicleId == v.Id)
                        .OrderByDescending(r => r.ReportPeriodEnd)
                        .Select(r => (DateTime?)r.ReportPeriodEnd)
                        .FirstOrDefaultAsync(stoppingToken);

                    // 2) SEMPRE 720H - FINESTRA MENSILE UNIFICATA
                    var start = lastReportEnd ?? now.AddHours(-MONTHLY_HOURS_THRESHOLD);
                    var end = now;

                    _ = _logger.Debug(
                        "ReportGenerationService.ProcessScheduledReportsAsync",
                        $"🔍 DEBUG: Vehicle {v.Vin} - Start: {start}, End: {end}, Now: {now}"
                    );

                    // 3) Infine genero con questi parametri
                    var analysisType = GetAnalysisType(scheduleType);
                    await GenerateReportForVehicle(db, v.Id, analysisType, start, end);

                    results.SuccessCount++;
                    _lastReportAttempts[v.Id] = now;
                    _retryCount[v.Id] = 0;
                }
                catch (Exception ex)
                {
                    _ = _logger.Error(ex.ToString(), "❌ Error report for vehicle {VIN}", v.Vin);
                    results.ErrorCount++;
                    _lastReportAttempts[v.Id] = now;
                    _retryCount[v.Id] = _retryCount.GetValueOrDefault(v.Id) + 1;
                }

                if (!_env.IsDevelopment() && scheduleType != ScheduleType.Development)
                    await Task.Delay(TimeSpan.FromMinutes(VEHICLE_DELAY_MINUTES), stoppingToken);
            }

            return results;
        }

        public async Task<RetryResults> ProcessRetriesAsync(CancellationToken stoppingToken = default)
        {
            var results = new RetryResults();
            var now = DateTime.Now;
            var threshold = _env.IsDevelopment()
                ? TimeSpan.FromMinutes(DEV_RETRY_MINUTES)
                : TimeSpan.FromHours(PROD_RETRY_HOURS);

            var toRetry = _retryCount
                .Where(kvp => kvp.Value > 0 && kvp.Value <= MAX_RETRIES)
                .Where(kvp => now - _lastReportAttempts.GetValueOrDefault(kvp.Key, DateTime.MinValue) >= threshold)
                .Select(kvp => kvp.Key)
                .ToList();

            results.ProcessedCount = toRetry.Count;

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            foreach (var id in toRetry)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    var veh = await db.ClientVehicles.FindAsync(id);
                    if (veh == null) continue;

                    _ = _logger.Info(
                        "ReportGenerationService.ProcessRetriesAsync",
                        $"🔄 Retry {_retryCount[id]}/{MAX_RETRIES} for {veh.Vin}"
                    );

                    var lastReportEnd = await db.PdfReports
                        .Where(r => r.VehicleId == id)
                        .OrderByDescending(r => r.ReportPeriodEnd)
                        .Select(r => (DateTime?)r.ReportPeriodEnd)
                        .FirstOrDefaultAsync(stoppingToken);

                    var start = lastReportEnd ?? now.AddHours(-MONTHLY_HOURS_THRESHOLD);
                    var end = now;
                    var analysisType = GetAnalysisType(ScheduleType.Monthly);

                    await GenerateReportForVehicle(db, id, analysisType, start, end);

                    results.SuccessCount++;
                    _retryCount[id] = 0;
                    _lastReportAttempts[id] = now;
                }
                catch (Exception ex)
                {
                    _ = _logger.Error(ex.ToString(), "❌ Retry failed for {VehicleId}", id.ToString());
                    results.ErrorCount++;
                    _retryCount[id]++;
                    _lastReportAttempts[id] = now;
                    if (_retryCount[id] > MAX_RETRIES)
                        _ = _logger.Warning("🚫 {VehicleId} exceeded max retries", id.ToString());
                }
            }

            return results;
        }

        /// <summary>
        /// Permette rigenerazione di un PDF report singolarmente
        /// </summary>
        public async Task<bool> GenerateSingleReportAsync(
            int companyId,
            int vehicleId,
            DateTime periodStart,
            DateTime periodEnd,
            bool isRegeneration = false,
            int? existingReportId = null)
        {
            const string source = "ReportGenerationService.GenerateSingleReportAsync";

            try
            {
                _ = _logger.Info(source,
                    "Inizio elaborazione report",
                    $"VehicleId: {vehicleId}, CompanyId: {companyId}, IsRegeneration: {isRegeneration}, ReportId: {existingReportId?.ToString() ?? "NEW"}");

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

                PdfReport? report;

                // ===== BRANCH RIGENERAZIONE =====
                if (isRegeneration && existingReportId.HasValue)
                {
                    report = await db.PdfReports
                        .Include(r => r.ClientCompany)
                        .Include(r => r.ClientVehicle)
                        .FirstOrDefaultAsync(r => r.Id == existingReportId.Value);

                    if (report == null)
                    {
                        _ = _logger.Warning(source,
                            "Report non trovato per rigenerazione",
                            $"ReportId: {existingReportId}");
                        return false;
                    }

                    // ⚠️ CONTROLLO IMMUTABILITÀ
                    if (!string.IsNullOrWhiteSpace(report.PdfHash) && report.PdfContent?.Length > 0)
                    {
                        _ = _logger.Warning(source,
                            "RIGENERAZIONE BLOCCATA - Report immutabile",
                            $"ReportId: {existingReportId}, PdfHash: {report.PdfHash}");
                        return false;
                    }

                    _ = _logger.Info(source,
                        "Reset report per rigenerazione",
                        $"ReportId: {existingReportId}");

                    report.Status = "REGENERATING";
                    report.PdfContent = null;
                    report.PdfHash = string.Empty;
                    report.GeneratedAt = null;
                    report.Notes = $"Rigenerato: {DateTime.Now:yyyy-MM-dd HH:mm}";

                    await db.SaveChangesAsync();
                }
                // ===== BRANCH NUOVO REPORT =====
                else
                {
                    var exists = await db.PdfReports.AnyAsync(r =>
                        r.ClientCompanyId == companyId &&
                        r.VehicleId == vehicleId &&
                        r.ReportPeriodStart == periodStart &&
                        r.ReportPeriodEnd == periodEnd);

                    if (exists)
                    {
                        _ = _logger.Warning(source,
                            "Report già esistente per questo periodo",
                            $"VehicleId: {vehicleId}, Period: {periodStart:yyyy-MM-dd} - {periodEnd:yyyy-MM-dd}");
                        return false;
                    }

                    report = new PdfReport
                    {
                        ClientCompanyId = companyId,
                        VehicleId = vehicleId,
                        ReportPeriodStart = periodStart,
                        ReportPeriodEnd = periodEnd,
                        Status = "PROCESSING",
                        GeneratedAt = null,
                        PdfHash = string.Empty
                    };

                    db.PdfReports.Add(report);
                    await db.SaveChangesAsync();

                    _ = _logger.Info(source,
                        "Nuovo report creato",
                        $"ReportId: {report.Id}, VehicleId: {vehicleId}");
                }

                // ===== VERIFICA DISPONIBILITÀ DATI =====
                var dataCount = await db.VehiclesData
                    .CountAsync(vd => vd.VehicleId == vehicleId &&
                                     vd.Timestamp >= periodStart &&
                                     vd.Timestamp <= periodEnd);

                if (dataCount == 0)
                {
                    _ = _logger.Warning(source,
                        "Nessun dato disponibile per il periodo",
                        $"ReportId: {report.Id}, VehicleId: {vehicleId}, Period: {periodStart:yyyy-MM-dd} - {periodEnd:yyyy-MM-dd}");

                    report.Status = "NO-DATA";
                    await db.SaveChangesAsync();
                }

                _ = _logger.Info(source,
                    "Dati disponibili verificati",
                    $"ReportId: {report.Id}, DataCount: {dataCount}");

                // ===== CARICA VEHICLE =====
                var vehicle = await db.ClientVehicles
                    .Include(v => v.ClientCompany)
                    .FirstOrDefaultAsync(v => v.Id == vehicleId);

                if (vehicle == null)
                {
                    _ = _logger.Error(source,
                        "Vehicle non trovato",
                        $"ReportId: {report.Id}, VehicleId: {vehicleId}");

                    report.Status = "ERROR";
                    await db.SaveChangesAsync();
                    return false;
                }

                // ===== PREPARA PERIOD INFO =====
                var period = new ReportPeriodInfo
                {
                    Start = periodStart,
                    End = periodEnd,
                    DataHours = (int)(periodEnd - periodStart).TotalHours,
                    AnalysisLevel = isRegeneration ? "Rigenerazione Mensile" : "Mensile",
                    MonitoringDays = (periodEnd - periodStart).TotalDays
                };

                // ===== GENERAZIONE AI INSIGHTS =====
                _ = _logger.Info(source,
                    "Avvio generazione AI insights",
                    $"ReportId: {report.Id}, VIN: {vehicle.Vin}, DataHours: {period.DataHours}h");

                using var scope_ollama = _serviceProvider.CreateScope();
                var ollamaOptions = scope_ollama.ServiceProvider
                    .GetRequiredService<IOptionsSnapshot<OllamaConfig>>();
                var aiGen = new PolarAiReportGenerator(db, ollamaOptions);

                var insights = await aiGen.GeneratePolarAiInsightsAsync(vehicleId);
                var googleAds = new GoogleAdsIntegrationService();
                await googleAds.SendAiInsightsToGoogleAds(insights, vehicleId, vehicle.Vin);

                if (string.IsNullOrWhiteSpace(insights))
                {
                    _ = _logger.Error(source,
                        "AI insights generation failed",
                        $"ReportId: {report.Id}, VehicleId: {vehicleId}");

                    report.Status = "ERROR";
                    report.Notes = "AI insights generation failed";
                    await db.SaveChangesAsync();
                    return false;
                }

                _ = _logger.Info(source,
                    "AI insights generati con successo",
                    $"ReportId: {report.Id}, InsightsLength: {insights.Length} chars");

                // ===== GENERAZIONE FILE PDF =====
                _ = _logger.Info(source,
                    "Avvio generazione PDF",
                    $"ReportId: {report.Id}, VIN: {vehicle.Vin}");

                await GenerateReportFiles(db, report, insights, period, vehicle);

                _ = _logger.Info(source,
                    "Report completato con successo",
                    $"ReportId: {report.Id}, PdfHash: {report.PdfHash}, PdfSize: {report.PdfContent?.Length ?? 0} bytes");

                return true;
            }
            catch (Exception ex)
            {
                _ = _logger.Error(source,
                    "Errore durante generazione report",
                    $"VehicleId: {vehicleId}, CompanyId: {companyId}, ReportId: {existingReportId}, Error: {ex.Message}, StackTrace: {ex.StackTrace}");

                // Aggiorna status a ERROR se possibile
                if (existingReportId.HasValue)
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();
                        var failedReport = await db.PdfReports.FindAsync(existingReportId.Value);
                        if (failedReport != null)
                        {
                            failedReport.Status = "ERROR";
                            failedReport.Notes = $"Error: {ex.Message}";
                            await db.SaveChangesAsync();
                        }
                    }
                    catch (Exception innerEx)
                    {
                        _ = _logger.Error(source,
                            "Errore durante aggiornamento status ERROR",
                            $"ReportId: {existingReportId}, InnerError: {innerEx.Message}");
                    }
                }

                return false;
            }
            finally
            {
                // Garbage Collection forzata per liberare memoria (PDF generation può essere memory-intensive)
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            }
        }

        private async Task GenerateReportForVehicle(
            PolarDriveDbContext db,
            int vehicleId,
            string analysisType,
            DateTime start,
            DateTime end)
        {
            var vehicle = await db.ClientVehicles
                                .Include(v => v.ClientCompany)
                                .FirstOrDefaultAsync(v => v.Id == vehicleId)
                            ?? throw new InvalidOperationException($"Vehicle {vehicleId} not found");

            var reportCount = await db.PdfReports.CountAsync(r => r.VehicleId == vehicleId);

            var period = new ReportPeriodInfo
            {
                Start = start,
                End = end,
                DataHours = (int)(end - start).TotalHours,
                AnalysisLevel = reportCount == 0 ? "Valutazione Iniziale" : analysisType,
                MonitoringDays = (end - start).TotalDays
            };

            // Record nel periodo specifico (per validazione generazione)
            var dataCountInPeriod = await db.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId &&
                            vd.Timestamp >= period.Start &&
                            vd.Timestamp <= period.End)
                .CountAsync();

            _ = _logger.Info(
                "ReportGenerationService.GenerateReportForVehicle",
                $"🧠 Generating {period.AnalysisLevel} for {vehicle.Vin} | {period.Start:yyyy-MM-dd HH:mm} to {period.End:yyyy-MM-dd HH:mm} | Report #{reportCount + 1} | DataRecords in period: {dataCountInPeriod}"
            );

            // Crea sempre il record del report per tracking
            var report = new PdfReport
            {
                VehicleId = vehicleId,
                ClientCompanyId = vehicle.ClientCompanyId,
                ReportPeriodStart = period.Start,
                ReportPeriodEnd = period.End,
                Status = "PROCESSING",
                GeneratedAt = null,
                Notes = $"{period.AnalysisLevel}"
            };

            // Se non ci sono dati nel periodo, imposta status ma genera comunque il file
            if (dataCountInPeriod == 0)
            {
                report.Status = "NO-DATA";
                report.Notes += " - Nessun dato disponibile per il periodo";

                db.PdfReports.Add(report);
                await db.SaveChangesAsync();

                _ = _logger.Warning(
                    "ReportGenerationService.GenerateReportForVehicle",
                    $"⚠️ Report {report.Id} created and file generated for {vehicle.Vin} - No data available for period"
                );
            }

            // Se ci sono dati, procedi con la generazione normale
            db.PdfReports.Add(report);
            await db.SaveChangesAsync();

            using var scope_ollama = _serviceProvider.CreateScope();
            var ollamaOptions = scope_ollama.ServiceProvider.GetRequiredService<IOptionsSnapshot<OllamaConfig>>();
            var aiGen = new PolarAiReportGenerator(db, ollamaOptions);

            var insights = await aiGen.GeneratePolarAiInsightsAsync(vehicleId);
            //var insights = "TEST_INSIGHTS_NO_AI";

            var googleAds = new GoogleAdsIntegrationService();
            await googleAds.SendAiInsightsToGoogleAds(insights, vehicleId, vehicle.Vin);

            if (string.IsNullOrWhiteSpace(insights))
                throw new InvalidOperationException($"No insights for {vehicle.Vin}");

            try
            {
                await GenerateReportFiles(db, report, insights, period, vehicle);
                _ = _logger.Info(
                "ReportGenerationService.GenerateReportForVehicle",
                $"✅ Report {report.Id} generated for {vehicle.Vin} | Period: {period.DataHours}h | Type: {period.AnalysisLevel} | Storage: PDF in DB"
            );
            }
            catch (Exception ex)
            {
                report.Status = "ERROR";
                report.Notes += $" - ERROR: {ex.Message}";
                await db.SaveChangesAsync();

                _ = _logger.Info(
                    "ReportGenerationService.GenerateReportForVehicle",
                    $"❌ Report {report.Id} not generated for {vehicle.Vin}"
                );

                throw;
            }

        }

        private async Task GenerateReportFiles(PolarDriveDbContext db,
                                               PdfReport report,
                                               string insights,
                                               ReportPeriodInfo period,
                                               ClientVehicle vehicle)
        {
            // ✅ CARICA I FONT UNA VOLTA SOLA
            var basePath = "/app/wwwroot/fonts/satoshi";
            var satoshiRegular = File.ReadAllText(Path.Combine(basePath, "Satoshi-Regular.b64"));
            var satoshiBold = File.ReadAllText(Path.Combine(basePath, "Satoshi-Bold.b64"));

            var fontStyles = $@"
            @font-face {{
                font-family: 'Satoshi';
                src: url(data:font/woff2;base64,{satoshiRegular}) format('woff2');
                font-weight: 400;
                font-style: normal;
            }}
            @font-face {{
                font-family: 'Satoshi';
                src: url(data:font/woff2;base64,{satoshiBold}) format('woff2');
                font-weight: 700;
                font-style: normal;
            }}";

            // HTML in memoria (nessun salvataggio su disco)
            var htmlSvc = new HtmlReportService(db);
            var htmlOpt = new HtmlReportOptions
            {
                ReportType = $"🧠 {period.AnalysisLevel}",
                AdditionalCss = PolarAiReports.Templates.DefaultCssTemplate.Value
            };
            var html = await htmlSvc.GenerateHtmlReportAsync(report, insights, htmlOpt);

            // PDF con Puppeteer → otteniamo byte[]
            var pdfSvc = new PdfGenerationService();
            var pdfOpt = new PdfConversionOptions
            {
                HeaderTemplate = $@"
            <html>
            <head>
                <style>
                    {fontStyles}
                    body {{
                        margin: 0;
                        padding: 0;
                        width: 100%;
                        height: 100%;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        font-family: 'Satoshi', 'Noto Color Emoji', sans-serif;
                        letter-spacing: normal;
                        word-spacing: normal;
                    }}
                    .header-content {{
                        font-size: 10px;
                        color: #ccc;
                        text-align: center;
                        border-bottom: 1px solid #ccc;
                        padding-bottom: 5px;
                        width: 100%;
                        letter-spacing: normal;
                        word-spacing: normal;
                    }}
                </style>
            </head>
            <body>
                <div class='header-content'>{vehicle.Vin} - {DateTime.Now:yyyy-MM-dd HH:mm}</div>
            </body>
            </html>",
                FooterTemplate = $@"
            <html>
            <head>
                <style>
                    {fontStyles}
                    body {{
                        margin: 0;
                        padding: 0;
                        width: 100%;
                        height: 100%;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        font-family: 'Satoshi', 'Noto Color Emoji', sans-serif;
                        letter-spacing: normal;
                        word-spacing: normal;
                    }}
                    .footer-content {{
                        font-size: 10px;
                        color: #ccc;
                        text-align: center;
                        border-top: 1px solid #ccc;
                        padding-top: 5px;
                        width: 100%;
                        letter-spacing: normal;
                        word-spacing: normal;
                    }}
                </style>
            </head>
            <body>
                <div class='footer-content'>
                    Pagina <span class='pageNumber'></span> di <span class='totalPages'></span> | DataPolar Analytics
                </div>
            </body>
            </html>"
            };

            // ⛔️ Se già presente, non rigenerare
            if (report.PdfContent is { Length: > 0 })
            {
                _ = _logger.Info("Report {Id} already has a PDF ({Size} bytes). Skipping.", report.Id.ToString(), report.PdfContent.Length.ToString());
                return;
            }

            // ========== PASSO 1: PDF PROVVISORIO (senza hash stampato) ==========
            var html1 = await htmlSvc.GenerateHtmlReportAsync(report, insights, htmlOpt);
            var pdf1 = await pdfSvc.ConvertHtmlToPdfAsync(html1, report, pdfOpt);

            if (pdf1 == null || pdf1.Length == 0)
                throw new InvalidOperationException("PDF provvisorio vuoto/non generato.");

            // Calcola l'hash DAL FILE (univoco)
            var fileHash = GenericHelpers.ComputeContentHash(pdf1);

            // Aggiorna entità con hash e GeneratedAt
            report.PdfHash = fileHash;
            report.GeneratedAt = DateTime.Now;

            // Nota: non persisto pdf1; serve solo per estrarre l'hash del file
            await db.SaveChangesAsync();

            // ========== PASSO 2: PDF FINALE (hash valorizzato e stampato) ==========
            var html2 = await htmlSvc.GenerateHtmlReportAsync(report, insights, htmlOpt); // ora {{pdfHash}} ha valore
            var pdf2 = await pdfSvc.ConvertHtmlToPdfAsync(html2, report, pdfOpt);

            if (pdf2 == null || pdf2.Length == 0)
                throw new InvalidOperationException("PDF finale vuoto/non generato.");

            // 🛡️ Double-check anti-race
            if (report.PdfContent is { Length: > 0 })
            {
                _ = _logger.Warning("Race detected for report {Id}. Another worker saved the PDF meanwhile. Discarding generated bytes.", report.Id.ToString());
                return;
            }

            // ✅ Persisti il PDF finale + stato
            report.PdfContent = pdf2;
            report.Status = "PDF-READY";
            report.GeneratedAt = DateTime.Now;
            await db.SaveChangesAsync();
        }

        private bool ShouldGenerateReports(ScheduleType scheduleType, DateTime now)
        {
            if (scheduleType == ScheduleType.Development)
            {
                return !_lastReportAttempts.Any()
                    || _lastReportAttempts.Values.All(t => now - t >= TimeSpan.FromMinutes(DEV_INTERVAL_MINUTES));
            }
            return true;
        }

        private async Task<List<ClientVehicle>> GetVehiclesToProcess(
            PolarDriveDbContext db,
            ScheduleType scheduleType)
        {
            var query = db.ClientVehicles
                .Include(v => v.ClientCompany)
                .Where(v => v.IsFetchingDataFlag);

            // In Development/Retry non filtri per periodo
            if (scheduleType == ScheduleType.Monthly)
            {
                var now = DateTime.Now;
                var start = now.AddHours(-MONTHLY_HOURS_THRESHOLD);

                query = query.Where(v => db.VehiclesData
                    .Any(d => d.VehicleId == v.Id && d.Timestamp >= start && d.Timestamp <= now));
            }

            return await query.ToListAsync();
        }

        private static string GetAnalysisType(ScheduleType scheduleType)
        {
            return scheduleType switch
            {
                ScheduleType.Development => "Development Analysis",
                ScheduleType.Monthly => "Analisi Mensile",
                _ => "Analisi Mensile"
            };
        }

        public async Task<int> RecoverStaleProcessingReportsAsync(CancellationToken stoppingToken = default)
        {
            const string source = "ReportGenerationService.RecoverStaleProcessingReportsAsync";

            var cutoffTime = _env.IsDevelopment() 
                ? DateTime.Now.AddMinutes(-DEV_RETRY_ORPHAN_PDF_REPEAT_MINUTES)
                : DateTime.Now.AddHours(-PROD_RETRY_ORPHAN_PDF_REPEAT_HOURS);

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            var staleReports = await db.PdfReports
                .Where(r => (r.Status == "PROCESSING" || r.Status == "REGENERATING")
                            && r.CreatedAt < cutoffTime)
                .ToListAsync(stoppingToken);

            if (!staleReports.Any())
                return 0;

            _ = _logger.Warning(source,
                $"🚨 Found {staleReports.Count} stale reports in PROCESSING/REGENERATING");

            foreach (var report in staleReports)
            {
                report.Status = "ERROR";
                report.Notes = $"Auto-recovered from stale {report.Status} state at {DateTime.Now:yyyy-MM-dd HH:mm}. Original notes: {report.Notes}";

                _ = _logger.Warning(source,
                    $"Report {report.Id} moved to ERROR (was stuck in {report.Status} since {report.CreatedAt})");
            }

            await db.SaveChangesAsync(stoppingToken);
            return staleReports.Count;
        }
    }
}