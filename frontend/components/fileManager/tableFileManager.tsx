import { FileManager } from "@/types/adminFileManagerTypes";
import { TFunction } from "i18next";
import { formatDateToDisplay } from "@/utils/date";
import { useEffect, useState } from "react";
import { usePreventUnload } from "@/hooks/usePreventUnload";
import { motion, AnimatePresence } from "framer-motion";
import { FileArchive, NotebookPen, Download, Trash2, FolderArchive, ChevronUp, ChevronDown } from "lucide-react";
import { logFrontendEvent } from "@/utils/logger";
import SearchBar from "@/components/generic/searchBar";
import Chip from "@/components/generic/chip";
import ModalEditNotes from "../generic/modalEditNotes";
import AddFormFileManager from "./addFormFileManager";
import Loader from "../generic/loader";

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
  if (status === "PENDING") return "-";
  if (!startedAt) return "-";

  const start = new Date(startedAt);
  if (isNaN(start.getTime())) return "Invalid date";

  const end = completedAt ? new Date(completedAt) : new Date();
  const diffMs = end.getTime() - start.getTime();

  if (diffMs < 1000) return "0s";

  const diffMins = Math.floor(diffMs / 60000);
  const diffSecs = Math.floor((diffMs % 60000) / 1000);

  if (diffMins >= 60) {
    const hours = Math.floor(diffMins / 60);
    const remainingMins = diffMins % 60;
    return `${hours}h ${remainingMins}m ${diffSecs}s`;
  }

  if (diffMins > 0) return `${diffMins}m ${diffSecs}s`;
  return `${diffSecs}s`;
};

const formatFileSize = (sizeMB: number): string => {
  if (sizeMB < 1) return `${(sizeMB * 1024).toFixed(0)} KB`;
  return `${sizeMB.toFixed(1)} MB`;
};

export default function TableFileManager({ t }: { t: TFunction }) {
  const [localJobs, setLocalJobs] = useState<FileManager[]>([]);
  const [loading, setLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [selectedJobForNotes, setSelectedJobForNotes] =
    useState<FileManager | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [downloadingJobId, setDownloadingJobId] = useState<number | null>(null);

  // Previene refresh pagina durante download
  usePreventUnload(downloadingJobId !== null);

  const [query, setQuery] = useState("");
  const [searchType, setSearchType] = useState<"id" | "status">("id");
  const fileManagerStatuses = [
    "PENDING",
    "PROCESSING",
    "COMPLETED",
    "FAILED",
    "CANCELLED",
    "UPLOADING",
  ];

  const fetchJobs = async (searchQuery: string = "") => {
    try {
      setLoading(true);
      const params = new URLSearchParams();
      if (searchQuery) {
        params.append("search", searchQuery);
        const type = searchType === "id" ? "id" : "status";
        params.append("searchType", type);
      }

      const url = params.toString() ? `/api/filemanager?${params}` : "/api/filemanager";
      const res = await fetch(url);
      if (!res.ok) throw new Error("HTTP " + res.status);

      const data = await res.json();
      setLocalJobs(data.data);

      logFrontendEvent(
        "TableFileManager",
        "INFO",
        "Jobs loaded",
        `Total: ${data.data.length}`
      );
    } catch (err) {
      logFrontendEvent(
        "TableFileManager",
        "ERROR",
        "Failed to load jobs",
        String(err)
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchJobs(query);
  }, [query]);

  useEffect(() => {
    const hasActiveJobs = localJobs.some(
      (j) =>
        j.status === "PENDING" ||
        j.status === "PROCESSING" ||
        j.status === "UPLOADING"
    );

    if (!hasActiveJobs) return;

    const interval = setInterval(() => {
      fetchJobs(query);
    }, 60000);

    return () => clearInterval(interval);
  }, [localJobs, query]);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchJobs(query);
    setIsRefreshing(false);
  };

  const handleDownloadZip = async (job: FileManager) => {
    setDownloadingJobId(job.id);
    if (
      job.status !== "COMPLETED" ||
      !job.zipFileSizeMB ||
      job.zipFileSizeMB <= 0
    )
      return;

    try {
      const response = await fetch(`/api/filemanager/${job.id}/download`, {
        method: "GET",
      });
      if (!response.ok) throw new Error("Download failed");

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
      setDownloadingJobId(null);
    } catch {
      setDownloadingJobId(null);
      alert(t("admin.filemanager.error.downloadZipFailed"));
    }
  };

  const handleDeleteJob = async (job: FileManager) => {
    if (!confirm(t("admin.filemanager.confirmDeleteJob")))
      return;

    try {
      const response = await fetch(`/api/filemanager/${job.id}`, {
        method: "DELETE",
      });
      if (!response.ok) throw new Error("Delete failed");

      setLocalJobs((prev) => prev.filter((j) => j.id !== job.id));
      setTimeout(() => fetchJobs(query), 200);
    } catch {
      alert(t("admin.filemanager.error.deleteJobFailed"));
    }
  };

  const handleCreateSuccess = async () => {
    setShowCreateModal(false);
    await fetchJobs(query);
  };

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: "easeOut", delay: 0.1 }}
      className="relative bg-white dark:bg-gray-900 rounded-2xl shadow-lg border border-gray-200 dark:border-gray-700 overflow-hidden"
    >
      {(loading || isRefreshing || downloadingJobId !== null) && <Loader local />}

      {/* Header con gradiente */}
      <div className="bg-gradient-to-r from-coldIndigo/10 via-purple-500/5 to-glacierBlue/10 dark:from-coldIndigo/20 dark:via-purple-900/10 dark:to-glacierBlue/20 px-6 py-5 border-b border-gray-200 dark:border-gray-700">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="flex items-center space-x-4">
            <div className="flex items-center space-x-3">
              <button
                onClick={handleRefresh}
                disabled={isRefreshing}
                className="p-3 bg-blue-500 hover:bg-blue-600 text-white rounded-lg text-sm font-medium transition-all duration-200 shadow-sm hover:shadow-md"
              >
                {t("admin.tableRefreshButton")}
              </button>
              <button
                onClick={() => setShowCreateModal(!showCreateModal)}
                className={`p-3 ${
                  showCreateModal
                    ? "bg-red-500 hover:bg-red-600"
                    : "bg-blue-500 hover:bg-blue-600"
                } text-white rounded-lg text-sm font-medium transition-all duration-200 shadow-sm hover:shadow-md flex items-center gap-2`}
              >
                {showCreateModal ? (
                  <>
                    <ChevronUp size={18} />
                    {t("admin.filemanager.modal.undoDownloadModal")}
                  </>
                ) : (
                  <>
                    <ChevronDown size={18} />
                    {t("admin.filemanager.modal.createDownloadModal")}
                  </>
                )}
              </button>
            </div>
            <div className="p-3 bg-gradient-to-br from-blue-400 to-indigo-500 rounded-xl shadow-md">
              <FolderArchive size={21} className="text-white" />
            </div>
            <div>
              <h1 className="text-xl sm:text-2xl font-bold text-polarNight dark:text-softWhite">
                {t("admin.filemanager.tableHeader")}
              </h1>
              <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">
                {localJobs.length} {t("admin.totals")}
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Form aggiunta (collapsabile) */}
      <AnimatePresence>
        {showCreateModal && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: "auto", opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.3, ease: "easeInOut" }}
            className="overflow-hidden border-b border-gray-200 dark:border-gray-700"
          >
            <div className="p-6 bg-gray-50 dark:bg-gray-800/50">
              <AddFormFileManager
                isOpen={showCreateModal}
                onClose={() => setShowCreateModal(false)}
                onSuccess={handleCreateSuccess}
                t={t}
              />
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Table Content */}
      <div className="p-6 overflow-x-auto">
        <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
          <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
            <tr>
              <th className="p-4">{t("admin.actions")}</th>
              <th className="p-4">{t("admin.generatedInfo")}</th>
              <th className="p-4">{t("admin.filemanager.status")}</th>
              <th className="p-4">{t("admin.filemanager.period")}</th>
              <th className="p-4">{t("admin.filemanager.duration")}</th>
              <th className="p-4">{t("admin.filemanager.pdfStats")}</th>
              <th className="p-4">{t("admin.filemanager.criteria")}</th>
            </tr>
          </thead>
          <tbody>
            {localJobs.map((job) => (
              <tr
                key={job.id}
                className="border-b border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800"
              >
                <td className="p-4 space-x-2">
                  {job.status === "COMPLETED" &&
                  job.zipFileSizeMB &&
                  job.zipFileSizeMB > 0 ? (
                    <button
                      className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600 disabled:opacity-50"
                      title={t("admin.downloadZip")}
                      onClick={() => handleDownloadZip(job)}
                      disabled={downloadingJobId === job.id}
                    >
                      <FileArchive size={16} />
                    </button>
                  ) : (
                    <button
                      className="p-2 bg-gray-400 text-softWhite rounded cursor-not-allowed"
                      disabled
                    >
                      <Download size={16} />
                    </button>
                  )}
                  <button
                    className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                    onClick={() => setSelectedJobForNotes(job)}
                  >
                    <NotebookPen size={16} />
                  </button>
                  <button
                    className="p-2 bg-red-500 text-softWhite rounded hover:bg-red-600"
                    onClick={() => handleDeleteJob(job)}
                  >
                    <Trash2 size={16} />
                  </button>
                </td>
                <td className="p-4">
                  {formatDateToDisplay(job.requestedAt)}
                  <div className="text-xs text-gray-400 mt-1">
                    ID {job.id} - {t("admin.from")} {job.requestedBy || "-"}
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
                  )}
                </td>
                <td className="p-4">
                  <div className="space-y-1">
                    <div>
                      📄PDF tot {job.includedPdfCount || 0} /{" "}
                      {job.totalPdfCount || 0}
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

        <div className="flex flex-wrap items-center gap-4 mt-4">
          <SearchBar
            query={query}
            setQuery={setQuery}
            resetPage={() => {}}
            searchMode="id-or-status"
            externalSearchType={searchType}
            onSearchTypeChange={(type) => {
              if (type === "id" || type === "status") {
                setSearchType(type);
              }
            }}
            availableStatuses={fileManagerStatuses}
          />
        </div>
      </div>

      {selectedJobForNotes && (
        <ModalEditNotes
          entity={selectedJobForNotes}
          isOpen={!!selectedJobForNotes}
          title={t("admin.filemanager.notes.modalTitle")}
          notesField="notes"
          onSave={async (updated) => {
            try {
              await fetch(`/api/filemanager/${updated.id}/notes`, {
                method: "PATCH",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ notes: updated.notes }),
              });
              setLocalJobs((prev) =>
                prev.map((j) =>
                  j.id === updated.id ? { ...j, notes: updated.notes } : j
                )
              );
              setSelectedJobForNotes(null);
              setTimeout(() => fetchJobs(query), 200);
            } catch {
              alert(t("admin.filemanager.notes.modalErrorUpdate"));
            }
          }}
          onClose={() => setSelectedJobForNotes(null)}
          t={t}
        />
      )}
    </motion.div>
  );
}