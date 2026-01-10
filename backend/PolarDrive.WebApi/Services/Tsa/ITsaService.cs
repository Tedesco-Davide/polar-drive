namespace PolarDrive.WebApi.Services.Tsa;

/// <summary>
/// Interfaccia per servizi di Timestamp Authority (TSA) - RFC 3161.
/// Utilizzato per ottenere marche temporali certificate sui PDF generati.
/// DEV: FreeTSA (gratuito, non qualificato)
/// PROD: Aruba TSA (a pagamento, qualificato eIDAS)
/// </summary>
public interface ITsaService
{
    /// <summary>
    /// Richiede una marca temporale RFC 3161 per il contenuto specificato.
    /// </summary>
    /// <param name="content">Contenuto binario (es. PDF) da marcare temporalmente</param>
    /// <param name="contentHash">Hash SHA-256 del contenuto (già calcolato)</param>
    /// <returns>Risultato con token TSA o errore</returns>
    Task<TsaResult> RequestTimestampAsync(byte[] content, string contentHash);

    /// <summary>
    /// Verifica un token TSA contro il contenuto originale.
    /// </summary>
    /// <param name="tsaToken">Token TSA RFC 3161 (DER-encoded)</param>
    /// <param name="originalContent">Contenuto originale per verifica</param>
    /// <returns>Risultato della verifica</returns>
    Task<TsaVerifyResult> VerifyTimestampAsync(byte[] tsaToken, byte[] originalContent);

    /// <summary>
    /// Nome del provider TSA (per logging e audit).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// URL del server TSA utilizzato.
    /// </summary>
    string ServerUrl { get; }
}
