namespace PolarDrive.Services;

/// <summary>
/// Interfaccia per il servizio di rilevamento outage
/// </summary>
public interface IOutageDetectionService
{
    /// <summary>
    /// Controlla lo stato delle Fleet API per ogni brand e rileva outages
    /// </summary>
    Task CheckFleetApiOutagesAsync();

    /// <summary>
    /// Controlla lo stato di ogni veicolo e rileva outages individuali
    /// </summary>
    Task CheckVehicleOutagesAsync();

    /// <summary>
    /// Risolve automaticamente gli outages che sono tornati online
    /// </summary>
    Task ResolveOutagesAsync();
}