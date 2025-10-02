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
    preferCSSPageSize: true,
  });
  await browser.close();
})();
