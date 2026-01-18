import { ClientVehicle } from "./vehicleInterfaces";
import { ClientCompany } from "./clientCompanyInterfaces";

/**
 * DTO esteso usato solo per il componente AdminVehicleWorkflow.
 * Include il veicolo ed i dati dell'azienda associata.
 */
export interface ClientVehicleWithCompany extends ClientVehicle {
  clientCompany: ClientCompany;
}
