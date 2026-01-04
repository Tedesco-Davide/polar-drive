namespace PolarDrive.WebApi.PolarAiReports.Templates;

/// <summary>
/// Template CSS per il PDF di certificazione probabilistica dei gap
/// </summary>
public static class GapCertificationTemplate
{
    /// <summary>
    /// Restituisce gli stili coerenti con gli stili di stampa PDF attuali
    /// </summary>
    public static string GetFontStyles()
    {
        // Path assoluto nel container Docker
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
        ";
    }

    /// <summary>
    /// Restituisce il CSS per il documento di certificazione (include font Satoshi)
    /// </summary>
    public static string GetCss() => GetFontStyles() + BaseCss;

    /// <summary>
    /// CSS base per il documento di certificazione
    /// </summary>
    private const string BaseCss = @"
        html, body {
            font-size: 12px !important;
            font-family: 'Satoshi', 'Noto Color Emoji', 'Apple Color Emoji', sans-serif;
            margin: 0;
            padding: 0;
            line-height: 1.5;
            background: white;
            color: #2d3748;
            -webkit-print-color-adjust: exact;
            color-adjust: exact;
            print-color-adjust: exact;
        }

        * {
            -webkit-print-color-adjust: exact;
            color-adjust: exact;
            print-color-adjust: exact;
            box-sizing: border-box;
        }

        /* HEADER */
        .header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 25px 30px;
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            page-break-inside: avoid;
        }

        .logo-section h1 {
            margin: 0 0 5px 0;
            font-size: 28px;
            font-weight: 700;
            color: white;
        }

        .logo-section .subtitle {
            font-size: 14px;
            color: rgba(255, 255, 255, 0.9);
            margin: 0;
            background: rgba(255, 255, 255, 0.2);
            padding: 5px 12px;
            border-radius: 20px;
            display: inline-block;
        }

        .doc-info {
            text-align: right;
            font-size: 12px;
            color: rgba(255, 255, 255, 0.9);
        }

        .doc-info p {
            margin: 5px 0;
            color: rgba(255, 255, 255, 0.9);
        }

        /* INFO SECTION */
        .info-section {
            display: flex;
            gap: 20px;
            padding: 25px 30px;
            background: linear-gradient(135deg, rgba(139, 159, 242, 0.05) 0%, rgba(156, 130, 199, 0.05) 100%);
            page-break-inside: avoid;
        }

        .info-box {
            flex: 1;
            background: white;
            padding: 20px;
            border-radius: 10px;
            border: 1px solid rgba(139, 159, 242, 0.2);
            box-shadow: 0 2px 8px rgba(139, 159, 242, 0.1);
        }

        .info-box h3 {
            margin: 0 0 15px 0;
            font-size: 14px;
            color: #8b9ff2;
            font-weight: 600;
            border-bottom: 2px solid rgba(139, 159, 242, 0.3);
            padding-bottom: 8px;
        }

        .info-box p {
            margin: 8px 0;
            font-size: 12px;
            color: #4a5568;
        }

        .info-box strong {
            color: #2d3748;
        }

        /* PERIOD SECTION */
        .period-section {
            padding: 20px 30px;
            background: white;
            border-bottom: 1px solid rgba(139, 159, 242, 0.1);
            page-break-inside: avoid;
        }

        .period-section p {
            margin: 8px 0;
            font-size: 13px;
            color: #4a5568;
        }

        .period-section strong {
            color: #8b9ff2;
        }

        /* DISCLAIMER */
        .disclaimer {
            margin: 25px 30px;
            padding: 25px;
            background: linear-gradient(135deg, rgba(237, 137, 54, 0.08) 0%, rgba(237, 137, 54, 0.03) 100%);
            border: 2px solid rgba(237, 137, 54, 0.3);
            border-radius: 12px;
            page-break-inside: avoid;
        }

        .disclaimer h3 {
            margin: 0 0 15px 0;
            font-size: 15px;
            color: #c05621;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .disclaimer h4 {
            margin: 20px 0 10px 0;
            font-size: 13px;
            color: #dd6b20;
            font-weight: 600;
        }

        .disclaimer p {
            margin: 10px 0;
            font-size: 12px;
            color: #4a5568;
            line-height: 1.7;
        }

        .disclaimer ul {
            margin: 10px 0;
            padding-left: 25px;
        }

        .disclaimer li {
            margin: 6px 0;
            font-size: 12px;
            color: #4a5568;
        }

        .disclaimer .important {
            background: linear-gradient(135deg, rgba(229, 62, 62, 0.1) 0%, rgba(229, 62, 62, 0.05) 100%);
            border: 1px solid rgba(229, 62, 62, 0.3);
            border-radius: 8px;
            padding: 15px;
            margin-top: 15px;
            color: #c53030;
            font-weight: 500;
        }

        /* GAPS TABLE SECTION */
        .gaps-section {
            margin: 25px 30px;
            page-break-inside: avoid;
        }

        .gaps-section h3 {
            margin: 0 0 15px 0;
            font-size: 15px;
            color: #2d3748;
            font-weight: 600;
            border-bottom: 2px solid #8b9ff2;
            padding-bottom: 8px;
        }

        .gaps-table {
            width: 100%;
            border-collapse: collapse;
            background: white;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 4px 12px rgba(139, 159, 242, 0.15);
            border: 1px solid rgba(139, 159, 242, 0.2);
        }

        .gaps-table thead {
            background: linear-gradient(135deg, #8b9ff2 0%, #9c82c7 100%);
        }

        .gaps-table th {
            padding: 14px 16px;
            text-align: left;
            font-weight: 600;
            font-size: 12px;
            color: white;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .gaps-table tbody tr {
            page-break-inside: avoid;
        }

        .gaps-table tbody tr:nth-child(even) {
            background: rgba(139, 159, 242, 0.03);
        }

        .gaps-table tbody tr:hover {
            background: rgba(139, 159, 242, 0.08);
        }

        .gaps-table td {
            padding: 12px 16px;
            font-size: 12px;
            color: #4a5568;
            border-bottom: 1px solid rgba(139, 159, 242, 0.1);
            vertical-align: top;
        }

        /* CONFIDENCE BADGES */
        .confidence {
            font-weight: 700;
            padding: 6px 12px;
            border-radius: 20px;
            display: inline-block;
            text-align: center;
            min-width: 70px;
        }

        .confidence.high {
            background: linear-gradient(135deg, rgba(72, 187, 120, 0.2) 0%, rgba(72, 187, 120, 0.1) 100%);
            color: #276749;
            border: 1px solid rgba(72, 187, 120, 0.4);
        }

        .confidence.medium {
            background: linear-gradient(135deg, rgba(237, 137, 54, 0.2) 0%, rgba(237, 137, 54, 0.1) 100%);
            color: #c05621;
            border: 1px solid rgba(237, 137, 54, 0.4);
        }

        .confidence.low {
            background: linear-gradient(135deg, rgba(229, 62, 62, 0.2) 0%, rgba(229, 62, 62, 0.1) 100%);
            color: #c53030;
            border: 1px solid rgba(229, 62, 62, 0.4);
        }

        .justification {
            font-size: 11px;
            color: #718096;
            line-height: 1.6;
            max-width: 400px;
        }

        /* SUMMARY SECTION */
        .summary-section {
            margin: 25px 30px;
            page-break-inside: avoid;
        }

        .summary-section h3 {
            margin: 0 0 15px 0;
            font-size: 15px;
            color: #2d3748;
            font-weight: 600;
            border-bottom: 2px solid #8b9ff2;
            padding-bottom: 8px;
        }

        .summary-grid {
            display: flex;
            gap: 15px;
        }

        .summary-item {
            flex: 1;
            padding: 20px;
            border-radius: 10px;
            text-align: center;
            border: 2px solid;
        }

        .summary-item.high {
            background: linear-gradient(135deg, rgba(72, 187, 120, 0.1) 0%, rgba(72, 187, 120, 0.05) 100%);
            border-color: rgba(72, 187, 120, 0.4);
        }

        .summary-item.medium {
            background: linear-gradient(135deg, rgba(237, 137, 54, 0.1) 0%, rgba(237, 137, 54, 0.05) 100%);
            border-color: rgba(237, 137, 54, 0.4);
        }

        .summary-item.low {
            background: linear-gradient(135deg, rgba(229, 62, 62, 0.1) 0%, rgba(229, 62, 62, 0.05) 100%);
            border-color: rgba(229, 62, 62, 0.4);
        }

        .summary-item .value {
            display: block;
            font-size: 32px;
            font-weight: 700;
            margin-bottom: 5px;
        }

        .summary-item.high .value {
            color: #276749;
        }

        .summary-item.medium .value {
            color: #c05621;
        }

        .summary-item.low .value {
            color: #c53030;
        }

        .summary-item .label {
            display: block;
            font-size: 11px;
            color: #718096;
            font-weight: 500;
        }

        /* FOOTER */
        .footer {
            background: linear-gradient(135deg, #2d3748 0%, #4a5568 100%);
            color: white;
            padding: 30px;
            margin-top: 30px;
            page-break-inside: avoid;
        }

        .signature-section {
            text-align: center;
        }

        .signature-section p {
            margin: 8px 0;
            color: rgba(255, 255, 255, 0.9);
            font-size: 12px;
        }

        .signature-section .hash {
            font-family: 'SF Mono', 'Monaco', 'Inconsolata', 'Consolas', monospace;
            font-size: 10px;
            background: rgba(255, 255, 255, 0.1);
            padding: 10px 15px;
            border-radius: 6px;
            word-break: break-all;
            color: rgba(255, 255, 255, 0.8);
            border: 1px solid rgba(255, 255, 255, 0.2);
            display: inline-block;
            margin: 10px 0;
        }

        /* PRINT OPTIMIZATIONS */
        @media print {
            body {
                padding: 0;
            }

            .header, .info-section, .period-section, .disclaimer,
            .gaps-section, .summary-section, .footer {
                margin-left: 0;
                margin-right: 0;
            }

            .gaps-table {
                page-break-inside: auto;
            }

            .gaps-table tbody tr {
                page-break-inside: avoid;
                page-break-after: auto;
            }

            .disclaimer, .summary-section {
                page-break-before: auto;
            }
        }

        @page {
            margin: 15mm;
            size: A4;
        }
    ";
}
