import { readFileSync } from "fs";
import { launch } from "puppeteer";

(async () => {
  const htmlPath = process.argv[2];
  const pdfPath = process.argv[3];
  const html = readFileSync(htmlPath, "utf8");
  const browser = await launch({ headless: true });
  const page = await browser.newPage();
  await page.setContent(html, { waitUntil: "networkidle0" });
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
    tagged: false,
    timeout: 15000,
    });
    await page.close();
    await browser.close();
})();
