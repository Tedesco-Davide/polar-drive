import { TFunction } from "i18next";
import { ClientConsent } from "@/types/clientConsentInterfaces";
import { Download, NotebookPen } from "lucide-react";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { formatDateToDisplay } from "@/utils/date";
import { useState, useEffect } from "react";
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
  const [showForm, setShowForm] = useState(false);

  useEffect(() => {
    setLocalConsents(consents);
    logFrontendEvent(
      "AdminClientConsentsTable",
      "INFO",
      "Component mounted and consents data initialized",
      "Loaded " + consents.length + " consent records"
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
      "Query: " + query
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
      "Current page: " + currentPage
    );
  }, [currentPage]);

    const handleZipDownload = async (consentId: number) => {
    try {
        const response = await fetch(
        `/api/clientconsents/${consentId}/download`
        );

        const contentType = response.headers.get("content-type");

        if (contentType && contentType.includes("application/json")) {
        const result = await response.json();
        alert(result.message || t("admin.noFileAvailable"));
        return;
        }

        if (!response.ok) throw new Error("HTTP " + response.status);

        const contentDisposition = response.headers.get("content-disposition");
        let filename = `consent_${consentId}.zip`;

        if (contentDisposition) {
            const filenameMatch = contentDisposition.match(/filename[^;=\n]*=["']?([^"';]*)["']?/);
            if (filenameMatch && filenameMatch[1]) {
                filename = filenameMatch[1];
            }
        }

        const blob = await response.blob();
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);

        logFrontendEvent(
        "AdminClientConsentsTable",
        "INFO",
        "ZIP download completed",
        "Consent ID: " + consentId + ", Filename: " + filename
        );
    } catch (error) {
        const errorMessage =
        error instanceof Error ? error.message : "Download failed";
        alert(`${t("admin.downloadError")}: ${errorMessage}`);
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
              "Now showing form: " + newValue
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
            const hasZipFile = !!consent.hasZipFile;

            return (
              <tr
                key={consent.id}
                className="border-b border-gray-300 dark:border-gray-600"
              >
                {/* Azioni - Aggiornate come negli outages */}
                <td className="px-4 py-3">
                  <div className="flex items-center space-x-2">
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
                          "Consent ID: " + consent.id + ", VIN: " + consent.vehicleVIN
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
                `/api/clientconsents/${updated.id}/notes`,
                {
                  method: "PATCH",
                  headers: { "Content-Type": "application/json" },
                  body: JSON.stringify({ notes: updated.notes }),
                }
              );

                if (!response.ok) {
                const txt = await response.text();
                if (response.status === 409) {
                    alert(t("admin.clientConsents.duplicateZipHash"));
                } else if (response.status === 400) {
                    alert(`${t("admin.validation.invalidZipType")} — ${txt}`);
                } else {
                    alert(`${t("admin.genericUploadError")}: ${txt}`);
                }
                throw new Error(txt);
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
                "Consent ID: " + updated.id
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
