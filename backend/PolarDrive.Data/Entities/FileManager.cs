namespace PolarDrive.Data.Entities;

public class AdminFileManager
{
    public int Id { get; set; }

    // Timestamps della richiesta
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Range temporale per i PDF da includere
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // Filtri specifici per PDF reports
    public List<string> CompanyList { get; set; } = [];
    public List<string> VinList { get; set; } = [];           // Nuovo: VIN specifici
    public List<string> BrandList { get; set; } = [];         // Manteniamo per compatibilità

    // Stato della richiesta
    public string Status { get; set; } = "PENDING";

    // Risultati della generazione ZIP
    public int TotalPdfCount { get; set; }                    // PDF trovati nel periodo
    public int IncludedPdfCount { get; set; }                 // PDF inclusi nello ZIP
    public decimal ZipFileSizeMB { get; set; }                // Dimensione ZIP in MB

    // Percorso del file ZIP risultante
    public string? ResultZipPath { get; set; }

    // Informazioni aggiuntive
    public string? Notes { get; set; }

    // Metadati della richiesta
    public string? RequestedBy { get; set; }

    // Metodi helper
    public bool IsCompleted => Status == "COMPLETED";
    public bool HasZipFile => !string.IsNullOrEmpty(ResultZipPath);
    public TimeSpan? ProcessingDuration =>
        StartedAt.HasValue && CompletedAt.HasValue
            ? CompletedAt.Value - StartedAt.Value
            : null;
}