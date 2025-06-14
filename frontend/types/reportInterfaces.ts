// ✅ Interface perfettamente allineata al PdfReportDTO backend
export interface PdfReport {
  // Proprietà esistenti
  id: number;
  reportPeriodStart: string;
  reportPeriodEnd: string;
  generatedAt: string | null;
  companyVatNumber: string;
  companyName: string;
  vehicleVin: string;
  vehicleModel: string;
  notes?: string;

  // Nuove proprietà dal backend
  HasPdfFile: boolean;
  HasHtmlFile: boolean;
  DataRecordsCount: number;
  PdfFileSize: number;
  HtmlFileSize: number;
  MonitoringDurationHours: number;
  LastModified?: string;
  IsRegenerated: boolean;
  RegenerationCount: number;
  ReportType: string;

  // Proprietà calcolate dal backend
  IsDownloadable: boolean;
  Status: string;
  AvailableFormats: string[];
}

// ✅ Interface per le statistiche di download
export interface DownloadStats {
  success: boolean;
  error?: string;
  fileSize?: number;
  downloadTime?: number;
  regenerated?: boolean;
}

// ✅ Interface per il controllo pre-download
export interface ReportFileStatus {
  hasPdfFile: boolean;
  hasHtmlFile: boolean;
  pdfFileSize: number;
  htmlFileSize: number;
  needsRegeneration: boolean;
  reportType: string;
}
