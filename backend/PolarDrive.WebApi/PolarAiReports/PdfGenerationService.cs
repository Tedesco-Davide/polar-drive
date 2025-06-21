using System.Diagnostics;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Servizio semplificato dedicato SOLO alla conversione HTML -> PDF
/// La generazione HTML è gestita da HtmlReportService
/// </summary>
public class PdfGenerationService(PolarDriveDbContext dbContext)
{
    private readonly PolarDriveLogger _logger = new(dbContext);

    /// <summary>
    /// Converte HTML in PDF usando Puppeteer/Node.js
    /// </summary>
    public async Task<byte[]> ConvertHtmlToPdfAsync(string htmlContent, PdfReport report, PdfConversionOptions? options = null)
    {
        var source = "PdfGenerationService.ConvertHtmlToPdf";
        options ??= new PdfConversionOptions();

        try
        {
            await _logger.Info(source, "Inizio conversione HTML -> PDF",
                $"ReportId: {report.Id}, HTML size: {htmlContent.Length} chars");

            // 1. Salva HTML temporaneo
            var tempHtmlPath = await SaveTemporaryHtmlAsync(htmlContent, report.Id);

            // 2. Prepara path di output
            var outputPdfPath = GetOutputPdfPath(report);

            // 3. Controlla disponibilità Node.js/NPX
            if (!IsNodeJsAvailable())
            {
                await _logger.Warning(source, "Node.js/NPX non disponibile, salvo come HTML",
                    $"ReportId: {report.Id}");
                return await SaveAsHtmlFallback(htmlContent, report, source);
            }

            // 4. Converti con Puppeteer
            var pdfBytes = await ConvertWithPuppeteerAsync(tempHtmlPath, outputPdfPath, options);

            // 5. Cleanup
            await CleanupTemporaryFiles(tempHtmlPath);

            await _logger.Info(source, "Conversione PDF completata",
                $"ReportId: {report.Id}, PDF size: {pdfBytes.Length} bytes");

            return pdfBytes;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore conversione PDF",
                $"ReportId: {report.Id}, Error: {ex.Message}");

            // Fallback: salva come HTML
            return await SaveAsHtmlFallback(htmlContent, report, source);
        }
    }

    /// <summary>
    /// Salva HTML in file temporaneo
    /// </summary>
    private async Task<string> SaveTemporaryHtmlAsync(string htmlContent, int reportId)
    {
        var tempDir = Path.GetTempPath();
        var htmlPath = Path.Combine(tempDir, $"PolarDrive_Report_{reportId}_{DateTime.UtcNow.Ticks}.html");

        await File.WriteAllTextAsync(htmlPath, htmlContent);

        await _logger.Debug("PdfGenerationService.SaveTemporaryHtml",
            "HTML temporaneo salvato", $"Path: {htmlPath}");

        return htmlPath;
    }

    /// <summary>
    /// Determina il path di output per il PDF
    /// </summary>
    private string GetOutputPdfPath(PdfReport report)
    {
        var outputDir = Path.Combine("storage", "reports",
            report.ReportPeriodStart.Year.ToString(),
            report.ReportPeriodStart.Month.ToString("D2"));

        Directory.CreateDirectory(outputDir);

        return Path.Combine(outputDir, $"PolarDrive_Report_{report.Id}.pdf");
    }

    /// <summary>
    /// Controlla se Node.js/NPX è disponibile
    /// </summary>
    private bool IsNodeJsAvailable()
    {
        try
        {
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            var npxPath = Path.Combine(programFiles, "nodejs", "npx.cmd");

            return File.Exists(npxPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Modifica anche ConvertWithPuppeteerAsync per usare la directory del progetto
    /// </summary>
    private async Task<byte[]> ConvertWithPuppeteerAsync(string htmlPath, string pdfPath, PdfConversionOptions options)
    {
        var source = "PdfGenerationService.ConvertWithPuppeteer";
        const int maxRetries = 2;
        const int timeoutSeconds = 90;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _logger.Info(source, $"Tentativo {attempt}/{maxRetries} conversione Puppeteer");

                var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
                var nodePath = Path.Combine(programFiles, "nodejs", "node.exe");

                // Script Puppeteer portabile
                var puppeteerScript = GenerateOptimizedPuppeteerScript(options);

                // ✅ SALVA LO SCRIPT NELLA DIRECTORY DEL PROGETTO, NON IN TEMP
                var projectDirectory = FindProjectDirectory();
                var scriptPath = Path.Combine(projectDirectory, $"temp_puppeteer_script_{DateTime.UtcNow.Ticks}_attempt{attempt}.js");
                await File.WriteAllTextAsync(scriptPath, puppeteerScript);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = nodePath,
                        Arguments = $"\"{scriptPath}\" \"{htmlPath}\" \"{pdfPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = projectDirectory  // ✅ WORKING DIRECTORY CORRETTO
                    }
                };

                await _logger.Info(source, "Avvio conversione Puppeteer",
                    $"Attempt: {attempt}, WorkingDir: {projectDirectory}, Script: {scriptPath}");

                process.Start();

                // Timeout management
                var processTask = Task.Run(() => process.WaitForExit());
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));

                var completedTask = await Task.WhenAny(processTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    await _logger.Warning(source, $"Timeout tentativo {attempt} ({timeoutSeconds}s)");

                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            await process.WaitForExitAsync();
                        }
                    }
                    catch (Exception killEx)
                    {
                        await _logger.Debug(source, "Errore terminazione processo", killEx.ToString());
                    }

                    // Cleanup e retry
                    try { File.Delete(scriptPath); } catch { }

                    if (attempt == maxRetries)
                    {
                        throw new TimeoutException($"PDF conversion failed after {maxRetries} attempts with {timeoutSeconds}s timeout each");
                    }

                    await Task.Delay(2000);
                    continue;
                }

                // Leggi output per debug
                var stdout = await process.StandardOutput.ReadToEndAsync();
                var stderr = await process.StandardError.ReadToEndAsync();

                await _logger.Debug(source, "Output Puppeteer", $"Stdout:\n{stdout}");
                if (!string.IsNullOrEmpty(stderr))
                {
                    await _logger.Debug(source, "Error Puppeteer", $"Stderr: {stderr}");
                }

                // Controlla risultato
                if (File.Exists(pdfPath))
                {
                    var pdfBytes = await File.ReadAllBytesAsync(pdfPath);
                    await _logger.Info(source, $"Conversione riuscita al tentativo {attempt}",
                        $"PDF size: {pdfBytes.Length} bytes");

                    // Cleanup
                    try { File.Delete(scriptPath); } catch { }

                    return pdfBytes;
                }
                else
                {
                    await _logger.Warning(source, $"Tentativo {attempt} fallito - file non creato",
                        $"ExitCode: {process.ExitCode}, Stdout: {stdout}, Stderr: {stderr}");

                    // Cleanup e retry
                    try { File.Delete(scriptPath); } catch { }

                    if (attempt == maxRetries)
                    {
                        throw new InvalidOperationException($"PDF file not created after {maxRetries} attempts. Last error: {stderr}");
                    }

                    await Task.Delay(2000);
                }
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                await _logger.Warning(source, $"Tentativo {attempt} fallito con eccezione", ex.Message);
                await Task.Delay(2000);
            }
        }

        throw new InvalidOperationException($"PDF conversion failed after {maxRetries} attempts");
    }

    /// <summary>
    /// Trova la directory del progetto in modo portabile
    /// </summary>
    private string FindProjectDirectory()
    {
        // Inizia dalla directory corrente
        var currentDir = Directory.GetCurrentDirectory();

        // Cerca verso l'alto per package.json
        while (!string.IsNullOrEmpty(currentDir))
        {
            var packageJsonPath = Path.Combine(currentDir, "package.json");
            if (File.Exists(packageJsonPath))
            {
                return currentDir;
            }

            // Cerca anche per .csproj o .sln (indicatori di progetto .NET)
            var csprojFiles = Directory.GetFiles(currentDir, "*.csproj");
            var slnFiles = Directory.GetFiles(currentDir, "*.sln");

            if (csprojFiles.Length > 0 || slnFiles.Length > 0)
            {
                // Se c'è anche node_modules, questa è probabilmente la directory giusta
                var nodeModulesPath = Path.Combine(currentDir, "node_modules");
                if (Directory.Exists(nodeModulesPath))
                {
                    return currentDir;
                }
            }

            var parentDir = Directory.GetParent(currentDir);
            if (parentDir == null)
                break;

            currentDir = parentDir.FullName;
        }

        // Fallback: directory corrente
        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Script Puppeteer
    /// </summary>
    private string GenerateOptimizedPuppeteerScript(PdfConversionOptions options)
    {
        return $@"
// ✅ FORZA L'USO DI CHROME DI SISTEMA
const path = require('path');
const fs = require('fs');

console.log('🔍 Using system Chrome instead of Puppeteer download...');

// Lista di possibili path di Chrome nel sistema
const systemChromePaths = [
    'C:\\\\Program Files\\\\Google\\\\Chrome\\\\Application\\\\chrome.exe',
    'C:\\\\Program Files (x86)\\\\Google\\\\Chrome\\\\Application\\\\chrome.exe',
    'C:\\\\Users\\\\' + (process.env.USERNAME || 'Default') + '\\\\AppData\\\\Local\\\\Google\\\\Chrome\\\\Application\\\\chrome.exe',
    'C:\\\\Program Files\\\\Chromium\\\\Application\\\\chrome.exe',
    'C:\\\\Program Files (x86)\\\\Microsoft\\\\Edge\\\\Application\\\\msedge.exe'
];

// Trova Chrome nel sistema
function findSystemChrome() {{
    for (const chromePath of systemChromePaths) {{
        if (fs.existsSync(chromePath)) {{
            console.log(`✅ Found system Chrome: ${{chromePath}}`);
            return chromePath;
        }}
    }}
    throw new Error('❌ No Chrome browser found in system');
}}

// ✅ USA PUPPETEER-CORE INVECE DI PUPPETEER COMPLETO
let puppeteer;
try {{
    // Prova prima puppeteer normale
    puppeteer = require('puppeteer');
    console.log('✅ Using full Puppeteer');
}} catch (err1) {{
    try {{
        // Fallback a puppeteer-core
        puppeteer = require('puppeteer-core');
        console.log('✅ Using Puppeteer-core');
    }} catch (err2) {{
        console.error('💥 Neither puppeteer nor puppeteer-core found!');
        console.error('Install with: npm install puppeteer');
        process.exit(1);
    }}
}}

(async () => {{
  const [htmlPath, pdfPath] = process.argv.slice(2);
  
  if (!htmlPath || !pdfPath) {{
    console.error('Usage: node script.js <htmlPath> <pdfPath>');
    process.exit(1);
  }}
  
  console.log(`Starting PDF conversion:`);
  console.log(`  HTML: ${{htmlPath}}`);
  console.log(`  PDF:  ${{pdfPath}}`);
  
  let browser;
  try {{
    console.log('🚀 Launching browser...');
    
    const launchOptions = {{
      headless: true,
      args: [
        '--no-sandbox',
        '--disable-dev-shm-usage', 
        '--disable-gpu',
        '--disable-web-security',
        '--disable-features=TranslateUI',
        '--disable-ipc-flooding-protection',
        '--disable-background-timer-throttling',
        '--disable-renderer-backgrounding',
        '--disable-backgrounding-occluded-windows',
        '--memory-pressure-off'
      ],
      timeout: 20000
    }};
    
    // ✅ FORZA L'USO DI CHROME DI SISTEMA
    try {{
        const systemChrome = findSystemChrome();
        launchOptions.executablePath = systemChrome;
        console.log(`🎯 Using system Chrome: ${{systemChrome}}`);
    }} catch (chromeError) {{
        console.log('⚠️ No system Chrome found, using Puppeteer default');
    }}
    
    browser = await puppeteer.launch(launchOptions);
    console.log('✅ Browser launched successfully');
    
    const page = await browser.newPage();
    await page.setViewport({{ width: 1024, height: 768 }});
    await page.setDefaultTimeout(15000);
    
    // Leggi e carica HTML
    console.log('📄 Loading HTML content...');
    const htmlContent = fs.readFileSync(htmlPath, 'utf8');
    console.log(`HTML content loaded (${{htmlContent.length}} chars)`);
    
    await page.setContent(htmlContent, {{ 
      waitUntil: 'domcontentloaded',
      timeout: 10000
    }});
    console.log('✅ HTML content set successfully');
    
    // Crea directory output se necessario
    const outputDir = path.dirname(pdfPath);
    if (!fs.existsSync(outputDir)) {{
      fs.mkdirSync(outputDir, {{ recursive: true }});
      console.log(`📁 Created output directory: ${{outputDir}}`);
    }}
    
    // Genera PDF
    console.log('🎨 Generating PDF...');
    await page.pdf({{
      path: pdfPath,
      format: '{options.PageFormat}',
      printBackground: {options.PrintBackground.ToString().ToLower()},
      margin: {{
        top: '{options.MarginTop}',
        right: '{options.MarginRight}',
        bottom: '{options.MarginBottom}',
        left: '{options.MarginLeft}'
      }},
      displayHeaderFooter: {options.DisplayHeaderFooter.ToString().ToLower()},
      headerTemplate: `{options.HeaderTemplate.Replace("`", "\\`")}`,
      footerTemplate: `{options.FooterTemplate.Replace("`", "\\`")}`,
      preferCSSPageSize: true,
      timeout: 15000
    }});
    
    // Verifica risultato
    if (fs.existsSync(pdfPath)) {{
      const stats = fs.statSync(pdfPath);
      console.log(`🎉 PDF generated successfully!`);
      console.log(`   File: ${{pdfPath}}`);
      console.log(`   Size: ${{stats.size}} bytes`);
    }} else {{
      throw new Error('❌ PDF file was not created');
    }}
    
  }} catch (error) {{
    console.error('💥 PDF generation failed:', error.message);
    console.error('Stack trace:', error.stack);
    process.exit(1);
  }} finally {{
    if (browser) {{
      try {{
        await browser.close();
        console.log('✅ Browser closed');
      }} catch (closeError) {{
        console.error('Warning: Error closing browser:', closeError.message);
      }}
    }}
  }}
}})();";
    }

    /// <summary>
    /// Fallback: salva come file HTML se PDF non è possibile
    /// </summary>
    private async Task<byte[]> SaveAsHtmlFallback(string htmlContent, PdfReport report, string source)
    {
        try
        {
            var outputDir = Path.Combine("storage", "reports",
                report.ReportPeriodStart.Year.ToString(),
                report.ReportPeriodStart.Month.ToString("D2"));
            var htmlPath = Path.Combine(outputDir, $"PolarDrive_Report_{report.Id}.html");

            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(htmlPath, htmlContent);

            var htmlBytes = System.Text.Encoding.UTF8.GetBytes(htmlContent);

            await _logger.Info(source, "Report salvato come HTML fallback",
                $"Dimensione: {htmlBytes.Length} bytes, Path: {htmlPath}");

            return htmlBytes;
        }
        catch (Exception fallbackEx)
        {
            await _logger.Error(source, "Errore anche nel fallback HTML", fallbackEx.ToString());

            // Ultimo fallback: HTML minimo in memoria
            var emergencyHtml = $@"
                <html><body>
                    <h1>Errore Report {report.Id}</h1>
                    <p>Impossibile generare il report. Vedere i log per dettagli.</p>
                    <p>Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm}</p>
                </body></html>";

            return System.Text.Encoding.UTF8.GetBytes(emergencyHtml);
        }
    }

    /// <summary>
    /// Cleanup file temporanei
    /// </summary>
    private async Task CleanupTemporaryFiles(params string[] filePaths)
    {
        foreach (var path in filePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    await _logger.Debug("PdfGenerationService.Cleanup",
                        "File temporaneo eliminato", $"Path: {path}");
                }
            }
            catch (Exception ex)
            {
                await _logger.Debug("PdfGenerationService.Cleanup",
                    "Errore eliminazione file temporaneo", $"Path: {path}, Error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Converte direttamente HTML string in PDF (metodo convenienza)
    /// </summary>
    public async Task<byte[]> GeneratePdfFromHtmlAsync(string htmlContent, string fileName = "report.pdf", PdfConversionOptions? options = null)
    {
        var tempReport = new PdfReport
        {
            Id = 0,
            ReportPeriodStart = DateTime.UtcNow.AddDays(-1),
            ReportPeriodEnd = DateTime.UtcNow
        };

        return await ConvertHtmlToPdfAsync(htmlContent, tempReport, options);
    }
}