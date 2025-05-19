export interface OutagePeriod {
  id: number;
  vehicleId: number;
  clientCompanyId: number;
  autoDetected: boolean;
  outageType: "Outage Vehicle" | "Outage Fleet Api";
  outageBrand: string;
  createdAt: string;
  outageStart: string;
  outageEnd?: string;
  vin: string;
  companyVatNumber: string;
  zipFilePath?: string;
  notes?: string;
}
