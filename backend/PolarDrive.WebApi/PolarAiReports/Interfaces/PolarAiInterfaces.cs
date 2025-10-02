namespace PolarDrive.WebApi.PolarAiReports;

// Opzioni per la conversione PDF
public class PdfConversionOptions
{
    public string PageFormat { get; set; } = "A4";
    public bool PrintBackground { get; set; } = true;
    public string MarginTop { get; set; } = "2cm";
    public string MarginRight { get; set; } = "0.5cm";
    public string MarginBottom { get; set; } = "1cm";
    public string MarginLeft { get; set; } = "0.5cm";
    public bool DisplayHeaderFooter { get; set; } = true;
    public string HeaderTemplate { get; set; } = @"
        <div style='font-size: 10px; width: 100%; text-align: center; color: #666;'>
            <span>PolarDrive Report</span>
        </div>";
    public string FooterTemplate { get; set; } = @"
        <div style='
            display: block;
            width: 100%;
            margin: 0;
            padding: 0;
            font-size: 10px;
            color: #666;
            text-align: center;
        '>
            Pagina <span class='pageNumber'></span> di <span class='totalPages'></span>
        </div>";
}