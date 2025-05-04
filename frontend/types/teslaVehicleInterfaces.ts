export interface ClientTeslaVehicle {
  id: number;
  clientCompanyId: number;
  vin: string;
  model: string;
  trim: string;
  color: string;
  isActive: boolean;
  isFetching: boolean;
  firstActivationAt: string;
  lastDeactivationAt: string | null;
  lastFetchingDataAt: string | null;
}

export interface TeslaWorkflow {
  teslaVehicleId: number;
  isActive: boolean;
  isFetching: boolean;
  lastStatusChangeAt: string;
}

export interface TeslaWorkflowEvent {
  id: number;
  teslaVehicleId: number;
  fieldChanged: "IsActiveFlag" | "FetchDataFlag";
  oldValue: 0 | 1;
  newValue: 0 | 1;
  eventTimestamp: string;
}
