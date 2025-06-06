export interface ScheduledFileJob {
  id: number;
  requestedAt: string;
  startedAt: string | null;
  completedAt: string | null;
  periodStart: string;
  periodEnd: string;

  fileTypeList: string[];
  companyList: string[];
  brandList: string[];
  consentTypeList: string[];
  outageTypeList: string[];
  outageAutoDetectedOptionList: string[];

  status: string;
  generatedFilesCount: number;
  infoMessage?: string;
  resultZipPath?: string;
}
