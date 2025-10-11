import { TFunction } from "i18next";
import { ClientConsent } from "@/types/clientConsentInterfaces";
import { Upload, Download, Trash2 } from "lucide-react";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { formatDateToDisplay } from "@/utils/date";
import { useState, useEffect } from "react";
import { NotebookPen, Clock } from "lucide-react";
import { API_BASE_URL } from "@/utils/api";
import { logFrontendEvent } from "@/utils/logger";
import NotesModal from "@/components/notesModal";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import Chip from "@/components/chip";
import AdminClientConsentAddForm from "./adminClientConsentAddForm";

type Props = {
  t: TFunction;
  consents: ClientConsent[];
  refreshClientConsents: () => Promise<void>;
};

const getConsentTypeColor = (type: string) => {
  switch (type) {
    case "Consent Activation":
      return "bg-green-100 text-green-700 border-green-500";
    case "Consent Deactivation":
      return "bg-yellow-100 text-yellow-800 border-yellow-500";
    case "Consent Stop Data Fetching":
      return "bg-red-100 text-red-700 border-red-500";
    case "Consent Reactivation":
      return "bg-fuchsia-100 text-fuchsia-700 border-fuchsia-500";
    default:
      return "bg-gray-100 text-polarNight border-gray-400";
  }
};

export default function AdminClientConsents({
  t,
  consents,
  refreshClientConsents,
}: Props) {
  const [localConsents, setLocalConsents] = useState<ClientConsent[]>([]);
  const [selectedConsentForNotes, setSelectedConsentForNotes] =
    useState<ClientConsent | null>(null);
  const [uploadingZip, setUploadingZip] = useState<Set<number>>(new Set());
  const [showForm, setShowForm] = useState(false);

  useEffect(() => {
    setLocalConsents(consents);
    logFrontendEvent(
      "AdminClientConsentsTable",
      "INFO",
      "Component mounted and consents data initialized",
      `Loaded ${consents.length} consent records`
    );
  }, [consents]);

  const { query, setQuery, filteredData } = useSearchFilter<ClientConsent>(
    localConsents,
    [
      "uploadDate",
      "consentHash",
      "consentType",
      "vehicleVIN",
      "companyVatNumber",
    ]
  );

  useEffect(() => {
    logFrontendEvent(
      "AdminClientConsentsTable",
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
  } = usePagination<ClientConsent>(filteredData, 5);

  useEffect(() => {
    logFrontendEvent(
      "AdminClientConsentsTable",
      "DEBUG",
      "Pagination interaction",
      `Current page: ${currentPage}`
    );
  }, [currentPage]);

  // ✅ Gestione upload ZIP (come negli outages)
  const handleZipUpload = async (consentId: number, file: File) => {
    if (!file.name.endsWith(".zip")) {
      alert(t("admin.validation.invalidZipType"));
      return;
    }

    const maxSize = 50 * 1024 * 1024; // 50MB
    if (file.size > maxSize) {
      alert(t("admin.validation.zipTooLarge"));
      return;
    }

    setUploadingZip((prev) => new Set(prev).add(consentId));

    try {
      const formData = new FormData();
      formData.append("zipFile", file);

      const response = await fetch(
        `${API_BASE_URL}/api/clientconsents/${consentId}/upload-zip`,
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
      await refreshClientConsents();

      logFrontendEvent(
        "AdminClientConsentsTable",
        "INFO",
        "ZIP uploaded successfully",
        `Consent ID: ${consentId}, File: ${file.name}`
      );

      alert(t("admin.successUploadZip"));
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Upload failed";
      logFrontendEvent(
        "AdminClientConsentsTable",
        "ERROR",
        "Failed to upload ZIP",
        errorMessage
      );
      alert(`${t("admin.genericUploadError")}: ${errorMessage}`);
    } finally {
      setUploadingZip((prev) => {
        const newSet = new Set(prev);
        newSet.delete(consentId);
        return newSet;
      });
    }
  };

  // ✅ Gestione download ZIP (come negli outages)
  const handleZipDownload = async (consentId: number) => {
    try {
      const response = await fetch(
        `${API_BASE_URL}/api/clientconsents/${consentId}/download`
      );

      const contentType = response.headers.get("content-type");

      if (contentType && contentType.includes("application/json")) {
        const result = await response.json();
        alert(result.message || t("admin.noFileAvailable"));
        return;
      }

      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `consent_${consentId}.zip`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);

      logFrontendEvent(
        "AdminClientConsentsTable",
        "INFO",
        "ZIP download completed",
        `Consent ID: ${consentId}`
      );
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Download failed";
      alert(`${t("admin.downloadError")}: ${errorMessage}`);
    }
  };

  // ✅ Gestione delete ZIP (nuova funzionalità)
  const handleZipDelete = async (consentId: number) => {
    if (!confirm(t("admin.confirmDeleteZip"))) {
      return;
    }

    try {
      const response = await fetch(
        `${API_BASE_URL}/api/clientconsents/${consentId}/delete-zip`,
        { method: "DELETE" }
      );

      if (!response.ok) {
        throw new Error("Failed to delete ZIP file");
      }

      // Refresh dei dati
      await refreshClientConsents();

      logFrontendEvent(
        "AdminClientConsentsTable",
        "INFO",
        "ZIP file deleted successfully",
        `Consent ID: ${consentId}`
      );

      alert(t("admin.successDeleteZip"));
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Delete failed";
      logFrontendEvent(
        "AdminClientConsentsTable",
        "ERROR",
        "Failed to delete ZIP",
        errorMessage
      );
      alert(`${t("admin.errorDeletingZip")}: ${errorMessage}`);
    }
  };

  return (
    <div>
      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite ">
          {t("admin.clientConsents.tableHeader")}
        </h1>
        <button
          className={`${
            showForm
              ? "bg-red-500 hover:bg-red-600"
              : "bg-blue-500 hover:bg-blue-600"
          } text-softWhite px-6 py-2 rounded font-medium transition-colors`}
          onClick={() => {
            const newValue = !showForm;
            setShowForm(newValue);
            logFrontendEvent(
              "AdminClientConsentsTable",
              "INFO",
              "Consent form visibility toggled",
              `Now showing form: ${newValue}`
            );
          }}
        >
          {showForm
            ? t("admin.clientConsents.undoAddNewConsent")
            : t("admin.clientConsents.addNewConsent")}
        </button>
      </div>

      {showForm && (
        <AdminClientConsentAddForm
          t={t}
          onSubmitSuccess={() => setShowForm(false)}
          refreshClientConsents={refreshClientConsents}
        />
      )}

      <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
        <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
          <tr>
            <th className="p-4 text-left font-semibold">
              {t("admin.actions")}
            </th>
            <th className="p-4 text-left font-semibold">
              {t("admin.clientConsents.consentType")}
            </th>
            <th className="p-4 text-left font-semibold">
              {t("admin.clientConsents.uploadDate")}
            </th>
            <th className="p-4 text-left font-semibold">
              {t("admin.clientConsents.companyVatNumber")} —{" "}
              {t("admin.clientConsents.vehicleVIN")}
            </th>
            <th className="p-4 text-left font-semibold">
              {t("admin.clientConsents.hash")}
            </th>
          </tr>
        </thead>
        <tbody>
          {currentPageData.map((consent) => {
            //  Usa il campo hasZipFile dal DTO come negli outages
            const hasZipFile =
              consent.hasZipFile ||
              !!(consent.zipFilePath && consent.zipFilePath.trim());

            return (
              <tr
                key={consent.id}
                className="border-b border-gray-300 dark:border-gray-600"
              >
                {/* Azioni - Aggiornate come negli outages */}
                <td className="px-4 py-3">
                  <div className="flex items-center space-x-2">
                    {/* Upload/Replace ZIP */}
                    <label
                      className="p-2 bg-blue-500 hover:bg-blue-600 text-white rounded transition-colors cursor-pointer"
                      title={
                        hasZipFile
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
                            if (hasZipFile) {
                              const confirmReplace = confirm(
                                t("admin.confirmReplaceZip")
                              );
                              if (!confirmReplace) {
                                e.target.value = "";
                                return;
                              }
                            }
                            handleZipUpload(consent.id, file);
                            e.target.value = "";
                          }
                        }}
                        disabled={uploadingZip.has(consent.id)}
                      />
                      {uploadingZip.has(consent.id) ? (
                        <Clock size={16} className="animate-spin" />
                      ) : (
                        <Upload size={16} />
                      )}
                    </label>

                    {/* Download ZIP - solo se presente */}
                    {hasZipFile && (
                      <>
                        <button
                          onClick={() => handleZipDownload(consent.id)}
                          className="p-2 bg-green-500 hover:bg-green-600 text-white rounded transition-colors"
                          title={t("admin.downloadZip")}
                        >
                          <Download size={16} />
                        </button>
                        <button
                          onClick={() => handleZipDelete(consent.id)}
                          className="p-2 bg-red-500 hover:bg-red-600 text-white rounded transition-colors"
                          title={t("admin.deleteZip")}
                        >
                          <Trash2 size={16} />
                        </button>
                      </>
                    )}

                    {/* Note */}
                    <button
                      className="p-2 bg-blue-500 hover:bg-blue-600 text-white rounded transition-colors"
                      title={t("admin.openNotesModal")}
                      onClick={() => {
                        setSelectedConsentForNotes(consent);
                        logFrontendEvent(
                          "AdminClientConsentsTable",
                          "INFO",
                          "Notes modal opened for consent",
                          `Consent ID: ${consent.id}, VIN: ${consent.vehicleVIN}`
                        );
                      }}
                    >
                      <NotebookPen size={16} />
                    </button>
                  </div>
                </td>

                <td className="p-4">
                  <Chip className={getConsentTypeColor(consent.consentType)}>
                    {consent.consentType}
                  </Chip>
                </td>
                <td className="p-4">
                  {formatDateToDisplay(consent.uploadDate)}
                </td>
                <td className="p-4">
                  <div className="text-sm">
                    <div className="font-mono">{consent.companyVatNumber}</div>
                    <div className="text-gray-500">—</div>
                    <div className="font-mono">
                        {consent.vehicleVIN} 
                    </div>
                  </div>
                </td>
                <td className="p-4 font-mono text-xs">{consent.consentHash}</td>
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

      {/* Modal Note */}
      {selectedConsentForNotes && (
        <NotesModal
          entity={selectedConsentForNotes}
          isOpen={!!selectedConsentForNotes}
          title={t("admin.clientConsents.notes.modalTitle")}
          notesField="notes"
          onSave={async (updated) => {
            try {
              const response = await fetch(
                `${API_BASE_URL}/api/clientconsents/${updated.id}/notes`,
                {
                  method: "PATCH",
                  headers: { "Content-Type": "application/json" },
                  body: JSON.stringify({ notes: updated.notes }),
                }
              );

              if (!response.ok) {
                throw new Error("Failed to update notes");
              }

              setLocalConsents((prev) =>
                prev.map((c) =>
                  c.id === updated.id ? { ...c, notes: updated.notes } : c
                )
              );
              setSelectedConsentForNotes(null);

              logFrontendEvent(
                "AdminClientConsentsTable",
                "INFO",
                "Notes updated for consent",
                `Consent ID: ${updated.id}`
              );
            } catch (err) {
              const details = err instanceof Error ? err.message : String(err);
              logFrontendEvent(
                "AdminClientConsentsTable",
                "ERROR",
                "Failed to update notes for consent",
                details
              );
              alert(
                `${t("admin.clientConsents.notes.genericError")}: ${details}`
              );
            }
          }}
          onClose={() => setSelectedConsentForNotes(null)}
          t={t}
        />
      )}
    </div>
  );
}
