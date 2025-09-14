using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.PolarAiReports;
using PolarDrive.WebApi.Scheduler;
using static PolarDrive.WebApi.Constants.CommonConstants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace PolarDrive.WebApi.Services
{
    public interface IReportGenerationService
    {
        // ✅ METODI ESISTENTI (per scheduler automatico)
        Task<SchedulerResults> ProcessScheduledReportsAsync(ScheduleType scheduleType, CancellationToken stoppingToken = default);
        Task<RetryResults> ProcessRetriesAsync(CancellationToken stoppingToken = default);
        Task ForceRegenerateFilesAsync(int reportId);

        // ✅ NUOVI METODI (per API controller)  
        Task<ReportGenerationResult> GenerateReportForVehicleAsync(int vehicleId, string analysisLevel = "Manual Generation");
        Task<ReportGenerationResult> GenerateReportForAllActiveVehiclesAsync();
        Task<ReportFileStatus> GetReportFileStatusAsync(int reportId);
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
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
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
        public int DefaultDataHours { get; set; }
    }

    // ✅ CLASSI NECESSARIE DAL VECCHIO SISTEMA (per compatibilità)
    public class PdfConversionOptions
    {
        public string PageFormat { get; set; } = "A4";
        public bool PrintBackground { get; set; } = true;
        public string MarginTop { get; set; } = "1cm";
        public string MarginRight { get; set; } = "1cm";
        public string MarginBottom { get; set; } = "1cm";
        public string MarginLeft { get; set; } = "1cm";
        public bool DisplayHeaderFooter { get; set; } = true;
        public string HeaderTemplate { get; set; } = @"
            <div style='font-size: 10px; width: 100%; text-align: center; color: #666;'>
                <span>PolarDrive Report</span>
            </div>";
        public string FooterTemplate { get; set; } = @"
            <div style='
                display: block;
                width: 100%;
                margin: 0;
                padding: 0;
                font-size: 10px;
                color: #666;
                text-align: center;
            '>
                Pagina <span class='pageNumber'></span> di <span class='totalPages'></span>
            </div>";
    }

    public class HtmlReportOptions
    {
        public string TemplateName { get; set; } = "default";
        public string StyleName { get; set; } = "default";
        public string ReportType { get; set; } = "Standard";
        public string DateFormat { get; set; } = "yyyy-MM-dd";
        public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm";
        public string? AdditionalCss { get; set; }
        public bool ShowDetailedStats { get; set; } = true;
        public bool ShowCharts { get; set; } = false;
        public bool ShowRawData { get; set; } = false;
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

        #region ✅ METODI ESISTENTI (Scheduler Automatico)

        public async Task<SchedulerResults> ProcessScheduledReportsAsync(
            ScheduleType scheduleType,
            CancellationToken stoppingToken = default)
        {
            var results = new SchedulerResults();
            var now = DateTime.UtcNow;

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
                        .Where(r => r.ClientVehicleId == v.Id)
                        .OrderByDescending(r => r.ReportPeriodEnd)
                        .Select(r => (DateTime?)r.ReportPeriodEnd)
                        .FirstOrDefaultAsync(stoppingToken);

                    // 2) SEMPRE 720H - FINESTRA MENSILE UNIFICATA
                    var start = lastReportEnd ?? now.AddHours(-MONTHLY_HOURS_THRESHOLD);
                    var end = now;

                    _logger.LogInformation("🔍 DEBUG: Vehicle {VIN} - Start: {Start}, End: {End}, Now: {Now} [FIXED 720h window]", v.Vin, start, end, now);

                    // 3) Infine genero con questi parametri
                    var info = GetReportInfo(scheduleType, now);
                    await GenerateReportForVehicle(db, v.Id, info, start, end);

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
            var now = DateTime.UtcNow;
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
                        .Where(r => r.ClientVehicleId == id)
                        .OrderByDescending(r => r.ReportPeriodEnd)
                        .Select(r => (DateTime?)r.ReportPeriodEnd)
                        .FirstOrDefaultAsync(stoppingToken);

                    var start = lastReportEnd ?? now.AddHours(-MONTHLY_HOURS_THRESHOLD);
                    var end = now;

                    var info = GetReportInfo(ScheduleType.Retry, now);
                    await GenerateReportForVehicle(db, id, info, start, end);

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

        public async Task ForceRegenerateFilesAsync(int reportId)
        {
            _logger.LogInformation("🔄 Rigenerazione file per report {ReportId}", reportId);

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            // 1) Carica il report e il veicolo associato
            var report = await db.PdfReports
                                 .Include(r => r.ClientVehicle!)
                                 .ThenInclude(v => v!.ClientCompany)
                                 .FirstOrDefaultAsync(r => r.Id == reportId)
                         ?? throw new InvalidOperationException($"Report {reportId} non trovato");

            var vehicle = report.ClientVehicle;

            // Controlla se ci sono dati nel periodo del report
            var dataCount = await db.VehiclesData
                .Where(vd => vd.VehicleId == vehicle!.Id &&
                             vd.Timestamp >= report.ReportPeriodStart &&
                             vd.Timestamp <= report.ReportPeriodEnd)
                .CountAsync();

            _logger.LogInformation("🔍 Report {ReportId} regeneration check: VIN={VIN}, Period={Start} to {End}, DataCount={DataCount}",
                                  reportId, vehicle!.Vin, report.ReportPeriodStart, report.ReportPeriodEnd, dataCount);

            // ✅ Se non ci sono dati, aggiorna status e NON rigenerare file
            if (dataCount == 0)
            {
                report.Status = "NO-DATA";
                report.Notes = $"Ultima rigenerazione: {DateTime.UtcNow:yyyy-MM-dd HH:mm} - numero rigenerazione #{report.RegenerationCount} - Nessun dato disponibile per il periodo";

                // Elimina eventuali file esistenti (cleanup)
                DeleteExistingFiles(report);

                await db.SaveChangesAsync();

                _logger.LogWarning("⚠️ Report {ReportId} rigenerazione saltata per {VIN} - Nessun dato disponibile nel periodo specificato",
                                  reportId, vehicle.Vin);
                return; // ← ESCI SENZA CREARE NUOVI FILE
            }

            // ✅ Se ci sono dati, procedi con la rigenerazione normale
            _logger.LogInformation("✅ Report {ReportId} ha {DataCount} record di dati - Procedendo con rigenerazione file",
                                  reportId, dataCount);

            // 2) Ricalcola gli insights con NUOVO SISTEMA
            using var scope_ollama = _serviceProvider.CreateScope();
            var ollamaOptions = scope_ollama.ServiceProvider.GetRequiredService<IOptionsSnapshot<OllamaConfig>>();
            var httpClient = scope_ollama.ServiceProvider.GetRequiredService<HttpClient>();
            var aiGen = new PolarAiReportGenerator(db, ollamaOptions, httpClient);

            var insights = await aiGen.GeneratePolarAiInsightsAsync(vehicle.Id);
            if (string.IsNullOrWhiteSpace(insights))
                throw new InvalidOperationException($"Nessun insight generato per {vehicle.Vin}");

            // 3) Crea un ReportPeriodInfo a partire dal report esistente
            var period = new ReportPeriodInfo
            {
                Start = report.ReportPeriodStart,
                End = report.ReportPeriodEnd,
                DataHours = (int)(report.ReportPeriodEnd - report.ReportPeriodStart).TotalHours,
                AnalysisLevel = $"Rigenerazione #{report.RegenerationCount}",
                MonitoringDays = (report.ReportPeriodEnd - report.ReportPeriodStart).TotalDays
            };

            // 4) Elimina i vecchi file prima di rigenerare
            DeleteExistingFiles(report);

            // 5) Rigenera i file con NUOVO SISTEMA
            await GenerateReportFiles(db, report, insights, period, vehicle);

            // 6) Aggiorna lo status del report
            report.Status = "COMPLETED"; // o null per far usare la logica file-based
            await db.SaveChangesAsync();

            _logger.LogInformation("✅ File rigenerati con successo per report {ReportId} - VIN: {VIN}, DataRecords: {DataCount}",
                                  reportId, vehicle.Vin, dataCount);
        }

        #endregion

        #region ✅ NUOVI METODI (API Controller)

        /// <summary>
        /// Genera report per un singolo veicolo (usato da API controller)
        /// </summary>
        public async Task<ReportGenerationResult> GenerateReportForVehicleAsync(int vehicleId, string analysisLevel = "Manual Generation")
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

                var vehicle = await db.ClientVehicles
                    .Include(v => v.ClientCompany)
                    .FirstOrDefaultAsync(v => v.Id == vehicleId);

                if (vehicle == null)
                {
                    _logger.LogWarning("Vehicle {VehicleId} not found", vehicleId);
                    return new ReportGenerationResult
                    {
                        Success = false,
                        ErrorMessage = $"Vehicle with ID {vehicleId} not found"
                    };
                }

                _logger.LogInformation("🚗 API Report generation triggered for VIN {VIN}", vehicle.Vin);

                // ✅ Chiama il metodo privato esistente adattandolo per l'API
                var reportId = await GenerateReportForVehicleInternal(db, vehicleId, analysisLevel);

                var result = new ReportGenerationResult
                {
                    Success = reportId.HasValue,
                    ReportId = reportId,
                    VehicleVin = vehicle.Vin,
                    AnalysisLevel = analysisLevel
                };

                if (result.Success && result.ReportId.HasValue)
                {
                    result.FileStatus = await GetReportFileStatusAsync(result.ReportId.Value);
                }
                else if (!result.Success)
                {
                    result.ErrorMessage = "Report generation failed - check logs for details";
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generating report for vehicle {VehicleId}", vehicleId);
                return new ReportGenerationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Genera report per tutti i veicoli attivi (usato da API controller)
        /// </summary>
        public async Task<ReportGenerationResult> GenerateReportForAllActiveVehiclesAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

                var activeVehicles = await db.ClientVehicles
                    .Where(v => v.IsActiveFlag && v.IsFetchingDataFlag)
                    .ToListAsync();

                if (!activeVehicles.Any())
                {
                    _logger.LogInformation("📭 No active vehicles found for report generation");
                    return new ReportGenerationResult
                    {
                        Success = false,
                        ErrorMessage = "No active vehicles found for report generation"
                    };
                }

                _logger.LogInformation("🚗 API Batch report generation triggered for {Count} vehicles", activeVehicles.Count);

                var successCount = 0;
                var errorCount = 0;

                foreach (var vehicle in activeVehicles)
                {
                    try
                    {
                        var reportId = await GenerateReportForVehicleInternal(db, vehicle.Id, "Manual API Generation");
                        if (reportId.HasValue)
                            successCount++;
                        else
                            errorCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogError(ex, "❌ Error generating report for vehicle {VIN}", vehicle.Vin);
                    }
                }

                _logger.LogInformation("✅ API Batch report generation completed: Success={Success}, Errors={Errors}",
                    successCount, errorCount);

                return new ReportGenerationResult
                {
                    Success = true,
                    ErrorMessage = $"Batch generation completed: {successCount} success, {errorCount} errors"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in batch report generation");
                return new ReportGenerationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Ottieni status dei file di un report
        /// </summary>
        public async Task<ReportFileStatus> GetReportFileStatusAsync(int reportId)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();

            var report = await db.PdfReports.FindAsync(reportId);
            if (report == null)
            {
                return new ReportFileStatus();
            }

            var pdfPath = GetFilePath(report, "pdf", "reports");
            var htmlPath = GetFilePath(report, "html", _env.IsDevelopment() ? "dev-reports" : "reports");

            return new ReportFileStatus
            {
                PdfExists = File.Exists(pdfPath),
                HtmlExists = File.Exists(htmlPath),
                PdfPath = pdfPath,
                HtmlPath = htmlPath,
                PdfSize = File.Exists(pdfPath) ? new FileInfo(pdfPath).Length : 0,
                HtmlSize = File.Exists(htmlPath) ? new FileInfo(htmlPath).Length : 0
            };
        }

        #endregion

        #region ✅ METODI PRIVATI (Core Logic)

        /// <summary>
        /// Metodo interno per generazione report che ritorna int? (compatibile con codice esistente)
        /// Adatta il metodo GenerateReportForVehicle esistente per API
        /// </summary>
        private async Task<int?> GenerateReportForVehicleInternal(PolarDriveDbContext db, int vehicleId, string analysisLevel)
        {
            try
            {
                // 1) Prendo la fine dell'ultimo report (se esiste)
                var lastReportEnd = await db.PdfReports
                    .Where(r => r.ClientVehicleId == vehicleId)
                    .OrderByDescending(r => r.ReportPeriodEnd)
                    .Select(r => (DateTime?)r.ReportPeriodEnd)
                    .FirstOrDefaultAsync();

                // 2) SEMPRE 720H - FINESTRA MENSILE UNIFICATA
                var now = DateTime.UtcNow;
                var start = lastReportEnd ?? now.AddHours(-MONTHLY_HOURS_THRESHOLD);
                var end = now;

                // 3) Crea ReportInfo per compatibilità
                var info = new ReportInfo
                {
                    AnalysisType = analysisLevel,
                    DefaultDataHours = (int)(end - start).TotalHours
                };

                // 4) Chiama il metodo esistente
                await GenerateReportForVehicle(db, vehicleId, info, start, end);

                // 5) Trova l'ultimo report creato per questo veicolo
                var latestReport = await db.PdfReports
                    .Where(r => r.ClientVehicleId == vehicleId)
                    .OrderByDescending(r => r.GeneratedAt)
                    .FirstOrDefaultAsync();

                return latestReport?.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in GenerateReportForVehicleInternal for vehicle {VehicleId}", vehicleId);
                return null;
            }
        }

        private async Task GenerateReportForVehicle(PolarDriveDbContext db, int vehicleId, ReportInfo info, DateTime start, DateTime end)
        {
            var vehicle = await db.ClientVehicles
                                  .Include(v => v.ClientCompany)
                                  .FirstOrDefaultAsync(v => v.Id == vehicleId)
                         ?? throw new InvalidOperationException($"Vehicle {vehicleId} not found");

            if (!vehicle.IsActiveFlag && vehicle.IsFetchingDataFlag)
                _logger.LogWarning("⏳ Grace Period for {VIN}", vehicle.Vin);

            var reportCount = await db.PdfReports.CountAsync(r => r.ClientVehicleId == vehicleId);

            // Per il primo report, usa la data di attivazione
            if (reportCount == 0 && vehicle.FirstActivationAt.HasValue)
            {
                start = vehicle.FirstActivationAt.Value;  // ✅ Dall'attivazione!
                end = DateTime.UtcNow;

                _logger.LogInformation("🔧 First report from activation: {ActivationDate} to {Now}",
                                       start, end);
            }

            var period = new ReportPeriodInfo
            {
                Start = start,
                End = end,
                DataHours = (int)(end - start).TotalHours,
                AnalysisLevel = reportCount == 0 ? "Valutazione Iniziale" : info.AnalysisType,
                MonitoringDays = (end - start).TotalDays
            };

            // Controlla se ci sono dati prima di creare il report
            var dataCount = await db.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId &&
                             vd.Timestamp >= period.Start &&
                             vd.Timestamp <= period.End)
                .CountAsync();

            _logger.LogInformation("🧠 Generating {Level} for {VIN} | {Start:yyyy-MM-dd HH:mm} to {End:yyyy-MM-dd HH:mm} | Report #{Count} | DataRecords: {DataCount}",
                                   period.AnalysisLevel, vehicle.Vin, period.Start, period.End, reportCount + 1, dataCount);

            // Crea sempre il record del report per tracking
            var report = new PdfReport
            {
                ClientVehicleId = vehicleId,
                ClientCompanyId = vehicle.ClientCompanyId,
                ReportPeriodStart = period.Start,
                ReportPeriodEnd = period.End,
                GeneratedAt = DateTime.UtcNow,
                Notes = $"[{period.AnalysisLevel}] DataHours: {period.DataHours}, Monitoring: {period.MonitoringDays:F1}d"
            };

            // Se non ci sono dati, imposta status e NON generare file
            if (dataCount == 0)
            {
                report.Status = "NO-DATA";
                report.Notes += " - Nessun dato disponibile per il periodo";

                db.PdfReports.Add(report);
                await db.SaveChangesAsync();

                _logger.LogWarning("⚠️ Report {Id} created but NO FILES generated for {VIN} - No data available for period",
                                   report.Id, vehicle.Vin);
                return; // ← ESCI SENZA CREARE FILE
            }

            // ✅ Se ci sono dati, procedi con la generazione normale
            db.PdfReports.Add(report);
            await db.SaveChangesAsync();

            // ✅ USA NUOVO SISTEMA
            using var scope_ollama = _serviceProvider.CreateScope();
            var ollamaOptions = scope_ollama.ServiceProvider.GetRequiredService<IOptionsSnapshot<OllamaConfig>>();
            var httpClient = scope_ollama.ServiceProvider.GetRequiredService<HttpClient>();
            var aiGen = new PolarAiReportGenerator(db, ollamaOptions, httpClient);

            var insights = await aiGen.GeneratePolarAiInsightsAsync(vehicleId);
            
            if (string.IsNullOrWhiteSpace(insights))
                throw new InvalidOperationException($"No insights for {vehicle.Vin}");

            await GenerateReportFiles(db, report, insights, period, vehicle);

            _logger.LogInformation("✅ Report {Id} generated for {VIN} | Period: {Hours}h | Type: {Type} | Files: Markdown",
                                   report.Id, vehicle.Vin, period.DataHours, period.AnalysisLevel);
        }

        private async Task GenerateReportFiles(PolarDriveDbContext db,
                                               PdfReport report,
                                               string markdownContent,
                                               ReportPeriodInfo period,
                                               ClientVehicle vehicle)
        {
            // ✅ IL NUOVO SISTEMA GENERA MARKDOWN, NON HTML+PDF
            // Salva il Markdown direttamente
            var markdownPath = GetFilePath(report, "md", "reports");
            await SaveFile(markdownPath, markdownContent);

            // ✅ Se in development, salva anche come HTML per compatibilità
            if (_env.IsDevelopment())
            {
                var htmlPath = GetFilePath(report, "html", "dev-reports");
                var simpleHtml = ConvertMarkdownToSimpleHtml(markdownContent, report, vehicle);
                await SaveFile(htmlPath, simpleHtml);
            }

            _logger.LogInformation("✅ Files generated: Markdown saved, {DevFiles}",
                _env.IsDevelopment() ? "HTML dev file saved" : "production mode");
        }

        /// <summary>
        /// Converte Markdown in HTML semplice per development
        /// </summary>
        private string ConvertMarkdownToSimpleHtml(string markdown, PdfReport report, ClientVehicle vehicle)
        {

            // Conversione basilare Markdown -> HTML
            var html = markdown
                .Replace("# ", "<h1>")
                .Replace("## ", "<h2>")
                .Replace("### ", "<h3>")
                .Replace("- ", "<li>")
                .Replace("**", "<strong>");

            // Chiudi i tag aperti
            html = html.Replace("<h1>", "<h1>").Replace("\n", "</h1>\n")
                .Replace("<h2>", "<h2>").Replace("\n", "</h2>\n")
                .Replace("<h3>", "<h3>").Replace("\n", "</h3>\n")
                .Replace("<li>", "<li>").Replace("\n", "</li>\n")
                .Replace("<strong>", "<strong>").Replace("\n", "</strong>\n");        

            return $@"<!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='utf-8'>
                        <title>PolarDrive Report {report.Id}</title>
                        <style>
                            body {{ font-family: Arial, sans-serif; margin: 40px; line-height: 1.6; }}
                            h1 {{ color: #2c3e50; }}
                            h2 {{ color: #34495e; border-bottom: 2px solid #3498db; }}
                            table {{ border-collapse: collapse; width: 100%; margin: 20px 0; }}
                            th, td {{ border: 1px solid #ddd; padding: 12px; text-align: left; }}
                            th {{ background-color: #f2f2f2; }}
                        </style>
                    </head>
                    <body>
                        <div style='text-align: center; margin-bottom: 30px;'>
                            <h1>PolarDrive Report {report.Id}</h1>
                            <p>VIN: {vehicle.Vin} | Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm}</p>
                        </div>
                        <div>
                            {html}
                        </div>
                    </body>
                    </html>";
        }

        private void DeleteExistingFiles(PdfReport report)
        {
            try
            {
                var htmlPath = GetFilePath(report, "html", _env.IsDevelopment() ? "dev-reports" : "reports");
                var pdfPath = GetFilePath(report, "pdf", "reports");
                var markdownPath = GetFilePath(report, "md", "reports");

                if (File.Exists(htmlPath))
                {
                    File.Delete(htmlPath);
                    _logger.LogDebug("🗑️ Eliminato file HTML esistente: {Path}", htmlPath);
                }

                if (File.Exists(pdfPath))
                {
                    File.Delete(pdfPath);
                    _logger.LogDebug("🗑️ Eliminato file PDF esistente: {Path}", pdfPath);
                }

                if (File.Exists(markdownPath))
                {
                    File.Delete(markdownPath);
                    _logger.LogDebug("🗑️ Eliminato file Markdown esistente: {Path}", markdownPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Errore durante eliminazione file esistenti per report {ReportId}", report.Id);
            }
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

        private async Task<List<ClientVehicle>> GetVehiclesToProcess(PolarDriveDbContext db, ScheduleType scheduleType)
        {
            var query = db.ClientVehicles.Include(v => v.ClientCompany)
                           .Where(v => v.IsFetchingDataFlag);

            if (scheduleType != ScheduleType.Development && scheduleType != ScheduleType.Retry)
            {
                var (start, end) = GetDateRangeForSchedule(scheduleType);
                query = query.Where(v => db.VehiclesData
                    .Any(d => d.VehicleId == v.Id && d.Timestamp >= start && d.Timestamp <= end));
            }

            return await query.ToListAsync();
        }

        private (DateTime start, DateTime end) GetDateRangeForSchedule(ScheduleType scheduleType)
        {
            var now = DateTime.UtcNow;
            return scheduleType switch
            {
                ScheduleType.Daily => (now.AddDays(-1).Date, now.Date.AddSeconds(-1)),
                ScheduleType.Weekly => (now.AddDays(-7), now),
                ScheduleType.Monthly => (now.AddMonths(-1), now),
                _ => (now.AddHours(-24), now)
            };
        }

        private ReportInfo GetReportInfo(ScheduleType scheduleType, DateTime now)
        {
            return scheduleType switch
            {
                ScheduleType.Development => new ReportInfo { AnalysisType = "Development Analysis", DefaultDataHours = 0 },
                ScheduleType.Daily => new ReportInfo { AnalysisType = "Analisi Giornaliera", DefaultDataHours = 24 },
                ScheduleType.Weekly => new ReportInfo { AnalysisType = "Analisi Settimanale", DefaultDataHours = 168 },
                ScheduleType.Monthly => new ReportInfo { AnalysisType = "Analisi Mensile", DefaultDataHours = 720 },
                ScheduleType.Retry => new ReportInfo { AnalysisType = "Retry Analysis", DefaultDataHours = 24 },
                _ => new ReportInfo { AnalysisType = "Standard Analysis", DefaultDataHours = 24 }
            };
        }

        private static string GetFilePath(PdfReport report, string ext, string folder)
        {
            // ✅ USA LA DATA DI GENERAZIONE, NON IL PERIODO DEI DATI
            var generationDate = report.GeneratedAt ?? DateTime.UtcNow;

            return Path.Combine("storage", folder,
                generationDate.Year.ToString(),
                generationDate.Month.ToString("D2"),
                $"PolarDrive_Report_{report.Id}.{ext}");
        }

        private async Task SaveFile(string path, object content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            switch (content)
            {
                case string txt:
                    await File.WriteAllTextAsync(path, txt);
                    break;
                case byte[] data:
                    await File.WriteAllBytesAsync(path, data);
                    break;
            }
        }

        #endregion
    }
}