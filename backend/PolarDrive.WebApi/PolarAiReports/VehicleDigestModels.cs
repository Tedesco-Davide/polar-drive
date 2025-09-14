namespace PolarDrive.WebApi.PolarAiReports;

// DTO per digest dati aggregati (solo numeri finali)
public class VehicleDataDigest
{
    // Periodo analisi
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int TotalHours { get; set; }
    public int TotalSamples { get; set; }

    // Metriche batteria
    public BatteryMetrics Battery { get; set; } = new();

    // Metriche ricarica
    public ChargingMetrics Charging { get; set; } = new();

    // Metriche guida
    public DrivingMetrics Driving { get; set; } = new();

    // Metriche climatiche
    public ClimateMetrics Climate { get; set; } = new();

    // Metriche efficienza
    public EfficiencyMetrics Efficiency { get; set; } = new();

    // Qualità dati
    public DataQualityMetrics Quality { get; set; } = new();
}

public class BatteryMetrics
{
    public decimal AvgLevel { get; set; }
    public int MinLevel { get; set; }
    public int MaxLevel { get; set; }
    public decimal AvgRange { get; set; }
    public decimal MinRange { get; set; }
    public decimal MaxRange { get; set; }
    public int ChargeCycles { get; set; }
    public decimal HealthScore { get; set; } // 0-100
}

public class ChargingMetrics
{
    public int TotalSessions { get; set; }
    public decimal TotalEnergyAdded { get; set; } // kWh
    public decimal TotalCost { get; set; }
    public decimal AvgCostPerKwh { get; set; }
    public decimal MinCostPerKwh { get; set; }
    public decimal MaxCostPerKwh { get; set; }
    public decimal AvgSessionDuration { get; set; } // minuti
    public List<string> TopStations { get; set; } = new(); // max 3
    public decimal HomeChargingPercentage { get; set; }
}

public class DrivingMetrics
{
    public decimal TotalDistance { get; set; } // km
    public decimal AvgSpeed { get; set; }
    public decimal MaxSpeed { get; set; }
    public decimal AvgPowerConsumption { get; set; } // W
    public decimal RegenerationEnergy { get; set; } // kWh
    public int TripsCount { get; set; }
    public decimal AvgTripDistance { get; set; }
    public Dictionary<string, int> DirectionDistribution { get; set; } = new(); // N, S, E, W percentages
}

public class ClimateMetrics
{
    public decimal AvgInsideTemp { get; set; }
    public decimal AvgOutsideTemp { get; set; }
    public decimal MinInsideTemp { get; set; }
    public decimal MaxInsideTemp { get; set; }
    public decimal ClimateUsagePercentage { get; set; }
    public decimal AvgTempDifference { get; set; }
    public decimal ClimateEnergyImpact { get; set; } // stima percentuale impatto autonomia
}

public class EfficiencyMetrics
{
    public decimal OverallEfficiency { get; set; } // km/kWh
    public decimal CityEfficiency { get; set; }
    public decimal HighwayEfficiency { get; set; }
    public decimal OptimalSpeedRange { get; set; } // km/h per max efficienza
    public List<string> EfficiencyTips { get; set; } = new(); // max 3 suggerimenti
}

public class DataQualityMetrics
{
    public decimal UptimePercentage { get; set; }
    public int DataGaps { get; set; }
    public int QualityScore { get; set; } // 0-100
    public string QualityLabel { get; set; } = ""; // Eccellente, Buono, etc.
    public decimal SamplingFrequency { get; set; } // campioni/ora
}

// Schema JSON per output LLM
public class ReportOutputSchema
{
    public ExecutiveSummary ExecutiveSummary { get; set; } = new();
    public TechnicalAnalysis TechnicalAnalysis { get; set; } = new();
    public PredictiveInsights PredictiveInsights { get; set; } = new();
    public Recommendations Recommendations { get; set; } = new();
}

public class ExecutiveSummary
{
    public string CurrentStatus { get; set; } = "";
    public List<string> KeyFindings { get; set; } = new(); // max 4
    public List<string> CriticalAlerts { get; set; } = new(); // max 2
    public string OverallTrend { get; set; } = "";
}

public class TechnicalAnalysis
{
    public string BatteryAssessment { get; set; } = "";
    public string ChargingBehavior { get; set; } = "";
    public string DrivingPatterns { get; set; } = "";
    public string EfficiencyAnalysis { get; set; } = "";
}

public class PredictiveInsights
{
    public string NextMonthPrediction { get; set; } = "";
    public List<string> RiskFactors { get; set; } = new(); // max 3
    public List<string> OpportunityAreas { get; set; } = new(); // max 3
    public string MaintenanceRecommendations { get; set; } = "";
}

public class Recommendations
{
    public List<ActionItem> ImmediateActions { get; set; } = new(); // max 3
    public List<ActionItem> MediumTermActions { get; set; } = new(); // max 3
    public string OptimizationStrategy { get; set; } = "";
}

public class ActionItem
{
    public string Action { get; set; } = "";
    public string Benefit { get; set; } = "";
    public string Timeline { get; set; } = "";
}