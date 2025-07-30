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
            position: relative; /* ✅ Per posizionamento logo */
        }

        .report-header {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            width: 100%;
        }

        .report-title-container {
            flex: 1;
        }

        .report-title {
            margin: 0;
            padding: 0;
        }

        /* ✅ STILI LOGO COMBINATO DATAPOLAR - ALLINEATO A DESTRA */
        .logo {
            width: 200px; /* ✅ Aumentato per logo combinato */
            height: auto;
            margin: 0; /* ✅ Rimosso margin-bottom */
            max-height: 60px; /* ✅ Limita altezza per mantenere proporzioni */
            flex-shrink: 0; /* ✅ Impedisce al logo di rimpicciolirsi */
        }

        .company-logo {
            display: flex;
            justify-content: flex-end;
            align-items: flex-start;
            margin: 0; /* ✅ Rimosso margin-bottom */
        }

        .logo-fallback {
            width: 200px; /* ✅ Stessa larghezza del logo combinato */
            height: 50px;
            background: linear-gradient(135deg, #004E92 0%, #0066CC 100%);
            color: white;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: 600;
            font-size: 16px;
            border-radius: 8px;
            margin: 0; /* ✅ Rimosso margin-bottom */
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            flex-shrink: 0;
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
        }

        /* ✅ STILI CERTIFICAZIONE DATAPOLAR */
        .certification-section {
            margin: 20px 0;
            padding: 0;
        }

        .certification-datapolar {
            background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%);
            border: 2px solid #004E92;
            border-radius: 8px;
            padding: 20px;
            margin: 15px 0;
            position: relative;
        }

        .certification-datapolar::before {
            content: '🏆 DataPolar Certification';
            position: absolute;
            top: -12px;
            left: 20px;
            background: #004E92;
            color: white;
            padding: 4px 12px;
            border-radius: 4px;
            font-size: 11px;
            font-weight: 600;
        }

        .certification-datapolar h3 {
            color: #004E92;
            margin-bottom: 15px;
            margin-top: 10px;
            font-size: 1.2em;
            font-weight: 600;
            border-bottom: 2px solid #004E92;
            padding-bottom: 8px;
        }

        .certification-datapolar h4 {
            color: #495057;
            margin: 20px 0 10px 0;
            font-size: 1.05em;
            font-weight: 500;
        }

        .certification-table, .statistics-table {
            width: 100%;
            border-collapse: collapse;
            margin: 10px 0;
            font-size: 0.95em;
            background: white;
            border-radius: 4px;
            overflow: hidden;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }

        .certification-table td, .statistics-table td {
            padding: 10px 12px;
            border-bottom: 1px solid #dee2e6;
            vertical-align: top;
        }

        .certification-table td:first-child, .statistics-table td:first-child {
            background: #f8f9fa;
            font-weight: 500;
            width: 45%;
            color: #495057;
            border-right: 1px solid #dee2e6;
        }

        .certification-table td:last-child, .statistics-table td:last-child {
            font-weight: normal;
            color: #212529;
        }

        .cert-warning {
            color: #dc3545;
            font-weight: 500;
            padding: 10px;
            background: #f8d7da;
            border: 1px solid #f5c6cb;
            border-radius: 4px;
            margin: 10px 0;
        }

        .certification-error {
            color: #721c24;
            background: #f8d7da;
            border: 1px solid #f5c6cb;
            padding: 15px;
            border-radius: 4px;
            font-weight: 500;
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

            /* ✅ LOGO COMBINATO PER STAMPA PDF */
            .company-logo-img {
                width: 120px;
                max-height: 40px;
                margin: 0; /* ✅ Rimosso margin-bottom */
                flex-shrink: 0;
            }

            .logo {
                width: 200px;
                height: auto;
                max-height: 60px;
                margin: 0; /* ✅ Rimosso margin */
            }
            
            /* ✅ CERTIFICAZIONE PER STAMPA */
            .certification-datapolar {
                page-break-inside: avoid;
                background: #f8f9fa !important;
                border: 2px solid #004E92 !important;
            }
            
            .certification-datapolar::before {
                background: #004E92 !important;
                color: white !important;
            }
            
            .certification-table, .statistics-table {
                page-break-inside: avoid;
            }
        }

        /* ✅ RESPONSIVE PER SCHERMI PICCOLI */
        @media (max-width: 768px) {
            /* ✅ HEADER RESPONSIVE - STACK VERTICALE */
            .report-header {
                flex-direction: column;
                align-items: center;
            }

            .report-title-container {
                order: 2;
                text-align: center;
                width: 100%;
            }

            .company-logo {
                order: 1;
                justify-content: center;
                margin-bottom: 15px;
                width: 100%;
            }

            /* ✅ LOGO RESPONSIVE */
            .logo {
                width: 150px;
                max-height: 45px;
            }
            
            .logo-fallback {
                width: 150px;
                height: 40px;
            }

            /* ✅ CERTIFICAZIONE RESPONSIVE */
            .certification-table, .statistics-table {
                font-size: 0.85em;
            }
            
            .certification-table td:first-child, .statistics-table td:first-child {
                width: 50%;
            }
            
            .certification-datapolar {
                padding: 15px;
            }
        }";
}