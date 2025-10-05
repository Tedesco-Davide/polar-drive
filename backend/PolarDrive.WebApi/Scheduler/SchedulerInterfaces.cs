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
    public int TotalProcessed => SuccessCount + ErrorCount;
}

public class RetryResults
{
    public int ProcessedCount { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
}

public class ReportInfo
{
    public DateTime ReportPeriodStart { get; set; }
    public DateTime ReportPeriodEnd { get; set; }
    public string AnalysisType { get; set; } = "";
    public int DefaultDataHours { get; set; }
    public ReportInfo() { }
    public ReportInfo(DateTime reportPeriodStart, DateTime reportPeriodEnd)
    {
        ReportPeriodStart = reportPeriodStart;
        ReportPeriodEnd = reportPeriodEnd;
    }
}

public class ReportPeriodInfo
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int DataHours { get; set; }
    public string AnalysisLevel { get; set; } = "";
    public double MonitoringDays { get; set; }
}