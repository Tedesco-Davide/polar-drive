import { TFunction } from "i18next";
import { PdfReport } from "@/types/reportInterfaces";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { formatDateToDisplay } from "@/utils/date";
import { useState, useEffect, useRef } from "react";
import { NotebookPen, FileBadge, RefreshCw } from "lucide-react";
import { API_BASE_URL } from "@/utils/api";
import { logFrontendEvent } from "@/utils/logger";
import Chip from "@/components/chip";
import AdminLoader from "@/components/adminLoader";
import NotesModal from "@/components/notesModal";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import { ApiErrorResponse, ApiResponse } from "@/types/apiResponse";

export default function AdminPdfReports({
  t,
  reports,
  refreshPdfReports,
}: {
  t: TFunction;
  reports: PdfReport[];
  refreshPdfReports?: () => Promise<PdfReport[] | void>;
}) {
  const [localReports, setLocalReports] = useState<PdfReport[]>([]);
  const refreshRef = useRef(refreshPdfReports);
  const [selectedReportForNotes, setSelectedReportForNotes] =
    useState<PdfReport | null>(null);
  const [downloadingId, setDownloadingId] = useState<number | null>(null);
  const [regeneratingId, setRegeneratingId] = useState<number | null>(null);

  useEffect(() => {
    refreshRef.current = refreshPdfReports;
  }, [refreshPdfReports]);

  useEffect(() => {
    setLocalReports(reports);
    logFrontendEvent(
      "AdminPdfReports",
      "INFO",
      "Component reports updated from parent",
      `Loaded ${reports.length} reports`
    );
  }, [reports]);

  useEffect(() => {
    const processingReports = localReports.filter(
      (r) => getReportStatus(r).text === "PROCESSING"
    );

    let interval: NodeJS.Timeout;

    if (processingReports.length > 0) {
      // Se ci sono report in processing, refresh ogni 10 secondi
      interval = setInterval(() => {
        refreshRef.current?.();
      }, 10000);
    } else {
      // Se non ci sono report in processing, refresh ogni 60 secondi per nuovi report
      interval = setInterval(() => {
        refreshRef.current?.();
      }, 60000);
    }

    return () => clearInterval(interval);
  }, [localReports]);

  const { query, setQuery, filteredData } = useSearchFilter<PdfReport>(
    localReports,
    [
      "companyVatNumber",
      "companyName",
      "vehicleVin",
      "vehicleModel",
      "vehicleBrand",
      "reportPeriodStart",
      "reportPeriodEnd",
    ]
  );

  const getStatusColor = (statusText: string) => {
    switch (statusText) {
      case "PDF-READY":
        return "bg-green-100 text-green-700 border-green-500";
      case "HTML-ONLY":
        return "bg-yellow-100 text-yellow-700 border-yellow-500";
      case "NO-DATA":
        return "bg-red-100 text-red-700 border-red-500";
      case "GENERATE-READY":
        return "bg-blue-100 text-blue-700 border-blue-500";
      case "WAITING-RECORDS":
        return "bg-orange-100 text-orange-700 border-orange-500";
      case "PROCESSING":
        return "bg-blue-100 text-blue-700 border-blue-500";
      case "ERROR":
        return "bg-red-100 text-red-700 border-red-500";
      case "FILE-MISSING":
        return "bg-purple-100 text-purple-700 border-purple-500";
      default:
        return "bg-gray-100 text-polarNight border-gray-400";
    }
  };

  const hasIssues = (report: PdfReport) => {
    return ["FILE-MISSING", "ERROR", "NO-DATA"].includes(report.status);
  };

  const {
    currentPage,
    totalPages,
    currentData: currentPageData,
    nextPage,
    prevPage,
    setCurrentPage,
  } = usePagination<PdfReport>(filteredData, 5);

  const handleDownload = async (report: PdfReport) => {
    setDownloadingId(report.id);

    try {
      logFrontendEvent(
        "AdminPdfReports",
        "INFO",
        "Download started",
        `ReportId: ${report.id}, Type: ${report.reportType}, HasPDF: ${report.hasPdfFile}`
      );

      const downloadUrl = `${API_BASE_URL}/api/pdfreports/${report.id}/download`;

      const response = await fetch(downloadUrl, {
        method: "GET",
        headers: { Accept: "application/pdf,text/html,*/*" },
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const blob = await response.blob();
      const contentType = response.headers.get("Content-Type") || "";
      const isHtml = contentType.includes("text/html");
      const fileName = `PolarDrive_${report.reportType.replace(/\s+/g, "_")}_${
        report.id
      }_${report.vehicleVin}_${report.reportPeriodStart.split("T")[0]}${
        isHtml ? ".html" : ".pdf"
      }`;

      // Download
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      logFrontendEvent(
        "AdminPdfReports",
        "INFO",
        "Download completed",
        `ReportId: ${report.id}, Size: ${blob.size} bytes, Type: ${
          isHtml ? "HTML" : "PDF"
        }`
      );

      if (isHtml) {
        alert(t("admin.vehicleReports.pdfNotAvailable"));
      }
    } catch (error) {
      logFrontendEvent(
        "AdminPdfReports",
        "ERROR",
        "Download failed",
        `ReportId: ${report.id}, Error: ${error}`
      );
      alert(`Errore download: ${error}`);
    } finally {
      setDownloadingId(null);
    }
  };

  const handleRegenerate = async (report: PdfReport) => {
    setRegeneratingId(report.id);

    try {
      logFrontendEvent(
        "AdminPdfReports",
        "INFO",
        "Manual regeneration started",
        `ReportId: ${report.id}`
      );

      const response = await fetch(
        `${API_BASE_URL}/api/pdfreports/${report.id}/regenerate`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
        }
      );

      let result: unknown;
      try {
        result = await response.json();
      } catch {
        // ✅ TRADUZIONE AGGIUNTA
        throw new Error(
          t("admin.vehicleReports.errors.serverError", { 0: response.status })
        );
      }

      if (!response.ok) {
        const errorMessage = getErrorMessage(result, response.status);
        throw new Error(errorMessage);
      }

      // ✅ VERIFICA CHE LA RISPOSTA SIA UNA SUCCESS RESPONSE
      if (result && typeof result === "object" && "success" in result) {
        const apiResult = result as ApiResponse;

        if (apiResult.success) {
          setLocalReports((prev) =>
            prev.map((r) =>
              r.id === report.id
                ? {
                    ...r,
                    status: "PROCESSING",
                    regenerationCount: r.regenerationCount + 1,
                  }
                : r
            )
          );

          alert(t("admin.vehicleReports.regenerateReportSuccess"));

          if (refreshRef.current) {
            await refreshRef.current();
          }

          logFrontendEvent(
            "AdminPdfReports",
            "INFO",
            "Regeneration completed with immediate refresh",
            `ReportId: ${report.id}`
          );
        } else {
          throw new Error(
            apiResult.message || t("admin.vehicleReports.regenerateReportFail")
          );
        }
      } else {
        // ✅ TRADUZIONE AGGIUNTA
        throw new Error(t("admin.vehicleReports.errors.invalidServerResponse"));
      }
    } catch (error) {
      const errorMessage =
        error instanceof Error
          ? error.message
          : t("admin.vehicleReports.errors.unknownRegeneration");

      logFrontendEvent(
        "AdminPdfReports",
        "ERROR",
        "Regeneration failed",
        `ReportId: ${report.id}, Error: ${errorMessage}`
      );

      alert(
        `${t("admin.vehicleReports.regenerateReportError")}: ${errorMessage}`
      );
    } finally {
      setRegeneratingId(null);
    }
  };

  const isApiErrorResponse = (
    response: unknown
  ): response is ApiErrorResponse => {
    return (
      typeof response === "object" &&
      response !== null &&
      "success" in response &&
      (response as ApiErrorResponse).success === false &&
      "message" in response &&
      typeof (response as ApiErrorResponse).message === "string"
    );
  };

  // ✅ ENUM PER I CODICI DI ERRORE (OPZIONALE, MA ANCORA MEGLIO)
  enum RegenerationErrorCode {
    ALREADY_PROCESSING = "ALREADY_PROCESSING",
    NO_DATA_AVAILABLE = "NO_DATA_AVAILABLE",
    INSUFFICIENT_DATA = "INSUFFICIENT_DATA",
    VEHICLE_DELETED = "VEHICLE_DELETED",
    COMPANY_DELETED = "COMPANY_DELETED",
    MAX_REGENERATIONS_REACHED = "MAX_REGENERATIONS_REACHED",
    INTERNAL_ERROR = "INTERNAL_ERROR",
  }

  // ✅ VERSIONE CON ENUM (ANCORA PIÙ TYPE-SAFE)
  const getErrorMessage = (result: unknown, statusCode: number): string => {
    if (isApiErrorResponse(result) && result.code) {
      switch (result.code as RegenerationErrorCode) {
        case RegenerationErrorCode.ALREADY_PROCESSING:
          return t("admin.vehicleReports.errors.alreadyProcessing");
        case RegenerationErrorCode.NO_DATA_AVAILABLE:
          return t("admin.vehicleReports.errors.noDataAvailable");
        case RegenerationErrorCode.INSUFFICIENT_DATA:
          return t("admin.vehicleReports.errors.insufficientData");
        case RegenerationErrorCode.VEHICLE_DELETED:
          return t("admin.vehicleReports.errors.vehicleDeleted");
        case RegenerationErrorCode.COMPANY_DELETED:
          return t("admin.vehicleReports.errors.companyDeleted");
        case RegenerationErrorCode.MAX_REGENERATIONS_REACHED:
          return t("admin.vehicleReports.errors.maxRegenerationsReached");
        default:
          return (
            result.message ||
            t("admin.vehicleReports.errors.unknownRegeneration")
          );
      }
    }

    if (isApiErrorResponse(result)) {
      return result.message;
    }

    // Errori HTTP generici basati sul status code
    switch (statusCode) {
      case 404:
        return t("admin.vehicleReports.errors.reportNotFound");
      case 400:
        return t("admin.vehicleReports.errors.badRequest");
      case 401:
        return t("admin.vehicleReports.errors.unauthorized");
      case 403:
        return t("admin.vehicleReports.errors.forbidden");
      case 500:
        return t("admin.vehicleReports.errors.internalServer");
      case 503:
        return t("admin.vehicleReports.errors.serviceUnavailable");
      default:
        return t("admin.vehicleReports.errors.defaultRegeneration");
    }
  };

  const handleNotesUpdate = async (updated: PdfReport) => {
    try {
      const response = await fetch(
        `${API_BASE_URL}/api/pdfreports/${updated.id}/notes`,
        {
          method: "PATCH",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ notes: updated.notes }),
        }
      );

      if (!response.ok) {
        throw new Error(
          `HTTP ${response.status}: ${t(
            "admin.vehicleReports.failedUpdateNotes"
          )}`
        );
      }

      setLocalReports((prev) =>
        prev.map((r) =>
          r.id === updated.id ? { ...r, notes: updated.notes } : r
        )
      );

      setSelectedReportForNotes(null);

      logFrontendEvent(
        "AdminPdfReports",
        "INFO",
        "Notes updated",
        `ReportId: ${updated.id}`
      );

      if (refreshRef.current) {
        setTimeout(() => refreshRef.current?.(), 200);
      }
    } catch (err) {
      const details = err instanceof Error ? err.message : String(err);
      logFrontendEvent(
        "AdminPdfReports",
        "ERROR",
        "Failed to update notes",
        details
      );
      alert(t("admin.notesGenericError"));
    }
  };

  const getReportStatus = (report: PdfReport) => {
    return { text: report.status || "UNKNOWN" };
  };

  return (
    <div>
      {/* ✅ Header con statistiche semplici */}
      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.vehicleReports.tableHeader")}: {localReports.length}{" "}
          {t("admin.vehicleReports.tableHeaderTotals")}
        </h1>
      </div>

      <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
        <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
          <tr>
            <th className="p-4">
              {refreshRef.current && (
                <button
                  onClick={async () => {
                    try {
                      await refreshRef.current?.();
                      alert(t("admin.vehicleReports.tableRefreshSuccess"));
                    } catch {
                      alert(t("admin.vehicleReports.tableRefreshFail"));
                    }
                  }}
                  className="px-1 bg-blue-500 text-white rounded text-sm hover:bg-blue-600"
                >
                  <span className="uppercase text-xs tracking-widest">
                    {t("admin.tableRefreshButton")}
                  </span>
                </button>
              )}{" "}
              {t("admin.actions")}
            </th>
            <th className="p-4">{t("admin.vehicleReports.generatedAt")}</th>
            <th className="p-4">{t("admin.vehicleReports.fileInfo")}</th>
            <th className="p-4">{t("admin.vehicleReports.reportPeriod")}</th>
            <th className="p-4">
              {t("admin.vehicleReports.clientCompanyVATName")}
            </th>
            <th className="p-4">
              {t("admin.vehicleReports.vehicleVinDisplay")}
            </th>
          </tr>
        </thead>
        <tbody>
          {currentPageData.map((report) => {
            const status = getReportStatus(report);
            const needsAttention = hasIssues(report);
            const dataCount = report.dataRecordsCount;
            const isDownloadable = report.hasPdfFile || report.hasHtmlFile;
            const fileSize = report.hasPdfFile
              ? report.pdfFileSize
              : report.htmlFileSize;

            return (
              <tr
                key={report.id}
                className="border-b border-gray-300 dark:border-gray-600"
              >
                {/* Actions Column */}
                <td className="p-4 space-x-2">
                  {/* Download Button */}
                  <button
                    className="p-2 text-softWhite rounded bg-blue-500 hover:bg-blue-600 disabled:bg-gray-400 disabled:opacity-20 disabled:cursor-not-allowed"
                    title={
                      status.text === "PROCESSING"
                        ? t("admin.vehicleReports.downloadDisabledProcessing")
                        : t("admin.vehicleReports.downloadSinglePdf")
                    }
                    disabled={
                      !isDownloadable ||
                      downloadingId === report.id ||
                      status.text === "PROCESSING"
                    }
                    onClick={() => handleDownload(report)}
                  >
                    {downloadingId === report.id ? (
                      <AdminLoader inline />
                    ) : (
                      <FileBadge size={16} />
                    )}
                  </button>

                  {/* Regenerate Button */}
                  <button
                    className="p-2 text-softWhite rounded bg-blue-500 hover:bg-blue-600 disabled:bg-gray-400 disabled:opacity-20 disabled:cursor-not-allowed"
                    title={
                      status.text === "PROCESSING"
                        ? t("admin.vehicleReports.regenerateDisabledProcessing")
                        : t("admin.vehicleReports.forceRegenerate")
                    }
                    disabled={
                      regeneratingId === report.id ||
                      status.text === "PROCESSING"
                    }
                    onClick={() => {
                      const message = t(
                        "admin.vehicleReports.regenerateConfirmAction"
                      );
                      if (window.confirm(message)) {
                        handleRegenerate(report);
                      }
                    }}
                  >
                    {regeneratingId === report.id ? (
                      <AdminLoader inline />
                    ) : (
                      <RefreshCw size={16} />
                    )}
                  </button>

                  {/* Notes Button */}
                  <button
                    className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                    title={t("admin.openNotesModal")}
                    onClick={() => setSelectedReportForNotes(report)}
                  >
                    <NotebookPen size={16} />
                  </button>
                </td>

                {/* Generated At */}
                <td className="p-4">
                  {report.generatedAt
                    ? formatDateToDisplay(report.generatedAt)
                    : "-"}
                  {report.monitoringDurationHours >= 0 && (
                    <div className="text-xs text-gray-400 mt-1">
                      {report.monitoringDurationHours < 1
                        ? "< 1h"
                        : `${Math.round(report.monitoringDurationHours)}h`}{" "}
                      {t("admin.vehicleReports.monitored")}
                    </div>
                  )}
                </td>

                {/* File Info */}
                <td className="p-4">
                  <div className="space-y-1">
                    <Chip className={getStatusColor(status.text)}>
                      {status.text}
                      {needsAttention}
                    </Chip>
                    {fileSize > 0 && (
                      <div className="text-xs text-gray-400">
                        {(fileSize / (1024 * 1024)).toFixed(2)} MB
                      </div>
                    )}
                  </div>
                </td>

                {/* Period */}
                <td className="p-4">
                  <div>
                    {formatDateToDisplay(report.reportPeriodStart)} -{" "}
                    {formatDateToDisplay(report.reportPeriodEnd)}
                    <div className="text-xs text-gray-400">
                      {dataCount} {t("admin.vehicleReports.totalRecords")}
                    </div>
                  </div>
                </td>

                {/* Company */}
                <td className="p-4">
                  <div>
                    {report.companyVatNumber} - {report.companyName}
                  </div>
                  <div className="flex flex-wrap items-center gap-1">
                    {report.reportType && (
                      <div className="text-xs text-gray-400 mt-1">
                        {t(report.reportType)}
                      </div>
                    )}
                    {report.isRegenerated && (
                      <div className="text-xs text-orange-600 font-medium mt-1">
                        - {t("admin.vehicleReports.regenerated")}{" "}
                        {report.regenerationCount}x
                      </div>
                    )}
                  </div>
                </td>

                {/* Vehicle */}
                <td className="p-4">
                  <div>
                    {report.vehicleVin && <span>{report.vehicleVin}</span>}
                    <div className="text-xs text-gray-400">
                      {report.vehicleBrand && (
                        <span>{report.vehicleBrand}</span>
                      )}
                    </div>
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>

      <div className="flex flex-wrap items-center gap-4 mt-4">
        <PaginationControls
          currentPage={currentPage}
          totalPages={totalPages}
          onPrev={prevPage}
          onNext={nextPage}
        />
        <SearchBar
          query={query}
          setQuery={setQuery}
          resetPage={() => setCurrentPage(1)}
        />
      </div>

      {selectedReportForNotes && (
        <NotesModal
          entity={selectedReportForNotes}
          isOpen={!!selectedReportForNotes}
          title={t("admin.vehicleReports.notesModalTitle")}
          notesField="notes"
          onSave={handleNotesUpdate}
          onClose={() => setSelectedReportForNotes(null)}
          t={t}
        />
      )}
    </div>
  );
}
