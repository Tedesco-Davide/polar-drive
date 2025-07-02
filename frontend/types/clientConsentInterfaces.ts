//  Interfacce allineate agli outages

export interface ClientConsent {
  id: number;
  clientCompanyId: number;
  vehicleId: number;
  uploadDate: string;
  zipFilePath: string;
  consentHash: string;
  consentType:
    | ""
    | "Consent Activation"
    | "Consent Deactivation"
    | "Consent Stop Data Fetching"
    | "Consent Reactivation";
  vehicleVIN: string;
  companyVatNumber: string;
  notes?: string;
  //  Campo dal DTO come negli outages
  hasZipFile: boolean;
  zipFile?: File | null;
}

// Form data per creazione consent (allineato agli outages)
export interface ConsentFormData {
  consentType:
    | "Consent Activation"
    | "Consent Deactivation"
    | "Consent Stop Data Fetching"
    | "Consent Reactivation"
    | "";
  companyVatNumber: string;
  vehicleVIN: string;
  uploadDate: string;
  notes: string;
  zipFile: File | null;
}

// Request per creazione consent via API
export interface CreateConsentRequest {
  clientCompanyId: number;
  vehicleId: number;
  consentType: string;
  uploadDate: string;
  notes?: string;
}

// Risultato upload consent
export type UploadConsentResult = {
  id?: number;
  consentHash?: string;
  isNew?: boolean;
};

// ✅ Costanti allineate al backend
export const consentTypeOptions = [
  "Consent Activation",
  "Consent Deactivation",
  "Consent Stop Data Fetching",
  "Consent Reactivation",
] as const;

export const validConsentTypesForManualEntry = [
  "Consent Deactivation",
  "Consent Stop Data Fetching",
  "Consent Reactivation",
] as const;

export type ConsentType = (typeof consentTypeOptions)[number] | "";
export type ManualConsentType =
  | (typeof validConsentTypesForManualEntry)[number]
  | "";
