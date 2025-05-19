export interface ClientFullProfile {
  companyId: number;
  companyVatNumber: string;
  name: string;
  address: string;
  email: string;
  pecAddress: string;
  vehicleId: number;
  vin: string;
  model: string;
  workflowStatus: boolean;
  dataCollectionFlag: boolean;
  lastStatusChangeAt: string;
  eventType: "IsActiveFlag" | "FetchDataFlag";
  eventTimestamp?: string;
}
