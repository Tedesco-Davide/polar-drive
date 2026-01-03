export interface GapStatus {
    canCertify: boolean;
    hasCertificationPdf: boolean;
    totalGaps: number;
};

export type GapAnalysisResponse = {
  reportId: number;
  vehicleVin: string;
  companyName: string;
  periodStart: string;
  periodEnd: string;
  totalGaps: number;
  averageConfidence: number;
  summary: {
    highConfidence: number;
    mediumConfidence: number;
    lowConfidence: number;
  };
  gaps: GapAnalysis[];
  message?: string;
};

export type GapAnalysis = {
  timestamp: string;
  confidence: number;
  justification: string;
  factors: {
    hasPreviousRecord: boolean;
    hasNextRecord: boolean;
    consecutiveGapHours: number;
    isWithinTypicalUsageHours: boolean;
    isTechnicalFailure: boolean;
    failureReason?: string;
  };
};

