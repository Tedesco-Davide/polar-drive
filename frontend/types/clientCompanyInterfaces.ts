export interface ClientCompany {
  id: number;
  vatNumber: string;
  name: string;
  address: string;
  email: string;
  pecAddress: string;
  landlineNumber: string;
  displayReferentName?: string;
  displayReferentMobile?: string;
  displayReferentEmail?: string;
  displayReferentPec?: string;
}
