export interface ClientVehicle {
  id: number;
  clientCompanyId: number;
  vin: string;
  brand: string;
  model: string;
  trim: string;
  color: string;
  isActive: boolean;
  isFetching: boolean;
  firstActivationAt: string;
  lastDeactivationAt: string | null;
  lastFetchingDataAt: string | null;
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
