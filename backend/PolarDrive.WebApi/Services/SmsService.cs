using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using Vonage;
using Vonage.Messaging;
using Vonage.Request;

namespace PolarDrive.WebApi.Services;

public interface ISmsConfigurationService
{
    SmsConfigurationDTO GetConfiguration();
    bool IsPhoneNumberAllowed(string phoneNumber);
    Task<bool> IsRateLimitExceeded(string phoneNumber);
    Task<bool> SendSmsAsync(string phoneNumber, string message);
}

public class SmsService(IConfiguration configuration, PolarDriveLogger logger) : ISmsConfigurationService
{
    private readonly SmsConfigurationDTO _config = configuration.GetSection("Vonage").Get<SmsConfigurationDTO>()
        ?? throw new InvalidOperationException("Vonage configuration not found");

    private readonly PolarDriveLogger _logger = logger;

    private readonly Dictionary<string, List<DateTime>> _rateLimitTracker = [];

    private readonly Lock _rateLimitLock = new();

    public SmsConfigurationDTO GetConfiguration() => _config;

    public bool IsPhoneNumberAllowed(string phoneNumber)
    {
        // In Development, accetta tutti i numeri
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (env == "Development")
        {
            _logger.Warning("DEV MODE: Allowing all numbers ({PhoneNumber})", phoneNumber);
            return true;
        }

        // In produzione, usa la whitelist
        if (_config?.AllowedPhoneNumbers?.Any() != true)
        {
            _logger.Warning("PROD MODE: No whitelist configured - REJECTING {PhoneNumber}", phoneNumber);
            return false;
        }

        var isAllowed = _config.AllowedPhoneNumbers.Contains(phoneNumber);
        if (!isAllowed)
        {
            _logger.Warning("PROD MODE: Phone {PhoneNumber} not in whitelist", phoneNumber);
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
                _rateLimitTracker[phoneNumber] = [];

            // Rimuovi timestamp più vecchi di 1 minuto
            _rateLimitTracker[phoneNumber].RemoveAll(t => t < oneMinuteAgo);

            // Controlla rate limit
            if (_rateLimitTracker[phoneNumber].Count >= _config.RateLimitPerMinute)
            {
                _logger.Info(
                    "SmsService.IsRateLimitExceeded",
                    $"Rate limit superato per {phoneNumber}: {_rateLimitTracker[phoneNumber].Count}/{_config.RateLimitPerMinute} in 60s"
                );
                return Task.FromResult(true);
            }

            // Aggiungi timestamp corrente
            _rateLimitTracker[phoneNumber].Add(now);
            return Task.FromResult(false);
        }
    }

    public async Task<bool> SendSmsAsync(string phoneNumber, string message)
    {
        try
        {
            var credentials = Credentials.FromApiKeyAndSecret(_config.AccountSid, _config.AuthToken);
            var client = new VonageClient(credentials);

            var request = new SendSmsRequest
            {
                To = NormalizeForVonage(phoneNumber),
                From = _config.PhoneNumber,
                Text = message
            };

            var response = await client.SmsClient.SendAnSmsAsync(request);

            // "message-count" è una stringa nel modello Vonage
            int.TryParse(response.MessageCount, out var declaredCount);
            var messages = response.Messages;

            if (messages is null || messages.Length == 0)
            {
                using var _ = _logger.Error(
                    "SmsService.SendSmsAsync",
                    $"Vonage: response.Messages è vuoto o null per destinatario {phoneNumber}. Dichiarati: {declaredCount}"
                );
                return false;
            }

            // Successo se tutti i segmenti hanno Status == "0"
            var allOk = messages.All(m => m.Status == "0");
            if (allOk)
            {
                _ = _logger.Info(
                    "SmsService.SendSmsAsync",
                    $"SMS inviato a {phoneNumber} ({messages.Length} segmenti; dichiarati: {declaredCount})."
                );
                return true;
            }

            // Logga il primo errore utile
            var firstError = messages.FirstOrDefault(m => m.Status != "0");
            _ = _logger.Warning(
                "SmsService.SendSmsAsync",
                $"Vonage error {firstError?.Status} per {phoneNumber}: {firstError?.ErrorText}"
            );
            return false;
        }
        catch (Exception ex)
        {
            _ = _logger.Error(ex.ToString(), "Vonage SendSmsAsync exception per {PhoneNumber}", phoneNumber);
            return false;
        }
    }

    private static string NormalizeForVonage(string phoneNumber)
    {
        var digits = System.Text.RegularExpressions.Regex.Replace(phoneNumber ?? "", @"\D", "");
        if (string.IsNullOrEmpty(digits)) return phoneNumber!;

        if (digits.StartsWith("39") && digits.Length >= 11)
            return "+" + digits;

        if (digits.Length == 10 && digits.StartsWith("3"))
            return "+39" + digits;

        if (phoneNumber!.StartsWith("+"))
            return phoneNumber;

        return "+39" + digits;
    }
}
