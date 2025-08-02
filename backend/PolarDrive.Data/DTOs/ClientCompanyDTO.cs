namespace PolarDrive.Data.DTOs;

public class ClientCompanyDTO
{
    public int Id { get; set; }
    public string VatNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PecAddress { get; set; }
    public string? LandlineNumber { get; set; }
}
