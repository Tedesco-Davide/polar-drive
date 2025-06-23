import { FileManager } from "@/types/adminFileManagerTypes";
import { TFunction } from "i18next";
import { formatDateToDisplay } from "@/utils/date";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { useEffect, useState } from "react";
import {
  FileArchive,
  NotebookPen,
  Download,
  Trash2,
  Calendar,
  Building,
  Car,
} from "lucide-react";
import { API_BASE_URL } from "@/utils/api";
import { logFrontendEvent } from "@/utils/logger";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import Chip from "@/components/chip";
import NotesModal from "./notesModal";
import AdminFileManagerModal from "./adminFileManagerModal"; // ✅ IMPORTA IL MODAL

// Status options per i job PDF
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
  completedAt: string | null
): string => {
  if (!startedAt) return "-";

  const start = new Date(startedAt);
  const end = completedAt ? new Date(completedAt) : new Date();
  const diffMs = end.getTime() - start.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffSecs = Math.floor((diffMs % 60000) / 1000);

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

type Props = {
  t: TFunction;
  jobs: FileManager[];
  refreshJobs?: () => Promise<FileManager[]>;
};

export default function AdminFileManagerTable({ t, jobs, refreshJobs }: Props) {
  const [localJobs, setLocalJobs] = useState<FileManager[]>([]);
  const [selectedJobForNotes, setSelectedJobForNotes] =
    useState<FileManager | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);

  const handleRefreshJobs = async () => {
    if (refreshJobs) {
      try {
        const updatedJobs = await refreshJobs();
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

  const { query, setQuery, filteredData } = useSearchFilter<FileManager>(
    localJobs,
    [
      "status",
      "infoMessage",
      "companyList",
      "vinList",
      "brandList",
      "requestedBy",
    ]
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
          📁 {t("admin.filemanager.title", "Gestione Download PDF")}
        </h1>

        <button
          className={`${
            showCreateModal
              ? "bg-red-500 hover:bg-red-600"
              : "bg-green-500 hover:bg-green-600"
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
          <FileArchive size={16} />
          {showCreateModal
            ? t("common.cancel", "Annulla")
            : t("admin.filemanager.createDownload", "Crea Nuovo Download")}
        </button>

        {refreshJobs && (
          <button
            onClick={async () => {
              try {
                await handleRefreshJobs();
                alert("✅ Refresh completato");
              } catch {
                alert("❌ Errore durante il refresh");
              }
            }}
            className="px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600 flex items-center gap-2"
            title="Aggiorna lista"
          >
            🔄 Refresh
          </button>
        )}
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
        <table className="w-full text-sm">
          <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
            <tr>
              <th className="p-4">{t("admin.actions", "Azioni")}</th>
              <th className="p-4">
                <Calendar size={16} className="inline mr-1" />
                {t("admin.filemanager.requestedAt", "Richiesto")}
              </th>
              <th className="p-4">{t("admin.filemanager.status", "Stato")}</th>
              <th className="p-4">
                <Calendar size={16} className="inline mr-1" />
                {t("admin.filemanager.period", "Periodo PDF")}
              </th>
              <th className="p-4">
                {t("admin.filemanager.duration", "Durata")}
              </th>
              <th className="p-4">
                📊 {t("admin.filemanager.pdfStats", "Statistiche PDF")}
              </th>
              <th className="p-4">
                <Building size={16} className="inline mr-1" />
                {t("admin.filemanager.companies", "Aziende")}
              </th>
              <th className="p-4">
                <Car size={16} className="inline mr-1" />
                {t("admin.filemanager.vins", "VIN")}
              </th>
              <th className="p-4">📝 {t("admin.filemanager.info", "Info")}</th>
              <th className="p-4">
                👤 {t("admin.filemanager.requestedBy", "Richiesto da")}
              </th>
            </tr>
          </thead>
          <tbody>
            {currentPageData.map((job) => (
              <tr
                key={job.id}
                className="border-b border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800"
              >
                <td className="p-4 space-x-2 flex items-center">
                  {job.status === "COMPLETED" && job.resultZipPath ? (
                    <button
                      className="p-2 bg-green-500 text-softWhite rounded hover:bg-green-600"
                      title={t("admin.filemanager.downloadZip", "Scarica ZIP")}
                      onClick={() => handleDownloadZip(job)}
                    >
                      <FileArchive size={16} />
                    </button>
                  ) : (
                    <button
                      className="p-2 bg-gray-400 text-softWhite rounded cursor-not-allowed"
                      title={t(
                        "admin.filemanager.downloadNotReady",
                        "Download non disponibile"
                      )}
                      disabled
                    >
                      <Download size={16} />
                    </button>
                  )}

                  <button
                    className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                    title={t("admin.openNotesModal", "Apri note")}
                    onClick={() => setSelectedJobForNotes(job)}
                  >
                    <NotebookPen size={16} />
                  </button>

                  <button
                    className="p-2 bg-red-500 text-softWhite rounded hover:bg-red-600"
                    title={t("admin.filemanager.deleteJob", "Elimina job")}
                    onClick={() => handleDeleteJob(job)}
                  >
                    <Trash2 size={16} />
                  </button>
                </td>

                <td className="p-4 text-xs">
                  {formatDateToDisplay(job.requestedAt)}
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

                <td className="p-4 text-xs">
                  {formatJobDuration(job.startedAt, job.completedAt)}
                </td>

                <td className="p-4">
                  <div className="space-y-1">
                    <div className="text-xs">
                      📄 {job.includedPdfCount || 0} / {job.totalPdfCount || 0}{" "}
                      PDF
                    </div>
                    {job.zipFileSizeMB && (
                      <div className="text-xs text-gray-600">
                        📦 {formatFileSize(job.zipFileSizeMB)}
                      </div>
                    )}
                    {job.downloadCount > 0 && (
                      <div className="text-xs text-blue-600">
                        ⬇️ {job.downloadCount}x
                      </div>
                    )}
                  </div>
                </td>

                <td className="p-4">
                  <div className="flex flex-wrap gap-1 max-w-32">
                    {job.companyList?.slice(0, 2).map((company, idx) => (
                      <Chip
                        key={idx}
                        className="bg-blue-100 text-blue-700 border-blue-300 text-xs"
                      >
                        {company.length > 8
                          ? company.substring(0, 8) + "..."
                          : company}
                      </Chip>
                    ))}
                    {job.companyList?.length > 2 && (
                      <Chip className="bg-gray-100 text-gray-600 border-gray-300 text-xs">
                        +{job.companyList.length - 2}
                      </Chip>
                    )}
                  </div>
                </td>

                <td className="p-4">
                  <div className="flex flex-wrap gap-1 max-w-32">
                    {job.vinList?.slice(0, 2).map((vin, idx) => (
                      <Chip
                        key={idx}
                        className="bg-orange-100 text-orange-700 border-orange-300 text-xs"
                      >
                        {vin.length > 8 ? vin.substring(0, 8) + "..." : vin}
                      </Chip>
                    ))}
                    {job.vinList?.length > 2 && (
                      <Chip className="bg-gray-100 text-gray-600 border-gray-300 text-xs">
                        +{job.vinList.length - 2}
                      </Chip>
                    )}
                  </div>
                </td>

                <td className="p-4 max-w-48">
                  <div
                    className="truncate text-xs"
                    title={job.infoMessage || "-"}
                  >
                    {job.infoMessage || "-"}
                  </div>
                </td>

                <td className="p-4 text-xs">{job.requestedBy || "-"}</td>
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
          title={t("admin.filemanager.notes.modalTitle", "Note Download PDF")}
          notesField="infoMessage"
          onSave={async (updated) => {
            try {
              await fetch(
                `${API_BASE_URL}/api/filemanager/${updated.id}/notes`,
                {
                  method: "PATCH",
                  headers: { "Content-Type": "application/json" },
                  body: JSON.stringify({ infoMessage: updated.infoMessage }),
                }
              );

              setLocalJobs((prev) =>
                prev.map((j) =>
                  j.id === updated.id
                    ? { ...j, infoMessage: updated.infoMessage }
                    : j
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
              console.error(
                t("admin.filemanager.notes.genericError", "Errore generico"),
                err
              );
              alert(
                err instanceof Error
                  ? err.message
                  : t(
                      "admin.filemanager.notes.genericError",
                      "Errore durante l'aggiornamento delle note"
                    )
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
