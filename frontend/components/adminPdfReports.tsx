import { TFunction } from "i18next";
import { PdfReport } from "@/types/reportInterfaces";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { formatDateToDisplay } from "@/utils/date";
import { useState, useEffect } from "react";
import { NotebookPen, Download, RefreshCw } from "lucide-react";
import { API_BASE_URL } from "@/utils/api";
import { logFrontendEvent } from "@/utils/logger";
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

    // ✅ DEBUG: Mostra la struttura dati reale
    if (reports.length > 0) {
      console.log("🔍 Updated report data structure:", {
        firstReport: reports[0],
        availableProperties: Object.keys(reports[0]),
      });
    }
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

  // ✅ Download migliorato con fallback
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

      // ✅ Usa sempre regenerate=true per sicurezza se non sappiamo lo stato file
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
        throw new Error("Il file ricevuto è vuoto");
      }

      // ✅ Nome file con data
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
        alert(
          t(
            "admin.vehicleReports.downloadHtmlFallback",
            "Download completato (formato HTML)"
          )
        );
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

      let userMessage = t(
        "admin.vehicleReports.downloadError",
        "Errore durante il download"
      );
      if (errorMessage.includes("500")) {
        userMessage = t(
          "admin.vehicleReports.serverError",
          "Errore del server. Riprova tra qualche minuto."
        );
      } else if (errorMessage.includes("404")) {
        userMessage = t(
          "admin.vehicleReports.reportNotFound",
          "Report non trovato."
        );
      } else if (errorMessage.includes("timeout")) {
        userMessage = t(
          "admin.vehicleReports.timeoutError",
          "Timeout. Il report è troppo grande, riprova."
        );
      }
      alert(`${userMessage}\n\nDettagli: ${errorMessage}`);
    } finally {
      setDownloadingId(null);
    }
  };

  // ✅ Rigenerazione con retry e refresh automatico
  const handleRegenerate = async (report: PdfReport) => {
    setRegeneratingId(report.id);

    try {
      console.log("🔄 Starting regeneration for report:", report.id);

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
      console.log("✅ Regeneration API response:", result);

      if (result.success) {
        logFrontendEvent(
          "AdminPdfReports",
          "INFO",
          "Regeneration completed",
          `ReportId: ${report.id}`
        );

        alert(
          t(
            "admin.vehicleReports.regenerationSuccess",
            "Report rigenerato con successo!"
          )
        );

        // ✅ Aggiornamento locale con più dettagli
        setLocalReports((prev) => {
          const updated = prev.map((r) =>
            r.id === report.id
              ? {
                  ...r,
                  notes: `[RIGENERATO] ${new Date().toISOString()} - ${
                    r.notes || ""
                  }`,
                  ...(result.updatedReport && result.updatedReport),
                }
              : r
          );
          console.log("📝 Local reports updated after regeneration");
          return updated;
        });

        // ✅ Refresh del parent con logging migliorato
        if (refreshPdfReports) {
          console.log("⏳ Scheduling parent refresh in 500ms...");
          setTimeout(async () => {
            try {
              console.log("🔄 Executing parent refresh...");
              await refreshPdfReports();
              console.log("✅ Parent refresh completed successfully");
              logFrontendEvent(
                "AdminPdfReports",
                "INFO",
                "Parent refresh completed after regeneration",
                `ReportId: ${report.id}`
              );
            } catch (refreshError) {
              console.error("❌ Parent refresh failed:", refreshError);
              logFrontendEvent(
                "AdminPdfReports",
                "ERROR",
                "Parent refresh failed after regeneration",
                refreshError instanceof Error
                  ? refreshError.message
                  : String(refreshError)
              );
            }
          }, 500);
        } else {
          console.warn("⚠️ refreshPdfReports function not available");
        }
      } else {
        throw new Error(result.message || "Rigenerazione fallita");
      }
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
      console.error("❌ Regeneration failed:", errorMessage);
      logFrontendEvent(
        "AdminPdfReports",
        "ERROR",
        "Regeneration failed",
        `ReportId: ${report.id}, Error: ${errorMessage}`
      );
      alert(
        t(
          "admin.vehicleReports.regenerationError",
          "Errore durante la rigenerazione"
        ) + `\n\n${errorMessage}`
      );
    } finally {
      setRegeneratingId(null);
      console.log("🏁 Regeneration process completed for report:", report.id);
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
        throw new Error(`HTTP ${response.status}: Failed to update notes`);
      }

      // ✅ Aggiorna subito lo stato locale
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

      // ✅ Opzionale: refresh del parent per sincronizzazione
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

  // ✅ Funzione helper per sicurezza - controlla sia PascalCase che camelCase
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

  // ✅ Cast prima a unknown, poi a Record
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

    if (dataCount < 5)
      return {
        text: "Waiting for records",
        color: "bg-blue-100 text-blue-800",
      };
    if (hasHtml)
      return { text: "HTML Only", color: "bg-yellow-100 text-yellow-800" };
    if (hasPdf)
      return { text: "PDF Ready", color: "bg-green-100 text-green-800" };
    return { text: "No Data", color: "bg-red-100 text-red-800" };
  };

  // ✅ Helper che fa il cast automaticamente
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
          {t("admin.vehicleReports.tableHeader")}
        </h1>
        <span className="text-sm text-gray-500">
          ({localReports.length}{" "}
          {t("admin.vehicleReports.totalReports", "report totali")})
        </span>
        {/* 🚨 DEBUG BUTTON - Rimuovi in produzione */}
        <button
          onClick={async () => {
            console.log("🔄 Manual refresh button clicked");
            if (refreshPdfReports) {
              try {
                await refreshPdfReports();
                console.log("✅ Manual refresh completed successfully");
              } catch (error) {
                console.error("❌ Manual refresh failed:", error);
              }
            } else {
              console.warn("⚠️ refreshPdfReports function not available");
            }
          }}
          className="px-3 py-1 bg-blue-500 text-white rounded text-sm hover:bg-blue-600 transition-colors"
          title="Debug: Refresh manuale"
        >
          🔄 Debug Refresh
        </button>
      </div>

      {/* ✅ Tabella - Layout IDENTICO alle altre tabelle */}
      <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
        <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
          <tr>
            <th className="p-4">{t("admin.actions")}</th>
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
            const isDownloadable = dataCount > 5;

            return (
              <tr
                key={report.id}
                className="border-b border-gray-300 dark:border-gray-600"
              >
                {/* ✅ Azioni - Layout identico alle altre tabelle */}
                <td className="p-4 space-x-2 inline-flex">
                  {/* Download button */}
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
                      <Download size={16} />
                    )}
                  </button>

                  {/* Regenerate button */}
                  <button
                    className={`p-2 text-softWhite rounded ${
                      isDownloadable
                        ? "bg-orange-500 hover:bg-orange-600"
                        : "bg-slate-500 cursor-not-allowed opacity-20 text-slate-200"
                    }`}
                    title={t(
                      "admin.vehicleReports.forceRegenerate",
                      "Rigenera"
                    )}
                    disabled={
                      !isDownloadable ||
                      downloadingId === report.id ||
                      regeneratingId === report.id
                    }
                    onClick={() => handleRegenerate(report)}
                  >
                    {regeneratingId === report.id ? (
                      <AdminLoader inline />
                    ) : (
                      <RefreshCw size={16} />
                    )}
                  </button>

                  {/* Notes button */}
                  <button
                    className="p-2 bg-gray-500 text-softWhite rounded hover:bg-gray-600"
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
                  <span className={`text-xs px-2 py-1 rounded ${status.color}`}>
                    {status.text}
                  </span>
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

      {/* ✅ Footer - Layout identico alle altre tabelle */}
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

      {/* Modal note */}
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
