namespace PolarDrive.WebApi.PolarAiReports.Templates;

public static class DefaultHtmlTemplate
{
    public static string Value => @"<!DOCTYPE html>
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
                        <div class=""report-id"">Report ID: {{reportId}}</div>
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
            
            <!-- ✅ HEADER FALLBACK per compatibilità (nascosto dal CSS) -->
            <div class=""report-header"">
                <div class=""report-title-container"">
                    <h1 class=""report-title"">PolarDrive™ Report</h1>
                    <div class=""report-id"">Report ID: {{reportId}}</div>
                </div>
                <div class=""company-logo"">
                    {{#if logoBase64}}
                    <img src=""data:image/svg+xml;base64,{{logoBase64}}"" alt=""DataPolar Logo"" class=""logo company-logo-img"" 
                         onerror=""this.src='data:image/png;base64,{{logoBase64}}'"" />
                    {{/if}}
                </div>
            </div>
        </header>

        <!-- Informazioni principali -->
        <section class=""section report-info"">
            <div class=""info-grid"">
                <div class=""info-item"">
                    <strong>Azienda:</strong> {{companyName}} ({{vatNumber}})
                </div>
                <div class=""info-item"">
                    <strong>Veicolo:</strong> {{vehicleModel}} - {{vehicleVin}}
                </div>
                <div class=""info-item"">
                    <strong>Periodo:</strong> {{periodStart}} → {{periodEnd}}
                </div>
                <div class=""info-item"">
                    <strong>Generato:</strong> {{generatedAt}}
                </div>
                {{#if notes}}
                <div class=""info-item notes"">
                    <strong>Note:</strong> {{notes}}
                </div>
                {{/if}}
            </div>
        </section>

        <!-- ✅ CERTIFICAZIONE DATAPOLAR (SEMPRE PRESENTE) -->
        <section class=""section certification-section"">
            {{dataPolarCertification}}
        </section>

        <!-- Analisi AI -->
        <section class=""section ai-insights"">
            <div class=""insights-content"">
                {{insights}}
            </div>
        </section>

        <!-- Statistiche dettagliate (condizionali) -->
        {{#if showDetailedStats}}
        <section class=""section detailed-stats"">
            <h2 class=""section-title"">Altre Statistiche</h2>
            <div class=""stats-content"">
                {{detailedStats}}
            </div>
        </section>
        {{/if}}

        <!-- Dati grezzi (condizionali) -->
        {{#if showRawData}}
        <section class=""section raw-data"">
            <h2 class=""section-title"">Riepilogo Dati Tecnici</h2>
            <div class=""raw-data-content"">
                {{rawDataSummary}}
            </div>
        </section>
        {{/if}}

        <!-- Footer -->
        <footer class=""report-footer footer"">
            <div class=""footer-content"">
                <p>Report generato da PolarDrive™ al {{generatedAt}}</p>
                <p class=""company-info"">DataPolar - The future of AI</p>
            </div>
        </footer>
    </div>
</body>
</html>";
}