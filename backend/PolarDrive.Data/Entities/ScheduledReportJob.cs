namespace PolarDrive.Data.Entities;

public class ScheduledFileJob
{
    public int Id { get; set; }

    // Timestamps
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Periodo (singolo mese o range)
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // Lista di oggetti filtrabili principali
    public List<string> FileTypeList { get; set; } = [];
    public List<string> CompanyList { get; set; } = [];
    public List<string> BrandList { get; set; } = [];
    public List<string> ConsentTypeList { get; set; } = [];
    public List<string> OutageTypeList { get; set; } = [];
    public List<string> OutageAutoDetectedOptionList { get; set; } = [];

    // Stato e risultati
    public string Status { get; set; } = "QUEUE";
    public int GeneratedFilesCount { get; set; }
    public string? InfoMessage { get; set; }

    // Azioni frontend collegate
    public string? ResultZipPath { get; set; }
}