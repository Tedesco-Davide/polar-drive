using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PolarDrive.Data.DbContexts;
using static PolarDrive.WebApi.Constants.CommonConstants;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// PolarAiReportGenerator - Pipeline completo digest → writer → renderer
/// Sostituisce completamente la logica precedente con approccio deterministico
/// </summary>
public class PolarAiReportGenerator
{
    private readonly PolarDriveDbContext _dbContext;
    private readonly PolarDriveLogger _logger;
    private readonly VehicleDigestBuilder _digestBuilder;
    private readonly ReportWriter _reportWriter;
    private readonly MarkdownRenderer _markdownRenderer;
    private readonly ZeroDataReportService _zeroDataService;

    public PolarAiReportGenerator(
        PolarDriveDbContext dbContext,
        IOptionsSnapshot<OllamaConfig> ollama,
        HttpClient httpClient)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = new PolarDriveLogger(_dbContext);
        
        // Inizializza i componenti del pipeline
        _digestBuilder = new VehicleDigestBuilder(_dbContext);
        _reportWriter = new ReportWriter(_dbContext, ollama, httpClient);
        _markdownRenderer = new MarkdownRenderer(_dbContext);
        _zeroDataService = new ZeroDataReportService(_dbContext);
    }

    /// <summary>
    /// Metodo principale che genera report intelligenti con pipeline
    /// </summary>
    public async Task<string> GeneratePolarAiInsightsAsync(int vehicleId)
    {
        var source = "PolarAiReportGenerator.GenerateInsights";
        
        await _logger.Info(source, "Avvio nuova pipeline analisi AI", $"VehicleId: {vehicleId}");

        try
        {
            // 1. Calcola periodo di analisi (sempre ultimi 30 giorni)
            var periodEnd = DateTime.UtcNow;
            var periodStart = periodEnd.AddHours(-MONTHLY_HOURS_THRESHOLD); // 720 ore
            
            // Determina livello analisi basato su maturità dataset
            var analysisLevel = await DetermineAnalysisLevelAsync(vehicleId);
            
            await _logger.Info(source, $"Configurazione analisi: {analysisLevel}", 
                $"Periodo: {periodStart:yyyy-MM-dd HH:mm} → {periodEnd:yyyy-MM-dd HH:mm}");

            // 2. Costruisce digest aggregato (TUTTI i calcoli in C#)
            var digest = await _digestBuilder.BuildDigestAsync(vehicleId, periodStart, periodEnd);
            
            // 3. Verifica se ci sono dati sufficienti
            if (digest.TotalSamples == 0)
            {
                await _logger.Warning(source, "Nessun dato disponibile - generazione report zero-data");
                return await _zeroDataService.GenerateZeroDataReportAsync(vehicleId, periodStart, periodEnd);
            }

            // 4. Genera contenuto narrativo tramite LLM (solo JSON strutturato)
            ReportOutputSchema reportContent;
            
            try
            {
                reportContent = await _reportWriter.GenerateReportJsonAsync(digest, analysisLevel);
                await _logger.Info(source, "Contenuto narrativo generato tramite LLM");
            }
            catch (Exception ex)
            {
                await _logger.Warning(source, "LLM non disponibile - utilizzo fallback", ex.Message);
                reportContent = _reportWriter.GenerateFallbackReport(digest, analysisLevel);
            }

            // 5. Rendering finale in Markdown (template fissi + contenuto dinamico)
            var finalMarkdown = await _markdownRenderer.RenderMarkdownReportAsync(
                reportContent, digest, analysisLevel);

            await _logger.Info(source, "Pipeline completato con successo", 
                $"Output size: {finalMarkdown.Length} chars, Samples: {digest.TotalSamples}");

            return finalMarkdown;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore fatale nel pipeline", ex.ToString());
            
            // Ultimo fallback: report di errore professionale
            return await GenerateErrorReportAsync(vehicleId, ex.Message);
        }
    }

    /// <summary>
    /// Determina il livello di analisi basato sulla maturità del dataset
    /// </summary>
    private async Task<string> DetermineAnalysisLevelAsync(int vehicleId)
    {
        try
        {
            var firstRecord = await _dbContext.VehiclesData
                .Where(vd => vd.VehicleId == vehicleId)
                .OrderBy(vd => vd.Timestamp)
                .Select(vd => vd.Timestamp)
                .FirstOrDefaultAsync();

            if (firstRecord == default)
                return "Valutazione Iniziale";

            var totalMonitoringPeriod = DateTime.UtcNow - firstRecord;

            return totalMonitoringPeriod.TotalDays switch
            {
                < 1 => "Valutazione Iniziale",
                < 7 => "Analisi Settimanale", 
                < 30 => "Deep Dive Mensile",
                < 90 => "Assessment Trimestrale",
                _ => "Analisi Comprensiva"
            };
        }
        catch
        {
            return "Valutazione Iniziale";
        }
    }

    /// <summary>
    /// Genera report di errore professionale per fallimenti catastrofici
    /// </summary>
    private async Task<string> GenerateErrorReportAsync(int vehicleId, string errorMessage)
    {
        var source = "PolarAiReportGenerator.GenerateErrorReport";
        
        await _logger.Error(source, "Generazione report di errore", errorMessage);

        var sb = new StringBuilder();
        
        sb.AppendLine("# ⚠️ Report di Sistema - Errore Temporaneo");
        sb.AppendLine();
        sb.AppendLine($"**Timestamp:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Veicolo ID:** {vehicleId}");
        sb.AppendLine($"**Stato:** Sistema temporaneamente non disponibile");
        sb.AppendLine();

        sb.AppendLine("## 🔧 Informazioni Tecniche");
        sb.AppendLine();
        sb.AppendLine("Il sistema di analisi intelligente ha riscontrato un problema tecnico temporaneo. ");
        sb.AppendLine("I nostri tecnici sono stati automaticamente notificati e stanno lavorando per ");
        sb.AppendLine("ripristinare il servizio nel più breve tempo possibile.");
        sb.AppendLine();

        sb.AppendLine("## 💡 Azioni Raccomandate");
        sb.AppendLine();
        sb.AppendLine("- **Riprova tra 15-30 minuti** - Il problema potrebbe risolversi automaticamente");
        sb.AppendLine("- **Verifica connessione** - Assicurati che il veicolo sia connesso");
        sb.AppendLine("- **Contatta supporto** - Se il problema persiste oltre 1 ora");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("**DataPolar S.R.L.S.** - Startup Innovativa");
        sb.AppendLine();
        sb.AppendLine("*Sistema PolarDrive™ in modalità di ripristino*");

        return sb.ToString();
    }

    /// <summary>
    /// Metodo per testing e validazione del pipeline
    /// </summary>
    public async Task<PipelineTestResult> TestPipelineAsync(int vehicleId)
    {
        var result = new PipelineTestResult { VehicleId = vehicleId };
        
        try
        {
            // Test 1: Digest Builder
            var periodEnd = DateTime.UtcNow;
            var periodStart = periodEnd.AddHours(-MONTHLY_HOURS_THRESHOLD);
            
            var digest = await _digestBuilder.BuildDigestAsync(vehicleId, periodStart, periodEnd);
            result.DigestGenerationSuccess = true;
            result.SamplesFound = digest.TotalSamples;
            result.QualityScore = digest.Quality.QualityScore;

            // Test 2: Report Writer (solo se ci sono dati)
            if (digest.TotalSamples > 0)
            {
                try
                {
                    var reportContent = await _reportWriter.GenerateReportJsonAsync(digest, "Test Analysis");
                    result.LlmGenerationSuccess = true;
                    result.LlmGeneratedContent = !string.IsNullOrEmpty(reportContent.ExecutiveSummary?.CurrentStatus);
                }
                catch
                {
                    result.LlmGenerationSuccess = false;
                    // Test fallback
                    var fallback = _reportWriter.GenerateFallbackReport(digest, "Test Analysis");
                    result.FallbackWorking = !string.IsNullOrEmpty(fallback.ExecutiveSummary?.CurrentStatus);
                }
            }

            // Test 3: Markdown Renderer
            var testReport = new ReportOutputSchema
            {
                ExecutiveSummary = new ExecutiveSummary 
                { 
                    CurrentStatus = "Test status",
                    KeyFindings = new List<string> { "Test finding 1", "Test finding 2" },
                    CriticalAlerts = new List<string>(),
                    OverallTrend = "Test trend"
                },
                TechnicalAnalysis = new TechnicalAnalysis
                {
                    BatteryAssessment = "Test battery",
                    ChargingBehavior = "Test charging", 
                    DrivingPatterns = "Test driving",
                    EfficiencyAnalysis = "Test efficiency"
                },
                PredictiveInsights = new PredictiveInsights
                {
                    NextMonthPrediction = "Test prediction",
                    RiskFactors = new List<string>(),
                    OpportunityAreas = new List<string>(),
                    MaintenanceRecommendations = "Test maintenance"
                },
                Recommendations = new Recommendations
                {
                    ImmediateActions = new List<ActionItem> 
                    { 
                        new() { Action = "Test action", Benefit = "Test benefit", Timeline = "Test timeline" }
                    },
                    MediumTermActions = new List<ActionItem>(),
                    OptimizationStrategy = "Test strategy"
                }
            };

            var markdown = await _markdownRenderer.RenderMarkdownReportAsync(testReport, digest, "Test Analysis");
            result.MarkdownGenerationSuccess = !string.IsNullOrEmpty(markdown) && markdown.Contains("Test status");

            result.OverallSuccess = result.DigestGenerationSuccess && result.MarkdownGenerationSuccess;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.OverallSuccess = false;
        }

        return result;
    }
}

/// <summary>
/// Risultato del test del pipeline per diagnostica
/// </summary>
public class PipelineTestResult
{
    public int VehicleId { get; set; }
    public bool OverallSuccess { get; set; }
    public bool DigestGenerationSuccess { get; set; }
    public bool LlmGenerationSuccess { get; set; }
    public bool FallbackWorking { get; set; }
    public bool MarkdownGenerationSuccess { get; set; }
    public int SamplesFound { get; set; }
    public int QualityScore { get; set; }
    public bool LlmGeneratedContent { get; set; }
    public string? ErrorMessage { get; set; }
}