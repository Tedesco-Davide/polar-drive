export interface FileManager {
  id: number;
  requestedAt: string;
  startedAt: string | null;
  completedAt: string | null;

  // Range di date per i PDF da includere nello ZIP
  periodStart: string;
  periodEnd: string;

  // Filtri specifici per PDF reports
  companyList: string[];
  vinList: string[]; // Lista VIN specifici
  brandList: string[]; // Lista brand

  // Stato della richiesta di download
  status: string; // PENDING, PROCESSING, COMPLETED, FAILED, CANCELLED

  // Risultati della generazione ZIP
  totalPdfCount: number; // Numero totale di PDF trovati nel periodo
  includedPdfCount: number; // Numero di PDF effettivamente inclusi nello ZIP
  zipFileSizeMB: number; // Dimensione del file ZIP in MB

  // Percorso del file ZIP generato
  resultZipPath?: string;

  // Informazioni aggiuntive
  infoMessage?: string;

  // Metadati della richiesta
  requestedBy?: string; // Chi ha fatto la richiesta
  downloadCount: number; // Quante volte è stato scaricato lo ZIP
}
