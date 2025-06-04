import puppeteer from "puppeteer";

export async function htmlToPdf(html: string): Promise<Buffer> {
  const browser = await puppeteer.launch({
    headless: "new",
    args: [
      "--allow-file-access-from-files",
      "--disable-web-security",
      "--disable-site-isolation-trials",
    ],
  });

  const page = await browser.newPage();

  await page.setContent(html, { waitUntil: "networkidle0" });

  const pdfBuffer = await page.pdf({
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
  return pdfBuffer;
}
