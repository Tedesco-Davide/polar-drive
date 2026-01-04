using System.Text;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Servizio dedicato ESCLUSIVAMENTE alla certificazione DataPolar
/// Gestisce: certificazioni generali, statistiche mensili, tabelle dettagliate log
/// </summary>
public class DataPolarCertification(PolarDriveDbContext dbContext)
{
    private readonly PolarDriveDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly PolarDriveLogger _logger = new();

    /// <summary>
    /// Genera il blocco HTML completo della certificazione DataPolar
    /// Include: certificazione generale + statistiche mensili + tabella dettagliata
    /// </summary>
    public async Task<string> GenerateCompleteCertificationHtmlAsync(
        int vehicleId, 
        TimeSpan totalMonitoringPeriod, 
        DateTime firstRecord,
        DateTime lastRecord,
        int totalRecords, 
        PdfReport report)
    {
        try
        {
            // Genera certificazione usando i dati lifetime
            var certification = GenerateDataCertificationHtml(
                totalRecords, 
                firstRecord, 
                lastRecord, 
                totalMonitoringPeriod);

            // Calcola first/last record mensili (ultimi 30 giorni)
            var startTime = DateTime.Now.AddHours(-MONTHLY_HOURS_THRESHOLD);
            
            var monthlyTimeRange = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= startTime)
                .GroupBy(vd => vd.VehicleId)
                .Select(g => new
                {
                    FirstRecord = g.Min(vd => vd.Timestamp),
                    LastRecord = g.Max(vd => vd.Timestamp),
                    TotalCount = g.Count()
                })
                .FirstOrDefaultAsync();

            var monthlyDataCount = monthlyTimeRange?.TotalCount ?? 0;
            var monthlyFirstRecord = monthlyTimeRange?.FirstRecord ?? DateTime.Now;
            var monthlyLastRecord = monthlyTimeRange?.LastRecord ?? DateTime.Now;

            var statistics = await GenerateMonthlyStatisticsHtml(
                monthlyDataCount, 
                totalMonitoringPeriod, 
                monthlyFirstRecord,
                monthlyLastRecord,
                vehicleId,
                MONTHLY_HOURS_THRESHOLD
            );
            
            var detailedTable = await GenerateDetailedLogTableAsync(vehicleId, MONTHLY_HOURS_THRESHOLD);

            // Genera sezione ADAPTIVE_PROFILE con legenda e cards utilizzatori
            var adaptiveProfileSection = await GenerateAdaptiveProfileLegendAndCardsAsync(vehicleId, MONTHLY_HOURS_THRESHOLD);

            return $@"
                <div class='certification-datapolar'>
                    <h4 class='certification-datapolar-generic'>📋 Statistiche Generali (Lifetime)</h4>
                    {certification}
                    
                    <h4>📊 Statistiche Analisi Mensile</h4>
                    {statistics}
                    
                    <h4 class='detailed-log-table-title'>💽 Tabella Dettagliata Log Timestamp Certificati - Ultime 720 ore (30 giorni)</h4>
                    {detailedTable}

                    {adaptiveProfileSection}
                </div>";
        }
        catch (Exception ex)
        {
            await _logger.Error("DataPolarCertification.GenerateCompleteCertification",
                "Errore generazione certificazione", ex.ToString());
            return "<div class='certification-error'>⚠️ Certificazione dati non disponibile.</div>";
        }
    }

    /// <summary>
    /// 📊 Genera certificazione dati generale in formato HTML usando dati già calcolati
    /// </summary>
    public string GenerateDataCertificationHtml(
        int totalRecords,
        DateTime firstRecord,
        DateTime lastRecord,
        TimeSpan totalMonitoringPeriod)
    {
        if (firstRecord == default || lastRecord == default || totalRecords == 0)
        {
            return "<p class='cert-warning'>⚠️ Dati insufficienti per certificazione</p>";
        }

        var actualMonitoringPeriod = lastRecord - firstRecord;
        var totalCertifiedHours = Math.Max(actualMonitoringPeriod.TotalHours, 1); // Evita divisione per zero

        var uptimePercentage = Math.Min(95.0, 80.0 + (totalRecords / totalCertifiedHours) * 10);
        var qualityScore = CalculateQualityScore(totalRecords, uptimePercentage, totalMonitoringPeriod);
        var qualityStars = GetQualityStars(qualityScore);

        var firstRecordDate = firstRecord.ToString("dd/MM/yyyy").Replace("-", "/");
        var lastRecordDate = lastRecord.ToString("dd/MM/yyyy").Replace("-", "/");
    
        return $@"
            <table class='certification-table'>
                <tr><td>Ore totali certificate</td><td>{totalCertifiedHours:F0} ore ({totalCertifiedHours / 24:0} giorni)</td></tr>
                <tr><td>Uptime raccolta</td><td>{uptimePercentage:0}%</td></tr>
                <tr><td>Qualità dataset</td><td>{qualityStars} ({GetQualityLabel(qualityScore)})</td></tr>
                <tr><td>Primo record</td><td>{firstRecordDate:dd/MM/yyyy} alle {firstRecord:HH:mm}</td></tr>
                <tr><td>Ultimo record</td><td>{lastRecordDate:dd/MM/yyyy} alle {lastRecord:HH:mm}</td></tr>
                <tr><td>Records totali (lifetime)</td><td>{totalRecords:N0}</td></tr>
                <tr><td>Frequenza media</td><td>{(totalRecords / totalCertifiedHours):0} campioni/ora</td></tr>
            </table>";
    }

    /// <summary>
    /// 📊 Genera statistiche mensili in formato HTML
    /// </summary>
    private async Task<string> GenerateMonthlyStatisticsHtml(
        int monthlyRecords, 
        TimeSpan totalMonitoringPeriod, 
        DateTime firstRecord, 
        DateTime lastRecord,
        int vehicleId,
        int dataHours)
    {
        const int monthlyThreshold = MONTHLY_HOURS_THRESHOLD;
        
        // Calcola le ore effettive dei dati mensili
        var actualMonthlyHours = Math.Max((lastRecord - firstRecord).TotalHours, 1);
        
        // Calcola periodo effettivo per tabella dettagliata
        var now = DateTime.Now;
        var maxStartTime = now.AddHours(-dataHours);
        var effectiveStartTime = firstRecord > maxStartTime ? firstRecord : maxStartTime;
        
        var startTime = new DateTime(
            effectiveStartTime.Year,
            effectiveStartTime.Month,
            effectiveStartTime.Day,
            effectiveStartTime.Hour,
            0, 0
        );
        
        var actualHoursToShow = (int)Math.Ceiling((now - startTime).TotalHours);
        
        // Conta record certificati nel periodo della tabella dettagliata
        var certifiedRecords = await _dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= startTime)
            .CountAsync();
        
        var totalExpectedRecords = actualHoursToShow;
        var completeness = totalExpectedRecords > 0 
            ? (certifiedRecords / (double)totalExpectedRecords * 100) 
            : 0;
        
        return $@"
            <table class='statistics-table'>
                <tr><td>Durata monitoraggio totale</td><td>{totalMonitoringPeriod.TotalDays:0} giorni</td></tr>
                <tr><td>Campioni mensili analizzati</td><td>{monthlyRecords:N0}</td></tr>
                <tr><td>Finestra unificata</td><td>{monthlyThreshold} ore ({PROD_MONTHLY_REPEAT_DAYS} giorni)</td></tr>
                <tr><td>Densità dati mensile</td><td>{monthlyRecords / actualMonthlyHours:0} campioni/ora</td></tr>
                <tr><td>Frequenza campionamento</td><td>{(monthlyRecords > 0 ? (lastRecord - firstRecord).TotalMinutes / monthlyRecords : 0):F2} min/campione</td></tr>
                <tr><td>Copertura dati</td><td>{Math.Min(100, (actualMonthlyHours / Math.Max(totalMonitoringPeriod.TotalHours, 1)) * 100):0}% del periodo totale</td></tr>
                <tr><td>Ore monitorate</td><td>{actualHoursToShow} ore ({actualHoursToShow / 24.0:0} giorni)</td></tr>
                <tr><td>N. Record certificati</td><td>{certifiedRecords:N0} su {totalExpectedRecords}</td></tr>
                <tr><td>Percentuale completezza</td><td>{completeness:0}%</td></tr>
                <tr><td>Strategia</td><td>Analisi mensile consistente con context evolutivo</td></tr>
            </table>";
    }

    /// <summary>
    /// 📋 Genera tabella dettagliata dei record certificati
    /// Mostra solo il periodo dal primo record effettivo fino ad oggi (max 30 giorni)
    /// </summary>
    private async Task<string> GenerateDetailedLogTableAsync(int vehicleId, int dataHours)
    {
        var sb = new StringBuilder();

        // Trova il primo record effettivo del veicolo
        var firstRecordTime = await _dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId)
            .OrderBy(vd => vd.Timestamp)
            .Select(vd => vd.Timestamp)
            .FirstOrDefaultAsync();

        // Se non ci sono dati, ritorna messaggio
        if (firstRecordTime == default)
        {
            return "<div class='detailed-log-table'><p class='cert-warning'>⚠️ Nessun dato disponibile per la tabella dettagliata</p></div>";
        }

        // Calcola il periodo effettivo da mostrare
        var now = DateTime.Now;
        var maxStartTime = now.AddHours(-dataHours);
        var effectiveStartTime = firstRecordTime > maxStartTime ? firstRecordTime : maxStartTime;

        // Arrotonda all'ora precedente per avere timestamp puliti
        var startTime = new DateTime(
            effectiveStartTime.Year,
            effectiveStartTime.Month,
            effectiveStartTime.Day,
            effectiveStartTime.Hour,
            0, 0
        );

        // Recupera i record effettivi
        var actualRecords = await _dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId && vd.Timestamp >= startTime)
            .OrderBy(vd => vd.Timestamp)
            .Select(vd => new { vd.Timestamp, vd.IsSmsAdaptiveProfile, vd.RawJsonAnonymized })
            .ToListAsync();

        var recordLookup = actualRecords
            .GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, 0, 0))
            .ToDictionary(g => g.Key, g => g.First());

        // Genera tabella
        sb.AppendLine("<table class='detailed-log-table'>");
        sb.AppendLine("<thead>");
        sb.AppendLine("<tr>");
        sb.AppendLine("<th>Timestamp</th>");
        sb.AppendLine("<th>Adaptive Profile</th>");
        sb.AppendLine("<th>Dati Operativi</th>");
        sb.AppendLine("</tr>");
        sb.AppendLine("</thead>");
        sb.AppendLine("<tbody>");

        // Genera righe solo per il periodo effettivo
        var currentTime = startTime;
        while (currentTime <= now)
        {
            if (recordLookup.TryGetValue(currentTime, out var record))
            {
                var adaptiveProfile = record.IsSmsAdaptiveProfile
                    ? "<span class='detailed-log-badge-success-profile'>Sì</span>"
                    : "<span class='detailed-log-badge-default-profile'>No</span>";

                // Se il record esiste nel database, viene certificato come "Dati operativi raccolti"
                var datiOperativi = "<span class='detailed-log-badge-success-dataValidated'>Dati operativi raccolti</span>";

                var formattedDate = $"{record.Timestamp:dd-MM-yyyy}".Replace("-", "/");
                
                sb.AppendLine("<tr class='record-present'>");
                sb.AppendLine($"<td>{formattedDate} - {record.Timestamp:HH:mm}</td>");
                sb.AppendLine($"<td>{adaptiveProfile}</td>");
                sb.AppendLine($"<td>{datiOperativi}</td>");
                sb.AppendLine("</tr>");
            }
            else
            {
                sb.AppendLine("<tr class='record-missing'>");
                sb.AppendLine($"<td>{currentTime:dd-MM-yyyy} - {currentTime:HH:mm}</td>");
                sb.AppendLine("<td><span class='detailed-log-badge-default-profile'> - </span></td>");
                sb.AppendLine("<td><span class='detailed-log-badge-error-dataValidated'>Record da validare</span></td>");
                sb.AppendLine("</tr>");
            }

            currentTime = currentTime.AddHours(1);
        }

        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");

        return sb.ToString();
    }

    /// <summary>
    /// 📋 Genera sezione HTML con legenda e cards degli utilizzatori ADAPTIVE_PROFILE
    /// Mostra chi ha utilizzato il laboratorio mobile nel periodo di reporting
    /// </summary>
    public async Task<string> GenerateAdaptiveProfileLegendAndCardsAsync(int vehicleId, int dataHours)
    {
        var sb = new StringBuilder();

        // Trova il primo record effettivo del veicolo per calcolare il periodo
        var now = DateTime.Now;
        var maxStartTime = now.AddHours(-dataHours);

        // Recupera tutte le sessioni ADAPTIVE_PROFILE_ON nel periodo
        // IMPORTANTE => Non filtrare su ConsentAccepted perché ai fini di certificazione
        // deve essere incluso chiunque abbia utilizzato la procedura, anche se poi ha revocato
        var adaptiveSessions = await _dbContext.SmsAdaptiveProfile
            .Where(p => p.VehicleId == vehicleId
                    && p.ReceivedAt >= maxStartTime
                    && p.ParsedCommand == Constants.CommonConstants.SmsCommand.ADAPTIVE_PROFILE_ON)
            .OrderByDescending(p => p.ReceivedAt)
            .ToListAsync();

        // Conta quanti record con IsSmsAdaptiveProfile = true nel periodo
        var adaptiveRecordsCount = await _dbContext.VehiclesData
            .Where(vd => vd.VehicleId == vehicleId 
                    && vd.Timestamp >= maxStartTime 
                    && vd.IsSmsAdaptiveProfile)
            .CountAsync();

        // Se non ci sono sessioni ADAPTIVE_PROFILE, ritorna sezione vuota con solo legenda
        sb.AppendLine("<div class='adaptive-profile-section'>");
        
        // --- LEGENDA ---
        sb.AppendLine("<h4 class='adaptive-profile-legend-title'>📖 Legenda Certificazione Utilizzi</h4>");
        sb.AppendLine("<div class='adaptive-profile-legend'>");
        
        sb.AppendLine("<div class='adaptive-legend-item adaptive-legend-yes'>");
        sb.AppendLine("<div class='adaptive-legend-badge'>Adaptive = Si</div>");
        sb.AppendLine("<div class='adaptive-legend-description'>");
        sb.AppendLine("<p>I dati di utilizzo del laboratorio mobile sono certificati anche in relazione all'identità degli utilizzatori terzi, secondo le procedure <strong>ADAPTIVE_GDPR</strong> ed <strong>ADAPTIVE_PROFILE</strong> descritte nel Contratto Principale e nei relativi allegati.</p>");
        sb.AppendLine("<p>In questo caso, per la fascia oraria indicata:</p>");
        sb.AppendLine("<ul>");
        sb.AppendLine("<li>È tracciato l'utilizzatore (o gli utilizzatori) che hanno usato il laboratorio mobile</li>");
        sb.AppendLine("<li>È disponibile la documentazione associata alle procedure eseguite</li>");
        sb.AppendLine("</ul>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='adaptive-legend-item adaptive-legend-no'>");
        sb.AppendLine("<div class='adaptive-legend-badge'>Adaptive = No</div>");
        sb.AppendLine("<div class='adaptive-legend-description'>");
        sb.AppendLine("<p>I dati di utilizzo del laboratorio mobile sono raccolti e certificati a livello tecnico-operativo, in modalità standard.</p>");
        sb.AppendLine("<p>In assenza delle procedure <strong>ADAPTIVE_GDPR</strong> ed <strong>ADAPTIVE_PROFILE</strong>:</p>");
        sb.AppendLine("<ul>");
        sb.AppendLine("<li>Non è certificabile l'uso da parte di utilizzatori terzi specifici</li>");
        sb.AppendLine("<li>Non è disponibile documentazione nominativa riferita a quella specifica finestra temporale</li>");
        sb.AppendLine("</ul>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("</div>"); // chiude .adaptive-profile-legend

        // --- CARDS UTILIZZATORI ---
        if (adaptiveSessions.Any())
        {
            sb.AppendLine("<h4 class='adaptive-profile-cards-title'>👥 Utilizzatori Terzi Autorizzati nel Periodo</h4>");
            sb.AppendLine("<div class='adaptive-profile-cards-container'>");

            // Raggruppa per utilizzatore (stesso numero + nome)
            var userGroups = adaptiveSessions
                .GroupBy(s => new { s.AdaptiveNumber, s.AdaptiveSurnameName })
                .ToList();

            foreach (var userGroup in userGroups)
            {
                var latestSession = userGroup.OrderByDescending(s => s.ReceivedAt).First();
                var sessionsCount = userGroup.Count();
                var firstSession = userGroup.OrderBy(s => s.ReceivedAt).First();

                // Mostra badge solo per "Revocato" (chi ha risposto STOP)
                // Non mostrare badge per Attivo/Scaduto - irrilevante per certificazione storica
                var revokedBadge = !latestSession.ConsentAccepted
                    ? "<span class='adaptive-card-status adaptive-status-revoked'> ⛔ Revocato</span>"
                    : "";

                sb.AppendLine("<div class='adaptive-profile-card'>");

                sb.AppendLine("<div class='adaptive-card-header'>");
                sb.AppendLine($"<span class='adaptive-card-name'>👤 {latestSession.AdaptiveSurnameName}</span>");
                sb.AppendLine(revokedBadge);
                sb.AppendLine("</div>");

                sb.AppendLine("<div class='adaptive-card-body'>");
                sb.AppendLine($"<div class='adaptive-card-row'><span class='adaptive-card-label'>📱 Telefono Utilizzatore</span><span class='adaptive-card-value'>{latestSession.AdaptiveNumber}</span></div>");
                sb.AppendLine($"<div class='adaptive-card-row'><span class='adaptive-card-label'>📅 Prima attivazione</span><span class='adaptive-card-value'>{firstSession.ReceivedAt:dd/MM/yyyy HH:mm}</span></div>");
                sb.AppendLine($"<div class='adaptive-card-row'><span class='adaptive-card-label'>🔄 Ultima attivazione</span><span class='adaptive-card-value'>{latestSession.ReceivedAt:dd/MM/yyyy HH:mm}</span></div>");
                sb.AppendLine($"<div class='adaptive-card-row'><span class='adaptive-card-label'>⏰ Scadenza profilo</span><span class='adaptive-card-value'>{latestSession.ExpiresAt:dd/MM/yyyy HH:mm}</span></div>");
                sb.AppendLine($"<div class='adaptive-card-row'><span class='adaptive-card-label'>🔢 Sessioni totali</span><span class='adaptive-card-value'>{sessionsCount}</span></div>");
                sb.AppendLine("</div>");

                sb.AppendLine("</div>"); // chiude .adaptive-profile-card
            }

            sb.AppendLine("</div>"); // chiude .adaptive-profile-cards-container

            // --- RIEPILOGO STATISTICO ---
            sb.AppendLine("<div class='adaptive-profile-summary-wrapper'>");
            sb.AppendLine("<div class='adaptive-profile-summary'>");
            sb.AppendLine("<h5>📊 Riepilogo ADAPTIVE_PROFILE</h5>");
            sb.AppendLine("<table class='adaptive-summary-table'>");
            sb.AppendLine($"<tr><td>Utilizzatori terzi profilati</td><td>{userGroups.Count}</td></tr>");
            sb.AppendLine($"<tr><td>Sessioni totali nel periodo</td><td>{adaptiveSessions.Count}</td></tr>");
            sb.AppendLine($"<tr><td>Numero records certificati con procedura attiva</td><td>{adaptiveRecordsCount}</td></tr>");
            sb.AppendLine("</table>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
        }
        else
        {
            sb.AppendLine("<div class='adaptive-profile-no-sessions'>");
            sb.AppendLine("<p>ℹ️ Nel periodo analizzato non risultano sessioni ADAPTIVE_PROFILE attive per questo veicolo.</p>");
            sb.AppendLine("<p>Il laboratorio mobile è stato utilizzato esclusivamente da soggetti interni all'azienda oppure non sono state eseguite le procedure ADAPTIVE_GDPR ed ADAPTIVE_PROFILE per eventuali utilizzatori terzi.</p>");
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</div>"); // chiude .adaptive-profile-section

        return sb.ToString();
    }

    /// <summary>
    /// Calcola qualità dataset
    /// </summary>
    private static double CalculateQualityScore(int totalRecords, double uptimePercentage, TimeSpan monitoringPeriod)
    {
        double score = 0;
        
        // 40% - Uptime
        score += (uptimePercentage / 100) * 40;
        
        // 30% - Densità records (migliorata)
        var recordDensity = totalRecords / Math.Max(monitoringPeriod.TotalHours, 1);
        var densityScore = Math.Min(1, recordDensity / 2.0); // ✅ Aumentato da 1.0 a 2.0
        score += densityScore * 30;
        
        // 20% - Stabilità (dinamica basata su continuità)
        var stabilityScore = CalculateStability(monitoringPeriod);
        score += stabilityScore * 20;
        
        // 10% - Maturità dataset
        var totalHours = monitoringPeriod.TotalHours;
        if (totalHours >= MONTHLY_HOURS_THRESHOLD) score += 10;      // 30+ giorni
        else if (totalHours >= WEEKLY_HOURS_THRESHOLD) score += 7;   // 7+ giorni  
        else if (totalHours >= DAILY_HOURS_THRESHOLD) score += 3;    // 1+ giorno
        
        return Math.Max(0, Math.Min(100, score));
    }

    /// <summary>
    /// Calcola stabilità basandosi sulla continuità del monitoraggio
    /// </summary>
    private static double CalculateStability(TimeSpan monitoringPeriod)
    {
        var hours = monitoringPeriod.TotalHours;
        
        // Più ore continuative = maggiore stabilità
        if (hours >= (MONTHLY_HOURS_THRESHOLD * 3)) return 1.0;   // 90+ giorni (3 mesi) = eccellente
        if (hours >= MONTHLY_HOURS_THRESHOLD) return 0.95;        // 30+ giorni (1 mese) = ottimo
        if (hours >= WEEKLY_HOURS_THRESHOLD) return 0.85;         // 7+ giorni (1 settimana) = buono
        if (hours >= (DAILY_HOURS_THRESHOLD * 3)) return 0.75;    // 3+ giorni = discreto
        if (hours >= DAILY_HOURS_THRESHOLD) return 0.60;          // 1+ giorno = sufficiente
        
        return 0.50; // < 24 ore = base
    }

    /// <summary>
    /// Converte score in stelle
    /// </summary>
    private static string GetQualityStars(double score)
    {
        return score switch
        {
            >= 90 => "⭐⭐⭐⭐⭐",
            >= 80 => "⭐⭐⭐⭐⚪",
            >= 70 => "⭐⭐⭐⚪⚪",
            >= 60 => "⭐⭐⚪⚪⚪",
            >= 50 => "⭐⚪⚪⚪⚪",
            _ => "⚪⚪⚪⚪⚪"
        };
    }

    /// <summary>
    /// Etichetta qualitativa
    /// </summary>
    private static string GetQualityLabel(double score)
    {
        return score switch
        {
            >= 90 => "Eccellente",
            >= 80 => "Ottimo",
            >= 70 => "Buono",
            >= 60 => "Discreto",
            >= 50 => "Sufficiente",
            _ => "Migliorabile"
        };
    }
}