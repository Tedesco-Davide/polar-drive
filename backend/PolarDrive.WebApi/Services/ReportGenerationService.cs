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
    }

    /// <summary>
    /// Risultato della generazione di un singolo report
    /// </summary>
    public class ReportGenerationResult
    {
        public bool Success { get; set; }
        public int? ReportId { get; set; }
        public string? VehicleVin { get; set; }
        public string? AnalysisLevel { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public ReportFileStatus? FileStatus { get; set; }
    }

    /// <summary>
    /// Status dei file di un report
    /// </summary>
    public class ReportFileStatus
    {
        public bool PdfExists { get; set; }
        public bool HtmlExists { get; set; }
        public string? PdfPath { get; set; }
        public string? HtmlPath { get; set; }
        public long PdfSize { get; set; }
        public long HtmlSize { get; set; }
    }

    /// <summary>
    /// Classi di supporto
    /// </summary>
    public class ReportPeriodInfo
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int DataHours { get; set; }
        public string AnalysisLevel { get; set; } = string.Empty;
        public double MonitoringDays { get; set; }
    }

    public class ReportInfo
    {
        public string AnalysisType { get; set; } = string.Empty;
    }

    public class ReportGenerationService(IServiceProvider serviceProvider,
                                  ILogger<ReportGenerationService> logger,
                                  IWebHostEnvironment env) : IReportGenerationService
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ILogger<ReportGenerationService> _logger = logger;
        private readonly IWebHostEnvironment _env = env;
        private readonly Dictionary<int, DateTime> _lastReportAttempts = [];
        private readonly Dictionary<int, int> _retryCount = [];

        public async Task<SchedulerResults> ProcessScheduledReportsAsync(
            ScheduleType scheduleType,
            CancellationToken stoppingToken = default)
        {
            var results = new SchedulerResults();
            var now = DateTime.Now;

            if (!ShouldGenerateReports(scheduleType, now))
            {
                _logger.LogDebug("⏰ Not time for {ScheduleType} reports", scheduleType);
                return results;
            }

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            var vehicles = await GetVehiclesToProcess(db, scheduleType);
            if (!vehicles.Any())
            {
                _logger.LogInformation("📭 No vehicles to process for {ScheduleType}", scheduleType);
                return results;
            }

            var activeCount = vehicles.Count(v => v.IsActiveFlag);
            var graceCount = vehicles.Count - activeCount;
            _logger.LogInformation("📊 {ScheduleType}: Total={Total}, Active={Active}, Grace={Grace}",
                                   scheduleType, vehicles.Count, activeCount, graceCount);
            if (graceCount > 0)
                _logger.LogWarning("⏳ {Count} vehicles in grace period still generating reports", graceCount);

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

                    _logger.LogInformation("🔍 DEBUG: Vehicle {VIN} - Start: {Start}, End: {End}, Now: {Now}", v.Vin, start, end, now);

                    // 3) Infine genero con questi parametri
                    var analysisType = GetAnalysisType(scheduleType);
                    await GenerateReportForVehicle(db, v.Id, analysisType, start, end);

                    results.SuccessCount++;
                    _lastReportAttempts[v.Id] = now;
                    _retryCount[v.Id] = 0;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error report for vehicle {VIN}", v.Vin);
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

                    _logger.LogInformation("🔄 Retry {Count}/{Max} for {VIN}", _retryCount[id], MAX_RETRIES, veh.Vin);

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
                    _logger.LogError(ex, "❌ Retry failed for {VehicleId}", id);
                    results.ErrorCount++;
                    _retryCount[id]++;
                    _lastReportAttempts[id] = now;
                    if (_retryCount[id] > MAX_RETRIES)
                        _logger.LogWarning("🚫 {VehicleId} exceeded max retries", id);
                }
            }

            return results;
        }

        /// <summary>
        /// Permette rigenerazione di un PDF report singolarmente
        /// </summary>
        /// <summary>
        /// Permette rigenerazione di un PDF report singolarmente
        /// </summary>
        public async Task<bool> GenerateSingleReportAsync(int companyId, int vehicleId, DateTime periodStart, DateTime periodEnd, bool isRegeneration = false, int? existingReportId = null)
        {
            // ✅ LOG 1: Appena entra
            Console.WriteLine($"🎯 [STEP 1] ENTRATA metodo - CompanyId: {companyId}, VehicleId: {vehicleId}, IsRegen: {isRegeneration}, ExistingId: {existingReportId}");

            try
            {
                // ✅ LOG 2: Prima del scope
                Console.WriteLine($"🎯 [STEP 2] Creazione scope...");

                using var scope = _serviceProvider.CreateScope();

                // ✅ LOG 3: Dopo scope
                Console.WriteLine($"🎯 [STEP 3] Scope creato, richiesta DbContext...");

                var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

                // ✅ LOG 4: Dopo DbContext
                Console.WriteLine($"🎯 [STEP 4] DbContext ottenuto!");

                PdfReport? report;

                // ✅ LOG 5: Prima del branch
                Console.WriteLine($"🎯 [STEP 5] Controllo branch - IsRegen: {isRegeneration}, HasExistingId: {existingReportId.HasValue}");

                if (isRegeneration && existingReportId.HasValue)
                {
                    // ✅ LOG 6: Dentro branch rigenerazione
                    Console.WriteLine($"🎯 [STEP 6] Branch RIGENERAZIONE - Carico report {existingReportId}");

                    report = await db.PdfReports
                        .Include(r => r.ClientCompany)
                        .Include(r => r.ClientVehicle)
                        .FirstOrDefaultAsync(r => r.Id == existingReportId.Value);

                    // ✅ LOG 7: Dopo caricamento
                    Console.WriteLine($"🎯 [STEP 7] Report caricato - Found: {report != null}");

                    if (report == null)
                    {
                        Console.WriteLine($"🎯 [STEP 7-ERROR] Report {existingReportId} non trovato");
                        return false;
                    }

                    // ⚠️ CONTROLLO IMMUTABILITÀ
                    if (!string.IsNullOrWhiteSpace(report.PdfHash) &&
                        report.PdfContent?.Length > 0)
                    {
                        Console.WriteLine($"🎯 [STEP 7-IMMUTABLE] Report {existingReportId} certificato (Hash: {report.PdfHash})");
                        return false;
                    }

                    // ✅ LOG 8: Reset report
                    Console.WriteLine($"🎯 [STEP 8] Reset report per rigenerazione");

                    report.Status = "REGENERATING";
                    report.PdfContent = null;
                    report.PdfHash = null;
                    report.GeneratedAt = null;
                    report.Notes = $"Rigenerato: {DateTime.Now:yyyy-MM-dd HH:mm}";

                    await db.SaveChangesAsync();

                    Console.WriteLine($"🎯 [STEP 9] Reset salvato!");
                }
                else
                {
                    // ✅ LOG 10: Branch nuovo report
                    Console.WriteLine($"🎯 [STEP 10] Branch NUOVO REPORT");

                    var exists = await db.PdfReports.AnyAsync(r =>
                        r.ClientCompanyId == companyId &&
                        r.VehicleId == vehicleId &&
                        r.ReportPeriodStart == periodStart &&
                        r.ReportPeriodEnd == periodEnd);

                    if (exists)
                    {
                        Console.WriteLine($"🎯 [STEP 10-EXISTS] Report già esistente");
                        return false;
                    }

                    report = new PdfReport
                    {
                        ClientCompanyId = companyId,
                        VehicleId = vehicleId,
                        ReportPeriodStart = periodStart,
                        ReportPeriodEnd = periodEnd,
                        Status = "PROCESSING",
                        GeneratedAt = null
                    };

                    db.PdfReports.Add(report);
                    await db.SaveChangesAsync();

                    Console.WriteLine($"🎯 [STEP 11] Nuovo report creato - Id: {report.Id}");
                }

                // ✅ LOG 12: Verifica dati
                Console.WriteLine($"🎯 [STEP 12] Verifica disponibilità dati...");

                var dataCount = await db.VehiclesData
                    .CountAsync(vd => vd.VehicleId == vehicleId &&
                                     vd.Timestamp >= periodStart &&
                                     vd.Timestamp <= periodEnd);

                Console.WriteLine($"🎯 [STEP 13] Dati trovati: {dataCount}");

                if (dataCount == 0)
                {
                    Console.WriteLine($"🎯 [STEP 13-NODATA] Nessun dato disponibile");
                    report.Status = "NO-DATA";
                    await db.SaveChangesAsync();
                    return false;
                }

                // ✅ LOG 14: Carica vehicle
                Console.WriteLine($"🎯 [STEP 14] Caricamento vehicle...");

                var vehicle = await db.ClientVehicles
                    .Include(v => v.ClientCompany)
                    .FirstOrDefaultAsync(v => v.Id == vehicleId);

                if (vehicle == null)
                {
                    Console.WriteLine($"🎯 [STEP 14-ERROR] Vehicle {vehicleId} non trovato");
                    report.Status = "ERROR";
                    await db.SaveChangesAsync();
                    return false;
                }

                Console.WriteLine($"🎯 [STEP 15] Vehicle caricato: {vehicle.Vin}");

                // ✅ LOG 16: Prepara period
                Console.WriteLine($"🎯 [STEP 16] Preparazione period info...");

                var period = new ReportPeriodInfo
                {
                    Start = periodStart,
                    End = periodEnd,
                    DataHours = (int)(periodEnd - periodStart).TotalHours,
                    AnalysisLevel = isRegeneration ? "Rigenerazione Mensile" : "Mensile",
                    MonitoringDays = (periodEnd - periodStart).TotalDays
                };

                Console.WriteLine($"🎯 [STEP 17] Period preparato - Hours: {period.DataHours}");

                // ✅ LOG 18: AI Insights
                Console.WriteLine($"🎯 [STEP 18] Inizio generazione AI insights...");

                using var scope_ollama = _serviceProvider.CreateScope();
                var ollamaOptions = scope_ollama.ServiceProvider
                    .GetRequiredService<IOptionsSnapshot<OllamaConfig>>();
                var aiGen = new PolarAiReportGenerator(db, ollamaOptions);

                Console.WriteLine($"🎯 [STEP 19] AI Generator creato, chiamata in corso...");

                var insights = await aiGen.GeneratePolarAiInsightsAsync(vehicleId);

                Console.WriteLine($"🎯 [STEP 20] AI Insights ricevuti - Length: {insights?.Length ?? 0}");

                if (string.IsNullOrWhiteSpace(insights))
                {
                    Console.WriteLine($"🎯 [STEP 20-ERROR] AI insights vuoti");
                    report.Status = "ERROR";
                    report.Notes = "AI insights generation failed";
                    await db.SaveChangesAsync();
                    return false;
                }

                // ✅ LOG 21: Genera files
                Console.WriteLine($"🎯 [STEP 21] Inizio GenerateReportFiles...");

                await GenerateReportFiles(db, report, insights, period, vehicle);

                Console.WriteLine($"🎯 [STEP 22] GenerateReportFiles completato!");
                Console.WriteLine($"✅ Report {report.Id} completato - Hash: {report.PdfHash}");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 [EXCEPTION] CompanyId: {companyId}, VehicleId: {vehicleId}");
                Console.WriteLine($"💥 Dettaglio eccezione: {ex.Message}");
                Console.WriteLine($"💥 Stack trace: {ex.StackTrace}");

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
                            await db.SaveChangesAsync();
                        }
                    }
                    catch (Exception innerEx)
                    {
                        Console.WriteLine($"💥 ERRORE durante aggiornamento status ERROR: {innerEx.Message}");
                    }
                }

                return false;
            }
            finally
            {
                Console.WriteLine($"🎯 [FINALLY] Garbage Collection...");
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                Console.WriteLine($"🎯 [FINALLY] Garbage Collection completato");
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

            _logger.LogInformation("🧠 Generating {Level} for {VIN} | {Start:yyyy-MM-dd HH:mm} to {End:yyyy-MM-dd HH:mm} | Report #{Count} | DataRecords in period: {DataCount}",
                                period.AnalysisLevel, vehicle.Vin, period.Start, period.End, reportCount + 1, dataCountInPeriod);

            // Crea sempre il record del report per tracking
            var report = new PdfReport
            {
                VehicleId = vehicleId,
                ClientCompanyId = vehicle.ClientCompanyId,
                ReportPeriodStart = period.Start,
                ReportPeriodEnd = period.End,
                GeneratedAt = DateTime.Now,
                Notes = $"{period.AnalysisLevel}"
            };

            // Se non ci sono dati nel periodo, imposta status e non generare file
            if (dataCountInPeriod == 0)
            {
                report.Status = "NO-DATA";
                report.Notes += " - Nessun dato disponibile per il periodo";

                db.PdfReports.Add(report);
                await db.SaveChangesAsync();

                _logger.LogWarning("⚠️ Report {Id} created but NO FILES generated for {VIN} - No data available for period",
                                report.Id, vehicle.Vin);
                return;
            }

            // Se ci sono dati, procedi con la generazione normale
            db.PdfReports.Add(report);
            await db.SaveChangesAsync();

            using var scope_ollama = _serviceProvider.CreateScope();
            var ollamaOptions = scope_ollama.ServiceProvider.GetRequiredService<IOptionsSnapshot<OllamaConfig>>();
            var aiGen = new PolarAiReportGenerator(db, ollamaOptions);

            var insights = await aiGen.GeneratePolarAiInsightsAsync(vehicleId);
            //var insights = "TEST_INSIGHTS_NO_AI";

            if (string.IsNullOrWhiteSpace(insights))
                throw new InvalidOperationException($"No insights for {vehicle.Vin}");

            await GenerateReportFiles(db, report, insights, period, vehicle);

            _logger.LogInformation("✅ Report {Id} generated for {VIN} | Period: {Hours}h | Type: {Type} | Storage: PDF in DB",
                report.Id, vehicle.Vin, period.DataHours, period.AnalysisLevel);

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
            var pdfSvc = new PdfGenerationService(db);
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
                _logger.LogInformation("Report {Id} already has a PDF ({Size} bytes). Skipping.", report.Id, report.PdfContent.Length);
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
                _logger.LogWarning("Race detected for report {Id}. Another worker saved the PDF meanwhile. Discarding generated bytes.", report.Id);
                return;
            }

            // ✅ Persisti il PDF finale + stato
            report.PdfContent = pdf2;
            report.Status = "PDF-READY";
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
    }
}