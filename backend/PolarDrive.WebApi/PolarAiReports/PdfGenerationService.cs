using System.Diagnostics;
using System.Runtime.InteropServices;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.PolarAiReports;

/// <summary>
/// Servizio semplificato dedicato SOLO alla conversione HTML -> PDF
/// La generazione HTML è gestita da HtmlReportService
/// Supporta Windows, Linux e Docker
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
                $"ReportId: {report.Id}, HTML size: {htmlContent.Length} chars, OS: {GetOSInfo()}");

            // 1. Salva HTML temporaneo
            var tempHtmlPath = await SaveTemporaryHtmlAsync(htmlContent, report.Id);

            // 2. Prepara path di output
            var outputPdfPath = GetOutputPdfPath(report);

            // 3. Controlla disponibilità Node.js
            var nodeInfo = GetNodeJsPath();
            if (nodeInfo.nodePath == null)
            {
                await _logger.Warning(source, "Node.js non disponibile, salvo come HTML",
                    $"ReportId: {report.Id}, OS: {GetOSInfo()}");
                return await SaveAsHtmlFallback(htmlContent, report, source);
            }

            await _logger.Info(source, $"Node.js trovato: {nodeInfo.nodePath}");

            // 4. Converti con Puppeteer
            var pdfBytes = await ConvertWithPuppeteerAsync(tempHtmlPath, outputPdfPath, nodeInfo.nodePath, options);

            // 5. Cleanup
            await CleanupTemporaryFiles(tempHtmlPath);

            await _logger.Info(source, "Conversione PDF completata",
                $"ReportId: {report.Id}, PDF size: {pdfBytes.Length} bytes");

            return pdfBytes;
        }
        catch (Exception ex)
        {
            await _logger.Error(source, "Errore conversione PDF",
                $"ReportId: {report.Id}, Error: {ex.Message}, StackTrace: {ex.StackTrace}");

            // Fallback: salva come HTML
            return await SaveAsHtmlFallback(htmlContent, report, source);
        }
        finally
        {
            // ✅ FORZA GARBAGE COLLECTION
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);   
            await _logger.Debug("PdfGenerationService", "Garbage Collection forzato post-conversione");
        }
    }

    /// <summary>
    /// Ottiene informazioni sul sistema operativo
    /// </summary>
    private string GetOSInfo()
    {
        return $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";
    }

    /// <summary>
    /// Trova il path di Node.js in modo cross-platform
    /// </summary>
    private (string? nodePath, string? npmPath) GetNodeJsPath()
    {
        try
        {
            // Prova con 'which' (Linux/Mac) o 'where' (Windows)
            var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
            var nodeExecutable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = nodeExecutable,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                var nodePath = output.Split('\n')[0].Trim(); // Prima riga se ci sono più risultati
                
                // Verifica che il file esista
                if (File.Exists(nodePath))
                {
                    return (nodePath, null);
                }
            }

            // Fallback: cerca in path comuni
            return FindNodeInCommonPaths();
        }
        catch (Exception ex)
        {
            _logger.Warning("PdfGenerationService.GetNodeJsPath", 
                "Errore ricerca Node.js", ex.Message).Wait();
            return (null, null);
        }
    }

    /// <summary>
    /// Cerca Node.js in path comuni (fallback)
    /// </summary>
    private (string? nodePath, string? npmPath) FindNodeInCommonPaths()
    {
        var commonPaths = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows paths
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            commonPaths.AddRange(new[]
            {
                Path.Combine(programFiles, "nodejs", "node.exe"),
                Path.Combine(programFilesX86, "nodejs", "node.exe"),
                Path.Combine(appData, "npm", "node.exe")
            });
        }
        else
        {
            // Linux/Mac paths
            commonPaths.AddRange(new[]
            {
                "/usr/bin/node",
                "/usr/local/bin/node",
                "/opt/node/bin/node",
                "/home/linuxbrew/.linuxbrew/bin/node",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nvm", "current", "bin", "node")
            });
        }

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return (path, null);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Salva HTML in file temporaneo
    /// </summary>
    private async Task<string> SaveTemporaryHtmlAsync(string htmlContent, int reportId)
    {
        var tempDir = Path.GetTempPath();
        var htmlPath = Path.Combine(tempDir, $"PolarDrive_PolarReport_{reportId}_{DateTime.Now.Ticks}.html");

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
        var generationDate = report.GeneratedAt ?? DateTime.Now;

        var outputDir = Path.Combine("storage", "reports",
            generationDate.Year.ToString(),
            generationDate.Month.ToString("D2"));

        Directory.CreateDirectory(outputDir);
        return Path.Combine(outputDir, $"PolarDrive_PolarReport_{report.Id}.pdf");
    }

    /// <summary>
    /// Converte HTML in PDF usando Puppeteer
    /// </summary>
    private async Task<byte[]> ConvertWithPuppeteerAsync(string htmlPath, string pdfPath, string nodePath, PdfConversionOptions options)
    {
        var source = "PdfGenerationService.ConvertWithPuppeteer";
        int maxRetries = options.MaxRetries;
        int timeoutSeconds = options.ConvertTimeoutSeconds;
        string? scriptPath = null;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _logger.Info(source, $"Tentativo {attempt}/{maxRetries} conversione Puppeteer");

                // Script Puppeteer
                var puppeteerScript = GenerateOptimizedPuppeteerScript(options);

                // Salva script in directory temporanea
                var tempDir = Path.GetTempPath();
                scriptPath = Path.Combine(tempDir, $"temp_puppeteer_script_{DateTime.Now.Ticks}_attempt{attempt}.js");
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
                        WorkingDirectory = "/app" // Directory dove sono installati i node_modules
                    }
                };

                await _logger.Info(source, "Avvio conversione Puppeteer",
                    $"Attempt: {attempt}, NodePath: {nodePath}, Script: {scriptPath}");

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

                await _logger.Debug(source, "Output Puppeteer STDOUT", $"Stdout:\n\n{stdout}");
                if (!string.IsNullOrEmpty(stderr))
                {
                    await _logger.Warning(source, "Puppeteer STDERR", $"Stderr: {stderr}");
                }

                // Controlla risultato
                if (File.Exists(pdfPath))
                {
                    var pdfBytes = await File.ReadAllBytesAsync(pdfPath);

                    // Verifica che non sia vuoto
                    if (pdfBytes.Length > 0)
                    {
                        await _logger.Info(source, $"PDF generato con successo (tentativo {attempt})",
                            $"Dimensione: {pdfBytes.Length} bytes");

                        // Cleanup script temporaneo
                        try { File.Delete(scriptPath); } catch { }

                        return pdfBytes;
                    }
                    else
                    {
                        await _logger.Warning(source, $"PDF vuoto generato (tentativo {attempt})");
                        File.Delete(pdfPath);
                    }
                }
                else
                {
                    await _logger.Warning(source, $"File PDF non creato (tentativo {attempt})");
                }

                // Cleanup e retry
                try { File.Delete(scriptPath); } catch { }

                if (attempt < maxRetries)
                {
                    await Task.Delay(2000 * attempt); // Backoff progressivo
                }
            }
            catch (Exception ex)
            {
                await _logger.Error(source, $"Errore tentativo {attempt}", ex.ToString());

                // Cleanup
                try { if (scriptPath != null) File.Delete(scriptPath); } catch { }

                if (attempt == maxRetries)
                {
                    throw;
                }

                await Task.Delay(2000 * attempt);
            }
        }

        throw new Exception($"Impossibile generare PDF dopo {maxRetries} tentativi");
    }

    /// <summary>
    /// Script Puppeteer ottimizzato per Docker/Linux con supporto font ed emoji
    /// </summary>
    private string GenerateOptimizedPuppeteerScript(PdfConversionOptions options)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var logFile = $"/app/logs/puppeteer/pdf_{timestamp}.log";
        
        return $@"
        const path = require('path');
        const fs = require('fs');

        // 📁 Setup logging
        const logFile = '{logFile}';
        const logDir = path.dirname(logFile);

        if (!fs.existsSync(logDir)) {{
            fs.mkdirSync(logDir, {{ recursive: true }});
        }}

        function log(msg) {{
            const timestamp = new Date().toISOString();
            const line = `[${{timestamp}}] ${{msg}}\n`;
            fs.appendFileSync(logFile, line);
            console.log(msg);
        }}

        log('🔍 Starting PDF conversion...');
        log(`📦 Platform: ${{process.platform}}`);
        log(`📦 Node version: ${{process.version}}`);
        log(`📁 Log file: ${{logFile}}`);

        let puppeteer;
        try {{
            puppeteer = require('puppeteer');
            log('✅ Using Puppeteer');
        }} catch (err1) {{
            try {{
                puppeteer = require('puppeteer-core');
                log('✅ Using Puppeteer-core');
            }} catch (err2) {{
                log(`💥 Puppeteer not found: ${{err2.message}}`);
                process.exit(1);
            }}
        }}

        (async () => {{
            const [htmlPath, pdfPath] = process.argv.slice(2);
            
            if (!htmlPath || !pdfPath) {{
                log('❌ Usage: node script.js <htmlPath> <pdfPath>');
                process.exit(1);
            }}
            
            log(`📄 HTML: ${{htmlPath}}`);
            log(`📄 PDF: ${{pdfPath}}`);
            
            let browser;
            let page;
            try {{
                log('🚀 Launching browser...');
                
                const launchOptions = {{
                    headless: 'new',
                    args: [
                        '--no-sandbox',
                        '--disable-setuid-sandbox',
                        '--disable-dev-shm-usage',
                        '--disable-gpu',
                        '--disable-web-security',
                        '--font-render-hinting=none',
                        '--enable-font-antialiasing',
                        '--enable-features=FontCache',
                        '--disable-features=IsolateOrigins,site-per-process'
                    ],
                    env: {{ TZ: 'Europe/Rome' }},
                    timeout: 60000
                }};
                
                browser = await puppeteer.launch(launchOptions);
                log('✅ Browser launched');
                
                page = await browser.newPage();
                
                // ✅ NUOVO: IMPOSTA USER AGENT E RISOLUZIONE OTTIMIZZATA
                await page.setUserAgent('Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.114 Safari/537.36');
                await page.setViewport({{ width: 1200, height: 1697 }}); // A4 in pixel a 96 DPI
                log('✅ Page created with optimized viewport');
                
                log('📄 Loading HTML...');
                
                // Verifica che il file esista
                if (!fs.existsSync(htmlPath)) {{
                    throw new Error(`HTML file not found: ${{htmlPath}}`);
                }}
                
                const htmlContent = fs.readFileSync(htmlPath, 'utf8');
                log(`📄 HTML size: ${{htmlContent.length}} bytes`);
                
                await page.setContent(htmlContent, {{ 
                    waitUntil: 'networkidle0',
                    timeout: 30000
                }});
                log('✅ HTML loaded');
                
                // ✅ NUOVO: ATTENDE ESPLICITAMENTE IL CARICAMENTO DEI FONT
                log('🔤 Waiting for fonts to load...');
                await page.evaluateHandle('document.fonts.ready');
                log('✅ Fonts loaded');
                
                // ✅ NUOVO: ATTESA AGGIUNTIVA PER FONT E EMOJI
                log('⏳ Additional wait for fonts and emojis...');
                await new Promise(resolve => setTimeout(resolve, 1500));
                log('✅ Additional wait completed');
                
                // Crea directory di output se necessaria
                const outputDir = path.dirname(pdfPath);
                if (!fs.existsSync(outputDir)) {{
                    fs.mkdirSync(outputDir, {{ recursive: true }});
                    log(`📁 Created output directory: ${{outputDir}}`);
                }}
                
                log('🎨 Generating PDF...');
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
                    timeout: 30000  // ✅ AUMENTATO TIMEOUT
                }});
                log('✅ PDF generation command completed');
                
                // Verifica che il PDF sia stato creato
                if (fs.existsSync(pdfPath)) {{
                    const stats = fs.statSync(pdfPath);
                    log(`🎉 PDF generated successfully: ${{stats.size}} bytes`);
                    
                    // Verifica header PDF
                    const pdfBuffer = fs.readFileSync(pdfPath);
                    const header = pdfBuffer.slice(0, 4).toString();
                    log(`📄 PDF header: ${{header}}`);
                    
                    if (header !== '%PDF') {{
                        log(`⚠️ Warning: Invalid PDF header detected!`);
                    }}
                }} else {{
                    throw new Error('❌ PDF file not created');
                }}
                
            }} catch (error) {{
                log(`💥 Conversion failed: ${{error.message}}`);
                log(`Stack: ${{error.stack}}`);
                process.exit(1);
            }} finally {{
                if (page) {{
                    await page.close();
                    log('✅ Page closed');
                }}
                if (browser) {{
                    await browser.close();
                    log('✅ Browser closed');
                }}
                log('🏁 Script completed');
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
            var htmlBytes = System.Text.Encoding.UTF8.GetBytes(htmlContent);
            await _logger.Info(source, "Fallback HTML in-memory (NO FS)", $"Size: {htmlBytes.Length} bytes");
            return htmlBytes; // 👈 niente più scrittura su disco
        }
        catch (Exception fallbackEx)
        {
            await _logger.Error(source, "Errore anche nel fallback HTML", fallbackEx.ToString());
            var emergencyHtml = $@"<html><body>
                <h1>Errore Report {report.Id}</h1>
                <p>Impossibile generare il report. Vedere i log per dettagli.</p>
                <p>Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm}</p>
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
}