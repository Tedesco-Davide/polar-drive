import { TFunction } from "i18next";
import { PdfReport } from "@/types/reportInterfaces";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { formatDateToDisplay } from "@/utils/date";
import { useState, useEffect } from "react";
import { NotebookPen, FileBadge, RefreshCw } from "lucide-react";
import { API_BASE_URL } from "@/utils/api";
import { logFrontendEvent } from "@/utils/logger";
import Chip from "@/components/chip";
import AdminLoader from "@/components/adminLoader";
import NotesModal from "@/components/notesModal";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";

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
  const [selectedReportForNotes, setSelectedReportForNotes] =
    useState<PdfReport | null>(null);
  const [downloadingId, setDownloadingId] = useState<number | null>(null);
  const [regeneratingId, setRegeneratingId] = useState<number | null>(null);

  useEffect(() => {
    setLocalReports(reports);
    logFrontendEvent(
      "AdminPdfReports",
      "INFO",
      "Component reports updated from parent",
      `Loaded ${reports.length} reports`
    );
  }, [reports]);

  const { query, setQuery, filteredData } = useSearchFilter<PdfReport>(
    localReports,
    [
      "companyVatNumber",
      "companyName",
      "vehicleVin",
      "vehicleModel",
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
      default:
        return "bg-gray-100 text-polarNight border-gray-400";
    }
  };

  useEffect(() => {
    logFrontendEvent(
      "AdminPdfReports",
      "DEBUG",
      "Search query updated",
      `Query: ${query}`
    );
  }, [query]);

  const {
    currentPage,
    totalPages,
    currentData: currentPageData,
    nextPage,
    prevPage,
    setCurrentPage,
  } = usePagination<PdfReport>(filteredData, 5);

  useEffect(() => {
    logFrontendEvent(
      "AdminPdfReports",
      "DEBUG",
      "Pagination interaction",
      `Current page: ${currentPage}`
    );
  }, [currentPage]);

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

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`HTTP ${response.status}: ${errorText}`);
      }

      const result = await response.json();

      if (result.success) {
        alert(t("admin.vehicleReports.regenerateReportSuccess"));

        if (refreshPdfReports) {
          await refreshPdfReports();
        }

        logFrontendEvent(
          "AdminPdfReports",
          "INFO",
          "Regeneration completed with full refresh",
          `ReportId: ${report.id}`
        );
      } else {
        throw new Error(
          result.message || t("admin.vehicleReports.regenerateReportFail")
        );
      }
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
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

      if (refreshPdfReports) {
        setTimeout(() => refreshPdfReports(), 200);
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
    if (report.status && report.status !== "") {
      return { text: report.status };
    }

    if (report.hasPdfFile) {
      return { text: "PDF-READY" };
    }

    if (report.hasHtmlFile) {
      return { text: "HTML-ONLY" };
    }

    if (report.dataRecordsCount >= 5) {
      return { text: "GENERATE-READY" };
    }

    if (report.dataRecordsCount < 5 && report.dataRecordsCount > 0) {
      return { text: "WAITING-RECORDS" };
    }

    return { text: "NO-DATA" };
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
              {refreshPdfReports && (
                <button
                  onClick={async () => {
                    try {
                      await refreshPdfReports();
                      alert(t("admin.vehicleReports.tableRefreshSuccess"));
                    } catch {
                      alert(t("admin.vehicleReports.tableRefreshFail"));
                    }
                  }}
                  className="px-1 bg-blue-500 text-white rounded text-sm hover:bg-blue-600"
                >
                  <span className="uppercase text-xs tracking-widest">
                    {t("admin.vehicleReports.tableRefreshButton")}
                  </span>
                </button>
              )}{" "}
              {t("admin.actions")}
            </th>
            <th className="p-4">{t("admin.vehicleReports.reportPeriod")}</th>
            <th className="p-4">
              {t("admin.vehicleReports.clientCompanyVATName")}
            </th>
            <th className="p-4">
              {t("admin.vehicleReports.vehicleVinDisplay")}
            </th>
            <th className="p-4">{t("admin.vehicleReports.fileInfo")}</th>
            <th className="p-4">{t("admin.vehicleReports.generatedAt")}</th>
          </tr>
        </thead>
        <tbody>
          {currentPageData.map((report) => {
            const status = getReportStatus(report);

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
                    className="p-2 text-softWhite rounded bg-blue-500 hover:bg-blue-600"
                    title={t("admin.vehicleReports.downloadSinglePdf")}
                    disabled={!isDownloadable || downloadingId === report.id}
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
                    className="p-2 text-softWhite rounded bg-blue-500 hover:bg-blue-600"
                    title={t("admin.vehicleReports.forceRegenerate")}
                    disabled={regeneratingId === report.id}
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
                    </Chip>
                    {fileSize > 0 && (
                      <div className="text-xs text-gray-400">
                        {(fileSize / (1024 * 1024)).toFixed(2)} MB
                      </div>
                    )}
                  </div>
                </td>

                {/* Generated At */}
                <td className="p-4">
                  {report.generatedAt
                    ? formatDateToDisplay(report.generatedAt)
                    : "-"}
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
