import React, { useState, useEffect, useRef, useCallback } from "react";
import { TFunction } from "i18next";
import {
  NotebookPen,
  Clock,
  Upload,
  Download,
  ShieldCheck,
  CircleX,
  CircleCheck,
} from "lucide-react";
import { format } from "date-fns";
import { logFrontendEvent } from "@/utils/logger";
import { OutagePeriod } from "@/types/outagePeriodInterfaces";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import Chip from "@/components/chip";
import NotesModal from "./notesModal";
import AdminOutagePeriodsAddForm from "./adminOutagePeriodsAddForm";
import AdminLoader from "./adminLoader";

const formatDateTime = (dateTime: string): string => {
  const date = new Date(dateTime.replace("Z", ""));
  return format(date, "dd/MM/yyyy HH:mm");
};

const formatDuration = (minutes: number): string => {
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  const mins = minutes % 60;
  const parts: string[] = [];
  if (days > 0) parts.push(days + "g");
  if (hours > 0) parts.push(hours + "h");
  if (mins > 0 || parts.length === 0) parts.push(mins + "m");
  return parts.join(" ");
};

const getStatusColor = (status: string): string => {
  return status === "OUTAGE-ONGOING"
    ? "bg-red-100 text-red-700 border-red-500"
    : "bg-green-100 text-green-700 border-green-500";
};

export default function AdminOutagePeriodsTable({ t }: { t: TFunction }) {
  const [outages, setOutages] = useState<OutagePeriod[]>([]);
  const [loading, setLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [showAddForm, setShowAddForm] = useState(false);
  const [selectedOutageForNotes, setSelectedOutageForNotes] =
    useState<OutagePeriod | null>(null);
  const [uploadingZip, setUploadingZip] = useState<Set<number>>(new Set());
  const fileInputRefs = useRef<Map<number, HTMLInputElement | null>>(new Map());

  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [query, setQuery] = useState("");
  const [searchType, setSearchType] = useState<"id" | "status" | "outageType">(
    "id"
  );
  const pageSize = 5;

  const fetchOutages = useCallback(async (page: number, searchQuery: string = "") => {
    try {
      setLoading(true);
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      });
      if (searchQuery) {
        params.append("search", searchQuery);
        params.append("searchType", searchType);
      }
      const res = await fetch(`/api/outageperiods?${params}`);
      if (!res.ok) throw new Error("HTTP " + res.status);

      const data = await res.json();
      setOutages(data.data);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
      setCurrentPage(data.page);

      logFrontendEvent(
        "AdminOutagePeriodsTable",
        "INFO",
        "Outages loaded",
        `Page: ${data.page}, Total: ${data.totalCount}`
      );
    } catch (err) {
      logFrontendEvent(
        "AdminOutagePeriodsTable",
        "ERROR",
        "Failed to load outages",
        String(err)
      );
    } finally {
      setLoading(false);
    }
  }, [searchType]);

  useEffect(() => {
    fetchOutages(currentPage, query);
  }, [currentPage, query, fetchOutages]);

  useEffect(() => {
    const interval = setInterval(() => fetchOutages(currentPage, query), 60000);
    return () => clearInterval(interval);
  }, [currentPage, query, fetchOutages]);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchOutages(currentPage, query);
    setIsRefreshing(false);
  };

  const handleZipUpload = async (outageId: number, file: File) => {
    if (!file.name.endsWith(".zip")) {
      alert(t("admin.validation.invalidZipType"));
      return;
    }

    const maxSize = 50 * 1024 * 1024;
    if (file.size > maxSize) {
      alert(t("admin.outagePeriods.validation.zipTooLarge"));
      return;
    }

    setUploadingZip((prev) => new Set(prev).add(outageId));

    try {
      const formData = new FormData();
      formData.append("zipFile", file);

      const response = await fetch(
        `/api/uploadoutagezip/${outageId}/upload-zip`,
        {
          method: "POST",
          body: formData,
        }
      );

      if (!response.ok) {
        const errorText = await response.text();
        try {
          const errorJson = JSON.parse(errorText);
          throw new Error(errorJson.message || errorText);
        } catch {
          throw new Error(errorText);
        }
      }

      await fetchOutages(currentPage, query);
      alert(t("admin.successUploadZip"));
      const inputEl = fileInputRefs.current.get(outageId);
      if (inputEl) inputEl.value = "";
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Upload failed";
      alert(`${t("admin.genericUploadError")}: ${errorMessage}`);
      const inputEl = fileInputRefs.current.get(outageId);
      if (inputEl) inputEl.value = "";
    } finally {
      setUploadingZip((prev) => {
        const newSet = new Set(prev);
        newSet.delete(outageId);
        return newSet;
      });
    }
  };

  const handleZipDownload = (outageId: number) => {
    window.open(`/api/outageperiods/${outageId}/download-zip`, "_blank");
  };

  const handleResolveOutage = async (outageId: number) => {
    if (!confirm(t("admin.outagePeriods.confirmResolveManually"))) return;

    try {
      const response = await fetch(`/api/outageperiods/${outageId}/resolve`, {
        method: "PATCH",
      });
      if (!response.ok) throw new Error("Failed to resolve outage");

      await fetchOutages(currentPage, query);
      alert(t("admin.outagePeriods.confirmResolveSuccess"));
    } catch {
      alert(t("admin.outagePeriods.confirmResolveError"));
    }
  };

  const handleNotesUpdate = async (updated: OutagePeriod) => {
    try {
      const response = await fetch(`/api/outageperiods/${updated.id}/notes`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ notes: updated.notes }),
      });

      if (!response.ok) throw new Error("HTTP " + response.status);

      setOutages((prev) =>
        prev.map((o) =>
          o.id === updated.id ? { ...o, notes: updated.notes } : o
        )
      );
      setSelectedOutageForNotes(null);
      setTimeout(() => fetchOutages(currentPage, query), 200);
    } catch {
      alert(t("admin.notesGenericError"));
    }
  };

  return (
    <div className="relative">
      {(loading || isRefreshing) && <AdminLoader local />}

      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.outagePeriods.tableHeader")} ➜ {totalCount}
        </h1>
        <button
          className={`px-6 py-2 rounded font-medium transition-colors ${
            showAddForm
              ? "bg-red-500 hover:bg-red-600"
              : "bg-blue-500 hover:bg-blue-600"
          } text-white`}
          onClick={() => setShowAddForm(!showAddForm)}
        >
          {showAddForm
            ? t("admin.outagePeriods.undoAddNewOutage")
            : t("admin.outagePeriods.addNewOutage")}
        </button>
      </div>

      {showAddForm && (
        <AdminOutagePeriodsAddForm
          t={t}
          onSubmitSuccess={() => setShowAddForm(false)}
          refreshOutagePeriods={async () =>
            await fetchOutages(currentPage, query)
          }
        />
      )}

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
            <th className="p-4">{t("admin.outagePeriods.autoDetected")}</th>
            <th className="p-4">{t("admin.outagePeriods.status")}</th>
            <th className="p-4">{t("admin.outageType")}</th>
            <th className="p-4">{t("admin.outagePeriods.outageBrand")}</th>
            <th className="p-4">
              {t("admin.outagePeriods.outageStart")} -{" "}
              {t("admin.outagePeriods.outageEnd")}
            </th>
            <th className="p-4">{t("admin.outagePeriods.duration")}</th>
            <th className="p-4">
              {t("admin.outagePeriods.companyVatNumber")} —{" "}
              {t("admin.outagePeriods.vehicleVIN")}
            </th>
          </tr>
        </thead>
        <tbody>
          {outages.map((outage) => (
            <tr
              key={outage.id}
              className="border-b border-gray-300 dark:border-gray-600"
            >
              <td className="px-4 py-3">
                <div className="flex items-center space-x-2">
                  {!outage.hasZipFile && (
                    <label
                      className="p-2 bg-blue-500 hover:bg-blue-600 text-white rounded cursor-pointer"
                      title={t("admin.uploadZip")}
                    >
                      <input
                        ref={(el) => {
                          if (el) fileInputRefs.current.set(outage.id, el);
                        }}
                        type="file"
                        accept=".zip"
                        className="hidden"
                        onChange={(e) => {
                          const file = e.target.files?.[0];
                          if (file) handleZipUpload(outage.id, file);
                        }}
                        disabled={uploadingZip.has(outage.id)}
                      />
                      {uploadingZip.has(outage.id) ? (
                        <Clock size={16} className="animate-spin" />
                      ) : (
                        <Upload size={16} />
                      )}
                    </label>
                  )}
                  {outage.hasZipFile && (
                    <button
                      onClick={() => handleZipDownload(outage.id)}
                      className="p-2 bg-green-500 hover:bg-green-600 text-white rounded"
                      title={t("admin.downloadZip")}
                    >
                      <Download size={16} />
                    </button>
                  )}
                  {outage.status === "OUTAGE-ONGOING" && (
                    <button
                      onClick={() => handleResolveOutage(outage.id)}
                      className="p-2 bg-blue-500 hover:bg-blue-600 text-white rounded"
                      title={t("admin.outagePeriods.resolveManually")}
                    >
                      <ShieldCheck size={16} />
                    </button>
                  )}
                  <button
                    onClick={() => setSelectedOutageForNotes(outage)}
                    className="p-2 bg-blue-500 hover:bg-blue-600 text-white rounded"
                    title={t("admin.openNotesModal")}
                  >
                    <NotebookPen size={16} />
                  </button>
                </div>
              </td>
              <td className="px-4 py-3">
                <div className="flex flex-wrap items-center gap-1">
                  {outage.autoDetected ? (
                    <CircleCheck size={30} className="text-green-600" />
                  ) : (
                    <CircleX size={30} className="text-red-600" />
                  )}
                  <div className="text-xs text-gray-400 ml-1">
                    ID {outage.id}
                  </div>
                </div>
              </td>
              <td className="px-4 py-3">
                <Chip className={getStatusColor(outage.status)}>
                  {outage.status}
                </Chip>
              </td>
              <td className="px-4 py-3">{outage.outageType}</td>
              <td className="px-4 py-3">{outage.outageBrand}</td>
              <td className="px-4 py-3">
                <div className="text-sm">
                  <div>{formatDateTime(outage.outageStart)}</div>
                  <div className="text-gray-500">↓</div>
                  <div>
                    {outage.outageEnd ? (
                      formatDateTime(outage.outageEnd)
                    ) : (
                      <Chip className="bg-red-100 text-red-700 border-red-500 text-xs">
                        ONGOING
                      </Chip>
                    )}
                  </div>
                </div>
              </td>
              <td className="px-4 py-3">
                {outage.outageEnd ? (
                  <span>{formatDuration(outage.durationMinutes)}</span>
                ) : (
                  <Chip className="bg-red-100 text-red-700 border-red-500">
                    ONGOING
                  </Chip>
                )}
              </td>
              <td className="px-4 py-3">
                {outage.outageType === "Outage Vehicle" ? (
                  <div className="text-sm">
                    <div className="font-mono">{outage.companyVatNumber}</div>
                    <div className="text-gray-500">—</div>
                    <div className="font-mono">{outage.vin}</div>
                  </div>
                ) : (
                  <span className="text-gray-500">—</span>
                )}
              </td>
            </tr>
          ))}
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
          searchMode="id-or-status"
          externalSearchType={searchType}
          onSearchTypeChange={setSearchType}
          outageLabel={t("admin.outageType")}
          availableStatuses={["OUTAGE-ONGOING", "OUTAGE-RESOLVED"]}
          availableOutageTypes={["Outage Vehicle", "Outage Fleet Api"]}
        />
      </div>

      {selectedOutageForNotes && (
        <NotesModal
          entity={selectedOutageForNotes}
          isOpen={!!selectedOutageForNotes}
          title={t("admin.outagePeriods.notes.modalTitle")}
          notesField="notes"
          onSave={handleNotesUpdate}
          onClose={() => setSelectedOutageForNotes(null)}
          t={t}
        />
      )}
    </div>
  );
}
