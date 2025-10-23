using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.Data.DTOs;
using PolarDrive.WebApi.Services;
using System.Text.RegularExpressions;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SmsController(
    PolarDriveDbContext db,
    ISmsConfigurationService smsConfig) : ControllerBase
{
    private readonly PolarDriveDbContext _db = db;
    private readonly ISmsConfigurationService _smsConfig = smsConfig;
    private readonly PolarDriveLogger _logger = new(db);

    [HttpGet("test-sms")]
    public async Task<IActionResult> TestSendSms([FromServices] ISmsConfigurationService sms)
    {
        await sms.SendSmsAsync("+393926321311", "🚀 Test SMS PolarDrive via Vonage OK");
        return Ok("SMS di test inviato.");
    }

    /// <summary>
    /// 🎯 WEBHOOK PRINCIPALE - Riceve SMS
    /// </summary>
    [HttpGet("webhook")] // Vonage può chiamare in GET
    [HttpPost("webhook")]
    public async Task<ActionResult> ReceiveSms([FromForm] SmsWebhookDTO? dto)
    {
        // Leggi dai parametri HTTP se dto è nullo o parziale
        string from = dto?.From 
            ?? GetParam("msisdn") 
            ?? string.Empty;

        string to = dto?.To
            ?? GetParam("to")
            ?? string.Empty;

        string body = dto?.Body
            ?? GetParam("text")
            ?? string.Empty;

        string messageId = dto?.MessageSid
            ?? GetParam("messageId")
            ?? string.Empty;

        var auditLog = new SmsAuditLog
        {
            MessageSid = messageId,
            FromPhoneNumber = from,
            ToPhoneNumber = to,
            MessageBody = body,
            ReceivedAt = DateTime.Now,
            ProcessingStatus = "PROCESSING"
        };

        try
        {
            // 🔒 1. VALIDAZIONE SICUREZZA (Vonage)
            if (!ValidateSignatureVonage())
            {
                auditLog.ProcessingStatus = "REJECTED";
                auditLog.ErrorMessage = "Invalid Vonage signature";
                await SaveAuditLogAsync(auditLog);
                return OkSms("Invalid signature");
            }

            if (!_smsConfig.IsPhoneNumberAllowed(from))
            {
                auditLog.ProcessingStatus = "REJECTED";
                auditLog.ErrorMessage = "Phone number not in whitelist";
                await SaveAuditLogAsync(auditLog);

                var response = GenerateSmsResponse("❌ Numero non autorizzato per questo servizio.");
                auditLog.ResponseSent = response;
                await UpdateAuditLogAsync(auditLog);

                return OkSms(response);
            }

            if (await _smsConfig.IsRateLimitExceeded(from))
            {
                auditLog.ProcessingStatus = "REJECTED";
                auditLog.ErrorMessage = "Rate limit exceeded";
                await SaveAuditLogAsync(auditLog);

                var response = GenerateSmsResponse("⏱️ Troppi messaggi. Riprova tra qualche minuto.");
                auditLog.ResponseSent = response;
                await UpdateAuditLogAsync(auditLog);

                return OkSms(response);
            }

            // 📱 2. PARSING COMANDO
            var command = ParseSmsCommand(body);

            // 🎯 3. GESTIONE COMANDI
            if (command.StartsWith("ADAPTIVE_GDPR"))
                return await HandleAdaptiveGdprCommand(
                    new SmsWebhookDTO { From = from, To = to, Body = body, MessageSid = messageId }, auditLog, command);

            if (command.StartsWith("ADAPTIVE_PROFILING"))
                return await HandleAdaptiveProfilingCommand(
                    new SmsWebhookDTO { From = from, To = to, Body = body, MessageSid = messageId }, auditLog, command);

            if (command == "ACCETTO")
                return await HandleAccettoCommand(
                    new SmsWebhookDTO { From = from, To = to, Body = body, MessageSid = messageId }, auditLog);

            if (command == "STOP")
                return await HandleStopCommand(
                    new SmsWebhookDTO { From = from, To = to, Body = body, MessageSid = messageId }, auditLog);

            // ❌ Comando non riconosciuto
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Comando non riconosciuto";
            var errorResponse = GenerateSmsResponse("❌ Comando non valido. Usa ADAPTIVE_GDPR o ADAPTIVE_PROFILING.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);

            return OkSms(errorResponse);
        }
        catch (Exception ex)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = ex.Message;
            auditLog.ResponseSent = GenerateSmsResponse("❌ Errore interno del server.");
            await SaveAuditLogAsync(auditLog);

            await _logger.Error("Sms.Webhook", "Fatal error processing SMS",
                $"Error: {ex.Message}, From: {from}");

            return OkSms(auditLog.ResponseSent);
        }
    }

    // 🎯 WEBHOOK DLR - Delivery Receipts (stato invii)
    [HttpGet("dlr")]   // Vonage può chiamare GET
    [HttpPost("dlr")]
    public async Task<ActionResult> DeliveryReceipt()
    {
        var auditLog = new SmsAuditLog
        {
            MessageSid = GetParam("messageId") ?? string.Empty,
            FromPhoneNumber = GetParam("msisdn") ?? string.Empty, // mittente
            ToPhoneNumber = GetParam("to") ?? string.Empty,       // numero Vonage DataPolar
            MessageBody = $"DLR status={GetParam("status")} err-code={GetParam("err-code")} price={GetParam("price")}",
            ReceivedAt = DateTime.Now,
            ProcessingStatus = "DLR"
        };

        try
        {
            // (opzionale) valida firma come su inbound
            if (!ValidateSignatureVonage())
            {
                auditLog.ErrorMessage = "Invalid Vonage signature (DLR)";
                await SaveAuditLogAsync(auditLog);
                return OkSms("Invalid signature");
            }

            await SaveAuditLogAsync(auditLog);
            return OkSms("DLR OK");
        }
        catch (Exception ex)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = $"DLR error: {ex.Message}";
            await SaveAuditLogAsync(auditLog);
            return OkSms("DLR ERROR");
        }
    }

    // Gestione ADAPTIVE_GDPR
    private async Task<ActionResult> HandleAdaptiveGdprCommand(SmsWebhookDTO dto, SmsAuditLog auditLog, string command)
    {
        // Estrai parametri: "ADAPTIVE_GDPR Rossi Mario +393331234567"
        var parts = dto.Body.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)  // comando + cognome + nome + numero
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Formato comando ADAPTIVE_GDPR non valido";
            var errorResponse = GenerateSmsResponse("❌ Formato non valido. Usa formato: ADAPTIVE_GDPR Cognome Nome 3334455666");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        var surname = parts[1];
        var name = parts[2];
        var fullName = $"{surname} {name}";  // "Rossi Mario"
        var targetPhone = NormalizePhoneNumber(parts[3]);

        // Verifica che il mittente sia un VehicleMobileNumber registrato
        var vehicle = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .FirstOrDefaultAsync(v => v.VehicleMobileNumber == dto.From && v.IsActiveFlag);

        if (vehicle == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Mittente non autorizzato";
            var errorResponse = GenerateSmsResponse("❌ Solo il Cellulare Operativo Autorizzato registrato può richiedere i consensi GDPR.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        // Crea richiesta GDPR con tutti i campi obbligatori
        var gdprRequest = new SmsAdaptiveGdpr
        {
            AdaptiveNumber = targetPhone,
            AdaptiveSurnameName = fullName,
            Brand = vehicle.Brand,
            ClientCompanyId = vehicle.ClientCompanyId,
            ConsentToken = SmsAdaptiveGdpr.GenerateSecureToken(),
            RequestedAt = DateTime.Now,
            ConsentAccepted = false,
            AttemptCount = 1
        };

        _db.SmsAdaptiveGdpr.Add(gdprRequest);
        await _db.SaveChangesAsync();

        // Crea riga ADAPTIVE_PROFILING (vuota, in attesa di attivazione)
        var profilingEvent = new SmsAdaptiveProfiling
        {
            VehicleId = vehicle.Id,
            AdaptiveProfilingNumber = targetPhone,
            AdaptiveProfilingName = fullName,
            ReceivedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddHours(SMS_ADPATIVE_HOURS_THRESHOLD),
            MessageContent = dto.Body,
            ParsedCommand = "ADAPTIVE_PROFILING_OFF",
            ConsentAccepted = false,
            SmsAdaptiveGdprId = gdprRequest.Id
        };

        _db.SmsAdaptiveProfiling.Add(profilingEvent);
        await _db.SaveChangesAsync();

        // Invia SMS al numero target
        var gdprMessage = $@"DataPolar - Procedura ADAPTIVE_GDPR - Consenso utilizzo {vehicle.Brand} da {vehicle.ClientCompany?.Name} Per guidare questo veicolo e dare il suo apporto di Ricerca & Sviluppo, deve accettare il trattamento dati GPS/telemetria come richiesto da {vehicle.Brand}. 📄 Informativa completa: [short.link/gdpr-pdf] Risponda: ✅ ACCETTO - per dare consenso esplicito ℹ️ Ignora SMS per negare il consenso";

        await _smsConfig.SendSmsAsync(targetPhone, gdprMessage);

        auditLog.ProcessingStatus = "SUCCESS";
        auditLog.ResponseSent = GenerateSmsResponse($"✅ Richiesta GDPR inviata a {fullName} ({targetPhone})");
        await SaveAuditLogAsync(auditLog);

        await _logger.Info("Sms.GDPR", "GDPR request sent",
            $"From: {dto.From}, To: {targetPhone}, Name: {fullName}, Brand: {vehicle.Brand}");

        return OkSms(auditLog.ResponseSent);
    }

    // Gestione ACCETTO
    private async Task<ActionResult> HandleAccettoCommand(SmsWebhookDTO dto, SmsAuditLog auditLog)
    {
        var gdprRequest = await _db.SmsAdaptiveGdpr
            .Include(g => g.ClientCompany)
            .Where(g => g.AdaptiveNumber == dto.From && !g.ConsentAccepted)
            .OrderByDescending(g => g.RequestedAt)
            .FirstOrDefaultAsync();

        if (gdprRequest == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Nessuna richiesta GDPR valida trovata";
            var errorResponse = GenerateSmsResponse("❌ Nessuna richiesta di consenso valida per questo numero.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        // Aggiorna consenso GDPR
        gdprRequest.ConsentAccepted = true;
        gdprRequest.ConsentGivenAt = DateTime.Now;
        gdprRequest.IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        gdprRequest.UserAgent = Request.Headers["User-Agent"].ToString();

        // Aggiorna tutte le righe ADAPTIVE_PROFILING legate a questo numero
        var profilingEvents = await _db.SmsAdaptiveProfiling
            .Where(p => p.AdaptiveProfilingNumber == dto.From)
            .ToListAsync();

        foreach (var pe in profilingEvents)
        {
            pe.ConsentAccepted = true;
        }

        await _db.SaveChangesAsync();

        // Invia SMS di conferma
        var confirmMessage = $@"Autorizzazione ADAPTIVE_GDPR confermata per {gdprRequest.Brand} da {gdprRequest.ClientCompany?.Name} ID Consenso: #{gdprRequest.Id} ❌ Consenso revocabile rispondendo a questo SMS con: STOP";

        await _smsConfig.SendSmsAsync(dto.From, confirmMessage);

        auditLog.ProcessingStatus = "SUCCESS";
        auditLog.ResponseSent = confirmMessage;
        await SaveAuditLogAsync(auditLog);

        return OkSms(confirmMessage);
    }

    // Gestione STOP
    private async Task<ActionResult> HandleStopCommand(SmsWebhookDTO dto, SmsAuditLog auditLog)
    {
        var gdprRequest = await _db.SmsAdaptiveGdpr
            .Where(g => g.AdaptiveNumber == dto.From && g.ConsentAccepted)
            .OrderByDescending(g => g.ConsentGivenAt)
            .FirstOrDefaultAsync();

        if (gdprRequest == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Nessun consenso attivo trovato";
            var errorResponse = GenerateSmsResponse("❌ Nessun consenso attivo per questo numero.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        // Revoca consenso GDPR
        gdprRequest.ConsentAccepted = false;

        // Disattiva tutte le righe ADAPTIVE_PROFILING
        var profilingEvents = await _db.SmsAdaptiveProfiling
            .Where(p => p.AdaptiveProfilingNumber == dto.From)
            .ToListAsync();

        foreach (var pe in profilingEvents)
        {
            pe.ConsentAccepted = false;
            pe.ParsedCommand = "ADAPTIVE_PROFILING_OFF";
        }

        await _db.SaveChangesAsync();

        // Invia SMS di conferma revoca
        var stopMessage = $@"Autorizzazione ADAPTIVE_GDPR rimossa per {gdprRequest.Brand} ID Consenso: #{gdprRequest.Id} ❌ Tutti i consensi ed i dati personali / informazioni sono state rimosse dal sistema a norma del GDPR";

        await _smsConfig.SendSmsAsync(dto.From, stopMessage);

        auditLog.ProcessingStatus = "SUCCESS";
        auditLog.ResponseSent = stopMessage;
        await SaveAuditLogAsync(auditLog);

        return OkSms(stopMessage);
    }

    /// <summary>
    /// Parsing del comando SMS
    /// </summary>
    private static string ParseSmsCommand(string messageContent)
    {
        if (string.IsNullOrWhiteSpace(messageContent))
            return "INVALID";

        var normalizedMessage = messageContent.Trim().ToUpperInvariant();

        // ADAPTIVE_GDPR XXXXXXXXXX
        if (normalizedMessage.StartsWith("ADAPTIVE_GDPR"))
        {
            return "ADAPTIVE_GDPR";
        }

        // ADAPTIVE_PROFILING XXXXXXXXXX Nome Cognome
        if (normalizedMessage.StartsWith("ADAPTIVE_PROFILING"))
        {
            return "ADAPTIVE_PROFILING";
        }

        // ACCETTO
        if (normalizedMessage == "ACCETTO")
        {
            return "ACCETTO";
        }

        // STOP
        if (normalizedMessage == "STOP" || normalizedMessage == "OFF")
        {
            return "STOP";
        }

        return "INVALID";
    }

    /// <summary>
    /// Gestione comando ADAPTIVE_PROFILING
    /// </summary>
    private async Task<ActionResult> HandleAdaptiveProfilingCommand(SmsWebhookDTO dto, SmsAuditLog auditLog, string command)
    {
        // Estrai parametri: "ADAPTIVE_PROFILING Rossi Mario +393331234567"
        var parts = dto.Body.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)  // comando + cognome + nome + numero
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Formato comando ADAPTIVE_PROFILING non valido";
            var errorResponse = GenerateSmsResponse("❌ Formato non valido. Usa: ADAPTIVE_PROFILING Cognome Nome NUMERO-DI-TELEFONO");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        var surname = parts[1];
        var name = parts[2];
        var fullName = $"{surname} {name}";  // "Rossi Mario"
        var targetPhone = NormalizePhoneNumber(parts[3]);

        // Verifica che il mittente sia un VehicleMobileNumber registrato
        var vehicle = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .FirstOrDefaultAsync(v => v.VehicleMobileNumber == dto.From && v.IsActiveFlag);

        if (vehicle == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Mittente non autorizzato";
            var errorResponse = GenerateSmsResponse("❌ Solo il Cellulare Operativo Autorizzato può attivare la procedura ADAPTIVE_PROFILE.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        // ⚠️ VERIFICA CHE ESISTA CONSENSO GDPR ATTIVO
        var gdprConsent = await _db.SmsAdaptiveGdpr
            .Where(g => g.AdaptiveNumber == targetPhone
                    && g.Brand == vehicle.Brand
                    && g.ConsentAccepted)
            .OrderByDescending(g => g.ConsentGivenAt)
            .FirstOrDefaultAsync();

        if (gdprConsent == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Procedura ADAPTIVE_GDPR mai eseguita";

            // Invia SMS al VehicleMobileNumber (il mittente)
            var warningMessage = $@"ATTENZIONE {vehicle.ReferentName}! Procedura ADAPTIVE_GDPR mai eseguita per {fullName} ({targetPhone}). Completare la procedura ADAPTIVE_GDPR prima di continuare";

            await _smsConfig.SendSmsAsync(dto.From, warningMessage);

            auditLog.ResponseSent = warningMessage;
            await SaveAuditLogAsync(auditLog);

            return OkSms(warningMessage);
        }

        // Trova o crea riga ADAPTIVE_PROFILING
        var profilingEvent = await _db.SmsAdaptiveProfiling
            .Where(p => p.VehicleId == vehicle.Id
                    && p.AdaptiveProfilingNumber == targetPhone)
            .OrderByDescending(p => p.ReceivedAt)
            .FirstOrDefaultAsync();

        if (profilingEvent == null)
        {
            // Crea nuova riga - INCLUDE SmsAdaptiveGdprId OBBLIGATORIO
            profilingEvent = new SmsAdaptiveProfiling
            {
                VehicleId = vehicle.Id,
                AdaptiveProfilingNumber = targetPhone,
                AdaptiveProfilingName = fullName,
                ReceivedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddHours(SMS_ADPATIVE_HOURS_THRESHOLD),
                MessageContent = dto.Body,
                ParsedCommand = "ADAPTIVE_PROFILING_ON",
                ConsentAccepted = true,
                SmsAdaptiveGdprId = gdprConsent.Id
            };

            _db.SmsAdaptiveProfiling.Add(profilingEvent);
        }
        else
        {
            // Aggiorna riga esistente (estende sessione di 24 ore)
            profilingEvent.AdaptiveProfilingName = fullName;
            profilingEvent.ReceivedAt = DateTime.Now;
            profilingEvent.ExpiresAt = DateTime.Now.AddHours(SMS_ADPATIVE_HOURS_THRESHOLD);
            profilingEvent.ParsedCommand = "ADAPTIVE_PROFILING_ON";
            profilingEvent.ConsentAccepted = true;
            profilingEvent.SmsAdaptiveGdprId = gdprConsent.Id;
        }

        await _db.SaveChangesAsync();

        // Invia SMS di conferma al numero target
        var confirmMessage = $@"Autorizzazione ADAPTIVE_PROFILING confermata 
        Giorno: {DateTime.Now:dd/MM/yyyy} 
        Azienda: {vehicle.ClientCompany?.Name} 
        Brand: {vehicle.Brand} 
        VIN: {vehicle.Vin} 
        Validità profilo: {SMS_ADPATIVE_HOURS_THRESHOLD} ore";

        await _smsConfig.SendSmsAsync(targetPhone, confirmMessage);

        auditLog.ProcessingStatus = "SUCCESS";
        auditLog.VehicleIdResolved = vehicle.Id;
        auditLog.ResponseSent = confirmMessage;
        await SaveAuditLogAsync(auditLog);

        await _logger.Info("Sms.PROFILING", "Adaptive Profiling activated",
            $"VehicleId: {vehicle.Id}, Phone: {targetPhone}, Name: {fullName}");

        return OkSms(confirmMessage);
    }

    // ============================================================================
    // METODI HELPER PRIVATI
    // ============================================================================

    private bool ValidateSignatureVonage()
    {
        if (!_smsConfig.GetConfiguration().EnableSignatureValidation)
            return true;

        // Vonage usa 'sig' (query/form) oppure header 'X-Nexmo-Signature'.
        var provided = GetParam("sig") 
                    ?? Request.Headers["X-Nexmo-Signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(provided))
            return false;

        // Costruisci il dizionario di parametri (senza 'sig') unendo form e query
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Request.Query.Count > 0)
            foreach (var kv in Request.Query) parameters[kv.Key] = kv.Value.ToString();

        if (Request.HasFormContentType && Request.Form.Count > 0)
            foreach (var kv in Request.Form) parameters[kv.Key] = kv.Value.ToString();

        parameters.Remove("sig");

        var secret = _smsConfig.GetConfiguration().AuthToken; // usa il tuo ApiSecret Vonage

        // Vonage consente MD5, SHA1, SHA256, SHA512. Accettiamo qualsiasi match.
        var candidates = new[]
        {
            VonageSignature(parameters, secret, "md5"),
            VonageSignature(parameters, secret, "sha1"),
            VonageSignature(parameters, secret, "sha256"),
            VonageSignature(parameters, secret, "sha512"),
        };

        return candidates.Any(x => x.Equals(provided, StringComparison.OrdinalIgnoreCase));
    }

    private static string VonageSignature(IDictionary<string, string> parameters, string secret, string algo)
    {
        // Ordina alfabeticamente per chiave, concatena "key=value" senza separatori, poi append 'secret'
        var concat = string.Concat(parameters
            .OrderBy(k => k.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}")) + secret;

        return algo switch
        {
            "md5"   => ToHex(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(concat))),
            "sha1"  => ToHex(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(concat))),
            "sha256"=> ToHex(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(concat))),
            "sha512"=> ToHex(System.Security.Cryptography.SHA512.HashData(System.Text.Encoding.UTF8.GetBytes(concat))),
            _       => string.Empty
        };
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new System.Text.StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private string GenerateSmsResponse(string message)
    {
        // Per Vonage rispondiamo semplice testo (il controller userà OkSms per il content-type)
        return message;
    }

    private string? GetParam(string key)
    {
        if (Request.HasFormContentType && Request.Form.ContainsKey(key))
            return Request.Form[key].ToString();
        if (Request.Query.ContainsKey(key))
            return Request.Query[key].ToString();
        return null;
    }

    private ActionResult OkSms(string message)
    {
        return Content(message, "text/plain"); // 200 OK con text/plain
    }

    private string NormalizePhoneNumber(string phoneNumber)
    {
        // Rimuove spazi, trattini, parentesi
        var cleaned = Regex.Replace(phoneNumber, @"[\s\-\(\)]", "");

        // Se inizia con 00, sostituisce con +
        if (cleaned.StartsWith("00"))
            cleaned = "+" + cleaned.Substring(2);

        // Se non inizia con +, aggiunge prefisso Italia
        if (!cleaned.StartsWith("+"))
            cleaned = "+39" + cleaned;

        return cleaned;
    }

    private async Task SaveAuditLogAsync(SmsAuditLog auditLog)
    {
        _db.SmsAuditLog.Add(auditLog);
        await _db.SaveChangesAsync();
    }

    private async Task UpdateAuditLogAsync(SmsAuditLog auditLog)
    {
        _db.SmsAuditLog.Update(auditLog);
        await _db.SaveChangesAsync();
    }

    // ============================================================================
    // ENDPOINT PER IL FRONTEND
    // ============================================================================

    /// <summary>
    /// Ottiene lo storico delle sessioni ADAPTIVE_PROFILING per un veicolo
    /// </summary>
    [HttpGet("adaptive-profiling/{vehicleId}/history")]
    public async Task<ActionResult> GetAdaptiveProfilingHistory(int vehicleId)
    {
        try
        {
            var sessions = await _db.SmsAdaptiveProfiling
                .Where(p => p.VehicleId == vehicleId)
                .OrderByDescending(p => p.ReceivedAt)
                .Select(p => new
                {
                    p.Id,
                    p.VehicleId,
                    p.AdaptiveProfilingNumber,
                    p.AdaptiveProfilingName,
                    p.ReceivedAt,
                    p.ExpiresAt,
                    p.ParsedCommand,
                    p.ConsentAccepted,
                    p.MessageContent
                })
                .ToListAsync();

            return Ok(sessions);
        }
        catch (Exception ex)
        {
            await _logger.Error("Sms.GetHistory", "Error fetching profiling history",
                $"VehicleId: {vehicleId}, Error: {ex.Message}");
            return StatusCode(500, new { error = "Errore nel recupero dello storico" });
        }
    }

    /// <summary>
    /// Ottiene lo stato della sessione ADAPTIVE_PROFILING attiva per un veicolo
    /// </summary>
    [HttpGet("adaptive-profiling/{vehicleId}/status")]
    public async Task<ActionResult> GetAdaptiveProfilingStatus(int vehicleId)
    {
        try
        {
            var activeSession = await _db.SmsAdaptiveProfiling
                .Where(p => p.VehicleId == vehicleId
                        && p.ConsentAccepted
                        && p.ParsedCommand == "ADAPTIVE_PROFILING_ON"
                        && p.ExpiresAt > DateTime.Now)
                .OrderByDescending(p => p.ReceivedAt)
                .FirstOrDefaultAsync();

            if (activeSession == null)
            {
                return Ok(new
                {
                    isActive = false,
                    sessionStartedAt = (DateTime?)null,
                    sessionEndTime = (DateTime?)null,
                    remainingMinutes = 0,
                    adaptiveProfilingName = (string?)null,
                    adaptiveProfilingNumber = (string?)null
                });
            }

            var remainingTime = activeSession.ExpiresAt - DateTime.Now;

            return Ok(new
            {
                isActive = true,
                sessionStartedAt = activeSession.ReceivedAt,
                sessionEndTime = activeSession.ExpiresAt,
                remainingMinutes = (int)remainingTime.TotalMinutes,
                adaptiveProfilingName = activeSession.AdaptiveProfilingName,
                adaptiveProfilingNumber = activeSession.AdaptiveProfilingNumber
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("Sms.GetStatus", "Error fetching profiling status",
                $"VehicleId: {vehicleId}, Error: {ex.Message}");
            return StatusCode(500, new { error = "Errore nel recupero dello stato" });
        }
    }

    /// <summary>
    /// Ottiene tutti i consensi GDPR per un Brand
    /// </summary>
    [HttpGet("gdpr/consents")]
    public async Task<ActionResult> GetGdprConsentsByBrand([FromQuery] string brand)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(brand))
            {
                return BadRequest(new { error = "Brand obbligatorio" });
            }

            var consents = await _db.SmsAdaptiveGdpr
                .Include(g => g.ClientCompany)
                .Where(g => g.Brand == brand)
                .OrderByDescending(g => g.RequestedAt)
                .Select(g => new
                {
                    g.Id,
                    PhoneNumber = g.AdaptiveNumber, // Per retrocompatibilità con il frontend
                    g.AdaptiveNumber,
                    g.AdaptiveSurnameName,
                    g.Brand,
                    g.RequestedAt,
                    g.ConsentGivenAt,
                    g.ConsentAccepted,
                    CompanyName = g.ClientCompany != null ? g.ClientCompany.Name : null
                })
                .ToListAsync();

            return Ok(consents);
        }
        catch (Exception ex)
        {
            await _logger.Error("Sms.GetConsents", "Error fetching GDPR consents",
                $"Brand: {brand}, Error: {ex.Message}");
            return StatusCode(500, new { error = "Errore nel recupero dei consensi GDPR" });
        }
    }

    /// <summary>
    /// Ottiene tutti i consensi GDPR (per amministratori)
    /// </summary>
    [HttpGet("gdpr/consents/all")]
    public async Task<ActionResult> GetAllGdprConsents([FromQuery] int pageSize = 50)
    {
        try
        {
            // CORREZIONE: PhoneNumber → AdaptiveNumber
            var consents = await _db.SmsAdaptiveGdpr
                .Include(g => g.ClientCompany)
                .OrderByDescending(g => g.RequestedAt)
                .Take(pageSize)
                .Select(g => new
                {
                    g.Id,
                    PhoneNumber = g.AdaptiveNumber, // Per retrocompatibilità con il frontend
                    g.AdaptiveNumber,
                    g.AdaptiveSurnameName,
                    g.Brand,
                    g.RequestedAt,
                    g.ConsentGivenAt,
                    g.ConsentAccepted,
                    g.AttemptCount,
                    CompanyName = g.ClientCompany != null ? g.ClientCompany.Name : null,
                    g.ClientCompanyId
                })
                .ToListAsync();

            return Ok(consents);
        }
        catch (Exception ex)
        {
            await _logger.Error("Sms.GetAllConsents", "Error fetching all GDPR consents",
                $"Error: {ex.Message}");
            return StatusCode(500, new { error = "Errore nel recupero dei consensi GDPR" });
        }
    }

    /// <summary>
    /// Ottiene gli audit logs SMS con paginazione
    /// </summary>
    [HttpGet("audit-logs")]
    public async Task<ActionResult> GetAuditLogs([FromQuery] int pageSize = 50, [FromQuery] int page = 1)
    {
        try
        {
            // CORREZIONE: Verificare che il DbSet sia corretto
            var totalCount = await _db.SmsAuditLog.CountAsync();
            
            var logs = await _db.SmsAuditLog
                .OrderByDescending(l => l.ReceivedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new
                {
                    l.Id,
                    l.MessageSid,
                    l.FromPhoneNumber,
                    l.ToPhoneNumber,
                    l.MessageBody,
                    l.ReceivedAt,
                    l.ProcessingStatus,
                    l.ErrorMessage,
                    l.VehicleIdResolved,
                    l.ResponseSent
                })
                .ToListAsync();

            return Ok(new
            {
                logs,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("Sms.GetAuditLogs", "Error fetching audit logs",
                $"Error: {ex.Message}");
            return StatusCode(500, new { error = "Errore nel recupero dei log" });
        }
    }

    /// <summary>
    /// Ottiene statistiche Adaptive Profiling per un veicolo
    /// </summary>
    [HttpGet("adaptive-profiling/{vehicleId}/stats")]
    public async Task<ActionResult> GetAdaptiveProfilingStats(int vehicleId)
    {
        try
        {
            var sessions = await _db.SmsAdaptiveProfiling
                .Where(p => p.VehicleId == vehicleId && p.ParsedCommand == "ADAPTIVE_PROFILING_ON")
                .ToListAsync();

            var totalSessions = sessions.Count;
            var activeSessions = sessions.Count(s => s.ConsentAccepted 
                                                && s.ParsedCommand == "ADAPTIVE_PROFILING_ON" 
                                                && s.ExpiresAt > DateTime.Now);
            var lastSession = sessions.MaxBy(s => s.ReceivedAt)?.ReceivedAt;
            var firstSession = sessions.MinBy(s => s.ReceivedAt)?.ReceivedAt;

            // Conta i dati raccolti durante le sessioni (se hai il campo IsSmsAdaptiveProfiling in VehicleData)
            var adaptiveDataCount = await _db.VehiclesData
                .Where(d => d.VehicleId == vehicleId && d.IsSmsAdaptiveProfiling)
                .CountAsync();

            return Ok(new
            {
                vehicleId,
                totalSessions,
                activeSessions,
                firstSession,
                lastSession,
                adaptiveDataPointsCollected = adaptiveDataCount,
                totalHoursApprox = totalSessions * SMS_ADPATIVE_HOURS_THRESHOLD
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("Sms.GetStats", "Error fetching profiling stats",
                $"VehicleId: {vehicleId}, Error: {ex.Message}");
            return StatusCode(500, new { error = "Errore nel recupero delle statistiche" });
        }
    }
}