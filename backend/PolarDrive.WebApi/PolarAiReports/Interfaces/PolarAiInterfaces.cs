namespace PolarDrive.WebApi.PolarAiReports;

// Opzioni per la conversione PDF
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


// Classe helper per l'analisi dei dati
public class VehicleDataAnalysis
{
    // Vehicle State
    public bool HasVehicleData { get; set; }
    public int TotalLocked { get; set; }
    public int TotalSentryMode { get; set; }
    public decimal TotalOdometer { get; set; }
    public int OdometerCount { get; set; }

    // Charge State
    public bool HasChargeData { get; set; }
    public int TotalBatteryLevel { get; set; }
    public decimal TotalRange { get; set; }
    public int TotalChargeLimit { get; set; }
    public int ChargeCount { get; set; }
    public int MinBatteryLevel { get; set; } = int.MaxValue;
    public int MaxBatteryLevel { get; set; } = int.MinValue;
    public decimal MinRange { get; set; } = decimal.MaxValue;
    public decimal MaxRange { get; set; } = decimal.MinValue;

    // Climate State
    public bool HasClimateData { get; set; }
    public decimal TotalInsideTemp { get; set; }
    public decimal TotalOutsideTemp { get; set; }
    public int TotalClimateOn { get; set; }
    public decimal TotalDriverTemp { get; set; }
    public decimal TotalPassengerTemp { get; set; }
    public int ClimateCount { get; set; }
    public decimal MinInsideTemp { get; set; } = decimal.MaxValue;
    public decimal MaxInsideTemp { get; set; } = decimal.MinValue;
    public decimal MinOutsideTemp { get; set; } = decimal.MaxValue;
    public decimal MaxOutsideTemp { get; set; } = decimal.MinValue;

    // TPMS Data
    public bool HasTpmsData { get; set; }
    public decimal TotalTpmsFL { get; set; }
    public decimal TotalTpmsFR { get; set; }
    public decimal TotalTpmsRL { get; set; }
    public decimal TotalTpmsRR { get; set; }
    public int TpmsCount { get; set; }

    public int TotalSamples { get; set; }

    // Proprietà calcolate
    public bool AvgSentryMode => TotalSamples > 0 && (double)TotalSentryMode / TotalSamples > 0.5;
    public decimal AvgBatteryLevel => ChargeCount > 0 ? (decimal)TotalBatteryLevel / ChargeCount : 0;
    public decimal AvgRange => ChargeCount > 0 ? TotalRange / ChargeCount : 0;
    public decimal AvgChargeLimit => ChargeCount > 0 ? (decimal)TotalChargeLimit / ChargeCount : 0;
    public decimal AvgInsideTemp => ClimateCount > 0 ? TotalInsideTemp / ClimateCount : 0;
    public decimal AvgDriverTemp => ClimateCount > 0 ? TotalDriverTemp / ClimateCount : 0;
    public decimal AvgPassengerTemp => ClimateCount > 0 ? TotalPassengerTemp / ClimateCount : 0;
    public bool AvgClimateOn => ClimateCount > 0 && (double)TotalClimateOn / ClimateCount > 0.5;
    public decimal AvgOutsideTemp => ClimateCount > 0 ? TotalOutsideTemp / ClimateCount : 0;
    public decimal AvgOdometer => OdometerCount > 0 ? TotalOdometer / OdometerCount : 0;
    public bool AvgLocked => TotalSamples > 0 && (double)TotalLocked / TotalSamples > 0.5;
    public decimal AvgTpmsFL => TpmsCount > 0 ? TotalTpmsFL / TpmsCount : 0;
    public decimal AvgTpmsFR => TpmsCount > 0 ? TotalTpmsFR / TpmsCount : 0;
    public decimal AvgTpmsRL => TpmsCount > 0 ? TotalTpmsRL / TpmsCount : 0;
    public decimal AvgTpmsRR => TpmsCount > 0 ? TotalTpmsRR / TpmsCount : 0;

    public void FinalizeAverages(int validSamples)
    {
        // Reset dei minimi se non ci sono dati validi
        if (MinBatteryLevel == int.MaxValue) MinBatteryLevel = 0;
        if (MaxBatteryLevel == int.MinValue) MaxBatteryLevel = 0;
        if (MinRange == decimal.MaxValue) MinRange = 0;
        if (MaxRange == decimal.MinValue) MaxRange = 0;
        if (MinInsideTemp == decimal.MaxValue) MinInsideTemp = 0;
        if (MaxInsideTemp == decimal.MinValue) MaxInsideTemp = 0;
        if (MinOutsideTemp == decimal.MaxValue) MinOutsideTemp = 0;
        if (MaxOutsideTemp == decimal.MinValue) MaxOutsideTemp = 0;
    }
}