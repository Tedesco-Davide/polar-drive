using System.ComponentModel.DataAnnotations;
namespace PolarDrive.Data.Entities;

public class ClientToken
{
    public int Id { get; set; }

    [Required]
    public int VehicleId { get; set; }

    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime AccessTokenExpiresAt { get; set; }

    public DateTime? RefreshTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ClientVehicle? ClientVehicle { get; set; }
}