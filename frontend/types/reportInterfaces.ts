export interface PdfReport {

  // Proprietà esistenti
  id: number;
  reportPeriodStart: string;
  reportPeriodEnd: string;
  generatedAt: string | null;
  companyVatNumber: string;
  companyName: string;
  vehicleBrand: string;
  vehicleVin: string;
  vehicleModel: string;
  notes?: string;

  // Nomenclatura camelCase coerente con quanto ricevuto dal backend
  hasPdfFile: boolean;
  hasHtmlFile: boolean;
  dataRecordsCount: number;
  pdfFileSize: number;
  htmlFileSize: number;
  monitoringDurationHours: number;
  lastModified?: string;
  reportType: string;

  // Proprietà calcolate dal backend
  isDownloadable: boolean;
  status: string;
  pdfHash?: string;

  // Gap Validation info
  gapValidationStatus?: string | null;
  gapValidationPdfHash?: string | null;
  hasGapValidationPdf?: boolean;
}

// ✅ Interface per il controllo pre-download
export interface ReportFileStatus {
  hasPdfFile: boolean;
  hasHtmlFile: boolean;
  pdfFileSize: number;
  htmlFileSize: number;
  reportType: string;
}