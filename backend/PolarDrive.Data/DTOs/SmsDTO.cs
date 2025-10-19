using System.ComponentModel.DataAnnotations;

namespace PolarDrive.Data.DTOs;

/// <summary>
/// DTO per webhook SMS
/// </summary>
public class SmsWebhookDTO
{
    [Required]
    public string MessageSid { get; set; } = string.Empty;

    [Required]
    public string From { get; set; } = string.Empty;

    [Required]
    public string To { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;
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