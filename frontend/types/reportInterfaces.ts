// ✅ Interface perfettamente allineata al PdfReportDTO backend
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

  // ✅ NOMI CORRETTI - DEVONO ESSERE camelCase COME RICEVUTI DAL BACKEND
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
}

// ✅ Interface per il controllo pre-download
export interface ReportFileStatus {
  hasPdfFile: boolean;
  hasHtmlFile: boolean;
  pdfFileSize: number;
  htmlFileSize: number;
  reportType: string;
}
