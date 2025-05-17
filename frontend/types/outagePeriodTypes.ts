export type OutageStatus = "" | "OUTAGE-ONGOING" | "OUTAGE-RESOLVED";

export type OutageType = "" | "Outage Vehicle" | "Outage Fleet Api";

export interface OutageFormData {
  autoDetected: boolean;
  status: OutageStatus;
  outageType: OutageType;
  outageStart: string;
  outageEnd?: string;
  companyVatNumber?: string;
  vin?: string;
  zipFilePath?: File | null;
}

export type UploadOutageResult = {
  id?: number;
  isNew?: boolean;
};
