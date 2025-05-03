export interface ClientFullProfile {
  companyId: number;
  companyVatNumber: string;
  name: string;
  address: string;
  email: string;
  pecAddress: string;

  teslaVehicleId: number;
  vin: string;
  model: string;
  workflowStatus: boolean; // mappa `IsActiveFlag` → true/false
  dataCollectionFlag: boolean; // mappa `FetchDataFlag` → true/false
  lastStatusChangeAt: string;

  eventType: "IsActiveFlag" | "FetchDataFlag";
  eventTimestamp?: string;
}
