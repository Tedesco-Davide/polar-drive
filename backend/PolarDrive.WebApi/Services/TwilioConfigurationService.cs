using PolarDrive.Data.DTOs;

namespace PolarDrive.WebApi.Services;

public interface ITwilioConfigurationService
{
    TwilioConfigurationDTO GetConfiguration();
    bool ValidateSignature(string expectedSignature, string url, Dictionary<string, string> parameters);
    bool IsPhoneNumberAllowed(string phoneNumber);
    Task<bool> IsRateLimitExceeded(string phoneNumber);
}

public class TwilioService : ITwilioConfigurationService
{
    private readonly TwilioConfigurationDTO _config;
    private readonly Dictionary<string, List<DateTime>> _rateLimitTracker = new();
    private readonly object _rateLimitLock = new();

    public TwilioService(IConfiguration configuration)
    {
        _config = configuration.GetSection("Twilio").Get<TwilioConfigurationDTO>()
                  ?? throw new InvalidOperationException("Twilio configuration not found");
    }

    public TwilioConfigurationDTO GetConfiguration() => _config;

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
        if (!_config.AllowedPhoneNumbers.Any())
            return true; // Se la lista è vuota, accetta tutti

        return _config.AllowedPhoneNumbers.Contains(phoneNumber);
    }

    public Task<bool> IsRateLimitExceeded(string phoneNumber)
    {
        lock (_rateLimitLock)
        {
            var now = DateTime.UtcNow;
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
}