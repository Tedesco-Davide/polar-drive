import { FileManager } from "@/types/adminFileManagerTypes";
import { TFunction } from "i18next";
import { formatDateToDisplay } from "@/utils/date";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { useEffect, useState, useRef } from "react";
import { FileArchive, NotebookPen, Download, Trash2 } from "lucide-react";
import { API_BASE_URL } from "@/utils/api";
import { logFrontendEvent } from "@/utils/logger";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import Chip from "@/components/chip";
import NotesModal from "./notesModal";
import AdminFileManagerModal from "./adminFileManagerModal";
import AdminLoader from "./adminLoader";

const PDF_JOB_STATUS = {
  PENDING: "PENDING",
  PROCESSING: "PROCESSING",
  COMPLETED: "COMPLETED",
  FAILED: "FAILED",
  CANCELLED: "CANCELLED",
} as const;

const getStatusColor = (status: string) => {
  switch (status) {
    case PDF_JOB_STATUS.PENDING:
      return "bg-yellow-100 text-yellow-700 border-yellow-500";
    case PDF_JOB_STATUS.PROCESSING:
      return "bg-blue-100 text-blue-700 border-blue-500";
    case PDF_JOB_STATUS.COMPLETED:
      return "bg-green-100 text-green-700 border-green-500";
    case PDF_JOB_STATUS.FAILED:
      return "bg-red-100 text-red-700 border-red-500";
    case PDF_JOB_STATUS.CANCELLED:
      return "bg-gray-100 text-gray-700 border-gray-500";
    default:
      return "bg-gray-100 text-polarNight border-gray-400";
  }
};

const formatJobDuration = (
  startedAt: string | null,
  completedAt: string | null,
  status?: string
): string => {
  // Se il job non è ancora iniziato (PENDING), mostra "-"
  if (status === "PENDING") return "-";

  // Se non c'è startedAt ma il job è in PROCESSING, mostra durata da ora
  if (!startedAt) return "-";

  const start = new Date(startedAt);

  // Verifica se la data di inizio è valida
  if (isNaN(start.getTime())) {
    console.error("Invalid startedAt date:", startedAt);
    return "Invalid date";
  }

  // ✅ FIX: Usa UTC per entrambe le date
  const end = completedAt ? new Date(completedAt) : new Date();

  // Se il job è ancora in corso, usa l'orario UTC corrente
  const endTimeUTC = completedAt ? end : new Date(Date.now());

  // Calcola la differenza
  const diffMs = endTimeUTC.getTime() - start.getTime();

  // Se la differenza è negativa o troppo piccola (meno di 1 secondo),
  // probabilmente il job è appena iniziato
  if (diffMs < 1000) {
    return "0s";
  }

  // Calcola minuti e secondi
  const diffMins = Math.floor(diffMs / 60000);
  const diffSecs = Math.floor((diffMs % 60000) / 1000);

  // Se sono più di 60 minuti, mostra anche le ore
  if (diffMins >= 60) {
    const hours = Math.floor(diffMins / 60);
    const remainingMins = diffMins % 60;
    return `${hours}h ${remainingMins}m ${diffSecs}s`;
  }

  if (diffMins > 0) {
    return `${diffMins}m ${diffSecs}s`;
  }

  return `${diffSecs}s`;
};

const formatFileSize = (sizeMB: number): string => {
  if (sizeMB < 1) {
    return `${(sizeMB * 1024).toFixed(0)} KB`;
  }
  return `${sizeMB.toFixed(1)} MB`;
};

export default function AdminFileManagerTable({
  jobs,
  t,
  refreshJobs,
  isRefreshing,
  setIsRefreshing,
}: {
  jobs: FileManager[];
  t: TFunction;
  refreshJobs?: () => Promise<FileManager[]>;
  isRefreshing?: boolean;
  setIsRefreshing?: (value: boolean) => void;
}) {
    const refreshRef = useRef(refreshJobs);
    const [localJobs, setLocalJobs] = useState<FileManager[]>([]);
    const [selectedJobForNotes, setSelectedJobForNotes] =
        useState<FileManager | null>(null);
    const [showCreateModal, setShowCreateModal] = useState(false);

    const handleRefreshJobs = async () => {
    if (refreshRef.current) {
        try {
        const updatedJobs = await refreshRef.current();
        setLocalJobs(updatedJobs);
        logFrontendEvent(
            "AdminFileManagerTable",
            "INFO",
            "PDF download jobs refreshed successfully",
            `Updated ${updatedJobs.length} job records`
        );
        } catch (error) {
        logFrontendEvent(
            "AdminFileManagerTable",
            "ERROR",
            "Failed to refresh PDF download jobs",
            `Error: ${error}`
        );
        throw error;
        }
    }
    };

    useEffect(() => {
        setLocalJobs(jobs);
        logFrontendEvent(
        "AdminFileManagerTable",
        "INFO",
        "PDF File Manager component mounted",
        `Loaded ${jobs.length} PDF download job records`
        );
  }, [jobs]);

  useEffect(() => {
    refreshRef.current = refreshJobs;
  }, [refreshJobs]);

  const { query, setQuery, filteredData } = useSearchFilter<FileManager>(
    localJobs,
    ["status", "companyList", "vinList", "brandList", "requestedBy"]
  );

  const {
    currentPage,
    totalPages,
    currentData: currentPageData,
    nextPage,
    prevPage,
    setCurrentPage,
  } = usePagination<FileManager>(filteredData, 10);

  const handleDownloadZip = async (job: FileManager) => {
    if (!job.resultZipPath || job.status !== "COMPLETED") return;

    try {
      const response = await fetch(
        `${API_BASE_URL}/api/filemanager/${job.id}/download`,
        { method: "GET" }
      );

      if (!response.ok) {
        throw new Error(`Download failed: ${response.statusText}`);
      }

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `PDF_Reports_${formatDateToDisplay(
        job.periodStart
      ).replace(/\//g, "")}_${formatDateToDisplay(job.periodEnd).replace(
        /\//g,
        ""
      )}_${job.id}.zip`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      logFrontendEvent(
        "AdminFileManagerTable",
        "INFO",
        "PDF ZIP download completed",
        `Job ID: ${job.id}, Files: ${job.includedPdfCount}, Size: ${job.zipFileSizeMB}MB`
      );
    } catch (error) {
      logFrontendEvent(
        "AdminFileManagerTable",
        "ERROR",
        "Failed to download PDF ZIP",
        `Job ID: ${job.id}, Error: ${error}`
      );
      alert("Errore durante il download del file ZIP");
    }
  };

  const handleDeleteJob = async (job: FileManager) => {
    if (!confirm(`Sei sicuro di voler eliminare questo job di download PDF?`))
      return;

    try {
      const response = await fetch(
        `${API_BASE_URL}/api/filemanager/${job.id}`,
        { method: "DELETE" }
      );

      if (!response.ok) {
        throw new Error(`Delete failed: ${response.statusText}`);
      }

      setLocalJobs((prev) => prev.filter((j) => j.id !== job.id));

      logFrontendEvent(
        "AdminFileManagerTable",
        "INFO",
        "PDF download job deleted",
        `Job ID: ${job.id}`
      );
    } catch (error) {
      logFrontendEvent(
        "AdminFileManagerTable",
        "ERROR",
        "Failed to delete PDF download job",
        `Job ID: ${job.id}, Error: ${error}`
      );
      alert("Errore durante l'eliminazione del job");
    }
  };

  // ✅ FUNZIONE PER GESTIRE IL SUCCESSO DELLA CREAZIONE
  const handleCreateSuccess = async () => {
    try {
      if (refreshJobs) {
        const updatedJobs = await refreshJobs();
        setLocalJobs(updatedJobs);
      }
      setShowCreateModal(false);

      logFrontendEvent(
        "AdminFileManagerTable",
        "INFO",
        "PDF download request created and jobs refreshed",
        ""
      );
    } catch (error) {
      console.warn("Failed to refresh jobs after creation:", error);
      setShowCreateModal(false);
    }
  };

  return (
    <div>
      {/* ✅ HEADER CON BOTTONE COME NEL PATTERN OUTAGES */}
      <div className="flex items-center mb-6 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.filemanager.tableHeader")}
        </h1>

        <button
          className={`${
            showCreateModal
              ? "bg-red-500 hover:bg-red-600"
              : "bg-blue-500 hover:bg-blue-600"
          } text-white px-6 py-2 rounded flex items-center gap-2`}
          onClick={() => {
            setShowCreateModal(!showCreateModal);
            logFrontendEvent(
              "AdminFileManagerTable",
              "INFO",
              "Create modal visibility toggled",
              `Now showing modal: ${!showCreateModal}`
            );
          }}
        >
          {showCreateModal
            ? t("admin.filemanager.modal.undoDownloadModal", "")
            : t("admin.filemanager.modal.createDownloadModal", "")}
        </button>
      </div>

      {/* ✅ MODAL DI CREAZIONE (come nel pattern outages) */}
      {showCreateModal && (
        <AdminFileManagerModal
          isOpen={showCreateModal}
          onClose={() => {
            setShowCreateModal(false);
            logFrontendEvent(
              "AdminFileManagerTable",
              "INFO",
              "Create modal closed",
              ""
            );
          }}
          onSuccess={handleCreateSuccess}
          t={t}
        />
      )}

      {/* TABELLA */}
      <div className="bg-softWhite dark:bg-polarNight rounded-lg overflow-hidden shadow-lg">

        {/* ✅ Loader fuori dalla tabella */}
        {isRefreshing && <AdminLoader />}

        <table className="w-full text-sm">
          <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
            <tr>
              <th className="p-4">
                {refreshRef.current && setIsRefreshing && (
                  <button
                    onClick={async () => {
                      setIsRefreshing(true);
                      try {
                        await handleRefreshJobs();
                        alert(t("admin.filemanager.tableRefreshSuccess"));
                      } catch {
                        alert(t("admin.filemanager.tableRefreshFail"));
                      } finally {
                        setIsRefreshing(false);
                      }
                    }}
                    disabled={isRefreshing}
                    className="px-1 bg-blue-500 text-white rounded text-sm hover:bg-blue-600 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    <span className="uppercase text-xs tracking-widest">
                      {t("admin.tableRefreshButton")}
                    </span>
                  </button>
                )}{" "}
                {t("admin.actions")}
              </th>
              <th className="p-4">{t("admin.filemanager.requestedAt")}</th>
              <th className="p-4">{t("admin.filemanager.status")}</th>
              <th className="p-4">{t("admin.filemanager.period")}</th>
              <th className="p-4">{t("admin.filemanager.duration")}</th>
              <th className="p-4">{t("admin.filemanager.pdfStats")}</th>
              <th className="p-4">{t("admin.filemanager.criteria")}</th>
            </tr>
          </thead>
          <tbody>
            {currentPageData.map((job) => (
              <tr
                key={job.id}
                className="border-b border-gray-300 dark:border-gray-600"
              >
                <td className="p-4 space-x-2">
                  {job.status === "COMPLETED" && job.resultZipPath ? (
                    <button
                      className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                      title={t("admin.downloadZip")}
                      onClick={() => handleDownloadZip(job)}
                    >
                      <FileArchive size={16} />
                    </button>
                  ) : (
                    <button
                      className="p-2 bg-gray-400 text-softWhite rounded cursor-not-allowed"
                      title={t("admin.downloadZipNotReady")}
                      disabled
                    >
                      <Download size={16} />
                    </button>
                  )}

                  <button
                    className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                    title={t("admin.openNotesModal")}
                    onClick={() => setSelectedJobForNotes(job)}
                  >
                    <NotebookPen size={16} />
                  </button>

                  <button
                    className="p-2 bg-red-500 text-softWhite rounded hover:bg-red-600"
                    title={t("admin.filemanager.deleteJob")}
                    onClick={() => handleDeleteJob(job)}
                  >
                    <Trash2 size={16} />
                  </button>
                </td>

                <td className="p-4">
                  {formatDateToDisplay(job.requestedAt)}
                  <div>
                    {t("admin.from")} {job.requestedBy || "-"}
                  </div>
                </td>

                <td className="p-4">
                  <Chip className={getStatusColor(job.status)}>
                    {job.status}
                  </Chip>
                </td>

                <td className="p-4">
                  <div className="text-xs">
                    <div>{formatDateToDisplay(job.periodStart)}</div>
                    <div className="text-gray-500">↓</div>
                    <div>{formatDateToDisplay(job.periodEnd)}</div>
                  </div>
                </td>

                <td className="p-4">
                  {formatJobDuration(
                    job.startedAt,
                    job.completedAt,
                    job.status
                  )}{" "}
                </td>

                <td className="p-4">
                  <div className="space-y-1">
                    <div>
                      📄PDF tot {job.includedPdfCount || 0} /{" "}
                      {job.totalPdfCount || 0}{" "}
                    </div>
                    <div className="flex">
                      📦
                      {job.zipFileSizeMB && (
                        <div>{formatFileSize(job.zipFileSizeMB)}</div>
                      )}
                    </div>
                  </div>
                </td>

                <td className="p-4">
                  <div className="flex flex-wrap gap-2">
                    {job.companyList && job.companyList.length > 0 ? (
                      job.companyList.slice(0, 2).map((company, idx) => (
                        <Chip
                          key={idx}
                          className="bg-blue-100 text-blue-700 border-blue-300"
                        >
                          {company}
                        </Chip>
                      ))
                    ) : (
                      <Chip className="bg-blue-100 text-blue-700 border-blue-300">
                        ALL-COMPANIES
                      </Chip>
                    )}
                    {job.brandList && job.brandList.length > 0 ? (
                      job.brandList.slice(0, 2).map((brand, idx) => (
                        <Chip
                          key={idx}
                          className="bg-purple-100 text-purple-700 border-purple-300"
                        >
                          {brand}
                        </Chip>
                      ))
                    ) : (
                      <Chip className="bg-purple-100 text-purple-700 border-purple-300">
                        ALL-BRAND
                      </Chip>
                    )}
                    {job.vinList && job.vinList.length > 0 ? (
                      job.vinList.map((vin, idx) => (
                        <Chip
                          key={idx}
                          className="bg-orange-100 text-orange-700 border-orange-300"
                        >
                          {vin}
                        </Chip>
                      ))
                    ) : (
                      <Chip className="bg-orange-100 text-orange-700 border-orange-300">
                        ALL-VIN
                      </Chip>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

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

      {/* MODAL PER LE NOTE */}
      {selectedJobForNotes && (
        <NotesModal
          entity={selectedJobForNotes}
          isOpen={!!selectedJobForNotes}
          title={t("admin.filemanager.notes.modalTitle")}
          notesField="notes"
          onSave={async (updated) => {
            try {
              await fetch(
                `${API_BASE_URL}/api/filemanager/${updated.id}/notes`,
                {
                  method: "PATCH",
                  headers: { "Content-Type": "application/json" },
                  body: JSON.stringify({ notes: updated.notes }),
                }
              );

              setLocalJobs((prev) =>
                prev.map((j) =>
                  j.id === updated.id ? { ...j, notes: updated.notes } : j
                )
              );

              setSelectedJobForNotes(null);

              logFrontendEvent(
                "AdminFileManagerTable",
                "INFO",
                "Notes updated for PDF download job",
                `Job ID: ${updated.id}`
              );
            } catch (err) {
              const details = err instanceof Error ? err.message : String(err);
              logFrontendEvent(
                "AdminFileManagerTable",
                "ERROR",
                "Failed to update notes for PDF download job",
                details
              );
              console.error(t("admin.filemanager.notes.modalError"), err);
              alert(
                err instanceof Error
                  ? err.message
                  : t("admin.filemanager.notes.modalErrorUpdate")
              );
            }
          }}
          onClose={() => setSelectedJobForNotes(null)}
          t={t}
        />
      )}
    </div>
  );
}
