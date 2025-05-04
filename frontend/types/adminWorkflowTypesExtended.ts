import { ClientTeslaVehicle } from "./teslaVehicleInterfaces";
import { ClientCompany } from "./clientCompanyInterfaces";

/**
 * DTO esteso usato solo per il componente AdminMainWorkflow.
 * Include il veicolo Tesla e i dati dell'azienda associata.
 */
export interface ClientTeslaVehicleWithCompany extends ClientTeslaVehicle {
  clientCompany: ClientCompany;
}
