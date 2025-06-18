namespace PolarDrive.WebApi.PolarAiReports.Templates;

public static class DefaultCssTemplate
{
    public static string Value => @"
        html, body, table {
            font-size: 12px !important;
        }

        body {
            font-family: Arial, sans-serif;
            margin: 20px;
            line-height: 1.4;
            font-weight: normal; /* ✅ Assicura font normale di base */
        }

        .header {
            border-bottom: 3px solid #004E92;
            padding-bottom: 20px;
            margin-bottom: 30px;
        }

        .logo {
            width: 120px;
            height: auto;
            margin-bottom: 12px;
            float: right;
        }

        h1 {
            color: #004E92;
            margin-bottom: 15px;
            font-size: 24px;
            font-weight: 700; /* ✅ Solo titoli principali in grassetto */
        }

        h2 {
            color: #004E92;
            margin-top: 25px;
            margin-bottom: 15px;
            font-size: 18px;
            font-weight: 600; /* ✅ Ridotto da bold (700) a 600 */
            border-bottom: 1px solid #ddd;
            padding-bottom: 5px;
        }

        h3 {
            color: #004E92;
            font-size: 16px;
            font-weight: 500; /* ✅ Ancora più leggero per h3 */
            margin-top: 20px;
            margin-bottom: 10px;
        }

        h4 {
            color: #333;
            font-size: 14px;
            font-weight: 500; /* ✅ Peso normale per h4 */
            margin-top: 15px;
            margin-bottom: 8px;
        }

        .report-info {
            background-color: #f8f9fa;
            padding: 15px;
            border-radius: 5px;
            margin-top: 10px;
            font-weight: normal; /* ✅ Testo normale per info report */
        }

        .report-info div {
            margin-bottom: 5px;
        }

        /* ✅ GRASSETTO SOLO DOVE NECESSARIO */
        .report-info strong {
            font-weight: 600; /* ✅ Ridotto da bold (700) a 600 */
        }

        .section {
            margin-bottom: 30px;
            page-break-inside: avoid;
        }

        /* ✅ GRASSETTO SELETTIVO NEGLI INSIGHTS */
        .insights-content h1,
        .insights-content h2,
        .insights-content h3 {
            font-weight: 600; /* ✅ Intestazioni insights più leggere */
        }

        .insights-content strong {
            font-weight: 600; /* ✅ Strong più leggero */
            color: #004E92; /* ✅ Usa colore invece di peso eccessivo */
        }

        .insights-content b {
            font-weight: 500; /* ✅ Tag <b> ancora più leggero */
            color: #004E92;
        }

        .stats-content {
            background-color: #f8f9fa;
            padding: 15px;
            border-radius: 5px;
            font-weight: normal; /* ✅ Statistiche in peso normale */
        }

        .raw-data-content {
            font-family: 'Courier New', monospace;
            background-color: #f4f4f4;
            padding: 15px;
            border-radius: 5px;
            font-size: 11px;
            overflow-x: auto;
            font-weight: normal; /* ✅ Dati raw normali */
        }

        /* ✅ TABELLE CON GRASSETTO RIDOTTO */
        table {
            width: 100%;
            max-width: 100%;
            table-layout: fixed;
            border-collapse: collapse;
            margin-bottom: 20px;
            font-weight: normal; /* ✅ Tabelle normali */
        }

        th, td {
            border: 1px solid #ddd;
            padding: 8px;
            text-align: left;
            font-weight: normal; /* ✅ Celle normali */
        }

        td {
            overflow: visible;
            white-space: normal;
            word-break: break-word;
        }

        th {
            background-color: #f2f2f2;
            font-weight: 600; /* ✅ Header tabelle un po' più pesanti ma non bold */
            color: #333;
        }

        /* ✅ PARAGRAFI E TESTO CORPO */
        p {
            font-weight: normal;
            margin-bottom: 10px;
            line-height: 1.5;
        }

        /* ✅ LISTE CON PESO NORMALE */
        ul, ol {
            font-weight: normal;
            margin-bottom: 15px;
            padding-left: 20px;
        }

        li {
            font-weight: normal;
            margin-bottom: 5px;
        }

        /* ✅ ENFASI CONTROLLATA */
        .important {
            font-weight: 600; /* ✅ Solo per elementi veramente importanti */
            color: #004E92;
        }

        .emphasis {
            font-weight: 500; /* ✅ Enfasi leggera */
            color: #333;
        }

        /* ✅ OVERRIDE PER MARKDOWN CONVERTITO */
        .insights-content h1 { font-weight: 600; font-size: 20px; }
        .insights-content h2 { font-weight: 500; font-size: 18px; }
        .insights-content h3 { font-weight: 500; font-size: 16px; }
        .insights-content h4 { font-weight: 400; font-size: 14px; }

        .footer {
            margin-top: 40px;
            padding-top: 20px;
            border-top: 1px solid #ddd;
            text-align: center;
            color: #666;
            font-weight: normal; /* ✅ Footer normale */
        }

        /* ✅ STILI SPECIFICI PER LA STAMPA */
        @media print {
            html, body {
                font-size: 12px !important;
            }

            /* ✅ Riduci ulteriormente i grassetti in stampa */
            h1 { font-weight: 600; }
            h2 { font-weight: 500; }
            h3 { font-weight: 500; }
            strong { font-weight: 500; }
            th { font-weight: 500; }

            .section {
                page-break-inside: avoid;
            }

            .header {
                page-break-after: avoid;
            }

            table {
                page-break-inside: auto;
            }

            tr {
                page-break-inside: avoid;
            }

            .logo {
                max-width: 100px;
            }    
        }

        /* Stili specifici per i report con analisi AI */
        .ai-report-badge {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 8px 16px;
            border-radius: 25px;
            font-size: 12px;
            font-weight: 500;
            display: inline-block;
            margin: 10px 15px 10px 0;
            box-shadow: 0 4px 8px rgba(0,0,0,0.2);
        }
        
        .ai-insights {
            border-left: 5px solid #667eea;
            background: linear-gradient(135deg, rgba(102, 126, 234, 0.1) 0%, rgba(118, 75, 162, 0.1) 100%);
            padding: 20px;
            border-radius: 0 12px 12px 0;
        }
        
        .ai-insights::before {
            content: '🧠 Analisi PolarAi';
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 4px 8px;
            border-radius: 12px;
            font-size: 10px;
            font-weight: 500;
            margin-left: 10px;
        }
        
        .ai-insights * {
            font-weight: normal !important;
        }
        
        .ai-insights h1, .ai-insights h2, .ai-insights h3, .ai-insights h4 {
            font-weight: 500 !important;
        }
        
        .ai-insights strong, .ai-insights b {
            font-weight: 500 !important;
            color: #667eea;
        }
        
        .data-evolution {
            border: 2px dashed #667eea;
            padding: 15px;
            margin: 20px 0;
            background: rgba(102, 126, 234, 0.05);
            border-radius: 8px;
        }
        
        .data-evolution::before {
            content: '📈 Evoluzione Dati • ';
            color: #667eea;
            font-weight: 500;
        }";
}