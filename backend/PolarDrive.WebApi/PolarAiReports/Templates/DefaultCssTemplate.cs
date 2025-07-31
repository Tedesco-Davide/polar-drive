namespace PolarDrive.WebApi.PolarAiReports.Templates;

public static class DefaultCssTemplate
{
    public static string Value => @"
        html, body, table {
            font-size: 12px !important;
        }

        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 20px;
            line-height: 1.5;
            font-weight: 400;
            background: linear-gradient(135deg, #f8f9ff 0%, #f0f2ff 100%);
            color: #2d3748;
        }

        .report-container {
            max-width: 100%;
            background: white;
            border-radius: 12px;
            box-shadow: 0 8px 32px rgba(102, 126, 234, 0.15);
            overflow: hidden;
        }

        /* ✅ HEADER MODERNO CON GRADIENTE */
        .header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px;
            margin: 0;
            border: none;
            border-radius: 0;
            position: relative;
            overflow: hidden;
        }

        .header::before {
            content: '';
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: url('data:image/svg+xml,<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100""><defs><pattern id=""grid"" width=""10"" height=""10"" patternUnits=""userSpaceOnUse""><path d=""M 10 0 L 0 0 0 10"" fill=""none"" stroke=""rgba(255,255,255,0.1)"" stroke-width=""0.5""/></pattern></defs><rect width=""100"" height=""100"" fill=""url(%23grid)"" /></svg>');
            pointer-events: none;
        }

        .report-header {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            width: 100%;
            position: relative;
            z-index: 1;
        }

        .report-title-container {
            flex: 1;
        }

        .report-title {
            margin: 0 0 8px 0;
            padding: 0;
            font-size: 28px;
            font-weight: 700;
            color: white;
            text-shadow: 0 2px 4px rgba(0,0,0,0.2);
        }

        .report-id {
            font-size: 14px;
            color: rgba(255, 255, 255, 0.9);
            background: rgba(255, 255, 255, 0.15);
            padding: 6px 12px;
            border-radius: 20px;
            display: inline-block;
            backdrop-filter: blur(10px);
            border: 1px solid rgba(255, 255, 255, 0.2);
        }

        /* ✅ LOGO MODERNO */
        .logo {
            width: 200px;
            height: auto;
            margin: 0;
            max-height: 60px;
            flex-shrink: 0;
            filter: drop-shadow(0 4px 8px rgba(0,0,0,0.2));
        }

        .company-logo {
            display: flex;
            justify-content: flex-end;
            align-items: flex-start;
            margin: 0;
        }

        .logo-fallback {
            width: 200px;
            height: 50px;
            background: rgba(255, 255, 255, 0.2);
            color: white;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: 600;
            font-size: 16px;
            border-radius: 8px;
            margin: 0;
            backdrop-filter: blur(10px);
            border: 1px solid rgba(255, 255, 255, 0.3);
            flex-shrink: 0;
        }

        /* ✅ TITOLI MODERNI */
        h1 {
            color: #2d3748;
            margin-bottom: 15px;
            font-size: 24px;
            font-weight: 700;
        }

        h2 {
            color: #4a5568;
            margin-top: 25px;
            margin-bottom: 15px;
            font-size: 18px;
            font-weight: 600;
            border-bottom: 2px solid transparent;
            background: linear-gradient(90deg, #667eea, #764ba2) left bottom no-repeat;
            background-size: 60px 2px;
            padding-bottom: 8px;
        }

        h3 {
            color: #667eea;
            font-size: 16px;
            font-weight: 600;
            margin-top: 20px;
            margin-bottom: 10px;
        }

        h4 {
            color: #4a5568;
            font-size: 14px;
            font-weight: 500;
            margin-top: 15px;
            margin-bottom: 8px;
        }

        /* ✅ INFO GRID MODERNIZZATA */
        .report-info {
            background: linear-gradient(135deg, rgba(102, 126, 234, 0.05) 0%, rgba(118, 75, 162, 0.05) 100%);
            border: 1px solid rgba(102, 126, 234, 0.1);
            padding: 25px;
            border-radius: 12px;
            margin: 25px;
            position: relative;
            overflow: hidden;
        }

        .report-info::before {
            content: '';
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            height: 4px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }

        .info-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 15px;
        }

        .info-item {
            background: white;
            padding: 15px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(102, 126, 234, 0.1);
            border: 1px solid rgba(102, 126, 234, 0.1);
            font-weight: normal;
        }

        .info-item strong {
            color: #667eea;
            font-weight: 600;
            display: block;
            margin-bottom: 4px;
        }

        .info-item.notes {
            grid-column: 1 / -1;
        }

        .section {
            margin: 25px;
            page-break-inside: avoid;
        }

        .section-title {
            position: relative;
            margin-bottom: 20px;
        }

        .section-title::before {
            content: '';
            position: absolute;
            left: -15px;
            top: 50%;
            transform: translateY(-50%);
            width: 4px;
            height: 100%;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 2px;
        }

        /* ✅ INSIGHTS AI MODERNI */
        .ai-report-badge {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 8px 16px;
            border-radius: 25px;
            font-size: 12px;
            font-weight: 500;
            display: inline-block;
            margin: 10px 15px 10px 0;
            box-shadow: 0 4px 12px rgba(102, 126, 234, 0.3);
            backdrop-filter: blur(10px);
        }
        
        .ai-insights {
            background: linear-gradient(135deg, rgba(102, 126, 234, 0.03) 0%, rgba(118, 75, 162, 0.03) 100%);
            border: 1px solid rgba(102, 126, 234, 0.15);
            border-radius: 12px;
            position: relative;
            overflow: hidden;
        }

        .ai-insights::before {
            content: '🧠 Analisi intelligente PolarAi';
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 12px 20px;
            font-size: 14px;
            font-weight: 600;
            border-radius: 12px 12px 0 0;
            margin: 0;
        }

        .insights-content {
            padding: 60px 20px 20px 20px;
        }
        
        .ai-insights * {
            font-weight: normal !important;
        }
        
        .ai-insights h1, .ai-insights h2, .ai-insights h3, .ai-insights h4 {
            font-weight: 600 !important;
            color: #4a5568 !important;
        }
        
        .ai-insights strong, .ai-insights b {
            font-weight: 600 !important;
            color: #667eea;
        }

        .ai-insights ul {
            list-style: none;
            padding-left: 0;
        }

        .ai-insights li {
            position: relative;
            padding-left: 25px;
            margin-bottom: 8px;
        }

        .ai-insights li::before {
            content: '▶';
            position: absolute;
            left: 0;
            color: #667eea;
            font-size: 10px;
            top: 4px;
        }
        
        /* ✅ SEZIONI DATI MODERNE */
        .data-evolution {
            border: 2px dashed rgba(102, 126, 234, 0.3);
            padding: 20px;
            margin: 20px 0;
            background: linear-gradient(135deg, rgba(102, 126, 234, 0.05) 0%, rgba(118, 75, 162, 0.05) 100%);
            border-radius: 12px;
            position: relative;
        }
        
        .data-evolution::before {
            content: '📈 Evoluzione Dati';
            position: absolute;
            top: -12px;
            left: 20px;
            background: white;
            color: #667eea;
            font-weight: 600;
            padding: 4px 12px;
            border-radius: 6px;
            border: 2px solid rgba(102, 126, 234, 0.3);
        }

        /* ✅ CERTIFICAZIONE DATAPOLAR MODERNA */
        .certification-section {
            margin: 25px;
            padding: 0;
        }

        .certification-datapolar {
            background: linear-gradient(135deg, rgba(102, 126, 234, 0.05) 0%, rgba(118, 75, 162, 0.05) 100%);
            border: 2px solid rgba(102, 126, 234, 0.2);
            border-radius: 12px;
            padding: 0;
            margin: 0;
            position: relative;
            overflow: hidden;
        }

        .certification-datapolar::before {
            content: '🏆 Certificazione Dati DataPolar';
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 15px 20px;
            font-size: 14px;
            font-weight: 600;
            margin: 0;
            border-radius: 12px 12px 0 0;
        }

        .certification-datapolar-generic {
            margin-top: 64px !important;
        }

        .certification-datapolar h4 {
            color: #667eea;
            margin: 0px 20px 10px 20px;
            font-size: 14px;
            font-weight: 600;
        }

        .certification-table, .statistics-table {
            width: calc(100% - 40px);
            margin: 15px 20px 20px 20px;
            border-collapse: collapse;
            background: white;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 4px 12px rgba(102, 126, 234, 0.1);
            border: 1px solid rgba(102, 126, 234, 0.1);
        }

        .certification-table td, .statistics-table td {
            padding: 12px 16px;
            border-bottom: 1px solid rgba(102, 126, 234, 0.1);
            vertical-align: top;
        }

        .certification-table td:first-child, .statistics-table td:first-child {
            background: linear-gradient(135deg, rgba(102, 126, 234, 0.05) 0%, rgba(118, 75, 162, 0.05) 100%);
            width: 45%;
            font-weight: 500;
            color: #667eea;
            border-right: 1px solid rgba(102, 126, 234, 0.1);
        }

        .certification-table td:last-child, .statistics-table td:last-child {
            font-weight: 400;
            color: #4a5568;
        }

        .cert-warning {
            color: #e53e3e;
            font-weight: 500;
            padding: 15px 20px;
            background: linear-gradient(135deg, rgba(229, 62, 62, 0.1) 0%, rgba(229, 62, 62, 0.05) 100%);
            border: 1px solid rgba(229, 62, 62, 0.2);
            border-radius: 8px;
            margin: 15px 20px;
        }

        .certification-error {
            color: #e53e3e;
            background: linear-gradient(135deg, rgba(229, 62, 62, 0.1) 0%, rgba(229, 62, 62, 0.05) 100%);
            border: 1px solid rgba(229, 62, 62, 0.2);
            padding: 15px 20px;
            border-radius: 8px;
            font-weight: 500;
            margin: 15px 20px;
        }

        /* ✅ STATISTICHE E TABELLE MODERNE */
        .stats-content {
            background: linear-gradient(135deg, rgba(102, 126, 234, 0.03) 0%, rgba(118, 75, 162, 0.03) 100%);
            border: 1px solid rgba(102, 126, 234, 0.1);
            padding: 20px;
            border-radius: 12px;
            font-weight: normal;
        }

        .detailed-stats {
            position: relative;
        }

        .detailed-stats::before {
            content: '';
            position: absolute;
            left: -15px;
            top: 0;
            bottom: 0;
            width: 4px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 2px;
        }

        .raw-data-content {
            font-family: 'SF Mono', 'Monaco', 'Inconsolata', 'Roboto Mono', 'Consolas', monospace;
            background: linear-gradient(135deg, #2d3748 0%, #4a5568 100%);
            color: #e2e8f0;
            padding: 20px;
            border-radius: 12px;
            font-size: 11px;
            overflow-x: auto;
            font-weight: normal;
            border: 1px solid rgba(102, 126, 234, 0.2);
        }

        /* ✅ TABELLE MODERNE */
        table {
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 20px;
            font-weight: normal;
            background: white;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 2px 8px rgba(102, 126, 234, 0.1);
        }

        th, td {
            border: none;
            border-bottom: 1px solid rgba(102, 126, 234, 0.1);
            padding: 12px 16px;
            text-align: left;
            font-weight: normal;
        }

        th {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            font-weight: 600;
        }

        tr:nth-child(even) {
            background: rgba(102, 126, 234, 0.03);
        }

        tr:hover {
            background: rgba(102, 126, 234, 0.08);
        }

        /* ✅ TESTO E PARAGRAFI */
        p {
            font-weight: normal;
            margin-bottom: 12px;
            line-height: 1.6;
            color: #4a5568;
        }

        ul, ol {
            font-weight: normal;
            margin-bottom: 15px;
            padding-left: 20px;
        }

        li {
            font-weight: normal;
            margin-bottom: 6px;
            color: #4a5568;
        }

        /* ✅ ENFASI MODERNA */
        .important {
            font-weight: 600;
            color: #667eea;
            background: linear-gradient(135deg, rgba(102, 126, 234, 0.1) 0%, rgba(118, 75, 162, 0.1) 100%);
            padding: 2px 6px;
            border-radius: 4px;
        }

        .emphasis {
            font-weight: 500;
            color: #764ba2;
        }

        /* ✅ FOOTER MODERNO */
        .footer {
            background: linear-gradient(135deg, #2d3748 0%, #4a5568 100%);
            color: white;
            margin: 0;
            padding: 25px;
            text-align: center;
            border-radius: 0 0 12px 12px;
            position: relative;
            overflow: hidden;
        }

        .footer::before {
            content: '';
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            height: 4px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }

        .footer-content p {
            margin: 5px 0;
            color: rgba(255, 255, 255, 0.9);
        }

        .company-info {
            font-style: italic;
            color: rgba(255, 255, 255, 0.7) !important;
        }

        /* ✅ STILI STAMPA MODERNIZZATI */
        @media print {
            html, body {
                font-size: 12px !important;
            }

            body {
                background: white !important;
            }

            .report-container {
                box-shadow: none !important;
                border-radius: 0 !important;
            }

            .section {
                page-break-inside: avoid;
            }

            .header {
                page-break-after: avoid;
                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%) !important;
                -webkit-print-color-adjust: exact;
                color-adjust: exact;
            }

            .ai-insights::before,
            .certification-datapolar::before {
                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%) !important;
                -webkit-print-color-adjust: exact;
                color-adjust: exact;
            }

            table {
                page-break-inside: auto;
            }

            tr {
                page-break-inside: avoid;
            }

            .logo {
                width: 200px;
                height: auto;
                max-height: 60px;
                margin: 0;
            }
            
            .certification-datapolar {
                page-break-inside: avoid;
            }
            
            .certification-table, .statistics-table {
                page-break-inside: avoid;
            }
        }

        /* ✅ RESPONSIVE MODERNO */
        @media (max-width: 768px) {
            .report-header {
                flex-direction: column;
                align-items: center;
                text-align: center;
            }

            .report-title-container {
                order: 2;
                width: 100%;
                margin-top: 15px;
            }

            .company-logo {
                order: 1;
                justify-content: center;
                width: 100%;
            }

            .logo {
                width: 150px;
                max-height: 45px;
            }
            
            .logo-fallback {
                width: 150px;
                height: 40px;
            }

            .info-grid {
                grid-template-columns: 1fr;
            }

            .certification-table, .statistics-table {
                font-size: 0.85em;
                width: calc(100% - 20px);
                margin: 15px 10px;
            }
            
            .certification-datapolar {
                margin: 15px 10px;
            }

            .section {
                margin: 15px 10px;
            }
        }";
}