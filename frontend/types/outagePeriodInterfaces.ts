export interface OutagePeriod {
  id: number;
  teslaVehicleId: number;
  clientCompanyId: number;
  autoDetected: boolean;
  outageType: "Outage Veichle" | "Outage Fleet Api";
  createdAt: string;
  outageStart: string;
  outageEnd?: string;
  vin: string;
  companyVatNumber: string;
  zipFilePath?: string;
  notes?: string;
}
