import { TFunction } from "i18next";
import { PdfReport } from "@/types/reportInterfaces";
import { formatDateToDisplay } from "@/utils/date";
import { useState, useEffect } from "react";
import { NotebookPen, FileBadge, RefreshCw, ShieldCheck, Download, FileLock } from "lucide-react";
import { logFrontendEvent } from "@/utils/logger";
import Chip from "@/components/chip";
import AdminLoader from "@/components/adminLoader";
import NotesModal from "@/components/notesModal";
import GapCertificationModal from "@/components/gapCertificationModal";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";

export default function AdminPdfReports({ t }: { t: TFunction }) {
  const [localReports, setLocalReports] = useState<PdfReport[]>([]);
  const [selectedReportForNotes, setSelectedReportForNotes] =
    useState<PdfReport | null>(null);
  const [downloadingId, setDownloadingId] = useState<number | null>(null);
  const [regeneratingId, setRegeneratingId] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);

  // Stato Gap Certification
  const [selectedReportForCertification, setSelectedReportForCertification] =
    useState<number | null>(null);
  const [downloadingCertId, setDownloadingCertId] = useState<number | null>(null);

  // Stato per certificazione gap in corso (globale)
  const [gapCertProcessing, setGapCertProcessing] = useState<{
    hasProcessing: boolean;
    reportId: number | null;
    companyName: string | null;
    vehicleVin: string | null;
  } | null>(null);

  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [query, setQuery] = useState("");
  const pageSize = 5;

  const fetchReports = async (page: number, searchQuery: string = "") => {
    try {
      setLoading(true);
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      });
      if (searchQuery) params.append("search", searchQuery);

      const res = await fetch(`/api/pdfreports?${params}`);
      if (!res.ok) throw new Error("HTTP " + res.status);

      const data = await res.json();
      setLocalReports(data.data);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
      setCurrentPage(data.page);

      logFrontendEvent(
        "AdminPdfReports",
        "INFO",
        "Reports loaded",
        `Page: ${data.page}, Total: ${data.totalCount}`
      );
    } catch (err) {
      logFrontendEvent(
        "AdminPdfReports",
        "ERROR",
        "Failed to load reports",
        String(err)
      );
    } finally {
      setLoading(false);
    }
  };

  // Fetch stato certificazione gap in corso
  const fetchGapCertStatus = async () => {
    try {
      const res = await fetch("/api/pdfreports/gap-certification-processing");
      if (!res.ok) throw new Error("HTTP " + res.status);
      const data = await res.json();
      setGapCertProcessing(data);
    } catch (err) {
      logFrontendEvent(
        "AdminPdfReports",
        "ERROR",
        "Failed to fetch gap cert status",
        String(err)
      );
    }
  };

  useEffect(() => {
    fetchReports(currentPage, query);
    fetchGapCertStatus();
  }, [currentPage, query]);

  useEffect(() => {
    const processingReports = localReports.filter(
      (r) => r.status === "PROCESSING" || r.status === "REGENERATING"
    );
    const hasGapCertInProgress = gapCertProcessing?.hasProcessing || false;

    // Polling più frequente se c'è qualcosa in corso
    const interval = setInterval(
      () => {
        fetchReports(currentPage, query);
        fetchGapCertStatus();
      },
      processingReports.length > 0 || hasGapCertInProgress ? 10000 : 60000
    );

    return () => clearInterval(interval);
  }, [localReports, currentPage, query, gapCertProcessing?.hasProcessing]);

  const handleSearch = (searchValue: string) => {
    setQuery(searchValue);
    setCurrentPage(1);
  };

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchReports(currentPage, query);
    setIsRefreshing(false);
  };

  const getStatusColor = (statusText: string) => {
    switch (statusText) {
      case "PDF-READY":
        return "bg-green-100 text-green-700 border-green-500";
      case "NO-DATA":
        return "bg-red-100 text-red-700 border-red-500";
      case "PROCESSING":
        return "bg-blue-100 text-blue-700 border-blue-500";
      case "REGENERATING":
        return "bg-purple-100 text-purple-700 border-purple-500";
      case "ERROR":
        return "bg-red-100 text-red-700 border-red-500";
      default:
        return "bg-gray-100 text-polarNight border-gray-400";
    }
  };

  const handleDownload = async (report: PdfReport) => {
    setDownloadingId(report.id);
    try {
      const response = await fetch(`/api/pdfreports/${report.id}/download`);
      if (!response.ok) throw new Error("HTTP " + response.status);

      const blob = await response.blob();
      const contentType = response.headers.get("Content-Type") || "";
      const isHtml = contentType.includes("text/html");
      const fileName = `PolarDrive_PolarReport_${report.id}_${report.vehicleVin}_${report.reportPeriodStart.split("T")[0]}${isHtml ? ".html" : ".pdf"}`;

      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      if (isHtml) alert(t("admin.vehicleReports.pdfNotAvailable"));
    } catch (error) {
      alert(t("admin.vehicleReports.downloadError", { error: String(error) }));
    } finally {
      setDownloadingId(null);
    }
  };

  const handleNotesUpdate = async (updated: PdfReport) => {
    try {
      const response = await fetch(`/api/pdfreports/${updated.id}/notes`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ notes: updated.notes }),
      });

      if (!response.ok) throw new Error("HTTP " + response.status);

      setLocalReports((prev) =>
        prev.map((r) =>
          r.id === updated.id ? { ...r, notes: updated.notes } : r
        )
      );
      setSelectedReportForNotes(null);

      setTimeout(() => fetchReports(currentPage, query), 200);
    } catch {
      alert(t("admin.notesGenericError"));
    }
  };

  const handleRegenerate = async (report: PdfReport) => {
    // 🔒 Check se esiste un report in PROCESSING prima di mostrare l'alert
    try {
      const checkRes = await fetch("/api/pdfreports/has-processing");      
      const checkData = await checkRes.json();
      
      if (checkData.hasProcessing) {
        alert(
          t("admin.vehicleReports.regenerationBlockedTitle") +
            "\n\n" +
            t("admin.vehicleReports.regenerationBlockedMessage", {
              reportId: checkData.processingReportId,
              companyName: checkData.companyName || "N/A",
              vehicleVin: checkData.vehicleVin || "N/A",
            })
        );
        logFrontendEvent(
          "AdminPdfReports",
          "WARNING",
          "Regeneration blocked - another report is PROCESSING",
          "ProcessingReportId: ${checkData.processingReportId}, RequestedReportId: ${report.id}"
        );
        return;
      }
    } catch (checkError) {
      logFrontendEvent(
        "AdminPdfReports",
        "ERROR",
        "Failed to check processing status",
        String(checkError)
      );
    }

    const confirmMessage = t(
      "admin.vehicleReports.regenerateConfirmationMessage",
      {
        id: report.id,
        companyName: report.companyName,
        companyVatNumber: report.companyVatNumber,
        vehicleVin: report.vehicleVin,
        start: formatDateToDisplay(report.reportPeriodStart),
        end: formatDateToDisplay(report.reportPeriodEnd),
        status: report.status,
      }
    );

    if (!confirm(confirmMessage)) {
      return;
    }

    setRegeneratingId(report.id);

    try {
      logFrontendEvent(
        "AdminPdfReports",
        "INFO",
        "Starting report Regeneration",
        `ReportId: ${report.id}`
      );

      const response = await fetch(`/api/pdfreports/${report.id}/regenerate`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
      });

      const data = await response.json();

      if (response.status === 409) {
        alert(
          t("admin.vehicleReports.regenerationBlocked409Title") +
            "\n\n" +
            t("admin.vehicleReports.regenerationBlocked409Message", {
              message: data.message || "",
              pdfHash: data.pdfHash || "N/A",
              generatedAt: data.generatedAt ? formatDateToDisplay(data.generatedAt) : "N/A"
            })
        );

        logFrontendEvent(
          "AdminPdfReports",
          "WARNING",
          "Regeneration blocked - report already exists",
          `ReportId: ${report.id}, ErrorCode: ${data.errorCode}, PdfHash: ${data.pdfHash}`
        );
      } else if (response.status === 400) {
        alert(
          t("admin.vehicleReports.operationNotAllowedTitle") +
            "\n\n" +
            t("admin.vehicleReports.operationNotAllowedMessage", {
              message: data.message || "",
              status: data.status || "N/A"
            })
        );

        logFrontendEvent(
          "AdminPdfReports",
          "WARNING",
          "Regeneration not allowed",
          `ReportId: ${report.id}, ErrorCode: ${data.errorCode}, Status: ${data.status}`
        );
      } else if (response.status === 202) {
        alert(
          t("admin.vehicleReports.regenerationStarted", {
            id: report.id,
            status: data.status,
          })
        );

        logFrontendEvent(
          "AdminPdfReports",
          "INFO",
          "Regeneration started successfully",
          `ReportId: ${report.id}, Status: ${data.status}`
        );

        await fetchReports(currentPage, query);

        let refreshCount = 0;
        const maxRefreshes = 24;
        const refreshInterval = setInterval(async () => {
          refreshCount++;
          await fetchReports(currentPage, query);

          if (refreshCount >= maxRefreshes) {
            clearInterval(refreshInterval);
          }

          const updatedReport = localReports.find((r) => r.id === report.id);
          if (updatedReport && updatedReport.status !== "REGENERATING") {
            clearInterval(refreshInterval);
          }
        }, 5000);
      } else if (!response.ok) {
        throw new Error(
          `HTTP ${response.status}: ${data.error || data.message}`
        );
      }
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);

      alert(
        t("admin.vehicleReports.regenerationErrorTitle") +
          "\n\n" +
          t("admin.vehicleReports.regenerationErrorMessage", {
            reportId: report.id,
            errorMessage
          })
      );

      logFrontendEvent("AdminPdfReports", "ERROR", "Regeneration error");
    } finally {
      setRegeneratingId(null);
    }
  };

  const canRegenerate = (report: PdfReport): boolean => {
    const hasHash = !!report.pdfHash && report.pdfHash.trim().length > 0;

    const hasPdfContent = report.hasPdfFile && report.pdfFileSize > 0;

    // Se ha hash e PDF completo, è immutabile
    if (hasHash && hasPdfContent) {
      return false;
    }

    // Se manca il contenuto PDF (anche se c'è già l'hash),
    // resta rigenerabile solo se in stato ERROR
    return report.status === "ERROR";
  };

  // Nota: Il check per gap viene fatto SOLO quando si apre la modale di certificazione
  // Non facciamo più fetch preventivo per evitare overhead al caricamento

  // Download Gap Certification in PDF
  const handleDownloadCertification = async (reportId: number) => {
    setDownloadingCertId(reportId);
    try {
      const response = await fetch(
        `/api/pdfreports/${reportId}/download-gap-certification`
      );
      if (!response.ok) throw new Error("HTTP " + response.status);

      const blob = await response.blob();
      const fileName = `PolarDrive_GapCertification_${reportId}_${new Date().toISOString().split("T")[0]}.pdf`;

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
        "Gap certification downloaded",
        `ReportId: ${reportId}`
      );
    } catch (error) {
      alert(t("admin.gapCertification.downloadError", { error: String(error) }));
      logFrontendEvent(
        "AdminPdfReports",
        "ERROR",
        "Gap certification download failed",
        String(error)
      );
    } finally {
      setDownloadingCertId(null);
    }
  };

  // Gestione completamento certificazione - aggiorna stato immediatamente poi refresh
  const handleCertificationComplete = (reportId: number) => {
    // 1. Immediatamente aggiorna lo stato locale per disabilitare altri bottoni
    setGapCertProcessing({
      hasProcessing: true,
      reportId: reportId,
      companyName: null,
      vehicleVin: null,
    });

    // 2. Chiudi la modale immediatamente
    setSelectedReportForCertification(null);

    // 3. Poi avvia il refresh in background (non bloccante)
    fetchReports(currentPage, query);
    fetchGapCertStatus();
  };

  return (
    <div className="relative">
      {(loading || isRefreshing) && <AdminLoader local />}

      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.vehicleReports.tableHeader")} ➜ {totalCount}{" "}
          {t("admin.vehicleReports.tableHeaderTotals")}
          {gapCertProcessing?.hasProcessing && (
            <span className="ml-2 text-purple-600 dark:text-purple-400 animate-pulse">
             ➜ {t("admin.gapCertification.processingInProgress", { id: gapCertProcessing.reportId })}
            </span>
          )}
        </h1>
      </div>

      <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
        <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
          <tr>
            <th className="p-4">
              <button
                onClick={handleRefresh}
                disabled={isRefreshing}
                className="px-1 bg-blue-500 text-white rounded text-sm hover:bg-blue-600 disabled:opacity-50"
              >
                <span className="uppercase text-xs tracking-widest">
                  {t("admin.tableRefreshButton")}
                </span>
              </button>
            </th>
            <th className="p-4">{t("admin.generatedInfo")}</th>
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
          {localReports.map((report) => {
            const isDownloadable = report.hasPdfFile || report.hasHtmlFile;
            const fileSize = report.hasPdfFile
              ? report.pdfFileSize
              : report.htmlFileSize;
            const isRegeneratable = canRegenerate(report);
            const isCurrentlyRegenerating = regeneratingId === report.id;

            return (
              <tr
                key={report.id}
                className="border-b border-gray-300 dark:border-gray-600"
              >
                <td className="p-4 space-x-2">
                  <button
                    className="p-2 text-softWhite rounded bg-blue-500 hover:bg-blue-600 disabled:bg-gray-400 disabled:opacity-20 disabled:cursor-not-allowed"
                    disabled={
                      !isDownloadable ||
                      downloadingId === report.id ||
                      report.status === "PROCESSING" ||
                      report.status === "REGENERATING"
                    }
                    onClick={() => handleDownload(report)}
                    title={
                      isDownloadable
                        ? t("admin.pdfReports.downloadReportButton")
                        : t("admin.pdfReports.reportNotAvailable")
                    }
                  >
                    {downloadingId === report.id ? (
                      <AdminLoader inline />
                    ) : (
                      <FileLock size={16} />
                    )}
                  </button>

                  <button
                    className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600 disabled:bg-gray-400 disabled:opacity-20"
                    disabled={
                      downloadingId === report.id ||
                      report.status === "PROCESSING" ||
                      report.status === "REGENERATING"
                    }
                    onClick={() => setSelectedReportForNotes(report)}
                    title={t("admin.pdfReports.editNotesButton")}
                  >
                    <NotebookPen size={16} />
                  </button>

                  {/* Bottone Certificazione GAP - condizionale */}
                  {!isRegeneratable && report.pdfHash && report.hasPdfFile && (
                    <>
                      {/* Se ha certificazione COMPLETED → Download verde */}
                      {report.hasGapCertificationPdf && report.gapCertificationStatus === "COMPLETED" ? (
                        <button
                          className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600 disabled:opacity-50"
                          disabled={downloadingCertId === report.id}
                          onClick={() => handleDownloadCertification(report.id)}
                          title={t("admin.gapCertification.downloadCertification")}
                        >
                          {downloadingCertId === report.id ? (
                            <AdminLoader inline />
                          ) : (
                            <FileBadge size={16} />
                          )}
                        </button>
                      ) : report.gapCertificationStatus === "PROCESSING" ? (
                        /* Se in PROCESSING → Loader viola */
                        <button
                          className="p-2 bg-purple-500 text-softWhite rounded animate-pulse"
                          disabled
                          title={t("admin.gapCertification.certificationInProgress")}
                        >
                          <AdminLoader inline />
                        </button>
                      ) : (
                        /* Altrimenti → Apri modale (disabilitato se altro in corso) */
                        <button
                          className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600 disabled:bg-gray-400 disabled:opacity-20"
                          disabled={
                            gapCertProcessing?.hasProcessing ||
                            downloadingId === report.id ||
                            report.status === "PROCESSING" ||
                            report.status === "REGENERATING"
                          }
                          onClick={() => {
                            const confirmMessage = t("admin.gapCertification.openModalConfirmation");
                            if (confirm(confirmMessage)) {
                              setSelectedReportForCertification(report.id);
                            }
                          }}
                          title={
                            gapCertProcessing?.hasProcessing
                              ? t("admin.gapCertification.anotherInProgress", { id: gapCertProcessing.reportId })
                              : t("admin.gapCertification.openCertificationModal")
                          }
                        >
                          <ShieldCheck size={16} />
                        </button>
                      )}
                    </>
                  )}

                  {isRegeneratable && (
                    <button
                      className={`p-2 rounded transition-all ${
                        isCurrentlyRegenerating
                          ? "bg-orange-500 animate-pulse"
                          : "bg-orange-500 hover:bg-orange-600"
                      } text-softWhite disabled:bg-gray-400 disabled:opacity-20 disabled:cursor-not-allowed`}
                      disabled={
                        isCurrentlyRegenerating ||
                        downloadingId === report.id ||
                        report.status === "REGENERATING"
                      }
                      onClick={() => handleRegenerate(report)}
                      title={
                        report.pdfHash
                          ? t("admin.pdfReports.reportImmutable")
                          : t("admin.pdfReports.regenerateReportButton", { status: report.status })
                      }
                    >
                      {isCurrentlyRegenerating ||
                      report.status === "REGENERATING" ? (
                        <AdminLoader inline />
                      ) : (
                        <RefreshCw size={16} />
                      )}
                    </button>
                  )}
                </td>
                <td className="p-4">
                {report.generatedAt
                    ? formatDateToDisplay(report.generatedAt)
                    : "-"}
                <div className="text-xs text-gray-400 mt-1">
                    ID {report.id}
                </div>
                </td>
                <td className="p-4">
                  <div className="space-y-1 flex flex-col w-[150px]">
                    <Chip className={getStatusColor(report.status)}>
                      {report.status}
                    </Chip>
                    {fileSize > 0 && (
                      <div className="text-xs text-gray-400 flex gap-1 items-center">
                        {report.pdfHash && (
                          <span
                            className="text-xs bg-gray-400 text-gray-200 font-mono cursor-pointer px-1 rounded"
                            title={`${t("admin.vehicleReports.fullHash")}: ${
                              report.pdfHash
                            }\n${t("admin.clickToCopy")}`}
                            onClick={() => {
                              navigator.clipboard.writeText(report.pdfHash!);
                              alert(t("admin.vehicleReports.hashCopied"));
                            }}
                          >
                            HASH
                          </span>
                        )}
                        → {(fileSize / (1024 * 1024)).toFixed(2)} MB
                      </div>
                    )}
                  </div>
                </td>
                <td className="p-4">
                  <div>
                    {formatDateToDisplay(report.reportPeriodStart)} -{" "}
                    {formatDateToDisplay(report.reportPeriodEnd)}
                  </div>
                </td>
                <td className="p-4">
                  <div>
                    {report.companyVatNumber} - {report.companyName}
                  </div>
                </td>
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
          onPrev={() => setCurrentPage((p) => Math.max(1, p - 1))}
          onNext={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
        />
        <SearchBar
          query={query}
          setQuery={setQuery}
          resetPage={() => setCurrentPage(1)}
          onSearch={handleSearch}
          searchMode="id-or-status"
          showVinFilter={true}
          vinPlaceholder={t("admin.vehicles.searchVinPlaceholder")}
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

      {selectedReportForCertification && (
        <GapCertificationModal
          reportId={selectedReportForCertification}
          isOpen={!!selectedReportForCertification}
          onClose={() => setSelectedReportForCertification(null)}
          onCertificationComplete={handleCertificationComplete}
          t={t}
        />
      )}
    </div>
  );
}