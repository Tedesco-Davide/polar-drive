using PolarDrive.WebApi.Services;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Scheduler
{
    public class PolarDriveScheduler(
        ILogger<PolarDriveScheduler> logger,
        IWebHostEnvironment env,
        IServiceProvider provider) : BackgroundService
    {
        private readonly ILogger<PolarDriveScheduler> _logger = logger;
        private readonly IWebHostEnvironment _env = env;
        private readonly IServiceProvider _provider = provider;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Starting PolarDriveScheduler in {Mode}", _env.IsDevelopment() ? "DEV" : "PROD");

            if (_env.IsDevelopment())
            {
                await Task.Delay(TimeSpan.FromMinutes(DEV_INITIAL_DELAY_MINUTES), stoppingToken);
                await RunDevelopmentLoop(stoppingToken);
            }
            else
            {
                await RunProductionSchedulers(stoppingToken);
            }
        }

        private async Task RunDevelopmentLoop(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔧 DEV Mode: running every {Minutes} minute(s)", DEV_REPEAT_DELAY_MINUTES);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Creo uno scope per risolvere IReportGenerationService
                    using var scope = _provider.CreateScope();
                    var schedulerService = scope.ServiceProvider.GetRequiredService<IReportGenerationService>();

                    var results = await schedulerService.ProcessScheduledReportsAsync(ScheduleType.Development, stoppingToken);
                    LogResults("DEV", results);

                    var retryResults = await schedulerService.ProcessRetriesAsync(stoppingToken);
                    if (retryResults.ProcessedCount > 0)
                    {
                        LogRetryResults(retryResults);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ DEV Mode: error in scheduler loop");
                }

                await Task.Delay(TimeSpan.FromMinutes(DEV_REPEAT_DELAY_MINUTES), stoppingToken);
            }
        }

        private async Task RunProductionSchedulers(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🏭 PRODUCTION Mode: starting scheduled tasks");

            var monthlyTask = ScheduleRecurring(
                async () =>
                {
                    using var scope = _provider.CreateScope();
                    var schedulerService = scope.ServiceProvider.GetRequiredService<IReportGenerationService>();

                    var results = await schedulerService.ProcessScheduledReportsAsync(ScheduleType.Monthly, stoppingToken);
                    LogResults("MONTHLY", results);
                },
                GetInitialDelayForFirstOfMonth(new TimeSpan(
                    PROD_MONTHLY_EXECUTION_HOUR,
                    PROD_MONTHLY_EXECUTION_MINUTE,
                    PROD_MONTHLY_EXECUTION_SECOND)),
                TimeSpan.FromDays(PROD_MONTHLY_REPEAT_DAYS),
                stoppingToken);

            var retryTask = ScheduleRecurring(
                async () =>
                {
                    using var scope = _provider.CreateScope();
                    var schedulerService = scope.ServiceProvider.GetRequiredService<IReportGenerationService>();

                    var retryResults = await schedulerService.ProcessRetriesAsync(stoppingToken);
                    if (retryResults.ProcessedCount > 0)
                    {
                        LogRetryResults(retryResults);
                    }
                },
                TimeSpan.Zero,
                TimeSpan.FromHours(PROD_RETRY_REPEAT_HOURS),
                stoppingToken);

            await Task.WhenAll(monthlyTask, retryTask);
        }

        private async Task ScheduleRecurring(Func<Task> action, TimeSpan initialDelay, TimeSpan period, CancellationToken token)
        {
            if (initialDelay > TimeSpan.Zero)
                await Task.Delay(initialDelay, token);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await action();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in scheduled task");
                }

                await Task.Delay(period, token);
            }
        }

        private TimeSpan GetInitialDelayForFirstOfMonth(TimeSpan timeOfDay)
        {
            var now = DateTime.Now;
            var next = new DateTime(now.Year, now.Month, 1).AddMonths(now.Day == 1 && now.TimeOfDay < timeOfDay ? 0 : 1).Add(timeOfDay);
            return next - now;
        }

        private void LogResults(string type, SchedulerResults results)
        {
            if (results.SuccessCount > 0 || results.ErrorCount > 0)
                _logger.LogInformation("📊 {Type} Results | Success: {Success} | Errors: {Errors}", type, results.SuccessCount, results.ErrorCount);
        }

        private void LogRetryResults(RetryResults results)
        {
            _logger.LogInformation("🔄 Retry Results | Processed: {Processed} | Success: {Success} | Failed: {Failed}",
                results.ProcessedCount, results.SuccessCount, results.ErrorCount);
        }
    }
}
