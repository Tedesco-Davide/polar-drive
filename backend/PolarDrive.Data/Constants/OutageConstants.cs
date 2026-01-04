namespace PolarDrive.Data.Constants;

public static class OutageConstants
{
    // Costanti singole per accesso diretto
    public const string OUTAGE_VEHICLE = "Outage Vehicle";
    public const string OUTAGE_FLEET_API = "Outage Fleet Api";
    public const string STATUS_ONGOING = "OUTAGE-ONGOING";
    public const string STATUS_RESOLVED = "OUTAGE-RESOLVED";

    // Array per validazione
    public static readonly string[] ValidOutageTypes =
    {
        OUTAGE_VEHICLE,
        OUTAGE_FLEET_API
    };
    public static readonly string[] ValidOutageStatuses =
    {
        STATUS_ONGOING,
        STATUS_RESOLVED
    };
}