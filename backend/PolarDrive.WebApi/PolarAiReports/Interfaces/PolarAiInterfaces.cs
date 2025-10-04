namespace PolarDrive.WebApi.PolarAiReports;

// Opzioni per la conversione PDF
public class PdfConversionOptions
{
    public bool PrintBackground { get; set; } = true;
    public bool DisplayHeaderFooter { get; set; } = true;
    public string PageFormat { get; set; } = "A4";
    public string MarginTop { get; set; } = "2cm";
    public string MarginRight { get; set; } = "0.5cm";
    public string MarginBottom { get; set; } = "2cm";
    public string MarginLeft { get; set; } = "0.5cm";
    public string HeaderTemplate { get; set; } = @"";
    public string FooterTemplate { get; set; } = @"";
}