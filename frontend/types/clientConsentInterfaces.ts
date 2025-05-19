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
  zipFile?: File | null;
}
