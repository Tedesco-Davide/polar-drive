namespace PolarDrive.WebApi.Services;

/// <summary>
/// Servizio per il monitoraggio proattivo delle anomalie Gap.
/// Rileva automaticamente situazioni critiche e crea alert.
/// </summary>
public interface IGapMonitoringService
{
    /// <summary>
    /// Scansiona tutti i veicoli attivi per anomalie Gap
    /// </summary>
    Task CheckAllVehiclesAsync();

    /// <summary>
    /// Analizza un singolo veicolo per anomalie Gap
    /// </summary>
    Task CheckVehicleAsync(int vehicleId);

    /// <summary>
    /// Ottiene statistiche aggregate degli alert
    /// </summary>
    Task<GapAlertStats> GetAlertStatsAsync();
}

/// <summary>
/// Statistiche aggregate degli alert Gap
/// </summary>
public class GapAlertStats
{
    public int TotalAlerts { get; set; }
    public int OpenAlerts { get; set; }
    public int EscalatedAlerts { get; set; }
    public int CompletedAlerts { get; set; }
    public int ContractBreachAlerts { get; set; }
    public int CriticalAlerts { get; set; }
    public int WarningAlerts { get; set; }
    public int InfoAlerts { get; set; }
}
