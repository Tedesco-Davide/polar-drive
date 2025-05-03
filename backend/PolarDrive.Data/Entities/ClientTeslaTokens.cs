using System.ComponentModel.DataAnnotations;
namespace PolarDrive.Data.Entities;

public class ClientTeslaToken
{
    public int Id { get; set; }

    [Required]
    public int TeslaVehicleId { get; set; }

    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime AccessTokenExpiresAt { get; set; }

    public DateTime? RefreshTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ClientTeslaVehicle? ClientTeslaVehicle { get; set; }
}