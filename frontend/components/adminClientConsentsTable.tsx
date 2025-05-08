import { TFunction } from "i18next";
import { ClientConsent } from "@/types/clientConsentInterfaces";
import { FileArchive } from "lucide-react";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { formatDateToDisplay } from "@/utils/date";
import { useState, useEffect } from "react";
import { parseISO, isAfter, isValid } from "date-fns";
import { NotebookPen } from "lucide-react";
import NotesModal from "@/components/notesModal";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import Chip from "@/components/chip";
import AdminClientConsentAddForm from "./adminClientConsentAddForm";
import { API_BASE_URL } from "@/utils/api";

type Props = {
  t: TFunction;
  consents: ClientConsent[];
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

export default function AdminClientConsents({ t, consents }: Props) {
  const [localConsents, setLocalConsents] = useState<ClientConsent[]>([]);
  const [selectedConsentForNotes, setSelectedConsentForNotes] =
    useState<ClientConsent | null>(null);

  useEffect(() => {
    setLocalConsents(consents);
  }, [consents]);

  const { query, setQuery, filteredData } = useSearchFilter<ClientConsent>(
    localConsents,
    [
      "uploadDate",
      "consentHash",
      "consentType",
      "teslaVehicleVIN",
      "companyVatNumber",
    ]
  );

  const {
    currentPage,
    totalPages,
    currentData: currentPageData,
    nextPage,
    prevPage,
    setCurrentPage,
  } = usePagination<ClientConsent>(filteredData, 5);

  const [showForm, setShowForm] = useState(false);

  const [formData, setFormData] = useState<ClientConsent>({
    id: 0,
    clientCompanyId: 0,
    teslaVehicleId: 0,
    uploadDate: "",
    zipFilePath: "",
    consentHash: "",
    consentType: "Consent Activation",
    companyVatNumber: "",
    teslaVehicleVIN: "",
    notes: "",
  });

  const handleSubmit = async () => {
    const requiredFields = [
      "consentType",
      "teslaVehicleVIN",
      "companyVatNumber",
      "uploadDate",
      "zipFilePath",
    ] as const;

    const validConsentTypes = new Set([
      "Consent Deactivation",
      "Consent Stop Data Fetching",
      "Consent Reactivation",
    ]);

    const missing = requiredFields.filter((f) => {
      if (f === "zipFilePath") {
        return !formData.zipFilePath || formData.zipFilePath.trim() === "";
      }
      if (f === "consentType") {
        return !validConsentTypes.has(formData.consentType);
      }
      return !formData[f];
    });

    if (missing.length > 0) {
      const translatedLabels = missing.map((field) =>
        t(`admin.clientConsents.${field}`)
      );
      alert(t("admin.missingFields") + ": " + translatedLabels.join(", "));
      return;
    }

    // ✅ Validazione VIN Tesla (regex: 17 caratteri alfanumerici, senza I/O/Q)
    const vinRegex = /^[A-HJ-NPR-Z0-9]{17}$/;
    if (!vinRegex.test(formData.teslaVehicleVIN)) {
      alert(t("admin.validation.invalidTeslaVehicleVIN"));
      return;
    }

    // ✅ Validazione Partita IVA (regex: 11 cifre)
    const partitaIVARegex = /^[0-9]{11}$/;
    if (!partitaIVARegex.test(formData.companyVatNumber)) {
      alert(t("admin.validation.invalidVat"));
      return;
    }

    const firmaDate = parseISO(formData.uploadDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    if (!isValid(firmaDate) || isAfter(firmaDate, today)) {
      alert(t("admin.mainWorkflow.validation.invalidSignatureDate"));
      return;
    }

    const dummyHash = Math.random().toString(36).substring(2, 10).toUpperCase();

    const formPayload = {
      clientCompanyId: formData.clientCompanyId,
      teslaVehicleId: formData.teslaVehicleId,
      uploadDate: formData.uploadDate,
      zipFilePath: `/pdfs/consents/${formData.teslaVehicleVIN}.pdf`,
      consentHash: dummyHash,
      consentType: formData.consentType,
      notes: formData.notes ?? "",
    };

    try {
      const response = await fetch(`${API_BASE_URL}/api/ClientConsents`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(formPayload),
      });

      if (!response.ok) {
        throw new Error("Errore nella creazione del consenso");
      }

      const newId = await response.json();

      setLocalConsents((prev) => [
        ...prev,
        {
          ...formPayload,
          id: newId,
          companyVatNumber: formData.companyVatNumber,
          teslaVehicleVIN: formData.teslaVehicleVIN,
        },
      ]);

      setCurrentPage(1);
      alert(t("admin.clientConsents.successAddNewConsent"));
    } catch (err) {
      console.error("Errore POST consenso:", err);
      alert("Errore nel salvataggio del consenso.");
    }

    setFormData({
      id: 0,
      clientCompanyId: 0,
      teslaVehicleId: 0,
      uploadDate: "",
      zipFilePath: "",
      consentHash: "",
      consentType: "Consent Activation",
      companyVatNumber: "",
      teslaVehicleVIN: "",
    });

    setShowForm(false);
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
              ? "bg-dataRed hover:bg-red-600"
              : "bg-blue-500 hover:bg-blue-600"
          } text-softWhite px-6 py-2 rounded`}
          onClick={() => setShowForm(!showForm)}
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
          onSubmit={handleSubmit}
          t={t}
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
              {t("admin.clientConsents.teslaVehicleVIN")}
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
                  onClick={() => setSelectedConsentForNotes(consent)}
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
                      } catch (err) {
                        console.error("Errore aggiornamento note:", err);
                        alert("Errore durante il salvataggio delle note.");
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
                {consent.teslaVehicleVIN}
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
