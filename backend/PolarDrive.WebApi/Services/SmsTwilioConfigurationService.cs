using PolarDrive.Data.DTOs;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace PolarDrive.WebApi.Services;

public interface ISmsTwilioConfigurationService
{
    SmsTwilioConfigurationDTO GetConfiguration();
    bool ValidateSignature(string expectedSignature, string url, Dictionary<string, string> parameters);
    bool IsPhoneNumberAllowed(string phoneNumber);
    Task<bool> IsRateLimitExceeded(string phoneNumber);
    Task<bool> SendSmsAsync(string phoneNumber, string message);
}

public class SmsTwilioService : ISmsTwilioConfigurationService
{
    private readonly SmsTwilioConfigurationDTO _config;
    private readonly Dictionary<string, List<DateTime>> _rateLimitTracker = new();
    private readonly object _rateLimitLock = new();

    public SmsTwilioService(IConfiguration configuration)
    {
        _config = configuration.GetSection("Twilio").Get<SmsTwilioConfigurationDTO>()
                  ?? throw new InvalidOperationException("Twilio configuration not found");
    }

    public SmsTwilioConfigurationDTO GetConfiguration() => _config;

    public bool ValidateSignature(string expectedSignature, string url, Dictionary<string, string> parameters)
    {
        if (!_config.EnableSignatureValidation)
            return true;

        try
        {
            var validator = new Twilio.Security.RequestValidator(_config.AuthToken);
            return validator.Validate(url, parameters, expectedSignature);
        }
        catch
        {
            return false;
        }
    }

    public bool IsPhoneNumberAllowed(string phoneNumber)
    {
        // In Development, accetta tutti i numeri
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (env == "Development")
        {
            Console.WriteLine($"🔴 DEV MODE: Allowing all numbers ({phoneNumber})");
            return true;
        }

        // In produzione, usa la whitelist
        if (_config?.AllowedPhoneNumbers?.Any() != true)
        {
            // Se non c'è whitelist configurata, per sicurezza RIFIUTA
            Console.WriteLine($"⚠️ PROD MODE: No whitelist configured - REJECTING {phoneNumber}");
            return false;
        }

        var isAllowed = _config.AllowedPhoneNumbers.Contains(phoneNumber);
        if (!isAllowed)
        {
            Console.WriteLine($"⚠️ PROD MODE: Phone {phoneNumber} not in whitelist");
        }

        return isAllowed;
    }

    public Task<bool> IsRateLimitExceeded(string phoneNumber)
    {
        lock (_rateLimitLock)
        {
            var now = DateTime.Now;
            var oneMinuteAgo = now.AddMinutes(-1);

            if (!_rateLimitTracker.ContainsKey(phoneNumber))
                _rateLimitTracker[phoneNumber] = new List<DateTime>();

            // Rimuovi timestamp più vecchi di 1 minuto
            _rateLimitTracker[phoneNumber].RemoveAll(t => t < oneMinuteAgo);

            // Controlla rate limit
            if (_rateLimitTracker[phoneNumber].Count >= _config.RateLimitPerMinute)
                return Task.FromResult(true);

            // Aggiungi timestamp corrente
            _rateLimitTracker[phoneNumber].Add(now);
            return Task.FromResult(false);
        }
    }

    public async Task<bool> SendSmsAsync(string phoneNumber, string message)
    {
        try
        {
            TwilioClient.Init(_config.AccountSid, _config.AuthToken);
            
            var messageResource = await MessageResource.CreateAsync(
                body: message,
                from: new PhoneNumber(_config.PhoneNumber),
                to: new PhoneNumber(phoneNumber)
            );

            return messageResource.Status != MessageResource.StatusEnum.Failed;
        }
        catch (Exception)
        {
            return false;
        }
    }
}