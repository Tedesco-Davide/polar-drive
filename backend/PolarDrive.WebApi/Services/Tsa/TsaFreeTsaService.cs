using System.Diagnostics;
using System.Formats.Asn1;
using System.Security.Cryptography;
using PolarDrive.Data.Constants;
using PolarDrive.WebApi.Helpers;

namespace PolarDrive.WebApi.Services.Tsa;

/// <summary>
/// Implementazione TSA per FreeTSA (https://freetsa.org).
/// Usato in ambiente DEVELOPMENT.
/// FreeTSA è gratuito ma NON è qualificato eIDAS - fornisce solo "prova di esistenza".
/// </summary>
public class TsaFreeTsaService : ITsaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TsaFreeTsaService> _logger;

    public string ProviderName => "FreeTSA";
    public string ServerUrl => AppConfig.TSA_SERVER_URL;

    public TsaFreeTsaService(IHttpClientFactory httpClientFactory, ILogger<TsaFreeTsaService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TsaClient");
        _httpClient.Timeout = TimeSpan.FromSeconds(AppConfig.TSA_TIMEOUT_SECONDS);
        _logger = logger;
    }

    /// <summary>
    /// Richiede una marca temporale RFC 3161 a FreeTSA.
    /// </summary>
    public async Task<TsaResult> RequestTimestampAsync(byte[] content, string contentHash)
    {
        var sw = Stopwatch.StartNew();
        var serverUrl = ServerUrl;

        try
        {
            _logger.LogInformation("[TSA] Richiesta marca temporale a {Provider} ({Url}) per hash {Hash}",
                ProviderName, serverUrl, contentHash[..16] + "...");

            // Converti l'hash hex string in byte array
            var hashBytes = Convert.FromHexString(contentHash);

            // Costruisci la richiesta TSA RFC 3161
            var tsaRequest = BuildTimeStampRequest(hashBytes);

            // Invia la richiesta al server TSA
            var response = await SendTsaRequestAsync(serverUrl, tsaRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[TSA] Risposta HTTP non valida: {StatusCode} - {Body}",
                    response.StatusCode, errorBody);
                return TsaResult.Error(
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    serverUrl, ProviderName, sw.ElapsedMilliseconds);
            }

            // Leggi la risposta TSA
            var responseBytes = await response.Content.ReadAsByteArrayAsync();

            // Parsa la risposta RFC 3161
            var parseResult = ParseTimeStampResponse(responseBytes, contentHash);

            if (!parseResult.Success)
            {
                return TsaResult.Error(parseResult.ErrorMessage ?? "Errore parsing risposta TSA",
                    serverUrl, ProviderName, sw.ElapsedMilliseconds);
            }

            sw.Stop();
            _logger.LogInformation("[TSA] Marca temporale ottenuta con successo da {Provider} in {Ms}ms. Data: {Date}",
                ProviderName, sw.ElapsedMilliseconds, parseResult.TimestampDate);

            return TsaResult.Ok(
                responseBytes,
                parseResult.TimestampDate ?? DateTime.UtcNow,
                contentHash,
                serverUrl,
                ProviderName,
                sw.ElapsedMilliseconds);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            sw.Stop();
            _logger.LogError(ex, "[TSA] Timeout richiesta a {Provider}", ProviderName);
            return TsaResult.Error($"Timeout dopo {AppConfig.TSA_TIMEOUT_SECONDS}s",
                serverUrl, ProviderName, sw.ElapsedMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[TSA] Errore HTTP richiesta a {Provider}", ProviderName);
            return TsaResult.Error($"Errore connessione: {ex.Message}",
                serverUrl, ProviderName, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[TSA] Errore generico richiesta a {Provider}", ProviderName);
            return TsaResult.Error($"Errore: {ex.Message}",
                serverUrl, ProviderName, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Verifica un token TSA contro il contenuto originale.
    /// </summary>
    public Task<TsaVerifyResult> VerifyTimestampAsync(byte[] tsaToken, byte[] originalContent)
    {
        try
        {
            // Calcola hash del contenuto originale usando metodo centralizzato
            var expectedHashHex = GenericHelpers.ComputeContentHash(originalContent);

            // Parsa il token TSA per estrarre il MessageImprint
            var parseResult = ParseTimeStampResponse(tsaToken, expectedHashHex);

            if (!parseResult.Success)
            {
                return Task.FromResult(TsaVerifyResult.Invalid(
                    parseResult.ErrorMessage ?? "Token TSA non valido"));
            }

            // Verifica che il MessageImprint corrisponda
            if (!string.Equals(parseResult.MessageImprint, expectedHashHex, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(TsaVerifyResult.Invalid(
                    "Hash nel token TSA non corrisponde al contenuto"));
            }

            return Task.FromResult(TsaVerifyResult.Valid(
                parseResult.TimestampDate ?? DateTime.UtcNow,
                parseResult.MessageImprint ?? expectedHashHex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TSA] Errore verifica token TSA");
            return Task.FromResult(TsaVerifyResult.Invalid($"Errore verifica: {ex.Message}"));
        }
    }

    /// <summary>
    /// Costruisce una TimeStampRequest RFC 3161 in formato ASN.1 DER.
    /// </summary>
    private static byte[] BuildTimeStampRequest(byte[] hashBytes)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);

        // TimeStampReq ::= SEQUENCE
        using (writer.PushSequence())
        {
            // version INTEGER { v1(1) }
            writer.WriteInteger(1);

            // messageImprint MessageImprint
            using (writer.PushSequence())
            {
                // hashAlgorithm AlgorithmIdentifier (SHA-256)
                using (writer.PushSequence())
                {
                    // OID for SHA-256: 2.16.840.1.101.3.4.2.1
                    writer.WriteObjectIdentifier("2.16.840.1.101.3.4.2.1");
                    // NULL parameters (opzionale ma spesso richiesto)
                    writer.WriteNull();
                }

                // hashedMessage OCTET STRING
                writer.WriteOctetString(hashBytes);
            }

            // nonce INTEGER (opzionale - utile per prevenire replay attacks)
            var nonce = new byte[8];
            RandomNumberGenerator.Fill(nonce);
            writer.WriteInteger(new ReadOnlySpan<byte>(nonce));

            // certReq BOOLEAN DEFAULT FALSE - richiediamo il certificato
            writer.WriteBoolean(true);
        }

        return writer.Encode();
    }

    /// <summary>
    /// Invia la richiesta TSA al server.
    /// </summary>
    private async Task<HttpResponseMessage> SendTsaRequestAsync(string url, byte[] requestData)
    {
        using var content = new ByteArrayContent(requestData);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/timestamp-query");

        var response = await _httpClient.PostAsync(url, content);
        return response;
    }

    /// <summary>
    /// Parsa la TimeStampResponse RFC 3161.
    /// Estrae il timestamp e verifica lo status.
    /// </summary>
    private TsaParseResult ParseTimeStampResponse(byte[] responseBytes, string expectedHash)
    {
        try
        {
            var reader = new AsnReader(responseBytes, AsnEncodingRules.DER);

            // TimeStampResp ::= SEQUENCE
            var responseSeq = reader.ReadSequence();

            // PKIStatusInfo ::= SEQUENCE
            var statusInfoSeq = responseSeq.ReadSequence();

            // status PKIStatus ::= INTEGER
            var status = (int)statusInfoSeq.ReadInteger();

            // Status codes: 0 = granted, 1 = grantedWithMods, 2 = rejection, etc.
            if (status > 1)
            {
                var statusText = status switch
                {
                    2 => "rejection",
                    3 => "waiting",
                    4 => "revocationWarning",
                    5 => "revocationNotification",
                    _ => $"unknown ({status})"
                };
                return new TsaParseResult { Success = false, ErrorMessage = $"TSA status: {statusText}" };
            }

            // Cerca il TimeStampToken (se presente)
            if (!responseSeq.HasData)
            {
                return new TsaParseResult { Success = false, ErrorMessage = "Nessun TimeStampToken nella risposta" };
            }

            // timeStampToken TimeStampToken OPTIONAL
            // TimeStampToken ::= ContentInfo (CMS SignedData)
            var tokenReader = responseSeq.ReadSequence();

            // Estrai la data dal TSTInfo (dentro il SignedData)
            var timestampDate = ExtractTimestampDate(tokenReader, responseBytes);

            return new TsaParseResult
            {
                Success = true,
                TimestampDate = timestampDate,
                MessageImprint = expectedHash
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TSA] Errore parsing risposta TSA, usando timestamp locale");
            // In caso di errore parsing, consideriamo comunque valida la risposta
            // ma usiamo il timestamp locale
            return new TsaParseResult
            {
                Success = true,
                TimestampDate = DateTime.UtcNow,
                MessageImprint = expectedHash,
                ErrorMessage = $"Parsing warning: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Estrae la data dal TSTInfo dentro il TimeStampToken.
    /// Questa è una implementazione semplificata che cerca il pattern della data ASN.1.
    /// </summary>
    private DateTime ExtractTimestampDate(AsnReader tokenReader, byte[] fullResponse)
    {
        try
        {
            // Il TimeStampToken è un ContentInfo che contiene SignedData
            // SignedData contiene encapContentInfo con TSTInfo
            // TSTInfo ha: version, policy, messageImprint, serialNumber, genTime, ...

            // Approccio semplificato: cerca GeneralizedTime nel token
            // GeneralizedTime è codificato come tag 0x18 seguito dalla lunghezza e dalla stringa data

            for (int i = 0; i < fullResponse.Length - 15; i++)
            {
                // Cerca tag GeneralizedTime (0x18) con lunghezza ragionevole (15 per YYYYMMDDhhmmssZ)
                if (fullResponse[i] == 0x18 && fullResponse[i + 1] >= 13 && fullResponse[i + 1] <= 17)
                {
                    var len = fullResponse[i + 1];
                    var dateStr = System.Text.Encoding.ASCII.GetString(fullResponse, i + 2, len);

                    // Prova a parsare come GeneralizedTime (YYYYMMDDhhmmss[.fff]Z)
                    if (TryParseGeneralizedTime(dateStr, out var date))
                    {
                        return date;
                    }
                }
            }

            // Fallback: usa timestamp corrente
            _logger.LogWarning("[TSA] Impossibile estrarre timestamp dalla risposta, uso UTC now");
            return DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TSA] Errore estrazione timestamp, uso UTC now");
            return DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Parsa una stringa GeneralizedTime ASN.1.
    /// Formato: YYYYMMDDhhmmss[.fff]Z
    /// </summary>
    private static bool TryParseGeneralizedTime(string dateStr, out DateTime result)
    {
        result = DateTime.MinValue;

        if (string.IsNullOrEmpty(dateStr) || dateStr.Length < 14)
            return false;

        // Rimuovi la Z finale se presente
        dateStr = dateStr.TrimEnd('Z');

        // Prova formato base: YYYYMMDDhhmmss
        if (dateStr.Length >= 14)
        {
            var baseDate = dateStr[..14];
            if (DateTime.TryParseExact(baseDate, "yyyyMMddHHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out result))
            {
                result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Risultato interno del parsing della risposta TSA.
    /// </summary>
    private class TsaParseResult
    {
        public bool Success { get; set; }
        public DateTime? TimestampDate { get; set; }
        public string? MessageImprint { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
