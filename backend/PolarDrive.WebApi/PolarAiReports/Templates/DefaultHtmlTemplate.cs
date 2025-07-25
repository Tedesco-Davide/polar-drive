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
        <!-- Header con logo aziendale -->
        <header class=""report-header header"">
            {{#if logoBase64}}
            <div class=""company-logo"">
                <img src=""data:image/png;base64,{{logoBase64}}"" alt=""Logo Aziendale"" class=""logo"" />
            </div>
            {{/if}}
            <h1 class=""report-title"">PolarDrive™ Report</h1>
            <div class=""report-id"">Report ID: {{reportId}}</div>
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

        <!-- Analisi AI -->
        <section class=""section ai-insights"">
            <h2 class=""section-title"">Analisi Intelligente del Veicolo</h2>
            <div class=""insights-content"">
                {{insights}}
            </div>
        </section>

        <!-- Statistiche dettagliate (condizionali) -->
        {{#if showDetailedStats}}
        <section class=""section detailed-stats"">
            <h2 class=""section-title"">Statistiche Dettagliate</h2>
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
                <p>Report generato da PolarDrive™ v{{reportVersion}} - {{generatedAt}}</p>
                <p class=""company-info"">DataPolar - The future is now</p>
            </div>
        </footer>
    </div>
</body>
</html>";
}