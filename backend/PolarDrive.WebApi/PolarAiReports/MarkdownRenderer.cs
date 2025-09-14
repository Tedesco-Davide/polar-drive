using System.Text;
using PolarDrive.Data.DbContexts;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Renderer che converte JSON strutturato in Markdown pulito
/// Template fissi per compliance + contenuto dinamico da JSON
/// </summary>
public class MarkdownRenderer(PolarDriveDbContext dbContext)
{
    private readonly PolarDriveLogger _logger = new(dbContext);

    public async Task<string> RenderMarkdownReportAsync(
        ReportOutputSchema reportData, 
        VehicleDataDigest digest, 
        string analysisLevel)
    {
        var source = "MarkdownRenderer.RenderMarkdownReport";
        
        await _logger.Info(source, "Inizio rendering Markdown", 
            $"AnalysisLevel: {analysisLevel}");

        var sb = new StringBuilder();

        // Header del report
        RenderReportHeader(sb, digest, analysisLevel);
        
        // Certificazione DataPolar (template fisso)
        RenderDataPolarCertification(sb, digest);
        
        // Contenuto dinamico dal JSON
        RenderExecutiveSummary(sb, reportData.ExecutiveSummary);
        RenderTechnicalAnalysis(sb, reportData.TechnicalAnalysis, digest);
        RenderPredictiveInsights(sb, reportData.PredictiveInsights);
        RenderRecommendations(sb, reportData.Recommendations);
        
        // Footer legale (template fisso)
        RenderLegalFooter(sb);

        var markdown = sb.ToString();
        
        await _logger.Info(source, "Rendering completato", 
            $"Markdown size: {markdown.Length} chars");

        return markdown;
    }

    private void RenderReportHeader(StringBuilder sb, VehicleDataDigest digest, string analysisLevel)
    {
        sb.AppendLine("# 🚗 Analisi intelligente PolarAi");
        sb.AppendLine();
        sb.AppendLine($"**Periodo di analisi:** {digest.PeriodStart:yyyy-MM-dd} → {digest.PeriodEnd:yyyy-MM-dd}");
        sb.AppendLine($"**Livello di analisi:** {analysisLevel}");
        sb.AppendLine($"**Campioni analizzati:** {digest.TotalSamples:N0}");
        sb.AppendLine($"**Qualità dati:** {digest.Quality.QualityLabel} ({digest.Quality.QualityScore}/100)");
        sb.AppendLine();
    }

    private void RenderDataPolarCertification(StringBuilder sb, VehicleDataDigest digest)
    {
        sb.AppendLine("## 🏆 Certificazione Dati DataPolar");
        sb.AppendLine();
        sb.AppendLine("### 📊 Statistiche Generali");
        sb.AppendLine();
        sb.AppendLine("| Metrica | Valore |");
        sb.AppendLine("|---------|--------|");
        sb.AppendLine($"| Ore totali certificate | {digest.TotalHours} ore ({digest.TotalHours/24:F1} giorni) |");
        sb.AppendLine($"| Uptime raccolta | {digest.Quality.UptimePercentage:F1}% |");
        sb.AppendLine($"| Qualità dataset | {GetQualityStars(digest.Quality.QualityScore)} ({digest.Quality.QualityLabel}) |");
        sb.AppendLine($"| Records totali | {digest.TotalSamples:N0} |");
        sb.AppendLine($"| Frequenza media | {digest.Quality.SamplingFrequency:F1} campioni/ora |");
        sb.AppendLine();
        
        sb.AppendLine("### 📊 Statistiche Analisi Mensile");
        sb.AppendLine();
        sb.AppendLine("| Metrica | Valore |");
        sb.AppendLine("|---------|--------|");
        sb.AppendLine($"| Durata monitoraggio totale | {digest.TotalHours/24:F1} giorni |");
        sb.AppendLine($"| Campioni mensili analizzati | {digest.TotalSamples:N0} |");
        sb.AppendLine($"| Finestra UNIFICATA | 720 ore (30 giorni) |");
        sb.AppendLine($"| Densità dati mensile | {digest.Quality.SamplingFrequency:F1} campioni/ora |");
        sb.AppendLine($"| Strategia | Analisi mensile consistente con context evolutivo |");
        sb.AppendLine();
    }

    private void RenderExecutiveSummary(StringBuilder sb, ExecutiveSummary summary)
    {
        sb.AppendLine("## 🎯 Executive Summary");
        sb.AppendLine();
        sb.AppendLine($"**Stato attuale:** {summary.CurrentStatus}");
        sb.AppendLine();
        sb.AppendLine($"**Trend generale:** {summary.OverallTrend}");
        sb.AppendLine();

        if (summary.KeyFindings?.Any() == true)
        {
            sb.AppendLine("### 📋 Risultati Chiave");
            sb.AppendLine();
            foreach (var finding in summary.KeyFindings.Take(4))
            {
                sb.AppendLine($"- {finding}");
            }
            sb.AppendLine();
        }

        if (summary.CriticalAlerts?.Any() == true)
        {
            sb.AppendLine("### ⚠️ Alert Critici");
            sb.AppendLine();
            foreach (var alert in summary.CriticalAlerts.Take(2))
            {
                sb.AppendLine($"- 🚨 {alert}");
            }
            sb.AppendLine();
        }
    }

    private void RenderTechnicalAnalysis(StringBuilder sb, TechnicalAnalysis analysis, VehicleDataDigest digest)
    {
        sb.AppendLine("## 🔧 Analisi Tecnica");
        sb.AppendLine();

        // Batteria
        sb.AppendLine("### 🔋 Analisi Batteria");
        sb.AppendLine();
        sb.AppendLine($"{analysis.BatteryAssessment}");
        sb.AppendLine();
        RenderBatteryMetricsTable(sb, digest.Battery);

        // Ricarica
        sb.AppendLine("### ⚡ Comportamento Ricarica");
        sb.AppendLine();
        sb.AppendLine($"{analysis.ChargingBehavior}");
        sb.AppendLine();
        RenderChargingMetricsTable(sb, digest.Charging);

        // Guida
        sb.AppendLine("### 🛣️ Pattern di Guida");
        sb.AppendLine();
        sb.AppendLine($"{analysis.DrivingPatterns}");
        sb.AppendLine();
        RenderDrivingMetricsTable(sb, digest.Driving);

        // Efficienza
        sb.AppendLine("### 📊 Analisi Efficienza");
        sb.AppendLine();
        sb.AppendLine($"{analysis.EfficiencyAnalysis}");
        sb.AppendLine();
        RenderEfficiencyMetricsTable(sb, digest.Efficiency);
    }

    private void RenderBatteryMetricsTable(StringBuilder sb, BatteryMetrics battery)
    {
        sb.AppendLine("| Metrica | Valore |");
        sb.AppendLine("|---------|--------|");
        sb.AppendLine($"| Livello medio | {battery.AvgLevel:F1}% |");
        sb.AppendLine($"| Range livello | {battery.MinLevel}% - {battery.MaxLevel}% |");
        sb.AppendLine($"| Autonomia media | {battery.AvgRange:F1} km |");
        sb.AppendLine($"| Range autonomia | {battery.MinRange:F1} - {battery.MaxRange:F1} km |");
        sb.AppendLine($"| Cicli di ricarica | {battery.ChargeCycles} |");
        sb.AppendLine($"| Health Score | {battery.HealthScore:F1}/100 |");
        sb.AppendLine();
    }

    private void RenderChargingMetricsTable(StringBuilder sb, ChargingMetrics charging)
    {
        sb.AppendLine("| Metrica | Valore |");
        sb.AppendLine("|---------|--------|");
        sb.AppendLine($"| Sessioni totali | {charging.TotalSessions} |");
        sb.AppendLine($"| Energia aggiunta | {charging.TotalEnergyAdded:F1} kWh |");
        sb.AppendLine($"| Costo totale | €{charging.TotalCost:F2} |");
        sb.AppendLine($"| Costo medio/kWh | €{charging.AvgCostPerKwh:F3} |");
        sb.AppendLine($"| Range costi | €{charging.MinCostPerKwh:F3} - €{charging.MaxCostPerKwh:F3} |");
        sb.AppendLine($"| Durata media sessione | {charging.AvgSessionDuration:F0} minuti |");
        sb.AppendLine($"| Ricarica domestica | {charging.HomeChargingPercentage:F1}% |");
        
        if (charging.TopStations?.Any() == true)
        {
            sb.AppendLine($"| Stazioni principali | {string.Join(", ", charging.TopStations)} |");
        }
        sb.AppendLine();
    }

    private void RenderDrivingMetricsTable(StringBuilder sb, DrivingMetrics driving)
    {
        sb.AppendLine("| Metrica | Valore |");
        sb.AppendLine("|---------|--------|");
        sb.AppendLine($"| Distanza totale | {driving.TotalDistance:F1} km |");
        sb.AppendLine($"| Velocità media | {driving.AvgSpeed:F1} km/h |");
        sb.AppendLine($"| Velocità massima | {driving.MaxSpeed:F1} km/h |");
        sb.AppendLine($"| Consumo medio | {driving.AvgPowerConsumption:F0} W |");
        sb.AppendLine($"| Energia rigenerata | {driving.RegenerationEnergy:F1} kWh |");
        sb.AppendLine($"| Numero viaggi | {driving.TripsCount} |");
        sb.AppendLine($"| Distanza media viaggio | {driving.AvgTripDistance:F1} km |");
        
        if (driving.DirectionDistribution?.Any() == true)
        {
            var directions = string.Join(", ", driving.DirectionDistribution.Select(d => $"{d.Key}: {d.Value}%"));
            sb.AppendLine($"| Distribuzione direzioni | {directions} |");
        }
        sb.AppendLine();
    }

    private void RenderEfficiencyMetricsTable(StringBuilder sb, EfficiencyMetrics efficiency)
    {
        sb.AppendLine("| Metrica | Valore |");
        sb.AppendLine("|---------|--------|");
        sb.AppendLine($"| Efficienza complessiva | {efficiency.OverallEfficiency:F1} km/kWh |");
        sb.AppendLine($"| Efficienza città | {efficiency.CityEfficiency:F1} km/kWh |");
        sb.AppendLine($"| Efficienza autostrada | {efficiency.HighwayEfficiency:F1} km/kWh |");
        sb.AppendLine($"| Velocità ottimale | {efficiency.OptimalSpeedRange:F0} km/h |");
        
        if (efficiency.EfficiencyTips?.Any() == true)
        {
            sb.AppendLine($"| Suggerimenti | {string.Join("; ", efficiency.EfficiencyTips)} |");
        }
        sb.AppendLine();
    }

    private void RenderPredictiveInsights(StringBuilder sb, PredictiveInsights insights)
    {
        sb.AppendLine("## 🔮 Insights Predittivi");
        sb.AppendLine();
        
        sb.AppendLine("### 📈 Previsioni Prossimo Mese");
        sb.AppendLine();
        sb.AppendLine($"{insights.NextMonthPrediction}");
        sb.AppendLine();

        if (insights.RiskFactors?.Any() == true)
        {
            sb.AppendLine("### ⚠️ Fattori di Rischio");
            sb.AppendLine();
            foreach (var risk in insights.RiskFactors.Take(3))
            {
                sb.AppendLine($"- 🔴 {risk}");
            }
            sb.AppendLine();
        }

        if (insights.OpportunityAreas?.Any() == true)
        {
            sb.AppendLine("### 🎯 Aree di Opportunità");
            sb.AppendLine();
            foreach (var opportunity in insights.OpportunityAreas.Take(3))
            {
                sb.AppendLine($"- 🟢 {opportunity}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(insights.MaintenanceRecommendations))
        {
            sb.AppendLine("### 🔧 Raccomandazioni Manutenzione");
            sb.AppendLine();
            sb.AppendLine($"{insights.MaintenanceRecommendations}");
            sb.AppendLine();
        }
    }

    private void RenderRecommendations(StringBuilder sb, Recommendations recommendations)
    {
        sb.AppendLine("## 💡 Raccomandazioni");
        sb.AppendLine();

        if (recommendations.ImmediateActions?.Any() == true)
        {
            sb.AppendLine("### 🚀 Azioni Immediate");
            sb.AppendLine();
            sb.AppendLine("| Azione | Beneficio | Timeline |");
            sb.AppendLine("|--------|-----------|----------|");
            
            foreach (var action in recommendations.ImmediateActions.Take(3))
            {
                sb.AppendLine($"| {action.Action} | {action.Benefit} | {action.Timeline} |");
            }
            sb.AppendLine();
        }

        if (recommendations.MediumTermActions?.Any() == true)
        {
            sb.AppendLine("### 📅 Azioni a Medio Termine");
            sb.AppendLine();
            sb.AppendLine("| Azione | Beneficio | Timeline |");
            sb.AppendLine("|--------|-----------|----------|");
            
            foreach (var action in recommendations.MediumTermActions.Take(3))
            {
                sb.AppendLine($"| {action.Action} | {action.Benefit} | {action.Timeline} |");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(recommendations.OptimizationStrategy))
        {
            sb.AppendLine("### 🎯 Strategia di Ottimizzazione");
            sb.AppendLine();
            sb.AppendLine($"{recommendations.OptimizationStrategy}");
            sb.AppendLine();
        }
    }

    private void RenderLegalFooter(StringBuilder sb)
    {
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 📋 Note Legali e Compliance");
        sb.AppendLine();
        sb.AppendLine("### 🔒 Privacy e Protezione Dati");
        sb.AppendLine();
        sb.AppendLine("Questo report è generato nel rispetto del **GDPR** e delle normative sulla privacy. ");
        sb.AppendLine("I dati del veicolo sono processati esclusivamente per finalità di analisi e ottimizzazione, ");
        sb.AppendLine("senza accesso a informazioni personali identificabili.");
        sb.AppendLine();
        
        sb.AppendLine("### ⚖️ Conformità Tesla Fleet API");
        sb.AppendLine();
        sb.AppendLine("L'accesso ai dati del veicolo avviene tramite **Tesla Fleet API** in conformità ai ");
        sb.AppendLine("termini di servizio Tesla. Tutti i dati sono utilizzati esclusivamente per le ");
        sb.AppendLine("funzionalità autorizzate dall'utente.");
        sb.AppendLine();
        
        sb.AppendLine("### 🏢 DataPolar - Startup Innovativa");
        sb.AppendLine();
        sb.AppendLine("**DataPolar S.R.L.S.** - Startup Innovativa iscritta nella sezione speciale ");
        sb.AppendLine("del Registro delle Imprese. Sviluppo, produzione e commercializzazione di ");
        sb.AppendLine("prodotti e servizi innovativi ad alto valore tecnologico per la mobilità elettrificata.");
        sb.AppendLine();
        sb.AppendLine("*Report generato da PolarDrive™ • The future of AI*");
    }

    private string GetQualityStars(int score) => score switch
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
/// Servizio per generare report anche quando non ci sono dati
/// Garantisce sempre un output professionale
/// </summary>
public class ZeroDataReportService(PolarDriveDbContext dbContext)
{
    private readonly PolarDriveLogger _logger = new(dbContext);

    public async Task<string> GenerateZeroDataReportAsync(int vehicleId, DateTime periodStart, DateTime periodEnd)
    {
        var source = "ZeroDataReportService.GenerateZeroDataReport";
        
        await _logger.Info(source, "Generazione report zero-data", 
            $"VehicleId: {vehicleId}, Period: {periodStart:yyyy-MM-dd} → {periodEnd:yyyy-MM-dd}");

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# 🚗 Analisi intelligente PolarAi");
        sb.AppendLine();
        sb.AppendLine($"**Periodo di analisi:** {periodStart:yyyy-MM-dd} → {periodEnd:yyyy-MM-dd}");
        sb.AppendLine($"**Livello di analisi:** Analisi Base");
        sb.AppendLine($"**Campioni analizzati:** 0");
        sb.AppendLine($"**Stato:** Nessun dato disponibile");
        sb.AppendLine();

        // Certificazione base
        sb.AppendLine("## 🏆 Certificazione Dati DataPolar");
        sb.AppendLine();
        sb.AppendLine("### 📊 Stato Raccolta Dati");
        sb.AppendLine();
        sb.AppendLine("| Metrica | Stato |");
        sb.AppendLine("|---------|-------|");
        sb.AppendLine("| Records disponibili | 0 |");
        sb.AppendLine("| Copertura periodo | 0% |");
        sb.AppendLine("| Stato connessione | Da verificare |");
        sb.AppendLine("| Qualità dataset | Non disponibile |");
        sb.AppendLine();

        // Informazioni diagnostiche
        sb.AppendLine("## 🔍 Informazioni Diagnostiche");
        sb.AppendLine();
        sb.AppendLine("### 📋 Possibili Cause");
        sb.AppendLine();
        sb.AppendLine("- **Connessione veicolo:** Verificare che il veicolo sia connesso a Internet");
        sb.AppendLine("- **Autorizzazioni API:** Controllare i permessi Tesla Fleet API");
        sb.AppendLine("- **Periodo selezione:** Il periodo potrebbe non avere dati di telemetria");
        sb.AppendLine("- **Stato veicolo:** Il veicolo potrebbe essere stato spento o in modalità sleep");
        sb.AppendLine();

        // Azioni raccomandate
        sb.AppendLine("## 💡 Azioni Raccomandate");
        sb.AppendLine();
        sb.AppendLine("### 🚀 Passi Immediati");
        sb.AppendLine();
        sb.AppendLine("| Azione | Descrizione | Timeline |");
        sb.AppendLine("|--------|-------------|----------|");
        sb.AppendLine("| Verifica connessione | Controllare connettività veicolo | Immediato |");
        sb.AppendLine("| Controllo autorizzazioni | Verificare permessi Tesla API | 5-10 minuti |");
        sb.AppendLine("| Test raccolta dati | Avviare una sessione di test | 1-2 ore |");
        sb.AppendLine();

        // Footer legale ridotto
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 📋 Note Tecniche");
        sb.AppendLine();
        sb.AppendLine("**DataPolar S.R.L.S.** - Startup Innovativa specializzata in soluzioni ");
        sb.AppendLine("per la mobilità elettrificata. Per supporto tecnico, contattare il team.");
        sb.AppendLine();
        sb.AppendLine("*Report diagnostico generato da PolarDrive™*");

        var markdown = sb.ToString();
        
        await _logger.Info(source, "Report zero-data generato", 
            $"Markdown size: {markdown.Length} chars");

        return markdown;
    }
}