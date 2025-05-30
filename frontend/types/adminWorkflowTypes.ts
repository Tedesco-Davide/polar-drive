import { FuelType } from "./fuelTypes";

export type adminWorkflowTypesInputForm = {
  companyVatNumber: string;
  companyName: string;
  referentName: string;
  referentMobile: string;
  referentEmail: string;
  zipFilePath: File;
  uploadDate: string;
  vehicleVIN: string;
  fuelType: FuelType;
  brand: string;
  model: string;
  trim: string;
  color: string;
  isVehicleActive: boolean;
  isVehicleFetchingData: boolean;
};

type InputWithoutFile = Omit<adminWorkflowTypesInputForm, "zipFilePath">;

export type WorkflowRow = InputWithoutFile & {
  id: number;
  zipFilePath: string;
};
