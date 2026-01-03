namespace PolarDrive.Data.Entities;

/// <summary>
/// Log dei fallimenti di fetch dati dai veicoli
/// Traccia il motivo specifico per cui un fetch non è andato a buon fine
/// Utilizzato per giustificare i gap nella certificazione probabilistica
/// </summary>
public class FetchFailureLog
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    /// <summary>
    /// Data/ora del tentativo di fetch fallito
    /// </summary>
    public DateTime AttemptedAt { get; set; }

    /// <summary>
    /// Motivo del fallimento (vedi FetchFailureReason enum)
    /// </summary>
    public string FailureReason { get; set; } = string.Empty;

    /// <summary>
    /// Dettagli aggiuntivi sull'errore (es. stack trace, messaggio eccezione)
    /// </summary>
    public string ErrorDetails { get; set; } = string.Empty;

    /// <summary>
    /// HTTP Status Code se disponibile
    /// </summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// URL della richiesta che ha fallito
    /// </summary>
    public string? RequestUrl { get; set; }

    /// <summary>
    /// Tempo di risposta prima del fallimento
    /// </summary>
    public long? ResponseTimeMs { get; set; }

    public ClientVehicle? ClientVehicle { get; set; }
}

/// <summary>
/// Enum per categorizzare i tipi di fallimento del fetch
/// </summary>
public static class FetchFailureReason
{
    public const string TESLA_API_UNAVAILABLE = "TESLA_API_UNAVAILABLE";
    public const string TESLA_API_RATE_LIMIT = "TESLA_API_RATE_LIMIT";
    public const string TESLA_VEHICLE_OFFLINE = "TESLA_VEHICLE_OFFLINE";
    public const string TESLA_VEHICLE_ASLEEP = "TESLA_VEHICLE_ASLEEP";
    public const string NETWORK_ERROR = "NETWORK_ERROR";
    public const string TIMEOUT = "TIMEOUT";
    public const string SERVER_ERROR = "SERVER_ERROR";
    public const string TOKEN_EXPIRED = "TOKEN_EXPIRED";
    public const string TOKEN_REFRESH_FAILED = "TOKEN_REFRESH_FAILED";
    public const string VEHICLE_NOT_FOUND = "VEHICLE_NOT_FOUND";
    public const string UNAUTHORIZED = "UNAUTHORIZED";
    public const string UNKNOWN = "UNKNOWN";

    /// <summary>
    /// Restituisce una descrizione leggibile del motivo del fallimento
    /// </summary>
    public static string GetDescription(string reason)
    {
        return reason switch
        {
            TESLA_API_UNAVAILABLE => "API Tesla non disponibile",
            TESLA_API_RATE_LIMIT => "Limite di richieste API Tesla raggiunto",
            TESLA_VEHICLE_OFFLINE => "Veicolo Tesla offline",
            TESLA_VEHICLE_ASLEEP => "Veicolo Tesla in modalità sleep",
            NETWORK_ERROR => "Errore di rete",
            TIMEOUT => "Timeout della richiesta",
            SERVER_ERROR => "Errore del server",
            TOKEN_EXPIRED => "Token di autenticazione scaduto",
            TOKEN_REFRESH_FAILED => "Impossibile aggiornare il token",
            VEHICLE_NOT_FOUND => "Veicolo non trovato",
            UNAUTHORIZED => "Non autorizzato",
            UNKNOWN => "Errore sconosciuto",
            _ => reason
        };
    }

    /// <summary>
    /// Indica se il fallimento è considerato un problema tecnico (non colpa dell'utente)
    /// </summary>
    public static bool IsTechnicalFailure(string reason)
    {
        return reason switch
        {
            TESLA_API_UNAVAILABLE => true,
            TESLA_API_RATE_LIMIT => true,
            NETWORK_ERROR => true,
            TIMEOUT => true,
            SERVER_ERROR => true,
            TOKEN_EXPIRED => true,
            TOKEN_REFRESH_FAILED => true,
            _ => false
        };
    }
}