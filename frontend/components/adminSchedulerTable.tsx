import { ScheduledFileJob } from "@/types/adminSchedulerTypes";
import { TFunction } from "i18next";
import { formatDateToDisplay } from "@/utils/date";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { useEffect, useState } from "react";
import { FileArchive, NotebookPen, Download } from "lucide-react";
import { API_BASE_URL } from "@/utils/api";
import { logFrontendEvent } from "@/utils/logger";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import Chip from "@/components/chip";
import NotesModal from "./notesModal";

// Status options for jobs
const JOB_STATUS = {
  PENDING: "PENDING",
  RUNNING: "RUNNING",
  COMPLETED: "COMPLETED",
  FAILED: "FAILED",
  CANCELLED: "CANCELLED",
} as const;

const getStatusColor = (status: string) => {
  switch (status) {
    case JOB_STATUS.PENDING:
      return "bg-yellow-100 text-yellow-700 border-yellow-500";
    case JOB_STATUS.RUNNING:
      return "bg-blue-100 text-blue-700 border-blue-500";
    case JOB_STATUS.COMPLETED:
      return "bg-green-100 text-green-700 border-green-500";
    case JOB_STATUS.FAILED:
      return "bg-red-100 text-red-700 border-red-500";
    case JOB_STATUS.CANCELLED:
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

type Props = {
  t: TFunction;
  jobs: ScheduledFileJob[];
  refreshJobs?: () => Promise<ScheduledFileJob[]>;
};

export default function AdminSchedulerTable({ t, jobs, refreshJobs }: Props) {
  const [localJobs, setLocalJobs] = useState<ScheduledFileJob[]>([]);
  const [selectedJobForNotes, setSelectedJobForNotes] =
    useState<ScheduledFileJob | null>(null);

  const handleRefreshJobs = async () => {
    if (refreshJobs) {
      try {
        const updatedJobs = await refreshJobs();
        setLocalJobs(updatedJobs);
        logFrontendEvent(
          "AdminSchedulerTable",
          "INFO",
          "Jobs refreshed successfully",
          `Updated ${updatedJobs.length} job records`
        );
      } catch (error) {
        logFrontendEvent(
          "AdminSchedulerTable",
          "ERROR",
          "Failed to refresh jobs",
          `Error: ${error}`
        );
      }
    }
  };

  useEffect(() => {
    setLocalJobs(jobs);
    logFrontendEvent(
      "AdminSchedulerTable",
      "INFO",
      "Component mounted and jobs data initialized",
      `Loaded ${jobs.length} job records`
    );
  }, [jobs]);

  const { query, setQuery, filteredData } = useSearchFilter<ScheduledFileJob>(
    localJobs,
    [
      "status",
      "infoMessage",
      "fileTypeList",
      "companyList",
      "brandList",
      "consentTypeList",
      "outageTypeList",
    ]
  );

  useEffect(() => {
    logFrontendEvent(
      "AdminSchedulerTable",
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
  } = usePagination<ScheduledFileJob>(filteredData, 5);

  useEffect(() => {
    logFrontendEvent(
      "AdminSchedulerTable",
      "DEBUG",
      "Pagination interaction",
      `Current page: ${currentPage}`
    );
  }, [currentPage]);

  const handleDownloadZip = async (job: ScheduledFileJob) => {
    if (!job.resultZipPath) return;

    try {
      // If it's a full URL, open directly
      if (job.resultZipPath.startsWith("http")) {
        window.open(job.resultZipPath, "_blank");
      } else {
        // Otherwise construct API endpoint
        window.open(
          `${API_BASE_URL}/api/scheduler/${job.id}/download`,
          "_blank"
        );
      }

      logFrontendEvent(
        "AdminSchedulerTable",
        "INFO",
        "Job ZIP download triggered",
        `Job ID: ${job.id}, Status: ${job.status}`
      );
    } catch (error) {
      logFrontendEvent(
        "AdminSchedulerTable",
        "ERROR",
        "Failed to download job ZIP",
        `Job ID: ${job.id}, Error: ${error}`
      );
    }
  };

  return (
    <div>
      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.scheduler.tableHeader")}
        </h1>
      </div>

      <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
        <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
          <tr>
            <th className="p-4">
              {refreshJobs && (
                <button
                  onClick={handleRefreshJobs}
                  className="px-1 bg-blue-500 text-white rounded text-sm hover:bg-blue-600"
                  title="Refresh jobs"
                >
                  <span className="uppercase text-xs tracking-widest">
                    REFRESH
                  </span>
                </button>
              )}{" "}
              {t("admin.actions")}
            </th>
            <th className="p-4">{t("admin.scheduler.requestedAt")}</th>
            <th className="p-4">{t("admin.scheduler.status")}</th>
            <th className="p-4">{t("admin.scheduler.period")}</th>
            <th className="p-4">{t("admin.scheduler.duration")}</th>
            <th className="p-4">{t("admin.scheduler.generatedCount")}</th>
            <th className="p-4">{t("admin.scheduler.fileTypes")}</th>
            <th className="p-4">{t("admin.scheduler.companies")}</th>
            <th className="p-4">{t("admin.scheduler.brand")}</th>
            <th className="p-4">{t("admin.scheduler.message")}</th>
            <th className="p-4">{t("admin.scheduler.startedAtcompletedAt")}</th>
          </tr>
        </thead>
        <tbody>
          {currentPageData.map((job) => (
            <tr
              key={job.id}
              className="border-b border-gray-300 dark:border-gray-600"
            >
              <td className="p-4 space-x-2 inline-flex items-center">
                {job.resultZipPath ? (
                  <button
                    className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                    title={t("admin.scheduler.downloadZip")}
                    onClick={() => handleDownloadZip(job)}
                  >
                    <FileArchive size={16} />
                  </button>
                ) : (
                  <button
                    className="p-2 bg-gray-400 text-softWhite rounded cursor-not-allowed"
                    title={t("admin.scheduler.noZipAvailable")}
                    disabled
                  >
                    <Download size={16} />
                  </button>
                )}

                <button
                  className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                  title={t("admin.openNotesModal")}
                  onClick={() => {
                    setSelectedJobForNotes(job);
                    logFrontendEvent(
                      "AdminSchedulerTable",
                      "INFO",
                      "Notes modal opened for job",
                      `Job ID: ${job.id}, Status: ${job.status}`
                    );
                  }}
                >
                  <NotebookPen size={16} />
                </button>
              </td>

              <td className="p-4 text-xs">
                {formatDateToDisplay(job.requestedAt)}
              </td>

              <td className="p-4">
                <Chip className={getStatusColor(job.status)}>{job.status}</Chip>
              </td>

              <td className="p-4">
                <div className="flex flex-col space-y-1">
                  <span>{formatDateToDisplay(job.periodStart)} -</span>
                  <span>{formatDateToDisplay(job.periodEnd)}</span>
                </div>
              </td>

              <td className="p-4">
                {formatJobDuration(job.startedAt, job.completedAt)}
              </td>

              <td className="p-4 text-center">
                <span className="inline-flex items-center justify-center px-2 py-1 bg-blue-100 text-blue-800 rounded-full text-xs font-medium">
                  {job.generatedFilesCount}
                </span>
              </td>

              <td className="p-4">
                <div className="flex flex-wrap gap-1 max-w-32">
                  {job.fileTypeList?.slice(0, 2).map((type, idx) => (
                    <Chip
                      key={idx}
                      className="bg-purple-100 text-purple-700 border-purple-300 text-xs"
                    >
                      {type}
                    </Chip>
                  ))}
                  {job.fileTypeList?.length > 2 && (
                    <Chip className="bg-gray-100 text-gray-600 border-gray-300 text-xs">
                      +{job.fileTypeList.length - 2}
                    </Chip>
                  )}
                </div>
              </td>

              <td className="p-4">
                <div className="flex flex-wrap gap-1 max-w-32">
                  {job.companyList?.slice(0, 2).map((company, idx) => (
                    <Chip
                      key={idx}
                      className="bg-green-100 text-green-700 border-green-300 text-xs"
                    >
                      {company.length > 10
                        ? company.substring(0, 10) + "..."
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
                  {job.brandList?.slice(0, 2).map((brand, idx) => (
                    <Chip
                      key={idx}
                      className="bg-orange-100 text-orange-700 border-orange-300 text-xs"
                    >
                      {brand}
                    </Chip>
                  ))}
                  {job.brandList?.length > 2 && (
                    <Chip className="bg-gray-100 text-gray-600 border-gray-300 text-xs">
                      +{job.brandList.length - 2}
                    </Chip>
                  )}
                </div>
              </td>

              <td className="p-4 max-w-48">
                <div className="truncate" title={job.infoMessage || "-"}>
                  {job.infoMessage || "-"}
                </div>
              </td>

              <td className="p-4">
                {formatJobDuration(job.startedAt, job.completedAt)}
              </td>
            </tr>
          ))}
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

      {selectedJobForNotes && (
        <NotesModal
          entity={selectedJobForNotes}
          isOpen={!!selectedJobForNotes}
          title={t("admin.scheduler.notes.modalTitle")}
          notesField="infoMessage"
          onSave={async (updated) => {
            try {
              await fetch(`${API_BASE_URL}/api/scheduler/${updated.id}/notes`, {
                method: "PATCH",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ infoMessage: updated.infoMessage }),
              });

              setLocalJobs((prev) =>
                prev.map((j) =>
                  j.id === updated.id
                    ? { ...j, infoMessage: updated.infoMessage }
                    : j
                )
              );

              setSelectedJobForNotes(null);

              logFrontendEvent(
                "AdminSchedulerTable",
                "INFO",
                "Notes updated for job",
                `Job ID: ${updated.id}`
              );
            } catch (err) {
              const details = err instanceof Error ? err.message : String(err);
              logFrontendEvent(
                "AdminSchedulerTable",
                "ERROR",
                "Failed to update notes for job",
                details
              );
              console.error(t("admin.scheduler.notes.genericError"), err);
              alert(
                err instanceof Error
                  ? err.message
                  : t("admin.scheduler.notes.genericError")
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
