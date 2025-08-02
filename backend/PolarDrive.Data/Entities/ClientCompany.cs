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

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual ICollection<ClientVehicle> ClientVehicles { get; set; } = [];

    public virtual ICollection<PdfReport> PdfReports { get; set; } = [];
}