using System.Diagnostics;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.AiReports;

/// <summary>
/// Servizio semplificato dedicato SOLO alla conversione HTML -> PDF
/// La generazione HTML è gestita da HtmlReportService
/// </summary>
public class PdfGenerationService(PolarDriveDbContext dbContext)
{
    private readonly PolarDriveLogger _logger = new(dbContext);
    private const int PDF_GENERATION_TIMEOUT_SECONDS = 60;

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
    /// Converte HTML in PDF usando Puppeteer
    /// </summary>
    private async Task<byte[]> ConvertWithPuppeteerAsync(string htmlPath, string pdfPath, PdfConversionOptions options)
    {
        var source = "PdfGenerationService.ConvertWithPuppeteer";

        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        var npxPath = Path.Combine(programFiles, "nodejs", "npx.cmd");

        // Genera script Puppeteer personalizzato
        var puppeteerScript = GeneratePuppeteerScript(options);
        var scriptPath = Path.Combine(Path.GetTempPath(), $"puppeteer_script_{DateTime.UtcNow.Ticks}.js");
        await File.WriteAllTextAsync(scriptPath, puppeteerScript);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = npxPath,
                Arguments = $"node \"{scriptPath}\" \"{htmlPath}\" \"{pdfPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            }
        };

        await _logger.Info(source, "Avvio conversione Puppeteer",
            $"Script: {scriptPath}, HTML: {htmlPath}, PDF: {pdfPath}");

        process.Start();

        // Timeout management
        var processTask = Task.Run(() => process.WaitForExit());
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(PDF_GENERATION_TIMEOUT_SECONDS));

        var completedTask = await Task.WhenAny(processTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            await _logger.Warning(source, $"Timeout conversione PDF ({PDF_GENERATION_TIMEOUT_SECONDS}s)");

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

            throw new TimeoutException($"PDF conversion timed out after {PDF_GENERATION_TIMEOUT_SECONDS} seconds");
        }

        // Controlla risultato
        if (!File.Exists(pdfPath))
        {
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();

            await _logger.Error(source, "Conversione fallita",
                $"ExitCode: {process.ExitCode}, Stdout: {stdout}, Stderr: {stderr}");

            throw new InvalidOperationException($"PDF conversion failed. Exit code: {process.ExitCode}");
        }

        // Cleanup script temporaneo
        try
        {
            File.Delete(scriptPath);
        }
        catch { }

        return await File.ReadAllBytesAsync(pdfPath);
    }

    /// <summary>
    /// Genera script Puppeteer personalizzato basato sulle opzioni
    /// </summary>
    private string GeneratePuppeteerScript(PdfConversionOptions options)
    {
        return $@"
const puppeteer = require('puppeteer');
const fs = require('fs');
const path = require('path');

(async () => {{
  const [htmlPath, pdfPath] = process.argv.slice(2);
  
  console.log(`Converting: ${{htmlPath}} -> ${{pdfPath}}`);
  
  let browser;
  try {{
    browser = await puppeteer.launch({{
      headless: true,
      args: ['--no-sandbox', '--disable-dev-shm-usage', '--disable-gpu']
    }});
    
    const page = await browser.newPage();
    
    // Carica HTML dal file
    const htmlContent = fs.readFileSync(htmlPath, 'utf8');
    await page.setContent(htmlContent, {{ 
      waitUntil: 'networkidle2',
      timeout: 30000
    }});
    
    // Attendi che eventuali immagini si carichino
    await page.evaluate(() => {{
      return new Promise((resolve) => {{
        const images = Array.from(document.images);
        let loadedImages = 0;
        
        if (images.length === 0) {{
          resolve();
          return;
        }}
        
        images.forEach((img) => {{
          if (img.complete) {{
            loadedImages++;
          }} else {{
            img.addEventListener('load', () => {{
              loadedImages++;
              if (loadedImages === images.length) {{
                resolve();
              }}
            }});
            img.addEventListener('error', () => {{
              loadedImages++;
              if (loadedImages === images.length) {{
                resolve();
              }}
            }});
          }}
        }});
        
        if (loadedImages === images.length) {{
          resolve();
        }}
      }});
    }});
    
    // Assicura che la directory di output esista
    const outputDir = path.dirname(pdfPath);
    if (!fs.existsSync(outputDir)) {{
      fs.mkdirSync(outputDir, {{ recursive: true }});
    }}
    
    // Genera PDF con opzioni personalizzate
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
      headerTemplate: `{options.HeaderTemplate}`,
      footerTemplate: `{options.FooterTemplate}`,
      preferCSSPageSize: true,
      timeout: 30000
    }});
    
    console.log(`PDF generated successfully: ${{pdfPath}}`);
    
  }} catch (error) {{
    console.error('Error generating PDF:', error);
    process.exit(1);
  }} finally {{
    if (browser) {{
      await browser.close();
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

/// <summary>
/// Opzioni per la conversione PDF
/// </summary>
public class PdfConversionOptions
{
    public string PageFormat { get; set; } = "A4";
    public bool PrintBackground { get; set; } = true;
    public string MarginTop { get; set; } = "1cm";
    public string MarginRight { get; set; } = "1cm";
    public string MarginBottom { get; set; } = "1cm";
    public string MarginLeft { get; set; } = "1cm";
    public bool DisplayHeaderFooter { get; set; } = true;
    public string HeaderTemplate { get; set; } = @"
        <div style='font-size: 10px; width: 100%; text-align: center; color: #666;'>
            <span>PolarDrive Report</span>
        </div>";
    public string FooterTemplate { get; set; } = @"
        <div style='font-size: 10px; width: 100%; text-align: center; color: #666;'>
            <span>Pagina <span class='pageNumber'></span> di <span class='totalPages'></span></span>
        </div>";
}