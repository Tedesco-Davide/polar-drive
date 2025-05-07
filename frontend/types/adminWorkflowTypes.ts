export type adminWorkflowTypesInputForm = {
  companyVatNumber: string;
  companyName: string;
  referentName: string;
  referentMobile: string;
  referentEmail: string;
  zipFilePath: File;
  uploadDate: string;
  teslaVehicleVIN: string;
  model: string;
  accessToken: string;
  refreshToken: string;
  isTeslaActive: boolean;
  isTeslaFetchingData: boolean;
};

type InputWithoutFile = Omit<adminWorkflowTypesInputForm, "zipFilePath">;

export type WorkflowRow = InputWithoutFile & {
  id: number;
  zipFilePath: string;
};
