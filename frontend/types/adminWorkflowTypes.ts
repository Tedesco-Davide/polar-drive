import { FuelType } from "./fuelTypes";

export type adminWorkflowTypesInputForm = {
  companyId: number;
  companyVatNumber: string;
  companyName: string;
  referentName: string;
  vehicleMobileNumber: string;
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
  clientOAuthAuthorized: boolean;
};

export interface AdminWorkflowExtendedDTO {
  id: number;
  vin: string;
  fuelType: string;
  brand: string;
  model: string;
  trim?: string;
  color?: string;
  isActive: boolean;
  isFetching: boolean;
  firstActivationAt?: string;
  clientOAuthAuthorized?: boolean;
  referentName?: string;
  vehicleMobileNumber?: string;
  referentEmail?: string;
  clientCompany?: {
    id: number;
    vatNumber: string;
    name: string;
  };
}

type InputWithoutFile = Omit<adminWorkflowTypesInputForm, "zipFilePath">;

export type WorkflowRow = InputWithoutFile & {
  id: number;
  zipFilePath: string;
};
