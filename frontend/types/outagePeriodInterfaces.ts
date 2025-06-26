export interface OutagePeriod {
  id: number;
  autoDetected: boolean;
  outageType: "Outage Vehicle" | "Outage Fleet Api";
  outageBrand: string;
  createdAt: string;
  outageStart: string;
  outageEnd: string | null;
  notes: string;
  zipFilePath: string | null;
  vehicleId: number | null;
  clientCompanyId: number | null;
  status: "OUTAGE-ONGOING" | "OUTAGE-RESOLVED";
  vin: string | null;
  companyVatNumber: string | null;
  durationMinutes: number;
  hasZipFile: boolean;
}
