using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using PolarDrive.Data.DTOs;
using PolarDrive.WebApi.Services;
using System.Text.RegularExpressions;
using System.Text;

namespace PolarDrive.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SmsTwilioController : ControllerBase
{
    private readonly PolarDriveDbContext _db;
    private readonly ISmsTwilioConfigurationService _twilioConfig;
    private readonly SmsAdaptiveProfilingController _adaptiveController;
    private readonly PolarDriveLogger _logger;
    private readonly IConfiguration _configuration;

    public SmsTwilioController(
        PolarDriveDbContext db,
        ISmsTwilioConfigurationService twilioConfig,
        SmsAdaptiveProfilingController adaptiveController,
        IConfiguration configuration)
    {
        _db = db;
        _twilioConfig = twilioConfig;
        _adaptiveController = adaptiveController;
         _configuration = configuration;
        _logger = new PolarDriveLogger(db);
    }

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
            ReceivedAt = DateTime.UtcNow,
            ProcessingStatus = "PROCESSING"
        };

        try
        {
            // 🔒 1. VALIDAZIONE SICUREZZA
            await _logger.Info("TwilioSms.Webhook", "SMS received",
                $"From: {dto.From}, Body: {dto.Body}, MessageSid: {dto.MessageSid}");

            // Verifica signature Twilio (anti-spoofing)
            if (!ValidateTwilioSignature())
            {
                auditLog.ProcessingStatus = "REJECTED";
                auditLog.ErrorMessage = "Invalid Twilio signature";
                await SaveAuditLogAsync(auditLog);

                await _logger.Warning("TwilioSms.Webhook", "Invalid signature", $"From: {dto.From}");
                return Unauthorized("Invalid signature");
            }

            // Verifica whitelist numeri (se configurata)
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

            // Rate limiting per numero
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

            // 🚗 2. RISOLUZIONE VEICOLO
            var vehicleId = await ResolveVehicleFromSms(dto.From, dto.Body);
            auditLog.VehicleIdResolved = vehicleId;

            if (vehicleId == null)
            {
                auditLog.ProcessingStatus = "ERROR";
                auditLog.ErrorMessage = "Vehicle not resolved";

                var helpMessage = await GenerateHelpMessage(dto.From);
                auditLog.ResponseSent = helpMessage;
                await SaveAuditLogAsync(auditLog);

                return Ok(helpMessage);
            }

            // 📱 3. ELABORAZIONE COMANDO
            var result = await _adaptiveController.ReceiveSms(new ReceiveSmsDTO
            {
                VehicleId = vehicleId.Value,
                MessageContent = dto.Body
            });

            // 📤 4. RISPOSTA AL CLIENTE
            string responseMessage;
            if (result is OkObjectResult okResult && okResult.Value != null)
            {
                auditLog.ProcessingStatus = "SUCCESS";

                var responseData = okResult.Value;
                var messageProperty = responseData.GetType().GetProperty("message");
                var sessionStartProperty = responseData.GetType().GetProperty("sessionStartedAt");
                var sessionEndProperty = responseData.GetType().GetProperty("sessionEndTime");

                var message = messageProperty?.GetValue(responseData)?.ToString() ?? "Comando elaborato";
                var sessionStart = sessionStartProperty?.GetValue(responseData) as DateTime?;
                var sessionEnd = sessionEndProperty?.GetValue(responseData) as DateTime?;

                responseMessage = await GenerateSuccessResponse(vehicleId.Value, message, sessionStart, sessionEnd);
            }
            else
            {
                auditLog.ProcessingStatus = "ERROR";
                auditLog.ErrorMessage = "Command processing failed";
                responseMessage = GenerateTwilioResponse("❌ Errore nell'elaborazione del comando. Riprova.");
            }

            auditLog.ResponseSent = responseMessage;
            await SaveAuditLogAsync(auditLog);

            // 📊 5. LOGGING FINALE
            await _logger.Info("TwilioSms.Webhook", "SMS processed successfully",
                $"VehicleId: {vehicleId}, Status: {auditLog.ProcessingStatus}, Response sent");

            return Ok(responseMessage);
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

    /// <summary>
    /// 🔧 GESTIONE ASSOCIAZIONI TELEFONO-VEICOLO
    /// </summary>
    [HttpPost("register-phone")]
    public async Task<ActionResult> RegisterPhoneToVehicle([FromBody] SmsRegisterPhoneDTO dto)
    {
        try
        {
            // Valida che il veicolo esista
            var vehicle = await _db.ClientVehicles
                .Include(v => v.ClientCompany)
                .FirstOrDefaultAsync(v => v.Id == dto.VehicleId);

            if (vehicle == null)
            {
                return NotFound(new { error = "Vehicle not found" });
            }

            // Normalizza numero di telefono
            var normalizedPhone = NormalizePhoneNumber(dto.PhoneNumber);

            // Controlla se esiste già mappatura per questo numero
            var existingMapping = await _db.PhoneVehicleMappings
                .FirstOrDefaultAsync(m => m.PhoneNumber == normalizedPhone);

            if (existingMapping != null)
            {
                // Aggiorna mappatura esistente
                existingMapping.VehicleId = dto.VehicleId;
                existingMapping.UpdatedAt = DateTime.UtcNow;
                existingMapping.IsActive = true;
                existingMapping.Notes = dto.Notes;

                await _logger.Info("TwilioSms.RegisterPhone", "Phone mapping updated",
                    $"Phone: {normalizedPhone}, OldVehicle: {existingMapping.VehicleId}, NewVehicle: {dto.VehicleId}");
            }
            else
            {
                // Crea nuova mappatura
                var mapping = new PhoneVehicleMapping
                {
                    PhoneNumber = normalizedPhone,
                    VehicleId = dto.VehicleId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Notes = dto.Notes
                };

                _db.PhoneVehicleMappings.Add(mapping);

                await _logger.Info("TwilioSms.RegisterPhone", "New phone mapping created",
                    $"Phone: {normalizedPhone}, VehicleId: {dto.VehicleId}");
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Phone number registered successfully",
                phoneNumber = normalizedPhone,
                vehicleId = dto.VehicleId,
                vehicleVin = vehicle.Vin,
                companyName = vehicle.ClientCompany?.Name
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("TwilioSms.RegisterPhone", "Error registering phone",
                $"Error: {ex.Message}, Phone: {dto.PhoneNumber}, VehicleId: {dto.VehicleId}");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// 📊 STATISTICHE E MONITORING
    /// </summary>
    [HttpGet("audit-logs")]
    public async Task<ActionResult> GetAuditLogs(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? phoneNumber = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var query = _db.SmsAuditLogs.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(log => log.ReceivedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(log => log.ReceivedAt <= toDate.Value);

            if (!string.IsNullOrEmpty(phoneNumber))
                query = query.Where(log => log.FromPhoneNumber.Contains(phoneNumber));

            if (!string.IsNullOrEmpty(status))
                query = query.Where(log => log.ProcessingStatus == status);

            var totalCount = await query.CountAsync();
            var logs = await query
                .OrderByDescending(log => log.ReceivedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(log => new
                {
                    log.Id,
                    log.MessageSid,
                    log.FromPhoneNumber,
                    log.MessageBody,
                    log.ReceivedAt,
                    log.ProcessingStatus,
                    log.ErrorMessage,
                    log.VehicleIdResolved,
                    log.ResponseSent
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                logs
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("TwilioSms.GetAuditLogs", "Error retrieving audit logs", $"Error: {ex.Message}");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("phone-mappings")]
    public async Task<ActionResult> GetPhoneMappings()
    {
        try
        {
            var mappings = await _db.PhoneVehicleMappings
                .Include(m => m.ClientVehicle)
                .ThenInclude(v => v!.ClientCompany)
                .Where(m => m.IsActive)
                .Select(m => new
                {
                    m.Id,
                    m.PhoneNumber,
                    m.VehicleId,
                    VehicleVin = m.ClientVehicle!.Vin,
                    CompanyName = m.ClientVehicle.ClientCompany!.Name,
                    m.CreatedAt,
                    m.UpdatedAt,
                    m.Notes
                })
                .OrderByDescending(m => m.UpdatedAt)
                .ToListAsync();

            return Ok(mappings);
        }
        catch (Exception ex)
        {
            await _logger.Error("TwilioSms.GetPhoneMappings", "Error retrieving phone mappings", $"Error: {ex.Message}");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpDelete("phone-mappings/{id}")]
    public async Task<ActionResult> DeletePhoneMapping(int id)
    {
        try
        {
            var mapping = await _db.PhoneVehicleMappings
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mapping == null)
            {
                return NotFound(new { error = "Phone mapping not found" });
            }

            _db.PhoneVehicleMappings.Remove(mapping);
            await _db.SaveChangesAsync();

            await _logger.Info("TwilioSms.DeletePhoneMapping", "Phone mapping deleted permanently",
                $"MappingId: {id}, Phone: {mapping.PhoneNumber}, VehicleId: {mapping.VehicleId}");

            return Ok(new { message = "Phone mapping deleted successfully" });
        }
        catch (Exception ex)
        {
            await _logger.Error("TwilioSms.DeletePhoneMapping", "Error deleting phone mapping",
                $"Error: {ex.Message}, MappingId: {id}");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("statistics")]
    public async Task<ActionResult> GetTwilioStatistics([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var query = _db.SmsAuditLogs.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(log => log.ReceivedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(log => log.ReceivedAt <= toDate.Value);

            var stats = await query
                .GroupBy(log => log.ProcessingStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalSms = await query.CountAsync();
            var uniquePhones = await query.Select(log => log.FromPhoneNumber).Distinct().CountAsync();
            var successfulSms = stats.FirstOrDefault(s => s.Status == "SUCCESS")?.Count ?? 0;
            var successRate = totalSms > 0 ? (double)successfulSms / totalSms * 100 : 0;

            return Ok(new
            {
                totalSms,
                uniquePhones,
                successRate = Math.Round(successRate, 2),
                statusBreakdown = stats,
                period = new { fromDate, toDate }
            });
        }
        catch (Exception ex)
        {
            await _logger.Error("TwilioSms.GetStatistics", "Error retrieving statistics", $"Error: {ex.Message}");
            return StatusCode(500, new { error = "Internal server error" });
        }
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

    private async Task<int?> ResolveVehicleFromSms(string phoneNumber, string messageBody)
    {
        // 1. Cerca per mappatura diretta numero -> veicolo
        var mapping = await _db.PhoneVehicleMappings
            .FirstOrDefaultAsync(m => m.PhoneNumber == NormalizePhoneNumber(phoneNumber) && m.IsActive);

        if (mapping != null)
        {
            // Carica il veicolo separatamente
            var vehicle = await _db.ClientVehicles
                .FirstOrDefaultAsync(v => v.Id == mapping.VehicleId);

            if (vehicle?.IsActiveFlag == true)
            {
            if (!await HasValidConsentAsync(phoneNumber, mapping.VehicleId))
                {
                    await SendConsentRequestSms(phoneNumber, mapping.VehicleId);
                    return null; // Blocca l'esecuzione fino al consenso
                }

                await _logger.Info("TwilioSms.ResolveVehicle", "Vehicle resolved by phone mapping",
                    $"Phone: {phoneNumber}, VehicleId: {mapping.VehicleId}");

                return mapping.VehicleId;
            }
        }

        // 2. Cerca VIN nel messaggio (es: "ADAPTIVE ABC1 test session")
        var vinMatch = Regex.Match(messageBody, @"ADAPTIVE\s+([A-Z0-9]{4})", RegexOptions.IgnoreCase);
        if (vinMatch.Success)
        {
            var vinLastFour = vinMatch.Groups[1].Value.ToUpper();
            var vehicle = await _db.ClientVehicles
                .Where(v => v.Vin.EndsWith(vinLastFour) && v.IsActiveFlag && v.IsFetchingDataFlag)
                .FirstOrDefaultAsync();

            if (vehicle != null)
            {
                if (!await HasValidConsentAsync(phoneNumber, vehicle.Id))
                {
                    await SendConsentRequestSms(phoneNumber, vehicle.Id);
                    return null; // Blocca l'esecuzione fino al consenso
                }           

                await _logger.Info("TwilioSms.ResolveVehicle", "Vehicle resolved by VIN suffix",
                    $"VIN suffix: {vinLastFour}, VehicleId: {vehicle.Id}");

                return vehicle.Id;
            }
        }

        await _logger.Warning("TwilioSms.ResolveVehicle", "Could not resolve vehicle",
            $"Phone: {phoneNumber}, Message: {messageBody}");
        return null;
    }

    private async Task<string> GenerateHelpMessage(string phoneNumber)
    {
        var userVehicles = await _db.PhoneVehicleMappings
            .Include(m => m.ClientVehicle)
            .Where(m => m.PhoneNumber == NormalizePhoneNumber(phoneNumber) && m.IsActive)
            .Select(m => new { m.ClientVehicle!.Vin, m.VehicleId })
            .ToListAsync();

        var helpText = new StringBuilder();
        helpText.AppendLine("❌ Veicolo non riconosciuto.");
        helpText.AppendLine();
        helpText.AppendLine("📱 Formati supportati:");
        helpText.AppendLine("• ADAPTIVE → Attiva profiling");
        helpText.AppendLine("• ADAPTIVE ABC1 descrizione → Per VIN specifico");
        helpText.AppendLine("• STOP → Disattiva profiling");

        if (userVehicles.Any())
        {
            helpText.AppendLine();
            helpText.AppendLine("🚗 I tuoi veicoli:");
            foreach (var vehicle in userVehicles)
            {
                var vinSuffix = vehicle.Vin.Length >= 4 ? vehicle.Vin.Substring(vehicle.Vin.Length - 4) : vehicle.Vin;
                helpText.AppendLine($"• {vehicle.Vin} (usa: ADAPTIVE {vinSuffix})");
            }
        }

        return GenerateTwilioResponse(helpText.ToString());
    }

    private async Task<string> GenerateSuccessResponse(int vehicleId, string baseMessage, DateTime? sessionStart, DateTime? sessionEnd)
    {
        var vehicle = await _db.ClientVehicles
            .Include(v => v.ClientCompany)
            .FirstOrDefaultAsync(v => v.Id == vehicleId);

        var response = new StringBuilder();
        response.AppendLine($"✅ {baseMessage}");

        if (vehicle != null)
        {
            response.AppendLine($"🚗 Veicolo: {vehicle.Vin}");
            if (vehicle.ClientCompany != null)
                response.AppendLine($"🏢 Cliente: {vehicle.ClientCompany.Name}");
        }

        if (sessionStart.HasValue && sessionEnd.HasValue)
        {
            response.AppendLine($"⏰ Fino alle: {sessionEnd.Value:HH:mm} UTC");
            response.AppendLine($"📊 Modalità: Adaptive Profiling ATTIVA");
        }

        return GenerateTwilioResponse(response.ToString());
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

    private async Task<bool> HasValidConsentAsync(string phoneNumber, int vehicleId)
    {
        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        
        var consent = await _db.SmsGdprConsent
            .FirstOrDefaultAsync(c => c.PhoneNumber == normalizedPhone
                                && c.VehicleId == vehicleId
                                && c.IsActive
                                && c.ConsentGivenAt.HasValue);

        return consent != null;
    }

    private async Task SendConsentRequestSms(string phoneNumber, int vehicleId)
    {
        var normalizedPhone = NormalizePhoneNumber(phoneNumber);

        // Rate limiting (max 3 tentativi per ora)
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var recentAttempts = await _db.SmsGdprConsent
            .CountAsync(c => c.PhoneNumber == normalizedPhone && c.RequestedAt >= oneHourAgo);
        
        if (recentAttempts >= 3)
        {
            await _logger.Warning("TwilioSms.SendConsent", "Rate limit exceeded", 
                $"Phone: {phoneNumber}, Attempts: {recentAttempts}");
            return; // Blocca silenziosamente
        }

        var vehicle = await _db.ClientVehicles.FindAsync(vehicleId);
        var consentToken = SmsGdprConsent.GenerateSecureToken();
        
        // Invalida richieste precedenti
        var existingRequests = await _db.SmsGdprConsent
            .Where(c => c.PhoneNumber == normalizedPhone 
                    && c.VehicleId == vehicleId 
                    && !c.IsActive)
            .ToListAsync();
        
        _db.SmsGdprConsent.RemoveRange(existingRequests);

        // Crea nuova richiesta
        var pendingConsent = new SmsGdprConsent
        {
            PhoneNumber = normalizedPhone,
            VehicleId = vehicleId,
            ConsentToken = consentToken,
            RequestedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            IsActive = false,
            AttemptCount = recentAttempts + 1
        };
        
        _db.SmsGdprConsent.Add(pendingConsent);
        await _db.SaveChangesAsync();

        // Invia SMS (sostituisci con il tuo URL reale)
        var baseUrl = _configuration["SmsConsentSettings:BaseUrl"];
        var consentUrl = $"{baseUrl}/{consentToken}";
        var message = $"Prima di usare la Tesla {vehicle?.Vin} devi accettare trattamento dati GPS.\n\nAccetta qui: {consentUrl}\n\nLink valido 24h. Poi riprova comando.";
        await _twilioConfig.SendSmsAsync(phoneNumber, message);
        
        await _logger.Info("TwilioSms.SendConsent", "Consent request sent", 
            $"Phone: {phoneNumber}, VehicleId: {vehicleId}");
    }

    private async Task SaveAuditLogAsync(SmsAuditLog auditLog)
    {
        _db.SmsAuditLogs.Add(auditLog);
        await _db.SaveChangesAsync();
    }

    private async Task UpdateAuditLogAsync(SmsAuditLog auditLog)
    {
        _db.SmsAuditLogs.Update(auditLog);
        await _db.SaveChangesAsync();
    }
}