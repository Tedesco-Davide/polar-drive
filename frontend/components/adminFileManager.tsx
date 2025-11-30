import { FileManager } from "@/types/adminFileManagerTypes";
import { TFunction } from "i18next";
import { formatDateToDisplay } from "@/utils/date";
import { useEffect, useState } from "react";
import { FileArchive, NotebookPen, Download, Trash2 } from "lucide-react";
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

export default function AdminFileManagerTable({ t }: { t: TFunction }) {
  const [localJobs, setLocalJobs] = useState<FileManager[]>([]);
  const [loading, setLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [selectedJobForNotes, setSelectedJobForNotes] =
    useState<FileManager | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [downloadingJobId, setDownloadingJobId] = useState<number | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [query, setQuery] = useState("");
  const [searchType, setSearchType] = useState<"id" | "status">("id");
  const pageSize = 10;
  const fileManagerStatuses = [
    "PENDING",
    "PROCESSING",
    "COMPLETED",
    "FAILED",
    "CANCELLED",
    "UPLOADING",
  ];

  const fetchJobs = async (page: number, searchQuery: string = "") => {
    try {
      setLoading(true);
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      });
      if (searchQuery) {
        params.append("search", searchQuery);
        const type = searchType === "id" ? "id" : "status";
        params.append("searchType", type);
      }

      const res = await fetch(`/api/filemanager?${params}`);
      if (!res.ok) throw new Error("HTTP " + res.status);

      const data = await res.json();
      setLocalJobs(data.data);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
      setCurrentPage(data.page);

      logFrontendEvent(
        "AdminFileManagerTable",
        "INFO",
        "Jobs loaded",
        `Page: ${data.page}, Total: ${data.totalCount}`
      );
    } catch (err) {
      logFrontendEvent(
        "AdminFileManagerTable",
        "ERROR",
        "Failed to load jobs",
        String(err)
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchJobs(currentPage, query);
  }, [currentPage, query]);

  useEffect(() => {
    const hasActiveJobs = localJobs.some(
      (j) =>
        j.status === "PENDING" ||
        j.status === "PROCESSING" ||
        j.status === "UPLOADING"
    );

    if (!hasActiveJobs) return;

    const interval = setInterval(() => {
      fetchJobs(currentPage, query);
    }, 60000);

    return () => clearInterval(interval);
  }, [localJobs, currentPage, query]);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchJobs(currentPage, query);
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
      alert("Errore durante il download del file ZIP");
    }
  };

  const handleDeleteJob = async (job: FileManager) => {
    if (!confirm("Sei sicuro di voler eliminare questo job di download PDF?"))
      return;

    try {
      const response = await fetch(`/api/filemanager/${job.id}`, {
        method: "DELETE",
      });
      if (!response.ok) throw new Error("Delete failed");

      setLocalJobs((prev) => prev.filter((j) => j.id !== job.id));
      setTimeout(() => fetchJobs(currentPage, query), 200);
    } catch {
      alert("Errore durante l'eliminazione del job");
    }
  };

  const handleCreateSuccess = async () => {
    setShowCreateModal(false);
    await fetchJobs(currentPage, query);
  };

  return (
    <div className="relative">
      {(loading || isRefreshing) && <AdminLoader local />}

      <div className="flex items-center mb-6 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.filemanager.tableHeader")} ➜ {totalCount}
        </h1>
        <button
          className={`${
            showCreateModal
              ? "bg-red-500 hover:bg-red-600"
              : "bg-blue-500 hover:bg-blue-600"
          } text-white px-6 py-2 rounded`}
          onClick={() => setShowCreateModal(!showCreateModal)}
        >
          {showCreateModal
            ? t("admin.filemanager.modal.undoDownloadModal")
            : t("admin.filemanager.modal.createDownloadModal")}
        </button>
      </div>

      {showCreateModal && (
        <AdminFileManagerModal
          isOpen={showCreateModal}
          onClose={() => setShowCreateModal(false)}
          onSuccess={handleCreateSuccess}
          t={t}
        />
      )}

      <div className="bg-softWhite dark:bg-polarNight rounded-lg overflow-hidden shadow-lg">
        <table className="w-full text-sm">
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
                </button>{" "}
                {t("admin.actions")}
              </th>
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
                className="border-b border-gray-300 dark:border-gray-600"
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
                      {downloadingJobId === job.id ? (
                        <AdminLoader inline />
                      ) : (
                        <FileArchive size={16} />
                      )}
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
      </div>

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
          searchMode="id-or-status"
          externalSearchType={searchType}
          onSearchTypeChange={setSearchType}
          availableStatuses={fileManagerStatuses}
        />
      </div>

      {selectedJobForNotes && (
        <NotesModal
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
              setTimeout(() => fetchJobs(currentPage, query), 200);
            } catch {
              alert(t("admin.filemanager.notes.modalErrorUpdate"));
            }
          }}
          onClose={() => setSelectedJobForNotes(null)}
          t={t}
        />
      )}
    </div>
  );
}
