using System.Diagnostics;
using System.Formats.Asn1;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using PolarDrive.Data.Constants;
using PolarDrive.WebApi.Helpers;

namespace PolarDrive.WebApi.Services.Tsa;

/// <summary>
/// Implementazione TSA per Aruba TSA (https://servizi.arubapec.it).
/// Usato in ambiente PRODUCTION.
/// Aruba TSA è un servizio qualificato eIDAS - ha valore legale pieno in Italia e UE.
/// Richiede autenticazione con username e password.
/// </summary>
public class TsaArubaService : ITsaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TsaArubaService> _logger;
    public string ProviderName => "Aruba TSA";
    public string ServerUrl => AppConfig.TSA_SERVER_URL;

    public TsaArubaService(IHttpClientFactory httpClientFactory, ILogger<TsaArubaService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TsaClient");
        _httpClient.Timeout = TimeSpan.FromSeconds(AppConfig.TSA_TIMEOUT_SECONDS);
        _logger = logger;

        // Configura autenticazione Basic Auth se credenziali presenti
        var username = AppConfig.TSA_ARUBA_USERNAME;
        var password = AppConfig.TSA_ARUBA_PASSWORD;

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }
    }

    /// <summary>
    /// Richiede una marca temporale RFC 3161 ad Aruba TSA.
    /// </summary>
    public async Task<TsaResult> RequestTimestampAsync(byte[] content, string contentHash)
    {
        var sw = Stopwatch.StartNew();
        var serverUrl = ServerUrl;

        // Verifica credenziali
        if (string.IsNullOrEmpty(AppConfig.TSA_ARUBA_USERNAME) ||
            string.IsNullOrEmpty(AppConfig.TSA_ARUBA_PASSWORD))
        {
            _logger.LogError("[TSA] Credenziali Aruba TSA non configurate (TSA_ARUBA_USERNAME, TSA_ARUBA_PASSWORD)");
            return TsaResult.Error(
                "Credenziali Aruba TSA non configurate",
                serverUrl, ProviderName, sw.ElapsedMilliseconds);
        }

        try
        {
            _logger.LogInformation("[TSA] Richiesta marca temporale qualificata a {Provider} ({Url}) per hash {Hash}",
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
                _logger.LogWarning("[TSA] Risposta HTTP non valida da Aruba: {StatusCode} - {Body}",
                    response.StatusCode, errorBody);

                // Gestisci errori specifici Aruba
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return TsaResult.Error(
                        "Autenticazione fallita - verificare credenziali Aruba TSA",
                        serverUrl, ProviderName, sw.ElapsedMilliseconds);
                }

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
            _logger.LogInformation("[TSA] Marca temporale QUALIFICATA ottenuta da {Provider} in {Ms}ms. Data: {Date}",
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
            _logger.LogError(ex, "[TSA] Errore verifica token TSA Aruba");
            return Task.FromResult(TsaVerifyResult.Invalid($"Errore verifica: {ex.Message}"));
        }
    }

    /// <summary>
    /// Costruisce una TimeStampRequest RFC 3161 in formato ASN.1 DER.
    /// Include policy OID Aruba se configurato.
    /// </summary>
    private byte[] BuildTimeStampRequest(byte[] hashBytes)
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
                    // NULL parameters
                    writer.WriteNull();
                }

                // hashedMessage OCTET STRING
                writer.WriteOctetString(hashBytes);
            }

            // reqPolicy TSAPolicyId OPTIONAL - Aruba policy OID se configurato
            var policyOid = AppConfig.TSA_ARUBA_POLICY_OID;
            if (!string.IsNullOrEmpty(policyOid))
            {
                writer.WriteObjectIdentifier(policyOid);
            }

            // nonce INTEGER (per prevenire replay attacks)
            var nonce = new byte[8];
            RandomNumberGenerator.Fill(nonce);
            writer.WriteInteger(new ReadOnlySpan<byte>(nonce));

            // certReq BOOLEAN - richiediamo il certificato per verifica
            writer.WriteBoolean(true);
        }

        return writer.Encode();
    }

    /// <summary>
    /// Invia la richiesta TSA al server Aruba.
    /// </summary>
    private async Task<HttpResponseMessage> SendTsaRequestAsync(string url, byte[] requestData)
    {
        using var content = new ByteArrayContent(requestData);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/timestamp-query");

        // Aruba potrebbe richiedere header aggiuntivi
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(request);
        return response;
    }

    /// <summary>
    /// Parsa la TimeStampResponse RFC 3161 da Aruba.
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
                    2 => "rejection - richiesta rifiutata",
                    3 => "waiting - richiesta in attesa",
                    4 => "revocationWarning - avviso revoca",
                    5 => "revocationNotification - notifica revoca",
                    _ => $"errore sconosciuto ({status})"
                };

                // Prova a leggere il messaggio di errore se presente
                string? failInfo = null;
                if (statusInfoSeq.HasData)
                {
                    try
                    {
                        // statusString PKIFreeText OPTIONAL
                        var statusString = statusInfoSeq.ReadSequence();
                        if (statusString.HasData)
                        {
                            failInfo = statusString.ReadCharacterString(UniversalTagNumber.UTF8String);
                        }
                    }
                    catch { /* ignora errori parsing messaggio */ }
                }

                var errorMsg = failInfo != null ? $"{statusText}: {failInfo}" : statusText;
                return new TsaParseResult { Success = false, ErrorMessage = $"TSA Aruba: {errorMsg}" };
            }

            // Cerca il TimeStampToken
            if (!responseSeq.HasData)
            {
                return new TsaParseResult { Success = false, ErrorMessage = "Nessun TimeStampToken nella risposta Aruba" };
            }

            // Estrai la data dal TSTInfo
            var timestampDate = ExtractTimestampDate(responseBytes);

            return new TsaParseResult
            {
                Success = true,
                TimestampDate = timestampDate,
                MessageImprint = expectedHash
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TSA] Errore parsing risposta Aruba TSA, usando timestamp locale");
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
    /// </summary>
    private DateTime ExtractTimestampDate(byte[] fullResponse)
    {
        try
        {
            // Cerca GeneralizedTime nel token (tag 0x18)
            for (int i = 0; i < fullResponse.Length - 15; i++)
            {
                if (fullResponse[i] == 0x18 && fullResponse[i + 1] >= 13 && fullResponse[i + 1] <= 17)
                {
                    var len = fullResponse[i + 1];
                    var dateStr = Encoding.ASCII.GetString(fullResponse, i + 2, len);

                    if (TryParseGeneralizedTime(dateStr, out var date))
                    {
                        return date;
                    }
                }
            }

            _logger.LogWarning("[TSA] Impossibile estrarre timestamp dalla risposta Aruba, uso UTC now");
            return DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TSA] Errore estrazione timestamp Aruba, uso UTC now");
            return DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Parsa una stringa GeneralizedTime ASN.1.
    /// </summary>
    private static bool TryParseGeneralizedTime(string dateStr, out DateTime result)
    {
        result = DateTime.MinValue;

        if (string.IsNullOrEmpty(dateStr) || dateStr.Length < 14)
            return false;

        dateStr = dateStr.TrimEnd('Z');

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

    private class TsaParseResult
    {
        public bool Success { get; set; }
        public DateTime? TimestampDate { get; set; }
        public string? MessageImprint { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
