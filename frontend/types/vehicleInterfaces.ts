import { FuelType } from "./fuelTypes";

export interface ClientVehicle {
  id: number;
  clientCompanyId: number;
  vin: string;
  fuelType: FuelType;
  brand: string;
  model: string;
  trim: string;
  color: string;
  isActive: boolean;
  isFetching: boolean;
  clientOAuthAuthorized: boolean;
  firstActivationAt: string;
  lastDeactivationAt: string | null;
  lastFetchingDataAt: string | null;
  referentName?: string;
  vehicleMobileNumber?: string;
  referentEmail?: string;
  // Oggetto nested ritornato dall'API
  clientCompany?: {
    id: number;
    vatNumber: string;
    name: string;
  };
}

export interface VehicleWorkflow {
  vehicleId: number;
  isActive: boolean;
  isFetching: boolean;
  lastStatusChangeAt: string;
}

export interface VehicleWorkflowEvent {
  id: number;
  vehicleId: number;
  fieldChanged: "IsActiveFlag" | "FetchDataFlag";
  oldValue: 0 | 1;
  newValue: 0 | 1;
  eventTimestamp: string;
}
