namespace PolarDrive.WebApi.PolarAiReports.Templates;

public static class DefaultHtmlTemplate
{
    // ✅ Metodo helper per caricare font (chiamalo una volta all'avvio)
    private static string GetFontStyles()
    {
        // ✅ Path assoluto nel container Docker
        var basePath = "/app/wwwroot/fonts/satoshi";
        
        var satoshiRegular = File.ReadAllText(Path.Combine(basePath, "Satoshi-Regular.b64"));
        var satoshiBold = File.ReadAllText(Path.Combine(basePath, "Satoshi-Bold.b64"));
        var satoshiBlack = File.ReadAllText(Path.Combine(basePath, "Satoshi-Black.b64"));

        return $@"
            @font-face {{
                font-family: 'Satoshi';
                src: url(data:font/woff2;base64,{satoshiRegular}) format('woff2');
                font-weight: 400;
                font-style: normal;
                font-display: swap;
            }}
            @font-face {{
                font-family: 'Satoshi';
                src: url(data:font/woff2;base64,{satoshiBold}) format('woff2');
                font-weight: 700;
                font-style: normal;
                font-display: swap;
            }}
            @font-face {{
                font-family: 'Satoshi';
                src: url(data:font/woff2;base64,{satoshiBlack}) format('woff2');
                font-weight: 800;
                font-style: normal;
                font-display: swap;
            }}
            body {{
                font-family: 'Satoshi', 'Noto Color Emoji', 'Apple Color Emoji', sans-serif;
                letter-spacing: normal;
                word-spacing: normal;
            }}
        ";
    }

    public static string Value =>
    $@"<!DOCTYPE html>
        <html lang=""it"">
            <head>
                <meta charset=""utf-8"" />
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
                <title>PolarReport™  {{{{reportId}}}}</title>
                <style>
                    {GetFontStyles()}
                    {{{{styles}}}}
                </style>
            </head>
            <body>
                <div class=""report-container"">
                    <header class=""header"">
                        <table class=""header-table"">
                            <tr>
                                <td class=""title-cell"">
                                    <h1 class=""report-title"">PolarDrive™</h1>
                                    <div class=""report-id"">PolarReport™ ID: {{{{reportId}}}}</div>
                                </td>
                                <td class=""logo-cell"">
                                    {{{{#if logoBase64}}}}
                                    <img src=""data:image/svg+xml;base64,{{{{logoBase64}}}}"" alt=""DataPolar Logo"" class=""logo"" 
                                        onerror=""this.src='data:image/png;base64,{{{{logoBase64}}}}'"" />
                                    {{{{/if}}}}
                                </td>
                            </tr>
                        </table>
                    </header>

                    <section class=""section report-info"">
                        <div class=""info-grid"">
                            <div class=""info-item"">
                                <strong>Azienda</strong> {{{{companyName}}}} ({{{{vatNumber}}}})
                            </div>
                            <div class=""info-item"">
                                <strong>Veicolo</strong> {{{{vehicleModel}}}} - {{{{vehicleVin}}}}
                            </div>
                            <div class=""info-item"">
                                <strong>Data generazione PDF</strong> {{{{generatedAtDays}}}} alle {{{{generatedAtHours}}}}
                            </div>
                            <div class=""info-item"">
                                <strong>Periodo monitorato</strong> Dal {{{{periodStart}}}} al {{{{periodEnd}}}}
                            </div>
                            <div class=""info-item"">
                                <strong>Codice HASH univoco del file</strong> {{{{pdfHash}}}}
                            </div>
                            {{{{#if notes}}}}
                                <div class=""info-item notes"">
                                    <strong>Note</strong> {{{{notes}}}}
                                </div>
                            {{{{/if}}}}
                        </div>
                    </section>

                    <section class=""section ai-insights page-break"">
                        <div class=""insights-content"">
                            {{{{insights}}}}
                        </div>
                    </section>

                    <section class=""section certification-section page-break"">
                        {{{{dataPolarCertification}}}}
                    </section>

                    <footer class=""report-footer footer"">
                        <div class=""footer-content"">
                            <p>PolarReport™ generato da PolarDrive™ • {{{{generatedAtDays}}}}</p>
                            <p class=""company-info"">DataPolar - The future of AI</p>
                        </div>
                    </footer>
                </div>
            </body>
        </html>
    ";
}