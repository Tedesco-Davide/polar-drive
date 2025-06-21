using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.PolarAiReports;
using PolarDrive.WebApi.Scheduler;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Services
{
    public interface IReportGenerationService
    {
        Task<SchedulerResults> ProcessScheduledReportsAsync(ScheduleType scheduleType, CancellationToken stoppingToken = default);
        Task<RetryResults> ProcessRetriesAsync(CancellationToken stoppingToken = default);
        Task ForceRegenerateFilesAsync(int reportId);
    }

    public class ReportGenerationService(IServiceProvider serviceProvider,
                                  ILogger<ReportGenerationService> logger,
                                  IWebHostEnvironment env) : IReportGenerationService
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ILogger<ReportGenerationService> _logger = logger;
        private readonly IWebHostEnvironment _env = env;
        private readonly Dictionary<int, DateTime> _lastReportAttempts = new();
        private readonly Dictionary<int, int> _retryCount = new();

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

                    // 2) Scelgo quante ore guardare indietro in base al tipo di scheduler
                    int thresholdHours = scheduleType switch
                    {
                        ScheduleType.Development => DAILY_HOURS_THRESHOLD,
                        ScheduleType.Daily => DAILY_HOURS_THRESHOLD,
                        ScheduleType.Weekly => WEEKLY_HOURS_THRESHOLD,
                        ScheduleType.Monthly => MONTHLY_HOURS_THRESHOLD,
                        _ => DAILY_HOURS_THRESHOLD
                    };

                    // 3) Calcolo start/end
                    var start = lastReportEnd ?? now.AddHours(-thresholdHours);
                    var end = now;

                    _logger.LogInformation("🔍 DEBUG: Vehicle {VIN} - Start: {Start}, End: {End}, Now: {Now}", v.Vin, start, end, now);

                    // 4) Infine genero con questi parametri
                    var info = new ReportInfo(start, end);
                    await GenerateReportForVehicle(db, v.Id, info);

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
                    var info = GetReportInfo(ScheduleType.Retry, now);
                    await GenerateReportForVehicle(db, id, info);

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

            // 2) Ricalcola gli insights (stessa logica di GenerateReportForVehicle)
            var aiGen = new PolarAiReportGenerator(db);
            var insights = await aiGen.GeneratePolarAiInsightsAsync(vehicle!.Id);
            if (string.IsNullOrWhiteSpace(insights))
                throw new InvalidOperationException($"Nessun insight per {vehicle.Vin}");

            // 3) Crea un ReportPeriodInfo a partire dal report esistente
            var period = new ReportPeriodInfo
            {
                Start = report.ReportPeriodStart,
                End = report.ReportPeriodEnd,
                DataHours = (int)(report.ReportPeriodEnd - report.ReportPeriodStart).TotalHours,
                AnalysisLevel = report.Notes,
                MonitoringDays = (report.ReportPeriodEnd - report.ReportPeriodStart).TotalDays
            };

            // 4) Rigenera soltanto i file
            await GenerateReportFiles(db, report, insights, period, vehicle);

            _logger.LogInformation("✅ File rigenerati per report {ReportId}", reportId);
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

        private async Task<ReportPeriodInfo> CalculateReportPeriod(PolarDriveDbContext db, int vehicleId, ReportInfo info)
        {
            var first = await db.VehiclesData
                .Where(d => d.VehicleId == vehicleId)
                .OrderBy(d => d.Timestamp)
                .Select(d => d.Timestamp)
                .FirstOrDefaultAsync();

            var now = DateTime.UtcNow;
            var days = (now - first).TotalDays;

            return _env.IsDevelopment()
                ? CalculateDevReportPeriod(now, days)
                : CalculateProductionReportPeriod(info.AnalysisType, now, days);
        }

        private ReportPeriodInfo CalculateDevReportPeriod(DateTime now, double days)
        {
            var mins = days * 24 * 60;
            var (hrs, lvl) = mins switch
            {
                < 5 => (1, "Valutazione Iniziale"),
                < 15 => (6, "Analisi Rapida"),
                < 30 => (24, "Pattern Recognition"),
                < 60 => (168, "Behavioral Analysis"),
                _ => (720, "Deep Dive Analysis")
            };

            return new ReportPeriodInfo
            {
                Start = now.AddHours(-hrs),
                End = now,
                DataHours = hrs,
                AnalysisLevel = $"{lvl} ({days:F1}d monitoring)",
                MonitoringDays = days
            };
        }

        private ReportPeriodInfo CalculateProductionReportPeriod(string type, DateTime now, double days)
        {
            DateTime start;
            DateTime end;
            int hrs;

            switch (type)
            {
                case "Analisi Giornaliera":
                    start = now.AddDays(-1).Date;
                    end = now.Date.AddSeconds(-1);
                    hrs = 24;
                    break;
                case "Analisi Settimanale":
                    var monday = now.AddDays(-7 - (int)now.DayOfWeek + 1).Date;
                    start = monday;
                    end = monday.AddDays(6).AddSeconds(-1);
                    hrs = 168;
                    break;
                case "Analisi Mensile":
                    var firstOfMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                    start = firstOfMonth;
                    end = new DateTime(now.Year, now.Month, 1).AddSeconds(-1);
                    hrs = 720;
                    break;
                default:
                    start = now.AddHours(-24);
                    end = now;
                    hrs = 24;
                    break;
            }

            return new ReportPeriodInfo
            {
                Start = start,
                End = end,
                DataHours = hrs,
                AnalysisLevel = $"{type} ({days:F1}d totali)",
                MonitoringDays = days
            };
        }

        private async Task GenerateReportForVehicle(PolarDriveDbContext db, int vehicleId, ReportInfo info)
        {
            var vehicle = await db.ClientVehicles
                                  .Include(v => v.ClientCompany)
                                  .FirstOrDefaultAsync(v => v.Id == vehicleId)
                         ?? throw new InvalidOperationException($"Vehicle {vehicleId} not found");

            if (!vehicle.IsActiveFlag && vehicle.IsFetchingDataFlag)
                _logger.LogWarning("⏳ Grace Period for {VIN}", vehicle.Vin);

            var period = await CalculateReportPeriod(db, vehicleId, info);
            _logger.LogInformation("🧠 Generating {Level} for {VIN} | {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}",
                                   period.AnalysisLevel, vehicle.Vin, period.Start, period.End);

            var aiGen = new PolarAiReportGenerator(db);
            var insights = await aiGen.GeneratePolarAiInsightsAsync(vehicleId);
            if (string.IsNullOrWhiteSpace(insights))
                throw new InvalidOperationException($"No insights for {vehicle.Vin}");

            var report = new PdfReport
            {
                ClientVehicleId = vehicleId,
                ClientCompanyId = vehicle.ClientCompanyId,
                ReportPeriodStart = period.Start,
                ReportPeriodEnd = period.End,
                GeneratedAt = DateTime.UtcNow,
                Notes = $"[{period.AnalysisLevel}] DataHours: {period.DataHours}, Monitoring: {period.MonitoringDays:F1}"
            };

            db.PdfReports.Add(report);
            await db.SaveChangesAsync();

            await GenerateReportFiles(db, report, insights, period, vehicle);
            _logger.LogInformation("✅ Report {Id} generated for {VIN}", report.Id, vehicle.Vin);
        }

        private async Task GenerateReportFiles(PolarDriveDbContext db,
                                               PdfReport report,
                                               string insights,
                                               ReportPeriodInfo period,
                                               ClientVehicle vehicle)
        {
            // HTML
            var htmlSvc = new HtmlReportService(db);
            var htmlOpt = new HtmlReportOptions
            {
                ShowDetailedStats = true,
                ShowRawData = false,
                ReportType = $"🧠 {period.AnalysisLevel}",
                AdditionalCss = PolarAiReports.Templates.DefaultCssTemplate.Value
            };
            var html = await htmlSvc.GenerateHtmlReportAsync(report, insights, htmlOpt);

            if (_env.IsDevelopment())
            {
                var path = GetFilePath(report, "html", "dev-reports");
                await SaveFile(path, html);
            }

            // PDF
            var pdfSvc = new PdfGenerationService(db);
            var pdfOpt = new PdfConversionOptions
            {
                PageFormat = "A4",
                MarginTop = "2cm",
                MarginBottom = "2cm",
                MarginLeft = "1.5cm",
                MarginRight = "1.5cm",
                DisplayHeaderFooter = true,
                HeaderTemplate = $"<div style='font-size:10px;text-align:center;color:#004E92;border-bottom:1px solid #004E92;padding-bottom:5px;'>{vehicle.Vin} - {DateTime.UtcNow:yyyy-MM-dd HH:mm}</div>",
                FooterTemplate = "<div style='font-size:10px;text-align:center;color:#666;border-top:1px solid #ccc;padding-top:5px;'>Pagina <span class='pageNumber'></span> di <span class='totalPages'></span> | DataPolar Analytics</div>"
            };
            var pdfBytes = await pdfSvc.ConvertHtmlToPdfAsync(html, report, pdfOpt);

            var pdfPath = GetFilePath(report, "pdf", "reports");
            await SaveFile(pdfPath, pdfBytes);
        }

        private string GetFilePath(PdfReport report, string ext, string folder)
        {
            return Path.Combine("storage", folder,
                report.ReportPeriodStart.Year.ToString(),
                report.ReportPeriodStart.Month.ToString("D2"),
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
    }
}
