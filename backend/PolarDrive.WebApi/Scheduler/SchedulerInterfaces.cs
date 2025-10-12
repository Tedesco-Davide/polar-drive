namespace PolarDrive.WebApi.Scheduler;

public enum ScheduleType
{
    Development,
    Monthly,
    Retry
}

public class SchedulerResults
{
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
}

public class RetryResults
{
    public int ProcessedCount { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
}