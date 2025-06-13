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
      case "HTML Only":
        return "bg-yellow-100 text-yellow-700 border-yellow-500";
      case "WAITING-RECORDS":
        return "bg-blue-100 text-blue-700 border-blue-500";
      case "NO-DATA":
        return "bg-red-100 text-red-700 border-red-500";
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

  const handleDownload = async (
    report: PdfReport,
    forceRegenerate: boolean = false
  ) => {
    setDownloadingId(report.id);
    const startTime = Date.now();

    try {
      logFrontendEvent(
        "AdminPdfReports",
        "INFO",
        "Download initiated",
        `ReportId: ${report.id}, ForceRegenerate: ${forceRegenerate}`
      );

      const regenerateParam = forceRegenerate ? "?regenerate=true" : "";
      const downloadUrl = `${API_BASE_URL}/api/pdfreports/${report.id}/download${regenerateParam}`;

      const response = await fetch(downloadUrl, {
        method: "GET",
        headers: {
          Accept: "application/pdf,text/html,*/*",
        },
      });

      if (!response.ok) {
        let errorMessage = `HTTP ${response.status}: ${response.statusText}`;
        try {
          const errorText = await response.text();
          if (errorText) errorMessage = errorText;
        } catch {}
        throw new Error(errorMessage);
      }

      const blob = await response.blob();
      if (blob.size === 0) {
        throw new Error(t("admin.vehicleReports.blobCheck"));
      }

      let fileName = `PolarDrive_Report_${report.id}_${report.vehicleVin}_${
        report.reportPeriodStart.split("T")[0]
      }.pdf`;

      const contentDisposition = response.headers.get("Content-Disposition");
      if (contentDisposition) {
        const fileNameMatch = contentDisposition.match(
          /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/
        );
        if (fileNameMatch) {
          fileName = fileNameMatch[1].replace(/['"]/g, "");
        }
      }

      const contentType = response.headers.get("Content-Type") || "";
      const isHtml = contentType.includes("text/html");

      if (isHtml) {
        fileName = fileName.replace(".pdf", ".html");
        logFrontendEvent(
          "AdminPdfReports",
          "WARNING",
          "PDF not available, downloading HTML fallback",
          `ReportId: ${report.id}`
        );
      }

      // ✅ Download
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      const downloadTime = Date.now() - startTime;
      logFrontendEvent(
        "AdminPdfReports",
        "INFO",
        "Download completed successfully",
        `ReportId: ${report.id}, Size: ${
          blob.size
        } bytes, Time: ${downloadTime}ms, Type: ${isHtml ? "HTML" : "PDF"}`
      );

      if (isHtml) {
        alert(t("admin.vehicleReports.downloadHtmlFallback"));
      }
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
      logFrontendEvent(
        "AdminPdfReports",
        "ERROR",
        "Download failed",
        `ReportId: ${report.id}, Error: ${errorMessage}`
      );

      let userMessage = t("admin.vehicleReports.downloadError");
      if (errorMessage.includes("500")) {
        userMessage = t("admin.vehicleReports.serverError");
      } else if (errorMessage.includes("404")) {
        userMessage = t("admin.vehicleReports.reportNotFound");
      } else if (errorMessage.includes("timeout")) {
        userMessage = t("admin.vehicleReports.timeoutError");
      }
      alert(`${userMessage}: ${errorMessage}`);
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

  const getReportProperty = <T,>(
    report: Record<string, unknown>,
    propName: string,
    fallback: T
  ): T => {
    const pascalCase = propName;
    const camelCase = propName.charAt(0).toLowerCase() + propName.slice(1);

    if (report[pascalCase] !== undefined) return report[pascalCase] as T;
    if (report[camelCase] !== undefined) return report[camelCase] as T;
    return fallback;
  };

  const getReportStatus = (report: PdfReport) => {
    const hasPdf = getReportProperty<boolean>(
      report as unknown as Record<string, unknown>,
      "HasPdfFile",
      false
    );
    const hasHtml = getReportProperty<boolean>(
      report as unknown as Record<string, unknown>,
      "HasHtmlFile",
      false
    );
    const dataCount = getReportProperty<number>(
      report as unknown as Record<string, unknown>,
      "DataRecordsCount",
      0
    );

    if (dataCount < 5) {
      return { text: "WAITING-RECORDS" };
    }

    if (hasPdf) {
      return { text: "PDF-READY" };
    }

    if (hasHtml) {
      return { text: "HTML Only" };
    }

    return { text: "NO-DATA" };
  };

  const getReportProp = <T,>(
    report: PdfReport,
    propName: string,
    fallback: T
  ): T => {
    return getReportProperty<T>(
      report as unknown as Record<string, unknown>,
      propName,
      fallback
    );
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
            <th className="p-4">
              {t("admin.vehicleReports.fileInfo", "Info File")}
            </th>
            <th className="p-4">
              {t("admin.vehicleReports.dataInfo", "Dati")}
            </th>
            <th className="p-4">{t("admin.vehicleReports.generatedAt")}</th>
          </tr>
        </thead>
        <tbody>
          {currentPageData.map((report) => {
            const status = getReportStatus(report);
            const dataCount = getReportProp<number>(
              report,
              "DataRecordsCount",
              0
            );

            // Download abilitato SOLO se PDF-READY
            const isDownloadable = status.text === "PDF-READY";

            // Regenerate abilitato se ci sono abbastanza dati (anche senza PDF)
            const isRegeneratable = dataCount > 5;

            return (
              <tr
                key={report.id}
                className="border-b border-gray-300 dark:border-gray-600"
              >
                <td className="p-4 space-x-2 inline-flex">
                  {/* Download button - Solo se PDF-READY */}
                  <button
                    className={`p-2 text-softWhite rounded ${
                      isDownloadable
                        ? "bg-blue-500 hover:bg-blue-600"
                        : "bg-slate-500 cursor-not-allowed opacity-20 text-slate-200"
                    }`}
                    title={t("admin.vehicleReports.downloadSinglePdf")}
                    disabled={
                      !isDownloadable ||
                      downloadingId === report.id ||
                      regeneratingId === report.id
                    }
                    onClick={() => handleDownload(report, false)}
                  >
                    {downloadingId === report.id ? (
                      <AdminLoader inline />
                    ) : (
                      <FileBadge size={16} />
                    )}
                  </button>

                  {/* Regenerate button - Se ci sono abbastanza dati */}
                  <button
                    className={`p-2 text-softWhite rounded ${
                      isRegeneratable
                        ? "bg-blue-500 hover:bg-blue-600"
                        : "bg-slate-500 cursor-not-allowed opacity-20 text-slate-200"
                    }`}
                    title={t("admin.vehicleReports.forceRegenerate")}
                    disabled={
                      !isRegeneratable ||
                      downloadingId === report.id ||
                      regeneratingId === report.id
                    }
                    onClick={() => {
                      const confirm = window.confirm(
                        t("admin.vehicleReports.regenerateConfirmAction")
                      );
                      if (!confirm) {
                        logFrontendEvent(
                          "AdminPdfReports",
                          "INFO",
                          "User cancelled regeneration operation",
                          `ReportId: ${report.id}, Status: ${status.text}`
                        );
                        return;
                      }
                      handleRegenerate(report);
                    }}
                  >
                    {regeneratingId === report.id ? (
                      <AdminLoader inline />
                    ) : (
                      <RefreshCw size={16} />
                    )}
                  </button>

                  {/* Notes button - Sempre abilitato */}
                  <button
                    className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                    title={t("admin.openNotesModal")}
                    onClick={() => {
                      setSelectedReportForNotes(report);
                      logFrontendEvent(
                        "AdminPdfReports",
                        "INFO",
                        "Notes modal opened",
                        `ReportId: ${report.id}`
                      );
                    }}
                  >
                    <NotebookPen size={16} />
                  </button>
                </td>

                {/* Periodo */}
                <td className="p-4">
                  {formatDateToDisplay(report.reportPeriodStart)} -{" "}
                  {formatDateToDisplay(report.reportPeriodEnd)}
                </td>

                {/* Company */}
                <td className="p-4">
                  {report.companyVatNumber} - {report.companyName}
                </td>

                {/* Vehicle */}
                <td className="p-4">
                  {report.vehicleVin} - {report.vehicleModel}
                </td>

                {/* Info File */}
                <td className="p-4">
                  <Chip className={getStatusColor(status.text)}>
                    {status.text}
                  </Chip>
                </td>

                {/* Dati */}
                <td className="p-4">{dataCount} record</td>

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
