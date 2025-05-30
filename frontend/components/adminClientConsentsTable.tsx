import { TFunction } from "i18next";
import { ClientConsent } from "@/types/clientConsentInterfaces";
import { FileArchive } from "lucide-react";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { formatDateToDisplay } from "@/utils/date";
import { useState, useEffect } from "react";
import { NotebookPen } from "lucide-react";
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

  const [showForm, setShowForm] = useState(false);

  const [formData, setFormData] = useState<ClientConsent>({
    id: 0,
    clientCompanyId: 0,
    vehicleId: 0,
    uploadDate: "",
    zipFilePath: "",
    consentHash: "",
    consentType: "",
    companyVatNumber: "",
    vehicleVIN: "",
    notes: "",
  });

  return (
    <div>
      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite ">
          {t("admin.clientConsents.tableHeader")}
        </h1>
        <button
          className={`${
            showForm
              ? "bg-dataRed hover:bg-red-600"
              : "bg-blue-500 hover:bg-blue-600"
          } text-softWhite px-6 py-2 rounded`}
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
            ? t("admin.clientConsents.addNewConsent")
            : t("admin.clientConsents.undoAddNewConsent")}
        </button>
      </div>

      {showForm && (
        <AdminClientConsentAddForm
          formData={formData}
          setFormData={setFormData}
          t={t}
          refreshClientConsents={refreshClientConsents}
        />
      )}

      <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
        <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
          <tr>
            <th className="p-4">{t("admin.actions")}</th>
            <th className="p-4">{t("admin.clientConsents.consentType")}</th>
            <th className="p-4">{t("admin.clientConsents.uploadDate")}</th>
            <th className="p-4">
              {t("admin.clientConsents.companyVatNumber")}{" "}
              {t("admin.basicPlaceholder")}{" "}
              {t("admin.clientConsents.vehicleVIN")}
            </th>
            <th className="p-4">{t("admin.clientConsents.hash")}</th>
          </tr>
        </thead>
        <tbody>
          {currentPageData.map((consent, index) => (
            <tr
              key={index}
              className="border-b border-gray-300 dark:border-gray-600"
            >
              <td className="p-4 space-x-2 inline-flex">
                <button
                  className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                  title={t("admin.mainWorkflow.button.singleZipConsent")}
                  onClick={() =>
                    window.open(
                      `${API_BASE_URL}/api/ClientConsents/${consent.id}/download`,
                      "_blank"
                    )
                  }
                >
                  <FileArchive size={16} />
                </button>
                <button
                  className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
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
                {selectedConsentForNotes && (
                  <NotesModal
                    entity={selectedConsentForNotes}
                    isOpen={!!selectedConsentForNotes}
                    title={t("admin.clientConsents.notes.modalTitle")}
                    notesField="notes"
                    onSave={async (updated) => {
                      try {
                        await fetch(
                          `${API_BASE_URL}/api/ClientConsents/${updated.id}/notes`,
                          {
                            method: "PATCH",
                            headers: { "Content-Type": "application/json" },
                            body: JSON.stringify({ notes: updated.notes }),
                          }
                        );
                        setLocalConsents((prev) =>
                          prev.map((c) =>
                            c.id === updated.id
                              ? { ...c, notes: updated.notes }
                              : c
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
                        const details =
                          err instanceof Error ? err.message : String(err);
                        logFrontendEvent(
                          "AdminClientConsentsTable",
                          "ERROR",
                          "Failed to update notes for consent",
                          details
                        );
                        console.error(t("admin.notesGenericError"), err);
                        alert(
                          err instanceof Error
                            ? err.message
                            : t("admin.notesGenericError")
                        );
                      }
                    }}
                    onClose={() => setSelectedConsentForNotes(null)}
                    t={t}
                  />
                )}
              </td>
              <td className="p-4">
                <Chip className={getConsentTypeColor(consent.consentType)}>
                  {consent.consentType}
                </Chip>
              </td>
              <td className="p-4">{formatDateToDisplay(consent.uploadDate)}</td>
              <td className="p-4">
                {consent.companyVatNumber} {t("admin.basicPlaceholder")}{" "}
                {consent.vehicleVIN}
              </td>
              <td className="p-4">{consent.consentHash}</td>
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
    </div>
  );
}
