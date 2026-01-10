namespace PolarDrive.WebApi.Services.Tsa;

/// <summary>
/// Risultato di una richiesta di marca temporale TSA.
/// </summary>
public class TsaResult
{
    /// <summary>
    /// True se la richiesta è andata a buon fine.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Token TSA RFC 3161 (DER-encoded bytes).
    /// Contiene la marca temporale firmata dal server TSA.
    /// </summary>
    public byte[]? TimestampToken { get; set; }

    /// <summary>
    /// Data/ora della marca temporale estratta dal token RFC 3161.
    /// </summary>
    public DateTime? TimestampDate { get; set; }

    /// <summary>
    /// Hash dell'impronta del documento (MessageImprint) contenuto nel token TSA.
    /// Deve corrispondere all'hash del documento originale.
    /// </summary>
    public string? MessageImprint { get; set; }

    /// <summary>
    /// URL del server TSA che ha emesso la marca temporale.
    /// </summary>
    public string? ServerUrl { get; set; }

    /// <summary>
    /// Nome del provider TSA (es. "FreeTSA", "Aruba TSA").
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// Messaggio di errore in caso di fallimento.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Tempo impiegato per la richiesta (ms).
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// Crea un risultato di successo.
    /// </summary>
    public static TsaResult Ok(byte[] token, DateTime timestampDate, string messageImprint, string serverUrl, string providerName, long elapsedMs)
    {
        return new TsaResult
        {
            Success = true,
            TimestampToken = token,
            TimestampDate = timestampDate,
            MessageImprint = messageImprint,
            ServerUrl = serverUrl,
            ProviderName = providerName,
            ElapsedMilliseconds = elapsedMs
        };
    }

    /// <summary>
    /// Crea un risultato di errore.
    /// </summary>
    public static TsaResult Error(string errorMessage, string serverUrl, string providerName, long elapsedMs = 0)
    {
        return new TsaResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ServerUrl = serverUrl,
            ProviderName = providerName,
            ElapsedMilliseconds = elapsedMs
        };
    }
}

/// <summary>
/// Risultato della verifica di un token TSA.
/// </summary>
public class TsaVerifyResult
{
    /// <summary>
    /// True se il token è valido e corrisponde al contenuto.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Data/ora della marca temporale (se valida).
    /// </summary>
    public DateTime? TimestampDate { get; set; }

    /// <summary>
    /// Hash del documento nel token (MessageImprint).
    /// </summary>
    public string? MessageImprint { get; set; }

    /// <summary>
    /// Messaggio di errore in caso di verifica fallita.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Crea un risultato di verifica positiva.
    /// </summary>
    public static TsaVerifyResult Valid(DateTime timestampDate, string messageImprint)
    {
        return new TsaVerifyResult
        {
            IsValid = true,
            TimestampDate = timestampDate,
            MessageImprint = messageImprint
        };
    }

    /// <summary>
    /// Crea un risultato di verifica negativa.
    /// </summary>
    public static TsaVerifyResult Invalid(string errorMessage)
    {
        return new TsaVerifyResult
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };
    }
}
