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
    format: "A4",
    printBackground: true,
    preferCSSPageSize: true,
    margin: {
      top: "30px",
      bottom: "30px",
      left: "20px",
      right: "20px",
    },
  });
  await browser.close();
})();
