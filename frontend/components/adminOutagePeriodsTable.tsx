import React, { useState, useEffect } from "react";
import { TFunction } from "i18next";
import {
  NotebookPen,
  Clock,
  Upload,
  Download,
  ShieldCheck,
  CircleX,
  CircleCheck,
  Trash2,
} from "lucide-react";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { format } from "date-fns";
import { API_BASE_URL } from "@/utils/api";
import { logFrontendEvent } from "@/utils/logger";
import { OutagePeriod } from "@/types/outagePeriodInterfaces";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import Chip from "@/components/chip";
import NotesModal from "./notesModal";
import AdminOutagePeriodsAddForm from "./adminOutagePeriodsAddForm";

interface Props {
  t: TFunction;
  outages: OutagePeriod[];
  refreshOutagePeriods: () => Promise<OutagePeriod[]>;
}

const formatDateTime = (dateTime: string): string => {
  // Forza interpretazione come ora locale invece che UTC
  const date = new Date(dateTime.replace("Z", ""));
  return format(date, "dd/MM/yyyy HH:mm");
};

const formatDuration = (minutes: number): string => {
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  const mins = minutes % 60;

  const parts: string[] = [];
  if (days > 0) parts.push(`${days}g`);
  if (hours > 0) parts.push(`${hours}h`);
  if (mins > 0 || parts.length === 0) parts.push(`${mins}m`);

  return parts.join(" ");
};

const getStatusColor = (status: string): string => {
  switch (status) {
    case "OUTAGE-ONGOING":
      return "bg-red-100 text-red-700 border-red-500";
    case "OUTAGE-RESOLVED":
      return "bg-green-100 text-green-700 border-green-500";
    default:
      return "bg-gray-100 text-gray-700 border-gray-400";
  }
};

export default function AdminOutagePeriodsTable({
  t,
  outages,
  refreshOutagePeriods,
}: Props) {
  const [localOutages, setLocalOutages] = useState<OutagePeriod[]>([]);
  const [showAddForm, setShowAddForm] = useState(false);
  const [selectedOutageForNotes, setSelectedOutageForNotes] =
    useState<OutagePeriod | null>(null);
  const [uploadingZip, setUploadingZip] = useState<Set<number>>(new Set());

  useEffect(() => {
    setLocalOutages(outages);
    logFrontendEvent(
      "AdminOutagePeriodsTable",
      "INFO",
      "Component mounted and outages data initialized",
      `Loaded ${outages.length} outage records`
    );
  }, [outages]);

  useEffect(() => {
    // Auto-refresh ogni 60 secondi
    const interval = setInterval(async () => {
      try {
        const updatedOutages = await refreshOutagePeriods();
        setLocalOutages(updatedOutages);

        logFrontendEvent(
          "AdminOutagePeriodsTable",
          "INFO",
          "Auto-refreshed outage data",
          `Updated ${updatedOutages.length} outage records`
        );
      } catch (error) {
        console.warn("Auto-refresh failed:", error);
      }
    }, 60000); // 60 secondi

    return () => clearInterval(interval);
  }, [refreshOutagePeriods]);

  const { query, setQuery, filteredData } = useSearchFilter<OutagePeriod>(
    localOutages,
    ["outageType", "notes", "companyVatNumber", "vin", "outageBrand"]
  );

  const {
    currentPage,
    totalPages,
    currentData: currentPageData,
    nextPage,
    prevPage,
    setCurrentPage,
  } = usePagination<OutagePeriod>(filteredData, 10);

  const handleZipUpload = async (outageId: number, file: File) => {
    // ✅ Verifica solo che sia un file .zip
    if (!file.name.endsWith(".zip")) {
      alert(t("admin.validation.invalidZipType"));
      return;
    }

    // ✅ Verifica dimensione (manteniamo il limite di sicurezza)
    const maxSize = 50 * 1024 * 1024; // 50MB
    if (file.size > maxSize) {
      alert(t("admin.outagePeriods.validation.zipTooLarge"));
      return;
    }

    setUploadingZip((prev) => new Set(prev).add(outageId));

    try {
      const formData = new FormData();
      formData.append("zipFile", file);

      const response = await fetch(
        `${API_BASE_URL}/api/outageperiods/${outageId}/upload-zip`,
        {
          method: "POST",
          body: formData,
        }
      );

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(errorText);
      }

      // Refresh dei dati
      const updatedOutages = await refreshOutagePeriods();
      setLocalOutages(updatedOutages);

      logFrontendEvent(
        "AdminOutagePeriodsTable",
        "INFO",
        "ZIP uploaded successfully",
        `Outage ID: ${outageId}, File: ${file.name}`
      );

      alert(t("admin.successUploadZip"));
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Upload failed";
      logFrontendEvent(
        "AdminOutagePeriodsTable",
        "ERROR",
        "Failed to upload ZIP",
        errorMessage
      );
      alert(`${t("admin.genericUploadError")}: ${errorMessage}`);
    } finally {
      setUploadingZip((prev) => {
        const newSet = new Set(prev);
        newSet.delete(outageId);
        return newSet;
      });
    }
  };

  const handleZipDownload = (outageId: number) => {
    window.open(
      `${API_BASE_URL}/api/outageperiods/${outageId}/download-zip`,
      "_blank"
    );
    logFrontendEvent(
      "AdminOutagePeriodsTable",
      "INFO",
      "ZIP download triggered",
      `Outage ID: ${outageId}`
    );
  };

  const handleResolveOutage = async (outageId: number) => {
    if (!confirm(t("admin.outagePeriods.confirmResolveManually"))) {
      return;
    }

    try {
      const response = await fetch(
        `${API_BASE_URL}/api/outageperiods/${outageId}/resolve`,
        { method: "PATCH" }
      );

      if (!response.ok) {
        throw new Error("Failed to resolve outage");
      }

      const updatedOutages = await refreshOutagePeriods();
      setLocalOutages(updatedOutages);

      logFrontendEvent(
        "AdminOutagePeriodsTable",
        "INFO",
        "Outage resolved manually",
        `Outage ID: ${outageId}`
      );
      alert(t("admin.outagePeriods.confirmResolveSuccess"));
    } catch (error) {
      logFrontendEvent(
        "AdminOutagePeriodsTable",
        "ERROR",
        "Failed to resolve outage",
        error instanceof Error ? error.message : "Unknown error"
      );
      alert(t("admin.outagePeriods.confirmResolveError"));
    }
  };

  const handleZipDelete = async (outageId: number) => {
    if (!confirm(t("admin.confirmDeleteZip"))) {
      return;
    }

    try {
      const response = await fetch(
        `${API_BASE_URL}/api/outageperiods/${outageId}/delete-zip`,
        { method: "DELETE" }
      );

      if (!response.ok) {
        throw new Error("Failed to delete ZIP file");
      }

      // Refresh dei dati
      const updatedOutages = await refreshOutagePeriods();
      setLocalOutages(updatedOutages);

      logFrontendEvent(
        "AdminOutagePeriodsTable",
        "INFO",
        "ZIP file deleted successfully",
        `Outage ID: ${outageId}`
      );

      alert(t("admin.successDeleteZip"));
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Delete failed";
      logFrontendEvent(
        "AdminOutagePeriodsTable",
        "ERROR",
        "Failed to delete ZIP",
        errorMessage
      );
      alert(`${t("admin.errorDeletingZip")}: ${errorMessage}`);
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.outagePeriods.tableHeader")}
        </h1>
        <button
          className={`px-6 py-2 rounded font-medium transition-colors ${
            showAddForm
              ? "bg-red-500 hover:bg-red-600 text-white"
              : "bg-blue-500 hover:bg-blue-600 text-white"
          }`}
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
          refreshOutagePeriods={async () => {
            await refreshOutagePeriods();
          }}
        />
      )}

      {/* Tabella */}
      <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
        <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
          <tr>
            <th className="p-4">
              <button
                onClick={async () => {
                  try {
                    await refreshOutagePeriods();
                    alert(t("admin.outagePeriods.tableRefreshSuccess"));
                  } catch {
                    alert(t("admin.outagePeriods.tableRefreshFail"));
                  }
                }}
                className="px-1 bg-blue-500 text-white rounded text-sm hover:bg-blue-600"
              >
                <span className="uppercase text-xs tracking-widest">
                  {t("admin.tableRefreshButton")}
                </span>
              </button>{" "}
              {t("admin.actions")}
            </th>
            <th className="p-4 text-left font-semibold">
              {t("admin.outagePeriods.autoDetected")}
            </th>
            <th className="p-4 text-left font-semibold">
              {t("admin.outagePeriods.status")}
            </th>
            <th className="p-4 text-left font-semibold">
              {t("admin.outagePeriods.outageType")}
            </th>
            <th className="p-4 text-left font-semibold">
              {t("admin.outagePeriods.outageBrand")}
            </th>
            <th className="p-4 text-left font-semibold">
              {t("admin.outagePeriods.outageStart")} -{" "}
              {t("admin.outagePeriods.outageEnd")}
            </th>
            <th className="p-4 text-left font-semibold">
              {t("admin.outagePeriods.duration")}
            </th>
            <th className="p-4 text-left font-semibold">
              {t("admin.outagePeriods.companyVatNumber")} —{" "}
              {t("admin.outagePeriods.vehicleVIN")}
            </th>
          </tr>
        </thead>
        <tbody>
          {currentPageData.map((outage) => {
            return (
              <tr
                key={outage.id}
                className="border-b border-gray-300 dark:border-gray-600"
              >
                {/* Azioni */}
                <td className="px-4 py-3">
                  <div className="flex items-center space-x-2">
                    {/* Upload ZIP - sempre disponibile */}
                    <label
                      className="p-2 bg-blue-500 hover:bg-blue-600 text-white rounded transition-colors cursor-pointer"
                      title={
                        outage.hasZipFile
                          ? t("admin.replaceZip")
                          : t("admin.uploadZip")
                      }
                    >
                      <input
                        type="file"
                        accept=".zip"
                        className="hidden"
                        onChange={(e) => {
                          const file = e.target.files?.[0];
                          if (file) {
                            // Aggiungi conferma se sta sostituendo un file esistente
                            if (outage.hasZipFile) {
                              const confirmReplace = confirm(
                                t("admin.confirmReplaceZip")
                              );
                              if (!confirmReplace) {
                                e.target.value = "";
                                return;
                              }
                            }
                            handleZipUpload(outage.id, file);
                            e.target.value = "";
                          }
                        }}
                        disabled={uploadingZip.has(outage.id)}
                      />
                      {uploadingZip.has(outage.id) ? (
                        <Clock size={16} className="animate-spin" />
                      ) : (
                        <Upload size={16} />
                      )}
                    </label>

                    {/* Download ZIP - solo se presente */}
                    {outage.hasZipFile && (
                      <>
                        <button
                          onClick={() => handleZipDownload(outage.id)}
                          className="p-2 bg-green-500 hover:bg-green-600 text-white rounded transition-colors"
                          title={t("admin.downloadZip")}
                        >
                          <Download size={16} />
                        </button>
                        <button
                          onClick={() => handleZipDelete(outage.id)}
                          className="p-2 bg-red-500 hover:bg-red-600 text-white rounded transition-colors"
                          title={t("admin.deleteZip")}
                        >
                          <Trash2 size={16} />
                        </button>
                      </>
                    )}

                    {/* Risolvi manualmente (solo per ongoing) */}
                    {outage.status === "OUTAGE-ONGOING" && (
                      <button
                        onClick={() => handleResolveOutage(outage.id)}
                        className="p-2 bg-blue-500 hover:bg-blue-600 text-white rounded transition-colors"
                        title={t("admin.outagePeriods.resolveManually")}
                      >
                        <ShieldCheck size={16} />
                      </button>
                    )}

                    {/* Note */}
                    <button
                      onClick={() => setSelectedOutageForNotes(outage)}
                      className="p-2 bg-blue-500 hover:bg-blue-600 text-white rounded transition-colors"
                      title={t("admin.openNotesModal")}
                    >
                      <NotebookPen size={16} />
                    </button>
                  </div>
                </td>

                {/* Auto rilevato */}
                <td className="px-4 py-3">
                  <div className="flex">
                    {outage.autoDetected ? (
                      <div className="flex items-center text-green-600">
                        <CircleCheck size={30} />
                      </div>
                    ) : (
                      <div className="flex items-center text-red-600">
                        <CircleX size={30} />
                      </div>
                    )}
                  </div>
                </td>

                {/* Status */}
                <td className="px-4 py-3">
                  <Chip className={getStatusColor(outage.status)}>
                    {outage.status}
                  </Chip>
                </td>

                {/* Tipo */}
                <td className="px-4 py-3">{outage.outageType}</td>

                {/* Brand */}
                <td className="px-4 py-3">
                  <span>{outage.outageBrand}</span>
                </td>

                {/* Periodo */}
                <td className="px-4 py-3">
                  <div className="text-sm">
                    <div>{formatDateTime(outage.outageStart)}</div>
                    <div className="text-gray-500">↓</div>
                    <div>
                      {outage.outageEnd ? (
                        formatDateTime(outage.outageEnd)
                      ) : (
                        <Chip className="bg-red-100 text-red-700 border-red-500 text-xs">
                          OUTAGE-ONGOING
                        </Chip>
                      )}
                    </div>
                  </div>
                </td>

                {/* Durata */}
                <td className="px-4 py-3">
                  {outage.outageEnd ? (
                    <span>{formatDuration(outage.durationMinutes)}</span>
                  ) : (
                    <Chip className="bg-red-100 text-red-700 border-red-500">
                      OUTAGE-ONGOING
                    </Chip>
                  )}
                </td>

                {/* Partita IVA — VIN */}
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
            );
          })}
        </tbody>
      </table>

      <div className="flex flex-wrap items-center gap-4 mt-4">
        {/* Paginazione */}
        <PaginationControls
          currentPage={currentPage}
          totalPages={totalPages}
          onPrev={prevPage}
          onNext={nextPage}
        />
        {/* Controlli di ricerca e paginazione */}
        <SearchBar
          query={query}
          setQuery={setQuery}
          resetPage={() => setCurrentPage(1)}
        />
      </div>

      {/* Modal Note */}
      {selectedOutageForNotes && (
        <NotesModal
          entity={selectedOutageForNotes}
          isOpen={!!selectedOutageForNotes}
          title={t("admin.outagePeriods.notes.modalTitle")}
          notesField="notes"
          onSave={async (updated) => {
            try {
              const response = await fetch(
                `${API_BASE_URL}/api/outageperiods/${updated.id}/notes`,
                {
                  method: "PATCH",
                  headers: { "Content-Type": "application/json" },
                  body: JSON.stringify({ notes: updated.notes }),
                }
              );

              if (!response.ok) {
                throw new Error("Failed to update notes");
              }

              setLocalOutages((prev) =>
                prev.map((o) =>
                  o.id === updated.id ? { ...o, notes: updated.notes } : o
                )
              );
              setSelectedOutageForNotes(null);

              logFrontendEvent(
                "AdminOutagePeriodsTable",
                "INFO",
                "Notes updated for outage",
                `Outage ID: ${updated.id}`
              );
            } catch (err) {
              const details = err instanceof Error ? err.message : String(err);
              logFrontendEvent(
                "AdminOutagePeriodsTable",
                "ERROR",
                "Failed to update notes for outage",
                details
              );
              alert(
                `${t("admin.outagePeriods.notes.genericError")}: ${details}`
              );
            }
          }}
          onClose={() => setSelectedOutageForNotes(null)}
          t={t}
        />
      )}
    </div>
  );
}
