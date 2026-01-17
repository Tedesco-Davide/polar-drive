namespace PolarDrive.WebApi.PolarAiReports.Templates;

/// <summary>
/// Template CSS per i PDF di validazione Gap.
/// Supporta 3 tipi: CERTIFICATION (viola), ESCALATION (arancione), CONTRACT_BREACH (rosso)
/// </summary>
public static class GapValidationTemplate
{
    /// <summary>
    /// Restituisce gli stili font Satoshi
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
    /// CSS per documento CERTIFICATION (viola/blu - tono positivo)
    /// </summary>
    public static string GetCertificationCss() => GetFontStyles() + CertificationBaseCss;

    /// <summary>
    /// CSS per documento ESCALATION (arancione - tono attenzione)
    /// </summary>
    public static string GetEscalationCss() => GetFontStyles() + EscalationBaseCss;

    /// <summary>
    /// CSS per documento CONTRACT_BREACH (rosso - tono critico/legale)
    /// </summary>
    public static string GetContractBreachCss() => GetFontStyles() + ContractBreachBaseCss;

    /// <summary>
    /// Mantiene retrocompatibilita col metodo esistente (usa CERTIFICATION)
    /// </summary>
    public static string GetCss() => GetCertificationCss();

    #region CSS Base Comune

    private const string CommonBaseCss = @"
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

        .info-box {
            flex: 1;
            background: white;
            padding: 20px;
            border-radius: 10px;
        }

        .info-box p {
            margin: 8px 0;
            font-size: 12px;
            color: #4a5568;
        }

        .info-box strong {
            color: #2d3748;
        }

        .period-section {
            padding: 20px 30px;
            background: white;
            page-break-inside: avoid;
        }

        .period-section p {
            margin: 8px 0;
            font-size: 13px;
            color: #4a5568;
        }

        .disclaimer {
            margin: 25px 30px;
            padding: 25px;
            border-radius: 12px;
            page-break-inside: avoid;
        }

        .disclaimer h3 {
            margin: 0 0 15px 0;
            font-size: 15px;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .disclaimer h4 {
            margin: 20px 0 10px 0;
            font-size: 13px;
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

        .gaps-section {
            margin: 25px 30px;
            page-break-inside: avoid;
        }

        .gaps-section h3 {
            margin: 0 0 15px 0;
            font-size: 15px;
            color: #2d3748;
            font-weight: 600;
            padding-bottom: 8px;
        }

        .gaps-table {
            width: 100%;
            border-collapse: collapse;
            background: white;
            border-radius: 8px;
            overflow: hidden;
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

        .gaps-table td {
            padding: 12px 16px;
            font-size: 12px;
            color: #4a5568;
            vertical-align: middle;
        }

        .gaps-table td:nth-child(2) {
            text-align: center;
            width: 80px;
        }

        .confidence {
            font-weight: 600;
            padding: 1px 5px;
            border-radius: 8px;
            font-size: 9px;
            white-space: nowrap;
        }

        .confidence.high {
            background: rgba(72, 187, 120, 0.15);
            color: #276749;
            border: 1px solid rgba(72, 187, 120, 0.4);
        }

        .confidence.medium {
            background: rgba(237, 137, 54, 0.15);
            color: #c05621;
            border: 1px solid rgba(237, 137, 54, 0.4);
        }

        .confidence.low {
            background: rgba(229, 62, 62, 0.15);
            color: #c53030;
            border: 1px solid rgba(229, 62, 62, 0.4);
        }

        .justification {
            font-size: 11px;
            color: #718096;
            line-height: 1.6;
            max-width: 400px;
        }

        .summary-section {
            margin: 25px 30px;
            page-break-inside: avoid;
        }

        .summary-section h3 {
            margin: 0 0 15px 0;
            font-size: 15px;
            color: #2d3748;
            font-weight: 600;
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

        .summary-item.high .value { color: #276749; }
        .summary-item.medium .value { color: #c05621; }
        .summary-item.low .value { color: #c53030; }

        .summary-item .label {
            display: block;
            font-size: 11px;
            color: #718096;
            font-weight: 500;
        }

        .footer {
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

        .signature-section .footer-type {
            font-size: 13px;
            font-weight: 700;
            color: white;
            margin: 15px 0;
            padding: 8px 20px;
            border-radius: 6px;
            display: inline-block;
        }

        /* Notes Section */
        .notes-section {
            margin: 25px 30px;
            padding: 20px;
            border-radius: 12px;
            page-break-inside: avoid;
        }

        .notes-section h3 {
            margin: 0 0 15px 0;
            font-size: 15px;
            font-weight: 700;
        }

        .notes-section .notes-content {
            background: white;
            padding: 15px;
            border-radius: 8px;
            font-size: 12px;
            color: #4a5568;
            line-height: 1.6;
            white-space: pre-wrap;
        }

        /* Outage Section */
        .outage-summary-section {
            margin: 25px 30px;
            padding: 25px;
            background: linear-gradient(135deg, rgba(229, 62, 62, 0.08) 0%, rgba(229, 62, 62, 0.03) 100%);
            border: 2px solid rgba(229, 62, 62, 0.3);
            border-radius: 12px;
            page-break-inside: avoid;
        }

        .outage-summary-section h3 {
            margin: 0 0 20px 0;
            font-size: 15px;
            color: #c53030;
            font-weight: 700;
            text-transform: uppercase;
            border-bottom: 2px solid rgba(229, 62, 62, 0.3);
            padding-bottom: 10px;
        }

        .outage-stats-grid {
            display: flex;
            gap: 15px;
            margin-bottom: 20px;
        }

        .outage-stat-box {
            flex: 1;
            background: white;
            padding: 15px;
            border-radius: 8px;
            text-align: center;
            border: 1px solid rgba(229, 62, 62, 0.2);
        }

        .outage-stat-box .stat-value {
            font-size: 28px;
            font-weight: 700;
            color: #c53030;
            margin-bottom: 5px;
        }

        .outage-stat-box .stat-label {
            font-size: 11px;
            color: #718096;
            font-weight: 500;
        }

        .outage-details-table {
            width: 100%;
            border-collapse: collapse;
            background: white;
            font-size: 11px;
        }

        .outage-details-table thead {
            background: linear-gradient(135deg, #c53030 0%, #9b2c2c 100%);
        }

        .outage-details-table th {
            padding: 10px 12px;
            text-align: left;
            font-weight: 600;
            font-size: 10px;
            color: white;
            text-transform: uppercase;
        }

        .outage-details-table td {
            padding: 8px 12px;
            color: #4a5568;
            border-bottom: 1px solid rgba(229, 62, 62, 0.1);
        }

        .outage-type-badge {
            display: inline-block;
            padding: 3px 8px;
            border-radius: 12px;
            font-size: 9px;
            font-weight: 600;
        }

        .outage-type-badge.fleet-api-outage {
            background: rgba(229, 62, 62, 0.15);
            color: #c53030;
            border: 1px solid rgba(229, 62, 62, 0.4);
        }

        .outage-type-badge.vehicle-outage {
            background: rgba(237, 137, 54, 0.15);
            color: #c05621;
            border: 1px solid rgba(237, 137, 54, 0.4);
        }

        .gaps-table .outage-cell {
            text-align: center;
            width: 100px;
            vertical-align: middle;
        }

        .outage-badge {
            display: inline-block;
            padding: 3px 8px;
            border-radius: 12px;
            font-size: 9px;
            font-weight: 600;
        }

        .outage-badge.fleet-api-badge {
            background: rgba(229, 62, 62, 0.15);
            color: #c53030;
            border: 1px solid rgba(229, 62, 62, 0.4);
        }

        .outage-badge.vehicle-badge {
            background: rgba(237, 137, 54, 0.15);
            color: #c05621;
            border: 1px solid rgba(237, 137, 54, 0.4);
        }

        .outage-brand {
            display: block;
            font-size: 8px;
            color: #a0aec0;
            margin-top: 2px;
        }

        .no-outage {
            color: #cbd5e0;
            font-size: 12px;
        }

        @media print {
            body { padding: 0; }
            .header, .info-section, .period-section, .disclaimer,
            .gaps-section, .summary-section, .footer, .notes-section {
                margin-left: 0;
                margin-right: 0;
            }
            .gaps-table { page-break-inside: auto; }
            .gaps-table tbody tr {
                page-break-inside: avoid;
                page-break-after: auto;
            }
            .disclaimer, .summary-section { page-break-before: auto; }
        }

        @page {
            margin: 15mm;
            size: A4;
        }
    ";

    #endregion

    #region CERTIFICATION CSS (Viola/Blu)

    private const string CertificationBaseCss = CommonBaseCss + @"
        /* CERTIFICATION - Colori Viola/Blu */
        .header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 25px 30px;
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            page-break-inside: avoid;
        }

        .info-section {
            display: flex;
            gap: 20px;
            padding: 25px 30px;
            background: linear-gradient(135deg, rgba(102, 126, 234, 0.05) 0%, rgba(118, 75, 162, 0.05) 100%);
            page-break-inside: avoid;
        }

        .info-box {
            border: 1px solid rgba(102, 126, 234, 0.2);
            box-shadow: 0 2px 8px rgba(102, 126, 234, 0.1);
        }

        .info-box h3 {
            margin: 0 0 15px 0;
            font-size: 14px;
            color: #667eea;
            font-weight: 600;
            border-bottom: 2px solid rgba(102, 126, 234, 0.3);
            padding-bottom: 8px;
        }

        .period-section {
            border-bottom: 1px solid rgba(102, 126, 234, 0.1);
        }

        .period-section strong {
            color: #667eea;
        }

        .disclaimer {
            background: linear-gradient(135deg, rgba(72, 187, 120, 0.08) 0%, rgba(72, 187, 120, 0.03) 100%);
            border: 2px solid rgba(72, 187, 120, 0.3);
        }

        .disclaimer h3 {
            color: #276749;
        }

        .disclaimer h4 {
            color: #38a169;
        }

        .disclaimer .important {
            background: linear-gradient(135deg, rgba(102, 126, 234, 0.1) 0%, rgba(102, 126, 234, 0.05) 100%);
            border: 1px solid rgba(102, 126, 234, 0.3);
            border-radius: 8px;
            padding: 15px;
            margin-top: 15px;
            color: #667eea;
            font-weight: 500;
        }

        .gaps-section h3 {
            border-bottom: 2px solid #667eea;
        }

        .gaps-table {
            box-shadow: 0 4px 12px rgba(102, 126, 234, 0.15);
            border: 1px solid rgba(102, 126, 234, 0.2);
        }

        .gaps-table thead {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }

        .gaps-table tbody tr:nth-child(even) {
            background: rgba(102, 126, 234, 0.03);
        }

        .gaps-table tbody tr:hover {
            background: rgba(102, 126, 234, 0.08);
        }

        .gaps-table td {
            border-bottom: 1px solid rgba(102, 126, 234, 0.1);
        }

        .summary-section h3 {
            border-bottom: 2px solid #667eea;
        }

        .footer {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }

        /* Badge Certificazione */
        .certification-badge {
            display: inline-block;
            background: linear-gradient(135deg, #48bb78 0%, #38a169 100%);
            color: white;
            padding: 8px 20px;
            border-radius: 20px;
            font-size: 14px;
            font-weight: 700;
            margin-top: 10px;
        }
    ";

    #endregion

    #region ESCALATION CSS (Arancione)

    private const string EscalationBaseCss = CommonBaseCss + @"
        /* ESCALATION - Colori Arancione */
        .header {
            background: linear-gradient(135deg, #ed8936 0%, #dd6b20 100%);
            color: white;
            padding: 25px 30px;
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            page-break-inside: avoid;
        }

        .info-section {
            display: flex;
            gap: 20px;
            padding: 25px 30px;
            background: linear-gradient(135deg, rgba(237, 137, 54, 0.05) 0%, rgba(221, 107, 32, 0.05) 100%);
            page-break-inside: avoid;
        }

        .info-box {
            border: 1px solid rgba(237, 137, 54, 0.2);
            box-shadow: 0 2px 8px rgba(237, 137, 54, 0.1);
        }

        .info-box h3 {
            margin: 0 0 15px 0;
            font-size: 14px;
            color: #ed8936;
            font-weight: 600;
            border-bottom: 2px solid rgba(237, 137, 54, 0.3);
            padding-bottom: 8px;
        }

        .period-section {
            border-bottom: 1px solid rgba(237, 137, 54, 0.1);
        }

        .period-section strong {
            color: #ed8936;
        }

        .disclaimer {
            background: linear-gradient(135deg, rgba(237, 137, 54, 0.08) 0%, rgba(237, 137, 54, 0.03) 100%);
            border: 2px solid rgba(237, 137, 54, 0.3);
        }

        .disclaimer h3 {
            color: #c05621;
        }

        .disclaimer h4 {
            color: #dd6b20;
        }

        .disclaimer .important {
            background: linear-gradient(135deg, rgba(237, 137, 54, 0.1) 0%, rgba(237, 137, 54, 0.05) 100%);
            border: 1px solid rgba(237, 137, 54, 0.3);
            border-radius: 8px;
            padding: 15px;
            margin-top: 15px;
            color: #c05621;
            font-weight: 500;
        }

        .gaps-section h3 {
            border-bottom: 2px solid #ed8936;
        }

        .gaps-table {
            box-shadow: 0 4px 12px rgba(237, 137, 54, 0.15);
            border: 1px solid rgba(237, 137, 54, 0.2);
        }

        .gaps-table thead {
            background: linear-gradient(135deg, #ed8936 0%, #dd6b20 100%);
        }

        .gaps-table tbody tr:nth-child(even) {
            background: rgba(237, 137, 54, 0.03);
        }

        .gaps-table tbody tr:hover {
            background: rgba(237, 137, 54, 0.08);
        }

        .gaps-table td {
            border-bottom: 1px solid rgba(237, 137, 54, 0.1);
        }

        .summary-section h3 {
            border-bottom: 2px solid #ed8936;
        }

        .footer {
            background: linear-gradient(135deg, #ed8936 0%, #dd6b20 100%);
        }

        /* Notes Section - Escalation */
        .notes-section {
            background: linear-gradient(135deg, rgba(237, 137, 54, 0.08) 0%, rgba(237, 137, 54, 0.03) 100%);
            border: 2px solid rgba(237, 137, 54, 0.3);
        }

        .notes-section h3 {
            color: #c05621;
        }

        .notes-section .notes-content {
            border: 1px solid rgba(237, 137, 54, 0.2);
        }

        /* Badge Escalation */
        .escalation-badge {
            display: inline-block;
            background: linear-gradient(135deg, #ed8936 0%, #dd6b20 100%);
            color: white;
            padding: 8px 20px;
            border-radius: 20px;
            font-size: 14px;
            font-weight: 700;
            margin-top: 10px;
        }
    ";

    #endregion

    #region CONTRACT_BREACH CSS (Rosso)

    private const string ContractBreachBaseCss = CommonBaseCss + @"
        /* CONTRACT_BREACH - Colori Rosso */
        .header {
            background: linear-gradient(135deg, #e53e3e 0%, #c53030 100%);
            color: white;
            padding: 25px 30px;
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            page-break-inside: avoid;
        }

        .info-section {
            display: flex;
            gap: 20px;
            padding: 25px 30px;
            background: linear-gradient(135deg, rgba(229, 62, 62, 0.05) 0%, rgba(197, 48, 48, 0.05) 100%);
            page-break-inside: avoid;
        }

        .info-box {
            border: 1px solid rgba(229, 62, 62, 0.2);
            box-shadow: 0 2px 8px rgba(229, 62, 62, 0.1);
        }

        .info-box h3 {
            margin: 0 0 15px 0;
            font-size: 14px;
            color: #e53e3e;
            font-weight: 600;
            border-bottom: 2px solid rgba(229, 62, 62, 0.3);
            padding-bottom: 8px;
        }

        .period-section {
            border-bottom: 1px solid rgba(229, 62, 62, 0.1);
        }

        .period-section strong {
            color: #e53e3e;
        }

        .disclaimer {
            background: linear-gradient(135deg, rgba(229, 62, 62, 0.08) 0%, rgba(229, 62, 62, 0.03) 100%);
            border: 2px solid rgba(229, 62, 62, 0.3);
        }

        .disclaimer h3 {
            color: #c53030;
        }

        .disclaimer h4 {
            color: #e53e3e;
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

        .gaps-section h3 {
            border-bottom: 2px solid #e53e3e;
        }

        .gaps-table {
            box-shadow: 0 4px 12px rgba(229, 62, 62, 0.15);
            border: 1px solid rgba(229, 62, 62, 0.2);
        }

        .gaps-table thead {
            background: linear-gradient(135deg, #e53e3e 0%, #c53030 100%);
        }

        .gaps-table tbody tr:nth-child(even) {
            background: rgba(229, 62, 62, 0.03);
        }

        .gaps-table tbody tr:hover {
            background: rgba(229, 62, 62, 0.08);
        }

        .gaps-table td {
            border-bottom: 1px solid rgba(229, 62, 62, 0.1);
        }

        .summary-section h3 {
            border-bottom: 2px solid #e53e3e;
        }

        .footer {
            background: linear-gradient(135deg, #e53e3e 0%, #c53030 100%);
        }

        /* Notes Section - Contract Breach */
        .notes-section {
            background: linear-gradient(135deg, rgba(229, 62, 62, 0.08) 0%, rgba(229, 62, 62, 0.03) 100%);
            border: 2px solid rgba(229, 62, 62, 0.3);
        }

        .notes-section h3 {
            color: #c53030;
        }

        .notes-section .notes-content {
            border: 1px solid rgba(229, 62, 62, 0.2);
        }

        /* Badge Contract Breach */
        .breach-badge {
            display: inline-block;
            background: linear-gradient(135deg, #e53e3e 0%, #c53030 100%);
            color: white;
            padding: 8px 20px;
            border-radius: 20px;
            font-size: 14px;
            font-weight: 700;
            margin-top: 10px;
        }

        /* Legal Warning Box */
        .legal-warning {
            margin: 25px 30px;
            padding: 20px;
            background: linear-gradient(135deg, rgba(229, 62, 62, 0.15) 0%, rgba(229, 62, 62, 0.08) 100%);
            border: 3px solid #c53030;
            border-radius: 12px;
            page-break-inside: avoid;
        }

        .legal-warning h3 {
            margin: 0 0 15px 0;
            font-size: 16px;
            color: #c53030;
            font-weight: 700;
            text-transform: uppercase;
        }

        .legal-warning p {
            margin: 10px 0;
            font-size: 12px;
            color: #742a2a;
            line-height: 1.7;
        }
    ";

    #endregion
}
