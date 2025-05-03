export interface PdfReport {
  reportPeriodStart: string;
  reportPeriodEnd: string;
  pdfFilePath: string;
  generatedAt: string;
  companyVatNumber: string;
  companyName: string;
  vehicleVin: string;
  vehicleDisplayName: string;
  notes?: string;
}
