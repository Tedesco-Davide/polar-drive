export interface ClientCompany {
  id: number;
  vatNumber: string;
  name: string;
  address: string;
  email: string;
  pecAddress: string;
  landlineNumber: string;

  // ✅ Campi display (da veicoli) - usati per mostrare i dati nella tabella
  displayReferentName?: string;
  displayReferentMobile?: string;
  displayReferentEmail?: string;
  displayReferentPec?: string;

  // ✅ Campi form (temporanei) - usati nel form di edit
  referentName?: string;
  referentMobileNumber?: string;
  referentEmail?: string;
  referentPecAddress?: string;

  // ✅ ID del veicolo corrispondente
  correspondingVehicleId?: number;
}
