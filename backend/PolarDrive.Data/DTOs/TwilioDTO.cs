
using System.ComponentModel.DataAnnotations;

namespace PolarDrive.Data.DTOs;

/// <summary>
/// DTO per webhook Twilio (tutti i campi che Twilio invia)
/// </summary>
public class TwilioSmsWebhookDTO
{
    public string MessageSid { get; set; } = string.Empty;
    public string AccountSid { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string MessageStatus { get; set; } = string.Empty;
    public string NumMedia { get; set; } = string.Empty;
    public string SmsStatus { get; set; } = string.Empty;
    public string SmsMessageSid { get; set; } = string.Empty;
    public string NumSegments { get; set; } = string.Empty;
    public string ReferralNumMedia { get; set; } = string.Empty;
}

/// <summary>
/// DTO per registrare numero telefono
/// </summary>
public class RegisterPhoneDTO
{
    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int VehicleId { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// DTO per configurazione Twilio
/// </summary>
public class TwilioConfigurationDTO
{
    [Required]
    public string AccountSid { get; set; } = string.Empty;

    [Required]
    public string AuthToken { get; set; } = string.Empty;

    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [Url]
    public string WebhookUrl { get; set; } = string.Empty;

    public bool EnableSignatureValidation { get; set; } = true;

    [Range(1, 100)]
    public int RateLimitPerMinute { get; set; } = 10;

    public List<string> AllowedPhoneNumbers { get; set; } = new();
}