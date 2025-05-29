export interface PdfReport {
  id: number;
  reportPeriodStart: string;
  reportPeriodEnd: string;
  generatedAt: string | null;
  companyVatNumber: string;
  companyName: string;
  vehicleVin: string;
  vehicleModel: string;
  notes?: string;
}
