export interface ClientConsent {
  id: number;
  clientCompanyId: number;
  teslaVehicleId: number;
  uploadDate: string;
  zipFilePath: string;
  consentHash: string;
  consentType:
    | ""
    | "Consent Activation"
    | "Consent Deactivation"
    | "Consent Stop Data Fetching"
    | "Consent Reactivation";
  teslaVehicleVIN: string;
  companyVatNumber: string;
  notes?: string;
  zipFile?: File | null;
}
