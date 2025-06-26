export type OutageStatus = "" | "OUTAGE-ONGOING" | "OUTAGE-RESOLVED";

export type OutageType = "" | "Outage Vehicle" | "Outage Fleet Api";

export interface OutageFormData {
  outageType: "Outage Vehicle" | "Outage Fleet Api" | "";
  outageBrand: string;
  outageStart: string;
  outageEnd: string | undefined;
  companyVatNumber: string;
  vin: string;
  notes: string;
  zipFile: File | null;
}

export type UploadOutageResult = {
  id?: number;
  isNew?: boolean;
};
