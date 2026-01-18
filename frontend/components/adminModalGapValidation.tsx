import { useState, useEffect } from "react";
import { TFunction } from "i18next";
import { logFrontendEvent } from "@/utils/logger";
import { formatDateToDisplay } from "@/utils/date";
import { GapAnalysisResponse } from "@/types/gapInterfaces";
import AdminGenericLoader from "@/components/adminGenericLoader";

type Props = {
  reportId: number;
  isOpen: boolean;
  onClose: () => void;
  onValidationComplete: (reportId: number, action?: "certify" | "escalate" | "breach") => void;
  t: TFunction;
  gapValidationStatus?: string | null; // null = nessun PDF, ESCALATED = già escalato
};

export default function AdminModalGapValidation({
  reportId,
  isOpen,
  onClose,
  onValidationComplete,
  t,
  gapValidationStatus,
}: Props) {
  const [loading, setLoading] = useState(true);
  const [certifying, setCertifying] = useState(false);
  const [escalating, setEscalating] = useState(false);
  const [breaching, setBreaching] = useState(false);
  const [notes, setNotes] = useState("");
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

      const res = await fetch(`/api/gapanalysis/${reportId}/analysis`, {
        signal: controller.signal,
      });

      clearTimeout(timeoutId);

      // Leggi la risposta come testo per gestire sia JSON che errori plain text
      const responseText = await res.text();

      if (!res.ok) {
        let errorMessage = `HTTP ${res.status}`;
        try {
          const errorData = JSON.parse(responseText);
          errorMessage = errorData.error || errorMessage;
        } catch {
          // Se non è JSON, usa il testo direttamente (es. "Internal Server Error")
          if (responseText) {
            errorMessage = responseText.substring(0, 200);
          }
        }
        throw new Error(errorMessage);
      }

      let data: GapAnalysisResponse;
      try {
        data = JSON.parse(responseText);
      } catch {
        throw new Error(t("admin.gapValidation.invalidServerResponse"));
      }
      setAnalysisData(data);
      logFrontendEvent(
        "AdminModalGapValidation",
        "INFO",
        "Gap analysis loaded",
        `ReportId: ${reportId}, TotalGaps: ${data.totalGaps}`
      );
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      setError(errorMessage);
      logFrontendEvent(
        "AdminModalGapValidation",
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
      const res = await fetch(`/api/gapanalysis/${reportId}/validate`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
      });

      // Leggi la risposta come testo per gestire sia JSON che errori plain text
      const responseText = await res.text();
      let data: { status?: string; gapsCertified?: number; error?: string } = {};

      try {
        data = JSON.parse(responseText);
      } catch {
        // Se non è JSON valido, crea un oggetto errore
        if (!res.ok) {
          throw new Error(responseText.substring(0, 200) || `HTTP ${res.status}`);
        }
      }

      // 202 Accepted = certificazione avviata in background
      if (res.status === 202) {
        logFrontendEvent(
          "AdminModalGapValidation",
          "INFO",
          "Gap certification started in background",
          `ReportId: ${reportId}, Status: ${data.status}`
        );
        // Notifica il parent che chiuderà la modale e aggiornerà lo stato
        onValidationComplete(reportId, "certify");
        return;
      }

      if (!res.ok) {
        throw new Error(data.error || `HTTP ${res.status}`);
      }

      // Fallback per risposta 200 (non dovrebbe più accadere)
      logFrontendEvent(
        "AdminModalGapValidation",
        "INFO",
        "Gap certification completed",
        `ReportId: ${reportId}, GapsCertified: ${data.gapsCertified}`
      );

      onValidationComplete(reportId, "certify");
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      setError(errorMessage);
      logFrontendEvent(
        "AdminModalGapValidation",
        "ERROR",
        "Certification failed",
        errorMessage
      );
      alert(t("admin.gapValidation.certificationError", { error: errorMessage }));
    } finally {
      setCertifying(false);
    }
  };

  const handleEscalate = async () => {
    setEscalating(true);
    try {
      const res = await fetch(`/api/gapanalysis/${reportId}/escalate`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ notes }),
      });

      const responseText = await res.text();
      let data: { status?: string; error?: string } = {};

      try {
        data = JSON.parse(responseText);
      } catch {
        if (!res.ok) {
          throw new Error(responseText.substring(0, 200) || `HTTP ${res.status}`);
        }
      }

      if (res.status === 202 || res.ok) {
        logFrontendEvent(
          "AdminModalGapValidation",
          "INFO",
          "Gap escalation started",
          `ReportId: ${reportId}, Status: ${data.status}`
        );
        onValidationComplete(reportId, "escalate");
        return;
      }

      throw new Error(data.error || `HTTP ${res.status}`);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      setError(errorMessage);
      logFrontendEvent(
        "AdminModalGapValidation",
        "ERROR",
        "Escalation failed",
        errorMessage
      );
      alert(t("admin.gapValidation.escalationError") || `Errore: ${errorMessage}`);
    } finally {
      setEscalating(false);
    }
  };

  const handleBreach = async () => {
    if (!confirm(t("admin.gapValidation.confirmBreach") || "Confermi Contract Breach? Questa azione e' irreversibile.")) {
      return;
    }

    setBreaching(true);
    try {
      const res = await fetch(`/api/gapanalysis/${reportId}/breach`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ notes }),
      });

      const responseText = await res.text();
      let data: { status?: string; error?: string } = {};

      try {
        data = JSON.parse(responseText);
      } catch {
        if (!res.ok) {
          throw new Error(responseText.substring(0, 200) || `HTTP ${res.status}`);
        }
      }

      if (res.status === 202 || res.ok) {
        logFrontendEvent(
          "AdminModalGapValidation",
          "INFO",
          "Contract breach recorded",
          `ReportId: ${reportId}, Status: ${data.status}`
        );
        onValidationComplete(reportId, "breach");
        return;
      }

      throw new Error(data.error || `HTTP ${res.status}`);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      setError(errorMessage);
      logFrontendEvent(
        "AdminModalGapValidation",
        "ERROR",
        "Contract breach failed",
        errorMessage
      );
      alert(t("admin.gapValidation.breachError") || `Errore: ${errorMessage}`);
    } finally {
      setBreaching(false);
    }
  };

  // Verifica se mostrare il bottone Escalate (solo se non già escalato)
  const showEscalateButton = !gapValidationStatus || gapValidationStatus === "PROCESSING";
  const isProcessing = certifying || escalating || breaching;

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
              {t("admin.gapValidation.modalTitle")}
            </h2>
            <p className="text-sm opacity-90">
              Report #{reportId}
            </p>
          </div>
        </div>

        <div className="flex-1 overflow-y-auto p-6">
          {loading ? (
            <div className="flex items-center justify-center h-64">
              <AdminGenericLoader />
            </div>
          ) : error ? (
            <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
              <strong>{t("admin.gapValidation.error")}</strong> {error}
            </div>
          ) : analysisData ? (
            <>
              {/* Report Info */}
              <div className="bg-gray-100 dark:bg-gray-700 rounded-lg p-4 mb-6">
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                  <div>
                    <span className="text-gray-500 dark:text-gray-400">
                      {t("admin.gapValidation.company")}
                    </span>
                    <p className="font-semibold text-polarNight dark:text-softWhite">
                      {analysisData.companyName || "N/A"}
                    </p>
                  </div>
                  <div>
                    <span className="text-gray-500 dark:text-gray-400">
                      {t("admin.gapValidation.vehicle")}
                    </span>
                    <p className="font-semibold text-polarNight dark:text-softWhite">
                      {analysisData.vehicleVin || "N/A"}
                    </p>
                  </div>
                  <div>
                    <span className="text-gray-500 dark:text-gray-400">
                      {t("admin.gapValidation.period")}
                    </span>
                    <p className="font-semibold text-polarNight dark:text-softWhite">
                      {formatDateToDisplay(analysisData.periodStart)} -{" "}
                      {formatDateToDisplay(analysisData.periodEnd)}
                    </p>
                  </div>
                  <div>
                    <span className="text-gray-500 dark:text-gray-400">
                      {t("admin.gapValidation.totalGaps")}
                    </span>
                    <p className="font-semibold text-polarNight dark:text-softWhite">
                      {analysisData.totalGaps}
                    </p>
                  </div>
                </div>
              </div>

              {analysisData.totalGaps === 0 ? (
                <div className="bg-green-100 border border-green-400 text-green-700 px-4 py-3 rounded text-center">
                  {analysisData.message || t("admin.gapValidation.noGaps")}
                </div>
              ) : (
                <>
                  <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
                    <div className="bg-gradient-to-br from-blue-500 to-blue-600 text-white rounded-lg p-4 text-center">
                      <div className="text-3xl font-bold">
                        {analysisData.averageConfidence}%
                      </div>
                      <div className="text-sm opacity-90">
                        {t("admin.gapValidation.avgConfidence")}
                      </div>
                    </div>
                    <div className="bg-gradient-to-br from-green-500 to-green-600 text-white rounded-lg p-4 text-center">
                      <div className="text-3xl font-bold">
                        {analysisData.summary.highConfidence}
                      </div>
                      <div className="text-sm opacity-90">
                        {t("admin.gapValidation.highConfidence")}
                      </div>
                    </div>
                    <div className="bg-gradient-to-br from-yellow-500 to-yellow-600 text-white rounded-lg p-4 text-center">
                      <div className="text-3xl font-bold">
                        {analysisData.summary.mediumConfidence}
                      </div>
                      <div className="text-sm opacity-90">
                        {t("admin.gapValidation.mediumConfidence")}
                      </div>
                    </div>
                    <div className="bg-gradient-to-br from-red-500 to-red-600 text-white rounded-lg p-4 text-center">
                      <div className="text-3xl font-bold">
                        {analysisData.summary.lowConfidence}
                      </div>
                      <div className="text-sm opacity-90">
                        {t("admin.gapValidation.lowConfidence")}
                      </div>
                    </div>
                  </div>

                  {/* Sezione Statistiche Outages */}
                  {analysisData.outages.total > 0 && (
                    <div className="bg-red-50 dark:bg-red-900/20 border border-red-300 dark:border-red-700 rounded-lg p-4 mb-6">
                      <h4 className="font-semibold text-red-800 dark:text-red-300 mb-3 flex items-center gap-2">
                        <span className="text-xl">⚠️</span>
                        {t("admin.gapValidation.outagesDetectedTitle")}
                      </h4>

                      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                        <div className="bg-white dark:bg-gray-800 rounded p-3 text-center border border-red-200">
                          <div className="text-2xl font-bold text-red-600">{analysisData.outages.total}</div>
                          <div className="text-xs text-gray-600 dark:text-gray-400">{t("admin.gapValidation.outagesTotal")}</div>
                        </div>

                        <div className="bg-white dark:bg-gray-800 rounded p-3 text-center border border-red-200">
                          <div className="text-2xl font-bold text-red-600">{analysisData.outages.gapsAffected}</div>
                          <div className="text-xs text-gray-600 dark:text-gray-400">
                            {t("admin.gapValidation.gapsJustified", { percentage: analysisData.outages.gapsAffectedPercentage })}
                          </div>
                        </div>

                        <div className="bg-white dark:bg-gray-800 rounded p-3 text-center border border-red-200">
                          <div className="text-2xl font-bold text-red-600">
                            {analysisData.outages.totalDowntimeDays}g {analysisData.outages.totalDowntimeHours % 24}h
                          </div>
                          <div className="text-xs text-gray-600 dark:text-gray-400">{t("admin.gapValidation.totalDowntime")}</div>
                        </div>

                        <div className="bg-white dark:bg-gray-800 rounded p-3 text-center border border-red-200">
                          <div className="text-2xl font-bold text-red-600">
                            {analysisData.outages.avgConfidenceWithOutage.toFixed(1)}%
                          </div>
                          <div className="text-xs text-gray-600 dark:text-gray-400">{t("admin.gapValidation.avgConfidenceWithOutage")}</div>
                        </div>
                      </div>
                    </div>
                  )}

                  <div className="bg-amber-50 dark:bg-amber-900/20 border border-amber-300 dark:border-amber-700 rounded-lg p-4 mb-6">
                    <h4 className="font-semibold text-amber-800 dark:text-amber-300 mb-2">
                      {t("admin.gapValidation.disclaimerTitle")}
                    </h4>
                    <p className="text-sm text-amber-700 dark:text-amber-400">
                      {t("admin.gapValidation.disclaimerText")}
                    </p>
                  </div>

                  <div className="overflow-x-auto">
                    <table className="w-full text-sm border-collapse">
                      <thead>
                        <tr className="bg-gray-200 dark:bg-gray-700">
                          <th className="p-3 text-left">
                            {t("admin.gapValidation.timestamp")}
                          </th>
                          <th className="p-3 text-center">
                            {t("admin.gapValidation.confidence")}
                          </th>
                          <th className="p-3 text-left">
                            {t("admin.gapValidation.justification")}
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
                                {/* Badge outage inline */}
                                {gap.outageInfo && (
                                  <div className="mt-1">
                                    {gap.outageInfo.outageType === "Outage Fleet Api" ? (
                                      <span
                                        className="inline-flex items-center gap-1 px-2 py-0.5 bg-red-100 text-red-700 border border-red-400 rounded-full text-xs font-semibold"
                                        title={`Fleet API Outage - ${gap.outageInfo.outageBrand} - Bonus: +${gap.outageInfo.bonusApplied}%`}
                                      >
                                        🔴 {t("admin.gapValidation.outageFleetApi")}
                                      </span>
                                    ) : (
                                      <span
                                        className="inline-flex items-center gap-1 px-2 py-0.5 bg-orange-100 text-orange-700 border border-orange-400 rounded-full text-xs font-semibold"
                                        title={`Vehicle Outage - Bonus: +${gap.outageInfo.bonusApplied}%`}
                                      >
                                        ⚠️ {t("admin.gapValidation.outageVehicle")}
                                      </span>
                                    )}
                                  </div>
                                )}
                              </div>
                            </td>
                            <td className="p-3 text-xs text-gray-600 dark:text-gray-400">
                              {gap.justification}
                              {gap.factors.isTechnicalFailure && (
                                <span className="ml-2 bg-blue-100 text-blue-700 px-2 py-0.5 rounded text-xs">
                                   {t("admin.gapValidation.technicalFailure")}
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

        <div className="border-t border-gray-200 dark:border-gray-600 px-6 py-4 bg-gray-50 dark:bg-gray-900">
          {/* Notes input for Escalate/Breach */}
          {analysisData && analysisData.totalGaps > 0 && (
            <div className="mb-4">
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                {t("admin.gapValidation.notesLabel")}
              </label>
              <textarea
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder={t("admin.gapValidation.notesPlaceholder")}
                className="w-full p-2 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-800 text-sm"
                rows={2}
              />
            </div>
          )}

          <div className="flex flex-wrap gap-3">
            {/* Certifica (verde) - sempre visibile se ci sono gap */}
            {analysisData && analysisData.totalGaps > 0 && (
              <button
                onClick={handleCertify}
                disabled={isProcessing}
                className="px-6 py-2 bg-gradient-to-r from-green-600 to-emerald-600 text-white rounded hover:from-green-700 hover:to-emerald-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
              >
                {certifying ? (
                  <>
                    <AdminGenericLoader inline />
                    {t("admin.gapValidation.certifying")}
                  </>
                ) : (
                  <>{t("admin.gapValidation.certifyBtn")}</>
                )}
              </button>
            )}

            {/* Escalate (arancione) - nascosto se già escalato */}
            {analysisData && analysisData.totalGaps > 0 && showEscalateButton && (
              <button
                onClick={handleEscalate}
                disabled={isProcessing}
                className="px-6 py-2 bg-gradient-to-r from-orange-500 to-amber-500 text-white rounded hover:from-orange-600 hover:to-amber-600 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
              >
                {escalating ? (
                  <>
                    <AdminGenericLoader inline />
                    {t("admin.gapValidation.escalatingBtn")}
                  </>
                ) : (
                  <>{t("admin.gapValidation.escalateBtn")}</>
                )}
              </button>
            )}

            {/* Contract Breach (rosso) - sempre visibile se ci sono gap */}
            {analysisData && analysisData.totalGaps > 0 && (
              <button
                onClick={handleBreach}
                disabled={isProcessing}
                className="px-6 py-2 bg-gradient-to-r from-red-600 to-rose-600 text-white rounded hover:from-red-700 hover:to-rose-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
              >
                {breaching ? (
                  <>
                    <AdminGenericLoader inline />
                    {t("admin.gapValidation.processingBtn")}
                  </>
                ) : (
                  <>{t("admin.gapValidation.breachBtn")}</>
                )}
              </button>
            )}

            {/* Annulla */}
            <button
              onClick={onClose}
              disabled={isProcessing}
              className="px-6 py-2 bg-gray-400 text-white rounded hover:bg-gray-500 transition-colors disabled:opacity-50"
            >
              {t("admin.cancelEditRow")}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
