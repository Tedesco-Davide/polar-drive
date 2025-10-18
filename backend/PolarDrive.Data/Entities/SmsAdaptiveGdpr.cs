using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace PolarDrive.Data.Entities
{
    public class SmsAdaptiveGdpr
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Brand { get; set; } = string.Empty;

        [Required]
        public int ClientCompanyId { get; set; }

        [Required]
        [StringLength(64)]
        public string ConsentToken { get; set; } = string.Empty;

        [Required]
        public DateTime RequestedAt { get; set; }

        public DateTime? ConsentGivenAt { get; set; }

        [Required]
        public bool ConsentAccepted { get; set; } = false;

        [StringLength(45)]
        public string? IpAddress { get; set; }

        [StringLength(500)]
        public string? UserAgent { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public int AttemptCount { get; set; } = 0;

        // Navigation property
        public ClientCompany? ClientCompany { get; set; }

        // Metodo per generare token sicuro
        public static string GenerateSecureToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                          .Replace("+", "-")
                          .Replace("/", "_")
                          .Replace("=", "");
        }
    }
}