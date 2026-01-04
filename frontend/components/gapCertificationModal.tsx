import { useState, useEffect } from "react";
import { TFunction } from "i18next";
import { logFrontendEvent } from "@/utils/logger";
import { formatDateToDisplay } from "@/utils/date";
import { GapAnalysisResponse } from "@/types/gapInterfaces";
import AdminLoader from "@/components/adminLoader";

type Props = {
  reportId: number;
  isOpen: boolean;
  onClose: () => void;
  onCertificationComplete: (reportId: number) => void;
  t: TFunction;
};

export default function GapCertificationModal({
  reportId,
  isOpen,
  onClose,
  onCertificationComplete,
  t,
}: Props) {
  const [loading, setLoading] = useState(true);
  const [certifying, setCertifying] = useState(false);
  const [analysisData, setAnalysisData] = useState<GapAnalysisResponse | null>(
    null
  );
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      document.body.classList.add("overflow-hidden");
      // Reset stato quando si apre la modale
      setAnalysisData(null);
      setError(null);
      setLoading(true);
      fetchGapAnalysis();
    } else {
      document.body.classList.remove("overflow-hidden");
      // Reset stato quando si chiude la modale
      setAnalysisData(null);
      setError(null);
    }
    return () => document.body.classList.remove("overflow-hidden");
  }, [isOpen, reportId]);

  const fetchGapAnalysis = async () => {
    setLoading(true);
    setError(null);
    try {
      // Timeout allineato con backend
      const TIMEOUT_MINUTES = 15;
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), TIMEOUT_MINUTES * 60 * 1000);

      const res = await fetch(`/api/pdfreports/${reportId}/gap-analysis`, {
        signal: controller.signal,
      });

      clearTimeout(timeoutId);

      if (!res.ok) {
        const errorData = await res.json();
        throw new Error(errorData.error || `HTTP ${res.status}`);
      }
      const data: GapAnalysisResponse = await res.json();
      setAnalysisData(data);
      logFrontendEvent(
        "GapCertificationModal",
        "INFO",
        "Gap analysis loaded",
        `ReportId: ${reportId}, TotalGaps: ${data.totalGaps}`
      );
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      setError(errorMessage);
      logFrontendEvent(
        "GapCertificationModal",
        "ERROR",
        "Failed to load gap analysis",
        errorMessage
      );
    } finally {
      setLoading(false);
    }
  };

  const handleCertify = async () => {
    setCertifying(true);
    try {
      const res = await fetch(`/api/pdfreports/${reportId}/certify-gaps`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
      });

      const data = await res.json();

      // 202 Accepted = certificazione avviata in background
      if (res.status === 202) {
        logFrontendEvent(
          "GapCertificationModal",
          "INFO",
          "Gap certification started in background",
          `ReportId: ${reportId}, Status: ${data.status}`
        );
        // Notifica il parent che chiuderà la modale e aggiornerà lo stato
        onCertificationComplete(reportId);
        return;
      }

      if (!res.ok) {
        throw new Error(data.error || `HTTP ${res.status}`);
      }

      // Fallback per risposta 200 (non dovrebbe più accadere)
      logFrontendEvent(
        "GapCertificationModal",
        "INFO",
        "Gap certification completed",
        `ReportId: ${reportId}, GapsCertified: ${data.gapsCertified}`
      );

      onCertificationComplete(reportId);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      setError(errorMessage);
      logFrontendEvent(
        "GapCertificationModal",
        "ERROR",
        "Certification failed",
        errorMessage
      );
      alert(t("admin.gapCertification.certificationError", { error: errorMessage }));
    } finally {
      setCertifying(false);
    }
  };

  const getConfidenceColor = (confidence: number): string => {
    if (confidence >= 80)
      return "bg-green-100 text-green-700 border-green-500";
    if (confidence >= 60)
      return "bg-yellow-100 text-yellow-700 border-yellow-500";
    return "bg-red-100 text-red-700 border-red-500";
  };

  const getConfidenceBgColor = (confidence: number): string => {
    if (confidence >= 80) return "bg-green-500";
    if (confidence >= 60) return "bg-yellow-500";
    return "bg-red-500";
  };

  if (!isOpen) return null;

  return (
    <div className="fixed top-[64px] md:top-[0px] inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm">
      <div className="w-full h-full md:w-11/12 md:h-[80vh] bg-softWhite dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg shadow-xl overflow-hidden flex flex-col">
        <div className="bg-gradient-to-r from-purple-600 to-indigo-600 text-white px-6 py-4 flex items-center justify-between">
          <div>
            <h2 className="text-xl font-semibold">
              {t("admin.gapCertification.modalTitle")}
            </h2>
            <p className="text-sm opacity-90">
              Report #{reportId}
            </p>
          </div>
        </div>

        <div className="flex-1 overflow-y-auto p-6">
          {loading ? (
            <div className="flex items-center justify-center h-64">
              <AdminLoader />
            </div>
          ) : error ? (
            <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
              <strong>{t("admin.gapCertification.error")}</strong> {error}
            </div>
          ) : analysisData ? (
            <>
              {/* Report Info */}
              <div className="bg-gray-100 dark:bg-gray-700 rounded-lg p-4 mb-6">
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                  <div>
                    <span className="text-gray-500 dark:text-gray-400">
                      {t("admin.gapCertification.company")}
                    </span>
                    <p className="font-semibold text-polarNight dark:text-softWhite">
                      {analysisData.companyName || "N/A"}
                    </p>
                  </div>
                  <div>
                    <span className="text-gray-500 dark:text-gray-400">
                      {t("admin.gapCertification.vehicle")}
                    </span>
                    <p className="font-semibold text-polarNight dark:text-softWhite">
                      {analysisData.vehicleVin || "N/A"}
                    </p>
                  </div>
                  <div>
                    <span className="text-gray-500 dark:text-gray-400">
                      {t("admin.gapCertification.period")}
                    </span>
                    <p className="font-semibold text-polarNight dark:text-softWhite">
                      {formatDateToDisplay(analysisData.periodStart)} -{" "}
                      {formatDateToDisplay(analysisData.periodEnd)}
                    </p>
                  </div>
                  <div>
                    <span className="text-gray-500 dark:text-gray-400">
                      {t("admin.gapCertification.totalGaps")}
                    </span>
                    <p className="font-semibold text-polarNight dark:text-softWhite">
                      {analysisData.totalGaps}
                    </p>
                  </div>
                </div>
              </div>

              {analysisData.totalGaps === 0 ? (
                <div className="bg-green-100 border border-green-400 text-green-700 px-4 py-3 rounded text-center">
                  {analysisData.message || t("admin.gapCertification.noGaps")}
                </div>
              ) : (
                <>
                  <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
                    <div className="bg-gradient-to-br from-blue-500 to-blue-600 text-white rounded-lg p-4 text-center">
                      <div className="text-3xl font-bold">
                        {analysisData.averageConfidence}%
                      </div>
                      <div className="text-sm opacity-90">
                        {t("admin.gapCertification.avgConfidence")}
                      </div>
                    </div>
                    <div className="bg-gradient-to-br from-green-500 to-green-600 text-white rounded-lg p-4 text-center">
                      <div className="text-3xl font-bold">
                        {analysisData.summary.highConfidence}
                      </div>
                      <div className="text-sm opacity-90">
                        {t("admin.gapCertification.highConfidence")}
                      </div>
                    </div>
                    <div className="bg-gradient-to-br from-yellow-500 to-yellow-600 text-white rounded-lg p-4 text-center">
                      <div className="text-3xl font-bold">
                        {analysisData.summary.mediumConfidence}
                      </div>
                      <div className="text-sm opacity-90">
                        {t("admin.gapCertification.mediumConfidence")}
                      </div>
                    </div>
                    <div className="bg-gradient-to-br from-red-500 to-red-600 text-white rounded-lg p-4 text-center">
                      <div className="text-3xl font-bold">
                        {analysisData.summary.lowConfidence}
                      </div>
                      <div className="text-sm opacity-90">
                        {t("admin.gapCertification.lowConfidence")}
                      </div>
                    </div>
                  </div>

                  <div className="bg-amber-50 dark:bg-amber-900/20 border border-amber-300 dark:border-amber-700 rounded-lg p-4 mb-6">
                    <h4 className="font-semibold text-amber-800 dark:text-amber-300 mb-2">
                      {t("admin.gapCertification.disclaimerTitle")}
                    </h4>
                    <p className="text-sm text-amber-700 dark:text-amber-400">
                      {t("admin.gapCertification.disclaimerText")}
                    </p>
                  </div>

                  <div className="overflow-x-auto">
                    <table className="w-full text-sm border-collapse">
                      <thead>
                        <tr className="bg-gray-200 dark:bg-gray-700">
                          <th className="p-3 text-left">
                            {t("admin.gapCertification.timestamp")}
                          </th>
                          <th className="p-3 text-center">
                            {t("admin.gapCertification.confidence")}
                          </th>
                          <th className="p-3 text-left">
                            {t("admin.gapCertification.justification")}
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {analysisData.gaps.map((gap, index) => (
                          <tr
                            key={index}
                            className="border-b border-gray-200 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700/50"
                          >
                            <td className="p-3 font-mono text-xs">
                              {formatDateToDisplay(gap.timestamp)}
                            </td>
                            <td className="p-3 text-center">
                              <div className="flex flex-col items-center gap-1">
                                <span
                                  className={`inline-block px-3 py-1 rounded-full text-xs font-semibold border ${getConfidenceColor(
                                    gap.confidence
                                  )}`}
                                >
                                  {gap.confidence.toFixed(1)}%
                                </span>
                                <div className="w-20 h-2 bg-gray-200 rounded-full overflow-hidden">
                                  <div
                                    className={`h-full ${getConfidenceBgColor(
                                      gap.confidence
                                    )}`}
                                    style={{ width: `${gap.confidence}%` }}
                                  ></div>
                                </div>
                              </div>
                            </td>
                            <td className="p-3 text-xs text-gray-600 dark:text-gray-400">
                              {gap.justification}
                              {gap.factors.isTechnicalFailure && (
                                <span className="ml-2 bg-blue-100 text-blue-700 px-2 py-0.5 rounded text-xs">
                                   {t("admin.gapCertification.technicalFailure")}
                                </span>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </>
              )}
            </>
          ) : null}
        </div>

        <div className="border-t border-gray-200 dark:border-gray-600 px-6 py-4 flex gap-4 bg-gray-50 dark:bg-gray-900">
          {analysisData && analysisData.totalGaps > 0 && (
            <button
              onClick={handleCertify}
              disabled={certifying}
              className="px-6 py-2 bg-gradient-to-r from-purple-600 to-indigo-600 text-white rounded hover:from-purple-700 hover:to-indigo-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
            >
              {certifying ? (
                <>
                  <AdminLoader inline />
                  {t("admin.gapCertification.certifying")}
                </>
              ) : (
                t("admin.gapCertification.confirmCertify")
              )}
            </button>
          )}
          <button
            onClick={onClose}
            className="px-6 py-2 bg-gray-400 text-white rounded hover:bg-gray-500 transition-colors"
          >
            {t("admin.cancelEditRow")}
          </button>
        </div>
      </div>
    </div>
  );
}
