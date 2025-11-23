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
    zipHash: string;
    hasZipFile: boolean;

    // Informazioni aggiuntive
    notes?: string;

    // Metadati della richiesta
    requestedBy?: string; // Chi ha fatto la richiesta
}
