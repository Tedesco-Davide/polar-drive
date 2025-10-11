namespace PolarDrive.WebApi.PolarAiReports.Templates;

public static class DefaultHtmlTemplate
{
    public static string Value =>
    @"<!DOCTYPE html>
        <html lang=""it"">
            <head>
                <meta charset=""utf-8"" />
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
                <title>PolarDrive™ Report {{reportId}}</title>
                {{styles}}
            </head>
            <body>
                <div class=""report-container"">
                    <!-- ✅ Header con TABLE LAYOUT per perfetto allineamento PDF -->
                    <header class=""header"">
                        <table class=""header-table"">
                            <tr>
                                <td class=""title-cell"">
                                    <h1 class=""report-title"">PolarDrive™</h1>
                                    <div class=""report-id"">PolarReport™ ID: {{reportId}}</div>
                                </td>
                                <td class=""logo-cell"">
                                    {{#if logoBase64}}
                                    <!-- ✅ SUPPORTO SVG E PNG -->
                                    <img src=""data:image/svg+xml;base64,{{logoBase64}}"" alt=""DataPolar Logo"" class=""logo"" 
                                        onerror=""this.src='data:image/png;base64,{{logoBase64}}'"" />
                                    {{/if}}
                                </td>
                            </tr>
                        </table>
                    </header>

                    <!-- ✅ Informazioni principali -->
                    <section class=""section report-info"">
                        <div class=""info-grid"">
                            <div class=""info-item"">
                                <strong>Azienda</strong> {{companyName}} ({{vatNumber}})
                            </div>
                            <div class=""info-item"">
                                <strong>Veicolo</strong> {{vehicleModel}} - {{vehicleVin}}
                            </div>
                            <div class=""info-item"">
                                <strong>Data generazione PDF</strong> {{generatedAtDays}} alle {{generatedAtHours}}
                            </div>
                            <div class=""info-item"">
                                <strong>Periodo monitorato</strong> Dal {{periodStart}} al {{periodEnd}}
                            </div>
                            <div class=""info-item"">
                                <strong>Codice HASH univoco del file</strong> {{pdfHash}}
                            </div>
                            {{#if notes}}
                                <div class=""info-item notes"">
                                    <strong>Note</strong> {{notes}}
                                </div>
                            {{/if}}
                        </div>
                    </section>

                    <!-- ✅ Analisi AI -->
                    <section class=""section ai-insights page-break"">
                        <div class=""insights-content"">
                            {{insights}}
                        </div>
                    </section>

                    <!-- ✅ Certificazione DataPolar -->
                    <section class=""section certification-section page-break"">
                        {{dataPolarCertification}}
                    </section>

                    <!-- ✅ Footer -->
                    <footer class=""report-footer footer"">
                        <div class=""footer-content"">
                            <p>PolarReport™ generato da PolarDrive™ • {{generatedAt}}</p>
                            <p class=""company-info"">DataPolar - The future of AI</p>
                        </div>
                    </footer>
                </div>
            </body>
        </html>
    ";
}