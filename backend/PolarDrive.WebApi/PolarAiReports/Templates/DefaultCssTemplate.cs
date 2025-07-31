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

        /* ✅ HEADER CON TABLE LAYOUT - Perfetto per PDF */
        .header {
            /* Background con gradiente più scuro per contrasto */
            color: white;
            padding: 25px 30px;
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

        /* TABLE LAYOUT per allineamento perfetto */
        .header-table {
            width: 100%;
            border-collapse: collapse;
            position: relative;
            z-index: 1;
            margin: 0;
            padding: 0;
        }

        .header-table td {
            border: none;
            padding: 0;
            margin: 0;
            vertical-align: middle;
        }

        /* Cella titolo (sinistra) */
        .title-cell {
            width: 65%;
            text-align: left;
            padding-right: 20px;
        }

        /* Cella logo (destra) */
        .logo-cell {
            width: 35%;
            text-align: right;
            vertical-align: middle;
        }

        .report-title {
            margin: 0;
            padding: 0;
            font-size: 32px;
            font-weight: 700;
            /* GRADIENTE per il titolo */
            background: linear-gradient(135deg, #ffffff 0%, #e2e8ff 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
            /* Fallback per browser che non supportano il gradiente */
            color: white;
        }

        .report-id {
            font-size: 14px;
            color: rgba(255, 255, 255, 0.95);
            background: rgba(255, 255, 255, 0.2);
            padding: 3px 12px;
            border-radius: 20px;
            display: inline-block;
            backdrop-filter: blur(10px);
            border: 1px solid rgba(255, 255, 255, 0.3);
        }

        .logo {
            width: 300px;
            height: auto;
            margin: 0;
            background: rgba(255, 255, 255, 0.95);
            padding: 10px;
            border-radius: 8px;
        }

        .logo-fallback {
            width: 200px;
            height: 50px;
            background: rgba(255, 255, 255, 0.95);
            color: #2d3748;
            display: inline-block;
            text-align: center;
            line-height: 50px;
            font-weight: 600;
            font-size: 16px;
            border-radius: 8px;
            margin: 0;
            border: 1px solid rgba(255, 255, 255, 0.4);
        }

        /* Nascondi i vecchi elementi */
        .report-header {
            display: none;
        }

        .report-title-container,
        .company-logo {
            display: none;
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
            background: linear-gradient(90deg, #8b9ff2, #9c82c7) left bottom no-repeat;
            background-size: 60px 2px;
            padding-bottom: 8px;
        }

        h3 {
            color: #8b9ff2;
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
            background: linear-gradient(135deg, rgba(139, 159, 242, 0.05) 0%, rgba(156, 130, 199, 0.05) 100%);
            border: 1px solid rgba(139, 159, 242, 0.1);
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
            background: linear-gradient(135deg, #8b9ff2 0%, #9c82c7 100%);
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
            box-shadow: 0 2px 8px rgba(139, 159, 242, 0.1);
            border: 1px solid rgba(139, 159, 242, 0.1);
            font-weight: normal;
        }

        .info-item strong {
            color: #8b9ff2;
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
            background: linear-gradient(135deg, #8b9ff2 0%, #9c82c7 100%);
            border-radius: 2px;
        }

        /* ✅ INSIGHTS AI MODERNI - Colori più chiari */
        .ai-report-badge {
            background: linear-gradient(135deg, #8b9ff2 0%, #9c82c7 100%);
            color: white;
            padding: 8px 16px;
            border-radius: 25px;
            font-size: 12px;
            font-weight: 500;
            display: inline-block;
            margin: 10px 15px 10px 0;
            box-shadow: 0 4px 12px rgba(139, 159, 242, 0.3);
            backdrop-filter: blur(10px);
        }
        
        .ai-insights {
            background: linear-gradient(135deg, rgba(139, 159, 242, 0.03) 0%, rgba(156, 130, 199, 0.03) 100%);
            border: 1px solid rgba(139, 159, 242, 0.15);
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
            background: linear-gradient(135deg, #8b9ff2 0%, #9c82c7 100%);
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
            color: #8b9ff2;
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
            color: #8b9ff2;
            font-size: 10px;
            top: 4px;
        }
        
        /* ✅ SEZIONI DATI MODERNE */
        .data-evolution {
            border: 2px dashed rgba(139, 159, 242, 0.3);
            padding: 20px;
            margin: 20px 0;
            background: linear-gradient(135deg, rgba(139, 159, 242, 0.05) 0%, rgba(156, 130, 199, 0.05) 100%);
            border-radius: 12px;
            position: relative;
        }
        
        .data-evolution::before {
            content: '📈 Evoluzione Dati';
            position: absolute;
            top: -12px;
            left: 20px;
            background: white;
            color: #8b9ff2;
            font-weight: 600;
            padding: 4px 12px;
            border-radius: 6px;
            border: 2px solid rgba(139, 159, 242, 0.3);
        }

        /* ✅ CERTIFICAZIONE DATAPOLAR MODERNA */
        .certification-section {
            margin: 25px;
            padding: 0;
        }

        .certification-datapolar {
            background: linear-gradient(135deg, rgba(139, 159, 242, 0.05) 0%, rgba(156, 130, 199, 0.05) 100%);
            border: 2px solid rgba(139, 159, 242, 0.2);
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
            background: linear-gradient(135deg, #8b9ff2 0%, #9c82c7 100%);
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
            color: #8b9ff2;
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
            box-shadow: 0 4px 12px rgba(139, 159, 242, 0.1);
            border: 1px solid rgba(139, 159, 242, 0.1);
        }

        .certification-table td, .statistics-table td {
            padding: 12px 16px;
            border-bottom: 1px solid rgba(139, 159, 242, 0.1);
            vertical-align: top;
        }

        .certification-table td:first-child, .statistics-table td:first-child {
            background: linear-gradient(135deg, rgba(139, 159, 242, 0.05) 0%, rgba(156, 130, 199, 0.05) 100%);
            width: 45%;
            font-weight: 500;
            color: #8b9ff2;
            border-right: 1px solid rgba(139, 159, 242, 0.1);
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
            background: linear-gradient(135deg, rgba(139, 159, 242, 0.03) 0%, rgba(156, 130, 199, 0.03) 100%);
            border: 1px solid rgba(139, 159, 242, 0.1);
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
            background: linear-gradient(135deg, #8b9ff2 0%, #9c82c7 100%);
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
            border: 1px solid rgba(139, 159, 242, 0.2);
        }

        /* ✅ TABELLE MODERNE */
        table {
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 20px;
            font-weight: normal;
            border-radius: 8px;
            overflow: hidden;
        }

        th, td {
            border: none;
            border-bottom: 1px solid rgba(139, 159, 242, 0.1);
            padding: 12px 16px;
            text-align: left;
            font-weight: normal;
        }

        th {
            background: linear-gradient(135deg, #8b9ff2 0%, #9c82c7 100%);
            color: white;
            font-weight: 600;
        }

        tr:nth-child(even) {
            background: rgba(139, 159, 242, 0.03);
        }

        tr:hover {
            background: rgba(139, 159, 242, 0.08);
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
            color: #8b9ff2;
            background: linear-gradient(135deg, rgba(139, 159, 242, 0.1) 0%, rgba(156, 130, 199, 0.1) 100%);
            padding: 2px 6px;
            border-radius: 4px;
        }

        .emphasis {
            font-weight: 500;
            color: #9c82c7;
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
            background: linear-gradient(135deg, #8b9ff2 0%, #9c82c7 100%);
        }

        .footer-content p {
            margin: 5px 0;
            color: rgba(255, 255, 255, 0.9);
        }

        .company-info {
            font-style: italic;
            color: rgba(255, 255, 255, 0.7) !important;
        }

        /* ✅ STILI STAMPA OTTIMIZZATI PER PDF */
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
                /* Mantieni il gradiente scuro per contrasto */
                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%) !important;
                color: white !important;
                -webkit-print-color-adjust: exact;
                color-adjust: exact;
            }

            /* Table layout funziona perfettamente nei PDF */
            .header-table {
                width: 100% !important;
                border-collapse: collapse !important;
            }

            .header-table td {
                border: none !important;
                padding: 0 !important;
                vertical-align: middle !important;
            }

            .title-cell {
                width: 65% !important;
                text-align: left !important;
                padding-right: 20px !important;
            }

            .logo-cell {
                width: 35% !important;
                text-align: right !important;
            }

            .report-title {
                color: white !important;
                background: linear-gradient(135deg, #ffffff 0%, #f0f2ff 100%) !important;
                -webkit-background-clip: text !important;
                -webkit-text-fill-color: transparent !important;
                background-clip: text !important;
            }

            .report-id {
                color: rgba(255, 255, 255, 0.95) !important;
                background: rgba(255, 255, 255, 0.25) !important;
                border: 1px solid rgba(255, 255, 255, 0.4) !important;
            }

            .logo {
                filter: drop-shadow(0 4px 8px rgba(0,0,0,0.4)) brightness(1.1) contrast(1.2) !important;
                background: rgba(255, 255, 255, 0.98) !important;
                padding: 12px !important;
                border: 2px solid rgba(255, 255, 255, 0.5) !important;
            }

            .ai-insights::before,
            .certification-datapolar::before {
                background: linear-gradient(135deg, #7a8fef 0%, #8f78c4 100%) !important;
                -webkit-print-color-adjust: exact;
                color-adjust: exact;
            }

            table {
                page-break-inside: auto;
            }

            tr {
                page-break-inside: avoid;
            }
            
            .certification-datapolar {
                page-break-inside: avoid;
            }
            
            .certification-table, .statistics-table {
                page-break-inside: avoid;
            }

            /* Assicura layout centrato senza clearfix */
            .header::after {
                display: none !important;
            }
        }

        /* ✅ RESPONSIVE MODERNO */
        @media (max-width: 768px) {
            .header {
                padding: 15px 20px;
            }

            .company-logo {
                margin-bottom: 15px;
            }

            .logo {
                width: 180px;
                max-height: 55px;
            }
            
            .logo-fallback {
                width: 180px;
                height: 50px;
            }

            .report-title {
                font-size: 26px;
            }

            .report-id {
                font-size: 14px;
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