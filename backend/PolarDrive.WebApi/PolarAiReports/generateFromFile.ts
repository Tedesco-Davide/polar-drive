// generateFromFile.ts - VERSIONE MIGLIORATA

import { readFileSync } from "fs";
import { launch } from "puppeteer";

(async () => {
  const htmlPath = process.argv[2];
  const pdfPath = process.argv[3];
  const html = readFileSync(htmlPath, "utf8");
  
    const browser = await launch({ 
    headless: true,
    args: [
        '--no-sandbox',
        '--disable-setuid-sandbox',
        '--disable-dev-shm-usage',
        '--disable-gpu',
        '--disable-web-security',
        '--font-render-hinting=none',
        '--enable-font-antialiasing',
    ],
    env: {
        TZ: 'Europe/Rome'
    }
    });
  
  const page = await browser.newPage();
  
  // ✅ IMPOSTA USER AGENT E LINGUA
  await page.setUserAgent('Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.114 Safari/537.36');
  
  // ✅ ATTENDE IL CARICAMENTO DEI FONT
  await page.setContent(html, { 
    waitUntil: 'networkidle0',
    timeout: 30000
  });

    // ✅ FORZA CARICAMENTO EMOJI
    await page.evaluate(() => {
    const link = document.createElement('link');
    link.href = 'https://fonts.googleapis.com/css2?family=Noto+Color+Emoji';
    link.rel = 'stylesheet';
    document.head.appendChild(link);
    });
  
  // ✅ ATTENDE ESPLICITAMENTE IL CARICAMENTO DEI FONT
  await page.evaluateHandle('document.fonts.ready');
  
  // ✅ ATTESA AGGIUNTIVA PER FONT E EMOJI
  await new Promise(resolve => setTimeout(resolve, 1500));
  
  await page.pdf({
    path: pdfPath,
    format: 'A4',
    printBackground: true,
    margin: {
      top: '10mm',
      right: '10mm',
      bottom: '10mm',
      left: '10mm'
    },
    preferCSSPageSize: true,  
    omitBackground: false,
    timeout: 30000, // ✅ AUMENTA TIMEOUT
  });
  
  await page.close();
  await browser.close();
})();