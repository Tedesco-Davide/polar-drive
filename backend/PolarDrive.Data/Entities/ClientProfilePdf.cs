namespace PolarDrive.Data.Entities;

public class ClientProfilePdf
{
    public int Id { get; set; }
    public int ClientCompanyId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public byte[] PdfContent { get; set; } = [];
    public DateTime GeneratedAt { get; set; }
    public long? FileSizeBytes { get; set; }
    public ClientCompany ClientCompany { get; set; } = null!;
}