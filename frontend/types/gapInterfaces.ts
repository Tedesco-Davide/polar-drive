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
  outages: {
    total: number;
    gapsAffected: number;
    gapsAffectedPercentage: number;
    totalDowntimeDays: number;
    totalDowntimeHours: number;
    avgConfidenceWithOutage: number;
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
    outageId?: number;
    outageType?: string;
    outageBrand?: string;
    outageBonusApplied?: number;
  };
  outageInfo?: {
    outageType: string;
    outageBrand?: string;
    bonusApplied: number;
  };
};

