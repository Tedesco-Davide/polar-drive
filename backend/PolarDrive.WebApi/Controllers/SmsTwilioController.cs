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
public class SmsTwilioController(
    PolarDriveDbContext db,
    ISmsTwilioConfigurationService twilioConfig) : ControllerBase
{
    private readonly PolarDriveDbContext _db = db;
    private readonly ISmsTwilioConfigurationService _twilioConfig = twilioConfig;
    private readonly PolarDriveLogger _logger = new(db);

    /// <summary>
    /// 🎯 WEBHOOK PRINCIPALE - Riceve SMS da Twilio
    /// </summary>
    [HttpPost("webhook")]
    public async Task<ActionResult> ReceiveTwilioSms([FromForm] SmsTwilioWebhookDTO dto)
    {
        var auditLog = new SmsAuditLog
        {
            MessageSid = dto.MessageSid,
            FromPhoneNumber = dto.From,
            ToPhoneNumber = dto.To,
            MessageBody = dto.Body,
            ReceivedAt = DateTime.Now,
            ProcessingStatus = "PROCESSING"
        };

        try
        {
            // 🔒 1. VALIDAZIONE SICUREZZA
            if (!ValidateTwilioSignature())
            {
                auditLog.ProcessingStatus = "REJECTED";
                auditLog.ErrorMessage = "Invalid Twilio signature";
                await SaveAuditLogAsync(auditLog);
                return Unauthorized("Invalid signature");
            }

            if (!_twilioConfig.IsPhoneNumberAllowed(dto.From))
            {
                auditLog.ProcessingStatus = "REJECTED";
                auditLog.ErrorMessage = "Phone number not in whitelist";
                await SaveAuditLogAsync(auditLog);

                var response = GenerateTwilioResponse("❌ Numero non autorizzato per questo servizio.");
                auditLog.ResponseSent = response;
                await UpdateAuditLogAsync(auditLog);

                return Ok(response);
            }

            if (await _twilioConfig.IsRateLimitExceeded(dto.From))
            {
                auditLog.ProcessingStatus = "REJECTED";
                auditLog.ErrorMessage = "Rate limit exceeded";
                await SaveAuditLogAsync(auditLog);

                var response = GenerateTwilioResponse("⏱️ Troppi messaggi. Riprova tra qualche minuto.");
                auditLog.ResponseSent = response;
                await UpdateAuditLogAsync(auditLog);

                return Ok(response);
            }

            // 📱 2. PARSING COMANDO
            var command = ParseSmsCommand(dto.Body);

            // 🎯 3. GESTIONE COMANDO ADAPTIVE_GDPR
            if (command.StartsWith("ADAPTIVE_GDPR"))
            {
                return await HandleAdaptiveGdprCommand(dto, auditLog, command);
            }

            // 🎯 4. GESTIONE COMANDO ADAPTIVE_PROFILING
            if (command.StartsWith("ADAPTIVE_PROFILING"))
            {
                return await HandleAdaptiveProfilingCommand(dto, auditLog, command);
            }

            // 🎯 5. GESTIONE ACCETTO
            if (command == "ACCETTO")
            {
                return await HandleAccettoCommand(dto, auditLog);
            }

            // 🎯 6. GESTIONE STOP
            if (command == "STOP")
            {
                return await HandleStopCommand(dto, auditLog);
            }

            // ❌ Comando non riconosciuto
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Comando non riconosciuto";
            var errorResponse = GenerateTwilioResponse("❌ Comando non valido. Usa ADAPTIVE_GDPR o ADAPTIVE_PROFILING.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);

            return Ok(errorResponse);
        }
        catch (Exception ex)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = ex.Message;
            auditLog.ResponseSent = GenerateTwilioResponse("❌ Errore interno del server.");
            await SaveAuditLogAsync(auditLog);

            await _logger.Error("TwilioSms.Webhook", "Fatal error processing SMS",
                $"Error: {ex.Message}, From: {dto.From}");

            return Ok(auditLog.ResponseSent);
        }
    }

    // Gestione ADAPTIVE_GDPR
    private async Task<ActionResult> HandleAdaptiveGdprCommand(SmsTwilioWebhookDTO dto, SmsAuditLog auditLog, string command)
    {
        // Estrai numero destinatario: "ADAPTIVE_GDPR XXXXXXXXXX"
        var parts = dto.Body.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Formato comando ADAPTIVE_GDPR non valido";
            var errorResponse = GenerateTwilioResponse("❌ Formato non valido. Usa: ADAPTIVE_GDPR XXXXXXXXXX");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return Ok(errorResponse);
        }

        var targetPhone = NormalizePhoneNumber(parts[1]);

        // Verifica che il mittente sia un ReferentMobileNumber registrato
        var vehicle = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .FirstOrDefaultAsync(v => v.ReferentMobileNumber == dto.From && v.IsActiveFlag);

        if (vehicle == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Mittente non autorizzato";
            var errorResponse = GenerateTwilioResponse("❌ Solo i referenti registrati possono richiedere consensi GDPR.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return Ok(errorResponse);
        }

        // Crea richiesta GDPR
        var gdprRequest = new SmsAdaptiveGdpr
        {
            PhoneNumber = targetPhone,
            Brand = vehicle.Brand,
            ClientCompanyId = vehicle.ClientCompanyId,
            ConsentToken = SmsAdaptiveGdpr.GenerateSecureToken(),
            RequestedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddHours(SMS_ADPATIVE_HOURS_THRESHOLD),
            ConsentAccepted = false,
            AttemptCount = 1
        };

        _db.SmsAdaptiveGdpr.Add(gdprRequest);

        // Crea riga ADAPTIVE_PROFILING (vuota, in attesa di attivazione)
        var profilingEvent = new SmsAdaptiveProfiling
        {
            VehicleId = vehicle.Id,
            AdaptiveProfilingNumber = targetPhone,
            AdaptiveProfilingName = string.Empty, // Verrà popolato dopo
            ReceivedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddHours(SMS_ADPATIVE_HOURS_THRESHOLD),
            MessageContent = dto.Body,
            ParsedCommand = "ADAPTIVE_PROFILING_OFF", // Non ancora attivo
            ConsentAccepted = false
        };

        _db.SmsAdaptiveProfiling.Add(profilingEvent);
        await _db.SaveChangesAsync();

        // Invia SMS al numero target
        var gdprMessage = $@"DataPolar - Consenso utilizzo {vehicle.Brand} da {vehicle.ClientCompany?.Name} Per guidare questo veicolo di ricerca, deve accettare il trattamento dati GPS/telemetria come richiesto da {vehicle.Brand}. 📄 Informativa completa: [short.link/gdpr-pdf] Risponda: ✅ ACCETTO - per dare consenso esplicito ℹ️ Ignora SMS per negare il consenso";

        await _twilioConfig.SendSmsAsync(targetPhone, gdprMessage);

        auditLog.ProcessingStatus = "SUCCESS";
        auditLog.ResponseSent = GenerateTwilioResponse($"✅ Richiesta GDPR inviata a {targetPhone}");
        await SaveAuditLogAsync(auditLog);

        await _logger.Info("TwilioSms.GDPR", "GDPR request sent",
            $"From: {dto.From}, To: {targetPhone}, Brand: {vehicle.Brand}");

        return Ok(auditLog.ResponseSent);
    }

    // Gestione ACCETTO
    private async Task<ActionResult> HandleAccettoCommand(SmsTwilioWebhookDTO dto, SmsAuditLog auditLog)
    {
        var gdprRequest = await _db.SmsAdaptiveGdpr
            .Include(g => g.ClientCompany)
            .Where(g => g.PhoneNumber == dto.From && !g.ConsentAccepted && g.ExpiresAt > DateTime.Now)
            .OrderByDescending(g => g.RequestedAt)
            .FirstOrDefaultAsync();

        if (gdprRequest == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Nessuna richiesta GDPR valida trovata";
            var errorResponse = GenerateTwilioResponse("❌ Nessuna richiesta di consenso valida per questo numero.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return Ok(errorResponse);
        }

        // Aggiorna consenso GDPR
        gdprRequest.ConsentAccepted = true;
        gdprRequest.ConsentGivenAt = DateTime.Now;
        gdprRequest.IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

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

        await _twilioConfig.SendSmsAsync(dto.From, confirmMessage);

        auditLog.ProcessingStatus = "SUCCESS";
        auditLog.ResponseSent = confirmMessage;
        await SaveAuditLogAsync(auditLog);

        return Ok(GenerateTwilioResponse(confirmMessage));
    }

    // Gestione STOP
    private async Task<ActionResult> HandleStopCommand(SmsTwilioWebhookDTO dto, SmsAuditLog auditLog)
    {
        var gdprRequest = await _db.SmsAdaptiveGdpr
            .Where(g => g.PhoneNumber == dto.From && g.ConsentAccepted)
            .OrderByDescending(g => g.ConsentGivenAt)
            .FirstOrDefaultAsync();

        if (gdprRequest == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Nessun consenso attivo trovato";
            var errorResponse = GenerateTwilioResponse("❌ Nessun consenso attivo per questo numero.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return Ok(errorResponse);
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

        await _twilioConfig.SendSmsAsync(dto.From, stopMessage);

        auditLog.ProcessingStatus = "SUCCESS";
        auditLog.ResponseSent = stopMessage;
        await SaveAuditLogAsync(auditLog);

        return Ok(GenerateTwilioResponse(stopMessage));
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
    private async Task<ActionResult> HandleAdaptiveProfilingCommand(SmsTwilioWebhookDTO dto, SmsAuditLog auditLog, string command)
    {
        // Estrai parametri: "ADAPTIVE_PROFILING XXXXXXXXXX Mario Rossi"
        var parts = dto.Body.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Formato comando ADAPTIVE_PROFILING non valido";
            var errorResponse = GenerateTwilioResponse("❌ Formato non valido. Usa: ADAPTIVE_PROFILING XXXXXXXXXX Nome Cognome");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return Ok(errorResponse);
        }

        var targetPhone = NormalizePhoneNumber(parts[1]);
        var fullName = string.Join(" ", parts.Skip(2)); // "Mario Rossi"

        // Verifica che il mittente sia un ReferentMobileNumber registrato
        var vehicle = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .FirstOrDefaultAsync(v => v.ReferentMobileNumber == dto.From && v.IsActiveFlag);

        if (vehicle == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Mittente non autorizzato";
            var errorResponse = GenerateTwilioResponse("❌ Solo i referenti registrati possono attivare profili.");
            auditLog.ResponseSent = errorResponse;
            await SaveAuditLogAsync(auditLog);
            return Ok(errorResponse);
        }

        // ⚠️ VERIFICA CHE ESISTA CONSENSO GDPR ATTIVO
        var gdprConsent = await _db.SmsAdaptiveGdpr
            .Where(g => g.PhoneNumber == targetPhone
                     && g.Brand == vehicle.Brand
                     && g.ConsentAccepted)
            .OrderByDescending(g => g.ConsentGivenAt)
            .FirstOrDefaultAsync();

        if (gdprConsent == null)
        {
            auditLog.ProcessingStatus = "ERROR";
            auditLog.ErrorMessage = "Procedura ADAPTIVE_GDPR mai eseguita";

            // Invia SMS al ReferentMobileNumber (il mittente)
            var warningMessage = $@"ATTENZIONE {vehicle.ReferentName}! Procedura ADAPTIVE_GDPR mai eseguita per {targetPhone}. Completare la procedura ADAPTIVE_GDPR per {targetPhone} prima di continuare";

            await _twilioConfig.SendSmsAsync(dto.From, warningMessage);

            auditLog.ResponseSent = warningMessage;
            await SaveAuditLogAsync(auditLog);

            return Ok(GenerateTwilioResponse(warningMessage));
        }

        // Trova o crea riga ADAPTIVE_PROFILING
        var profilingEvent = await _db.SmsAdaptiveProfiling
            .Where(p => p.VehicleId == vehicle.Id
                     && p.AdaptiveProfilingNumber == targetPhone)
            .OrderByDescending(p => p.ReceivedAt)
            .FirstOrDefaultAsync();

        if (profilingEvent == null)
        {
            // Crea nuova riga
            profilingEvent = new SmsAdaptiveProfiling
            {
                VehicleId = vehicle.Id,
                AdaptiveProfilingNumber = targetPhone,
                AdaptiveProfilingName = fullName,
                ReceivedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddHours(SMS_ADPATIVE_HOURS_THRESHOLD),
                MessageContent = dto.Body,
                ParsedCommand = "ADAPTIVE_PROFILING_ON",
                ConsentAccepted = true
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
        }

        await _db.SaveChangesAsync();

        // Invia SMS di conferma al numero target
        var confirmMessage = $@"Autorizzazione ADAPTIVE_PROFILING confermata Giorno: {DateTime.Now:dd/MM/yyyy} Azienda: {vehicle.ClientCompany?.Name} Brand: {vehicle.Brand} VIN: {vehicle.Vin} Validità profilo: {SMS_ADPATIVE_HOURS_THRESHOLD} ORE";

        await _twilioConfig.SendSmsAsync(targetPhone, confirmMessage);

        auditLog.ProcessingStatus = "SUCCESS";
        auditLog.VehicleIdResolved = vehicle.Id;
        auditLog.ResponseSent = confirmMessage;
        await SaveAuditLogAsync(auditLog);

        await _logger.Info("TwilioSms.PROFILING", "Adaptive Profiling activated",
            $"VehicleId: {vehicle.Id}, Phone: {targetPhone}, Name: {fullName}");

        return Ok(GenerateTwilioResponse(confirmMessage));
    }

    // ============================================================================
    // METODI HELPER PRIVATI
    // ============================================================================

    private bool ValidateTwilioSignature()
    {
        if (!_twilioConfig.GetConfiguration().EnableSignatureValidation)
            return true;

        try
        {
            var signature = Request.Headers["X-Twilio-Signature"].FirstOrDefault();
            if (string.IsNullOrEmpty(signature))
                return false;

            var url = $"{Request.Scheme}://{Request.Host}{Request.Path}";

            // Leggi form data per validazione
            var formData = new Dictionary<string, string>();
            foreach (var kvp in Request.Form)
            {
                formData[kvp.Key] = kvp.Value.ToString();
            }

            return _twilioConfig.ValidateSignature(signature, url, formData);
        }
        catch
        {
            return false;
        }
    }

    private string GenerateTwilioResponse(string message)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
        <Response>
            <Message>{System.Net.WebUtility.HtmlEncode(message)}</Message>
        </Response>";
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
        _db.SmsAdaptiveAuditLogs.Add(auditLog);
        await _db.SaveChangesAsync();
    }

    private async Task UpdateAuditLogAsync(SmsAuditLog auditLog)
    {
        _db.SmsAdaptiveAuditLogs.Update(auditLog);
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
            await _logger.Error("SmsTwilio.GetHistory", "Error fetching profiling history",
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
            await _logger.Error("SmsTwilio.GetStatus", "Error fetching profiling status",
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
                    g.PhoneNumber,
                    g.Brand,
                    g.RequestedAt,
                    g.ConsentGivenAt,
                    g.ConsentAccepted,
                    g.ExpiresAt,
                    CompanyName = g.ClientCompany != null ? g.ClientCompany.Name : null
                })
                .ToListAsync();

            return Ok(consents);
        }
        catch (Exception ex)
        {
            await _logger.Error("SmsTwilio.GetConsents", "Error fetching GDPR consents",
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
            var consents = await _db.SmsAdaptiveGdpr
                .Include(g => g.ClientCompany)
                .OrderByDescending(g => g.RequestedAt)
                .Take(pageSize)
                .Select(g => new
                {
                    g.Id,
                    g.PhoneNumber,
                    g.Brand,
                    g.RequestedAt,
                    g.ConsentGivenAt,
                    g.ConsentAccepted,
                    g.ExpiresAt,
                    g.AttemptCount,
                    CompanyName = g.ClientCompany != null ? g.ClientCompany.Name : null,
                    g.ClientCompanyId
                })
                .ToListAsync();

            return Ok(consents);
        }
        catch (Exception ex)
        {
            await _logger.Error("SmsTwilio.GetAllConsents", "Error fetching all GDPR consents",
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
            var totalCount = await _db.SmsAdaptiveAuditLogs.CountAsync();
            
            var logs = await _db.SmsAdaptiveAuditLogs
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
            await _logger.Error("SmsTwilio.GetAuditLogs", "Error fetching audit logs",
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
            await _logger.Error("SmsTwilio.GetStats", "Error fetching profiling stats",
                $"VehicleId: {vehicleId}, Error: {ex.Message}");
            return StatusCode(500, new { error = "Errore nel recupero delle statistiche" });
        }
    }
}