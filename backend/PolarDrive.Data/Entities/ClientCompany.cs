namespace PolarDrive.Data.Entities;

public class ClientCompany
{
    public int Id { get; set; }

    public string VatNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? PecAddress { get; set; }

    public string? LandlineNumber { get; set; }

    public string? ReferentName { get; set; }

    public string? ReferentMobileNumber { get; set; }

    public string? ReferentEmail { get; set; }

    public string? ReferentPecAddress { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}