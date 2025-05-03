import { ClientConsent } from "./clientConsentInterfaces";

export type ClientConsentFormData = Omit<ClientConsent, "zipFilePath"> & {
  zipFilePath: File;
};
