using System.ComponentModel.DataAnnotations;

namespace PolarDrive.Data.DTOs;

/// <summary>
/// DTO per webhook SMS (Vonage usa: msisdn, to, text, messageId)
/// </summary>
public class SmsWebhookDTO
{
    public string? messageId { get; set; }
    public string? MessageSid { get; set; }
    
    public string? msisdn { get; set; }
    public string? From { get; set; }

    public string? to { get; set; }
    public string? To { get; set; }

    public string? text { get; set; }
    public string? Body { get; set; }
}

/// <summary>
/// DTO per configurazione SMS
/// </summary>
public class SmsConfigurationDTO
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