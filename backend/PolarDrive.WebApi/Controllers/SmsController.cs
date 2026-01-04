using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Constants;
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
    private readonly PolarDriveLogger _logger = new();

    /// <summary>
    /// 🎯 WEBHOOK PRINCIPALE - Riceve SMS
    /// </summary>
    [HttpGet("webhook")] // Vonage può chiamare in GET
    [HttpPost("webhook")]
    [Consumes("application/x-www-form-urlencoded", "application/json")]
    public async Task<ActionResult> ReceiveSms([FromForm] SmsWebhookDTO? dto)
    {
        // Unifica parametri Vonage (msisdn, text) e custom (From, Body)
        string from = dto?.msisdn
            ?? dto?.From
            ?? GetParam("msisdn")
            ?? GetParam("From")
            ?? string.Empty;

        string to = dto?.to
            ?? dto?.To
            ?? GetParam("to")
            ?? GetParam("To")
            ?? string.Empty;

        string body = dto?.text
            ?? dto?.Body
            ?? GetParam("text")
            ?? GetParam("Body")
            ?? string.Empty;

        string messageId = dto?.messageId
            ?? dto?.MessageSid
            ?? GetParam("messageId")
            ?? GetParam("MessageSid")
            ?? Guid.NewGuid().ToString();

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

                var response = GenerateSmsResponse("Numero non autorizzato per questo servizio.");
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
                    new SmsWebhookDTO { From = from, To = to, Body = body, MessageSid = messageId }, auditLog, command, from);

            if (command.StartsWith("ADAPTIVE_PROFILE"))
                return await HandleAdaptiveProfileCommand(
                    new SmsWebhookDTO { From = from, To = to, Body = body, MessageSid = messageId }, auditLog, command, from);

            if (command == SmsCommand.ACCETTO)
                return await HandleAccettoCommand(
                    new SmsWebhookDTO { From = from, To = to, Body = body, MessageSid = messageId }, auditLog, from);

            if (command == SmsCommand.STOP)
                return await HandleStopCommand(
                    new SmsWebhookDTO { From = from, To = to, Body = body, MessageSid = messageId }, auditLog, from);

            // Comando non riconosciuto
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Comando non riconosciuto";
            var errorResponse = GenerateSmsResponse("Comando non valido. Usa ADAPTIVE_GDPR o ADAPTIVE_PROFILE");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);

            return OkSms(errorResponse);
        }
        catch (Exception ex)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = ex.Message;
            auditLog.ResponseSent = GenerateSmsResponse("Errore interno del server.");
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
    private async Task<ActionResult> HandleAdaptiveGdprCommand(SmsWebhookDTO dto, SmsAuditLog auditLog, string command, string from)
    {
        // Estrai parametri: "ADAPTIVE_GDPR Brand Cognome Nome +393331234567"
        var parts = dto.Body?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        if (parts.Length < 5)  // comando + brand + cognome + nome + numero
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Formato comando ADAPTIVE_GDPR non valido";
            var errorResponse = GenerateSmsResponse("Formato non valido. Usa formato: ADAPTIVE_GDPR Brand Cognome Nome 3334455666");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        // Valida il brand (case-insensitive)
        var brandFromSms = parts[1];
        var validBrands = VehicleConstants.ValidBrands;
        var matchedBrand = validBrands.FirstOrDefault(b =>
            b.Equals(brandFromSms, StringComparison.OrdinalIgnoreCase));

        if (matchedBrand == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = $"Brand non esistente: {brandFromSms}";
            var brandList = string.Join(", ", validBrands);
            var errorResponse = GenerateSmsResponse($"Brand '{brandFromSms}' non esistente. Brand validi: {brandList}. Inviare SMS ADAPTIVE_GDPR con brand corretto.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        // Usa il brand normalizzato
        var brand = matchedBrand;

        // Formato fisso: ADAPTIVE_GDPR <brand> <cognome> <nome> <numero>
        string surname = parts[2];
        string name = parts[3];
        string targetPhoneRaw = parts[4];
        string fullName = $"{surname} {name}".Trim();
        var targetPhone = NormalizePhoneNumber(targetPhoneRaw);

        // Verifica che il mittente sia un VehicleMobileNumber registrato
        var normalizedFrom = NormalizePhoneNumber(from);

        // Confronto più tollerante: compariamo solo le cifre (rimuovendo + e altri simboli)
        // e accettiamo anche che uno dei due numeri sia suffisso dell'altro (es. +39XXXXXXXXXX vs XXXXXXXXXX).
        static string DigitsOnly(string s) => Regex.Replace(s ?? string.Empty, "\\D", "");

        var fromDigits = DigitsOnly(normalizedFrom);

        // Carica in memoria solo i veicoli attivi e poi fai il matching tollerante sui numeri
        var candidates = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .Where(v => v.IsActiveFlag)
            .ToListAsync();

        var vehicle = candidates.FirstOrDefault(v =>
        {
            var vDigits = DigitsOnly(NormalizePhoneNumber(v.VehicleMobileNumber ?? string.Empty));
            return vDigits == fromDigits || vDigits.EndsWith(fromDigits) || fromDigits.EndsWith(vDigits);
        });

        if (vehicle == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Mittente non autorizzato";
            var errorResponse = GenerateSmsResponse("Solo il Cellulare Operativo Autorizzato registrato può richiedere i consensi GDPR");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        // Verifica che il brand specificato nell'SMS corrisponda al brand del veicolo mittente
        if (!vehicle.Brand.Equals(brand, StringComparison.OrdinalIgnoreCase))
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = $"Brand SMS ({brand}) non corrisponde al brand del veicolo ({vehicle.Brand})";
            var errorResponse = GenerateSmsResponse($"Il brand '{brand}' non corrisponde al brand del veicolo registrato ({vehicle.Brand}). Usa: ADAPTIVE_GDPR {vehicle.Brand} Cognome Nome Numero");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        var targetDigits = DigitsOnly(targetPhone);

        var pendingRequests = await _db.SmsAdaptiveGdpr
            .Where(g => !g.ConsentAccepted 
                        && g.RequestedAt >= DateTime.Now.AddMinutes(-SMS_ADAPTIVE_GDPR_REQUEST_INTERVAL_MINUTES))
            .ToListAsync();

        var hasPendingRequest = pendingRequests.Any(g => 
            DigitsOnly(NormalizePhoneNumber(g.AdaptiveNumber)) == targetDigits);

        if (hasPendingRequest)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Richiesta GDPR già pendente per questo numero";
            var errorResponse = GenerateSmsResponse(
                $"Il numero {targetPhone} ha già una richiesta GDPR in attesa. Attendi la risposta ACCETTO prima di inviare nuove richieste.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        // Controlla se esiste già un consenso accettato per lo stesso numero ma con nome diverso
        var existingAcceptedForNumber = await _db.SmsAdaptiveGdpr
            .Where(g => g.ConsentAccepted
                        && g.Brand == brand)
            .ToListAsync();

        var conflictingConsent = existingAcceptedForNumber
            .FirstOrDefault(g => DigitsOnly(NormalizePhoneNumber(g.AdaptiveNumber)) == targetDigits
                            && g.AdaptiveSurnameName != fullName);

        if (conflictingConsent != null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Numero già associato ad altra persona";
            var errorResponse = GenerateSmsResponse(
                $"Il numero {targetPhone} è già associato a {conflictingConsent.AdaptiveSurnameName} per {brand}. Usa un numero diverso per {fullName}, oppure revoca prima il consenso di {conflictingConsent.AdaptiveSurnameName} con STOP.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }        

        // Crea richiesta GDPR con tutti i campi obbligatori
        var gdprRequest = await _db.SmsAdaptiveGdpr
                    .FirstOrDefaultAsync(g => g.AdaptiveNumber == targetPhone
                                        && g.AdaptiveSurnameName == fullName
                                        && g.Brand == brand);

        if (gdprRequest != null && gdprRequest.ConsentAccepted)
        {
            auditLog.ProcessingStatus = "SUCCESS";
            auditLog.VehicleIdResolved = vehicle.Id;
            var alreadyAcceptedMessage = "Consenso GDPR già accettato per l'utente. Rispondere STOP per revocare il consenso.";
            auditLog.ResponseSent = alreadyAcceptedMessage;
            await SaveAuditLogAsync(auditLog);
            
            await _smsConfig.SendSmsAsync(from, alreadyAcceptedMessage);
            
            return OkSms(alreadyAcceptedMessage);
        }

        if (gdprRequest == null)
        {
            gdprRequest = new SmsAdaptiveGdpr
            {
                AdaptiveNumber = targetPhone,
                AdaptiveSurnameName = fullName,
                Brand = brand,
                ClientCompanyId = vehicle.ClientCompanyId,
                ConsentToken = SmsAdaptiveGdpr.GenerateSecureToken(),
                RequestedAt = DateTime.Now,
                ConsentAccepted = false,
                AttemptCount = 1
            };
            _db.SmsAdaptiveGdpr.Add(gdprRequest);
        }
        else
        {
            gdprRequest.AttemptCount++;
            gdprRequest.RequestedAt = DateTime.Now;
            gdprRequest.ConsentToken = SmsAdaptiveGdpr.GenerateSecureToken();
        }

        await _db.SaveChangesAsync();

        // Invia SMS al numero target con link GDPR specifico per brand
        var gdprInfoLink = GetGdprInfoLinkForBrand(brand);
        var gdprMessage = $"DataPolar: Consenso GDPR per {brand} e Google Ads. Info: {gdprInfoLink}. Rispondi ACCETTO per confermare.";

        // Validazione preventiva: assicuriamoci che il numero target contenga cifre vere        
        if (string.IsNullOrWhiteSpace(targetDigits) || targetDigits.Length < 6)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Numero destinatario non valido";
            var invalidResponse = GenerateSmsResponse("Numero destinatario non valido. Verifica il formato del numero.");
            auditLog.ResponseSent = invalidResponse;
            await SaveAuditLogAsync(auditLog);
            await _logger.Warning("Sms.GDPR", "Invalid target phone detected", $"From: {from}, TargetRaw: {targetPhone}");
            return OkSms(invalidResponse);
        }

        await _smsConfig.SendSmsAsync(targetPhone, gdprMessage);

        auditLog.VehicleIdResolved = vehicle.Id;
        auditLog.ProcessingStatus = "SUCCESS";
        auditLog.ResponseSent = GenerateSmsResponse($"Richiesta GDPR inviata a {fullName} ({targetPhone})");
        await SaveAuditLogAsync(auditLog);

        await _logger.Info("Sms.GDPR", "GDPR request sent",
            $"From: {from}, To: {targetPhone}, Name: {fullName}, Brand: {brand}");

        return OkSms(auditLog.ResponseSent);
    }

    /// <summary>
    /// Restituisce il link informativo GDPR specifico per brand.
    /// </summary>
    private static string GetGdprInfoLinkForBrand(string brand)
    {
        // Mapping brand -> link GDPR specifico
        return brand.ToUpperInvariant() switch
        {
            "TESLA" => "FUTURELINKPDF_TESLA",
            // Aggiungi altri brand quando disponibili
            _ => $"FUTURELINKPDF_{brand.ToUpperInvariant()}"
        };
    }

    // Gestione ACCETTO
    private async Task<ActionResult> HandleAccettoCommand(SmsWebhookDTO dto, SmsAuditLog auditLog, string from)
    {
        var fromDigits = DigitsOnly(NormalizePhoneNumber(from));

        var gdprCandidates = await _db.SmsAdaptiveGdpr
            .Include(g => g.ClientCompany)
            .Where(g => !g.ConsentAccepted)
            .OrderByDescending(g => g.RequestedAt)
            .ToListAsync();

        var gdprRequest = gdprCandidates
            .FirstOrDefault(g => DigitsOnly(NormalizePhoneNumber(g.AdaptiveNumber)) == fromDigits);

        if (gdprRequest == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Nessuna richiesta GDPR valida trovata";
            var errorResponse = GenerateSmsResponse("Nessuna richiesta di consenso valida per questo numero.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        // Aggiorna consenso GDPR
        gdprRequest.ConsentAccepted = true;
        gdprRequest.ConsentGivenAt = DateTime.Now;
        gdprRequest.IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        gdprRequest.UserAgent = Request.Headers["User-Agent"].ToString();

        // Aggiorna tutte le righe ADAPTIVE_PROFILE legate a questo numero
        var profileCandidates = await _db.SmsAdaptiveProfile
            .ToListAsync();

        var profileEvents = profileCandidates
            .Where(p => DigitsOnly(NormalizePhoneNumber(p.AdaptiveNumber)) == fromDigits)
            .ToList();

        foreach (var pe in profileEvents)
        {
            pe.ConsentAccepted = true;
        }

        await _db.SaveChangesAsync();

        // Invia SMS di conferma
        var confirmMessage = $@"Autorizzazione ADAPTIVE_GDPR confermata per {gdprRequest.Brand} da {gdprRequest.ClientCompany?.Name} ID Consenso: #{gdprRequest.Id}. Consenso revocabile rispondendo a questo SMS con: STOP";

        await _smsConfig.SendSmsAsync(from, confirmMessage);

        auditLog.ProcessingStatus = "SUCCESS";
        auditLog.ResponseSent = confirmMessage;
        await SaveAuditLogAsync(auditLog);

        return OkSms(confirmMessage);
    }

    // Gestione STOP
    private async Task<ActionResult> HandleStopCommand(SmsWebhookDTO dto, SmsAuditLog auditLog, string from)
    {
        var fromDigits = DigitsOnly(NormalizePhoneNumber(from));

        var gdprCandidates = await _db.SmsAdaptiveGdpr
            .OrderByDescending(g => g.ConsentGivenAt ?? g.RequestedAt)
            .ToListAsync();

        var gdprRequest = gdprCandidates
            .FirstOrDefault(g => DigitsOnly(NormalizePhoneNumber(g.AdaptiveNumber)) == fromDigits);

        if (gdprRequest == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Nessuna richiesta GDPR trovata";
            var errorResponse = GenerateSmsResponse("Nessuna richiesta GDPR trovata per questo numero.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        bool wasPending = !gdprRequest.ConsentAccepted;

        if (wasPending)
        {
            _db.SmsAdaptiveGdpr.Remove(gdprRequest);
        }
        else
        {
            gdprRequest.ConsentAccepted = false;
        }

        // Disattiva tutte le righe ADAPTIVE_PROFILE
        var profileCandidates = await _db.SmsAdaptiveProfile
            .ToListAsync();

        var profileEvents = profileCandidates
            .Where(p => DigitsOnly(NormalizePhoneNumber(p.AdaptiveNumber)) == fromDigits)
            .ToList();

        foreach (var pe in profileEvents)
        {
            pe.ConsentAccepted = false;
            pe.ParsedCommand = SmsCommand.ADAPTIVE_PROFILE_OFF;
        }

        await _db.SaveChangesAsync();

        // Invia SMS di conferma revoca
        var stopMessage = wasPending 
            ? $"Richiesta GDPR pendente annullata per {gdprRequest.Brand}. Puoi inviare una nuova richiesta corretta."
            : $"Autorizzazione ADAPTIVE_GDPR rimossa per {gdprRequest.Brand} ID Consenso: #{gdprRequest.Id}. Tutti i consensi ed i dati personali sono stati rimossi dal sistema a norma del GDPR.";

        await _smsConfig.SendSmsAsync(from, stopMessage);

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

        // ADAPTIVE_PROFILE XXXXXXXXXX Nome Cognome
        if (normalizedMessage.StartsWith("ADAPTIVE_PROFILE"))
        {
            return "ADAPTIVE_PROFILE";
        }

        // ACCETTO
        if (normalizedMessage == SmsCommand.ACCETTO)
        {
            return SmsCommand.ACCETTO;
        }

        // STOP
        if (normalizedMessage == SmsCommand.STOP || normalizedMessage == SmsCommand.OFF)
        {
            return SmsCommand.STOP;
        }

        return "INVALID";
    }

    /// <summary>
    /// Gestione comando ADAPTIVE_PROFILE
    /// </summary>
    private async Task<ActionResult> HandleAdaptiveProfileCommand(SmsWebhookDTO dto, SmsAuditLog auditLog, string command, string from)
    {
        // Formato coerente con ADAPTIVE_GDPR: "ADAPTIVE_PROFILE VIN Cognome Nome +393331234567"
        var parts = dto.Body!.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)  // comando + VIN + cognome + nome + numero
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Formato comando ADAPTIVE_PROFILE non valido";
            var errorResponse = GenerateSmsResponse("Formato non valido. Usa: ADAPTIVE_PROFILE VIN Cognome Nome 3334455666");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        var targetVin = parts[1].ToUpper().Trim();  // VIN subito dopo il comando
        var surname = parts[2];
        var name = parts[3];
        var fullName = $"{surname} {name}";  // "Rossi Mario"
        var targetPhone = NormalizePhoneNumber(parts[4]);

        // Cerca veicolo per VIN specificato nel comando
        var vehicle = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .FirstOrDefaultAsync(v => v.Vin == targetVin && v.IsActiveFlag);

        if (vehicle == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = $"Veicolo con VIN {targetVin} non trovato o non attivo";
            var errorResponse = GenerateSmsResponse($"Veicolo con VIN {targetVin} non trovato o non attivo.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        // Verifica che il mittente sia autorizzato per questo specifico veicolo (comparazione digit-only)
        var fromDigits = DigitsOnly(NormalizePhoneNumber(from));
        var vehicleDigits = DigitsOnly(NormalizePhoneNumber(vehicle.VehicleMobileNumber ?? string.Empty));
        if (vehicleDigits != fromDigits && !vehicleDigits.EndsWith(fromDigits) && !fromDigits.EndsWith(vehicleDigits))
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = $"Mittente non autorizzato per VIN {targetVin}";
            var errorResponse = GenerateSmsResponse($"Non sei autorizzato a gestire ADAPTIVE_PROFILE per il veicolo {targetVin}");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return OkSms(errorResponse);
        }

        // ⚠️ VERIFICA CHE ESISTA CONSENSO GDPR ATTIVO
        var gdprConsent = await _db.SmsAdaptiveGdpr
            .Where(g => g.AdaptiveNumber == targetPhone
                    && g.AdaptiveSurnameName == fullName // Impedisce di usare il consenso GDPR di una persona per creare il profilo di un'altra
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

            await _smsConfig.SendSmsAsync(from, warningMessage);

            auditLog.ResponseSent = warningMessage;
            await SaveAuditLogAsync(auditLog);

            return OkSms(warningMessage);
        }

        // Trova o crea riga ADAPTIVE_PROFILE
        var profileEvent = await _db.SmsAdaptiveProfile
            .Where(p => p.VehicleId == vehicle.Id
                    && p.AdaptiveNumber == targetPhone
                    && p.AdaptiveSurnameName == fullName)
            .OrderByDescending(p => p.ReceivedAt)
            .FirstOrDefaultAsync();

        if (profileEvent == null)
        {
            // Crea nuova riga - INCLUDE SmsAdaptiveGdprId OBBLIGATORIO
            profileEvent = new SmsAdaptiveProfile
            {
                VehicleId = vehicle.Id,
                AdaptiveNumber = targetPhone,
                AdaptiveSurnameName = fullName,
                ReceivedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddHours(SMS_ADAPTIVE_HOURS_THRESHOLD),
                MessageContent = dto.Body,
                ParsedCommand = SmsCommand.ADAPTIVE_PROFILE_ON,
                ConsentAccepted = true,
                SmsAdaptiveGdprId = gdprConsent.Id
            };

            _db.SmsAdaptiveProfile.Add(profileEvent);
        }
        else
        {
            // Aggiorna riga esistente (estende sessione di 24 ore)
            profileEvent.AdaptiveSurnameName = fullName;
            profileEvent.ReceivedAt = DateTime.Now;
            profileEvent.ExpiresAt = DateTime.Now.AddHours(SMS_ADAPTIVE_HOURS_THRESHOLD);
            profileEvent.MessageContent = dto.Body!;
            profileEvent.ParsedCommand = SmsCommand.ADAPTIVE_PROFILE_ON;
            profileEvent.ConsentAccepted = true;
            profileEvent.SmsAdaptiveGdprId = gdprConsent.Id;
        }

        await _db.SaveChangesAsync();

        // Invia SMS di conferma al numero target
        var confirmMessage = $@"Autorizzazione ADAPTIVE_PROFILE confermata 
        Giorno: {DateTime.Now:dd/MM/yyyy} 
        Azienda: {vehicle.ClientCompany?.Name} 
        Brand: {vehicle.Brand} 
        VIN: {vehicle.Vin} 
        Validità profilo: {SMS_ADAPTIVE_HOURS_THRESHOLD} ore";

        await _smsConfig.SendSmsAsync(targetPhone, confirmMessage);

        auditLog.ProcessingStatus = "SUCCESS";
        auditLog.VehicleIdResolved = vehicle.Id;
        auditLog.ResponseSent = confirmMessage;
        await SaveAuditLogAsync(auditLog);

        await _logger.Info("Sms.PROFILE", "Adaptive Profile activated",
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
            "md5" => ToHex(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(concat))),
            "sha1" => ToHex(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(concat))),
            "sha256" => ToHex(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(concat))),
            "sha512" => ToHex(System.Security.Cryptography.SHA512.HashData(System.Text.Encoding.UTF8.GetBytes(concat))),
            _ => string.Empty
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

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        // Rimuove spazi, trattini, parentesi
        var cleaned = Regex.Replace(phoneNumber, @"[\s\-\(\)]", "");

        // Se inizia con 00, sostituisce con +
        if (cleaned.StartsWith("00"))
            cleaned = "+" + cleaned.Substring(2);

        // Non aggiungiamo più prefissi automatici (es. +39). Conserviamo il numero così com'è.
        return cleaned;
    }

    private static string DigitsOnly(string s) => Regex.Replace(s ?? string.Empty, "\\D", "");

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
    /// Ottiene lo storico delle sessioni ADAPTIVE_PROFILE per un veicolo
    /// </summary>
    [HttpGet("adaptive-profile/{vehicleId}/history")]
    public async Task<ActionResult> GetAdaptiveProfileHistory(int vehicleId)
    {
        try
        {
            var sessions = await _db.SmsAdaptiveProfile
                .Where(p => p.VehicleId == vehicleId)
                .OrderByDescending(p => p.ReceivedAt)
                .Select(p => new
                {
                    p.Id,
                    p.VehicleId,
                    p.AdaptiveNumber,
                    p.AdaptiveSurnameName,
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
            await _logger.Error("Sms.GetHistory", "Error fetching profile history",
                $"VehicleId: {vehicleId}, Error: {ex.Message}");
            return StatusCode(500, new { error = "Errore nel recupero dello storico" });
        }
    }

    /// <summary>
    /// Ottiene lo stato della sessione ADAPTIVE_PROFILE attiva per un veicolo
    /// </summary>
    [HttpGet("adaptive-profile/{vehicleId}/status")]
    public async Task<ActionResult> GetAdaptiveProfileStatus(int vehicleId)
    {
        try
        {
            var activeSession = await _db.SmsAdaptiveProfile
                .Where(p => p.VehicleId == vehicleId
                        && p.ConsentAccepted
                        && p.ParsedCommand == SmsCommand.ADAPTIVE_PROFILE_ON
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
                    adaptiveSurnameName = (string?)null,
                    adaptiveNumber = (string?)null
                });
            }

            var remainingTime = activeSession.ExpiresAt - DateTime.Now;

            return Ok(new
            {
                isActive = true,
                sessionStartedAt = activeSession.ReceivedAt,
                sessionEndTime = activeSession.ExpiresAt,
                remainingMinutes = (int)remainingTime.TotalMinutes,
                adaptiveSurnameName = activeSession.AdaptiveSurnameName,
                adaptiveNumber = activeSession.AdaptiveNumber
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("Sms.GetStatus", "Error fetching profile status",
                $"VehicleId: {vehicleId}, Error: {ex.Message}");
            return StatusCode(500, new { error = "Errore nel recupero dello stato" });
        }
    }

    /// <summary>
    /// Ottiene la lista dei brand disponibili per ADAPTIVE_GDPR
    /// </summary>
    [HttpGet("gdpr/brands")]
    public ActionResult GetAvailableBrands()
    {
        try
        {
            var brands = VehicleConstants.ValidBrands;
            return Ok(new { brands, polarDriveMobileNumber = SMS_ADAPTIVE_MOBILE_NUMBER });
        }
        catch (Exception ex)
        {
            _logger.Error("Sms.GetBrands", "Error fetching available brands", ex.Message);
            return StatusCode(500, new { error = "Errore nel recupero dei brand disponibili" });
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
                return BadRequest(new { errorCode = "BRAND_REQUIRED" });
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
    /// Ottiene statistiche Adaptive Profile per un veicolo
    /// </summary>
    [HttpGet("adaptive-profile/{vehicleId}/stats")]
    public async Task<ActionResult> GetAdaptiveProfileStats(int vehicleId)
    {
        try
        {
            var sessions = await _db.SmsAdaptiveProfile
                .Where(p => p.VehicleId == vehicleId && p.ParsedCommand == SmsCommand.ADAPTIVE_PROFILE_ON)
                .ToListAsync();

            var totalSessions = sessions.Count;
            var activeSessions = sessions.Count(s => s.ConsentAccepted
                                                && s.ParsedCommand == SmsCommand.ADAPTIVE_PROFILE_ON
                                                && s.ExpiresAt > DateTime.Now);
            var lastSession = sessions.MaxBy(s => s.ReceivedAt)?.ReceivedAt;
            var firstSession = sessions.MinBy(s => s.ReceivedAt)?.ReceivedAt;

            // Conta i dati raccolti durante le sessioni (se hai il campo IsSmsAdaptiveProfe in VehicleData)
            var adaptiveDataCount = await _db.VehiclesData
                .Where(d => d.VehicleId == vehicleId && d.IsSmsAdaptiveProfile)
                .CountAsync();

            return Ok(new
            {
                vehicleId,
                totalSessions,
                activeSessions,
                firstSession,
                lastSession,
                adaptiveDataPointsCollected = adaptiveDataCount,
                totalHoursApprox = totalSessions * SMS_ADAPTIVE_HOURS_THRESHOLD
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("Sms.GetStats", "Error fetching profile stats",
                $"VehicleId: {vehicleId}, Error: {ex.Message}");
            return StatusCode(500, new { error = "Errore nel recupero delle statistiche" });
        }
    }
}