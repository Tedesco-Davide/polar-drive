import { useEffect, useState } from "react";
import { useTranslation } from "next-i18next";
import { FileArchive, UserSearch } from "lucide-react";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { adminWorkflowTypesInputForm } from "@/types/adminWorkflowTypes";
import { ClientTeslaVehicleWithCompany } from "@/types/adminWorkflowTypesExtended";
import { parseISO, isAfter, isValid } from "date-fns";
import SearchBar from "@/components/searchBar";
import AdminMainWorkflowInputForm from "@/components/adminMainWorkflowInputForm";
import PaginationControls from "@/components/paginationControls";
import TeslaStatusToggle from "./teslaStatusToggle";

export default function AdminMainWorkflow() {
  const [formData, setFormData] = useState<adminWorkflowTypesInputForm>({
    companyVatNumber: "",
    companyName: "",
    referentName: "",
    referentMobile: "",
    referentEmail: "",
    zipFilePath: new File([""], ""),
    uploadDate: "",
    model: "",
    teslaVehicleVIN: "",
    accessToken: "",
    refreshToken: "",
    isTeslaActive: true,
    isTeslaFetchingData: true,
  });

  type WorkflowRow = {
    id: number;
  } & Omit<adminWorkflowTypesInputForm, "zipFilePath"> & {
      zipFilePath: string;
    };

  const [workflowData, setWorkflowData] = useState<WorkflowRow[]>([]);
  const [showForm, setShowForm] = useState(false);
  const { t } = useTranslation("");
  const { query, setQuery, filteredData } = useSearchFilter<WorkflowRow>(
    workflowData,
    [
      "companyVatNumber",
      "companyName",
      "referentMobile",
      "referentEmail",
      "teslaVehicleVIN",
    ]
  );
  const {
    currentPage,
    totalPages,
    currentData: currentPageData,
    nextPage,
    prevPage,
    setCurrentPage,
  } = usePagination<WorkflowRow>(filteredData, 5);

  useEffect(() => {
    fetch("https://localhost:5041/api/ClientTeslaVehicles")
      .then((res) => res.json())
      .then((data: ClientTeslaVehicleWithCompany[]) => {
        setWorkflowData(
          data.map((entry) => ({
            id: entry.id,
            companyVatNumber: entry.clientCompany?.vatNumber ?? "",
            companyName: entry.clientCompany?.name ?? "",
            referentName: entry.clientCompany?.referentName ?? "",
            referentMobile: entry.clientCompany?.referentMobileNumber ?? "",
            referentEmail: entry.clientCompany?.referentEmail ?? "",
            zipFilePath: "",
            uploadDate: entry.firstActivationAt ?? "",
            model: entry.model ?? "",
            teslaVehicleVIN: entry.vin ?? "",
            accessToken: "", // opzionale
            refreshToken: "", // opzionale
            isTeslaActive: entry.isActiveFlag ?? false,
            isTeslaFetchingData: entry.isFetchingDataFlag ?? false,
          }))
        );
        setCurrentPage(1);
      })
      .catch((err) => {
        console.error("API error (admin main workflow):", err);
      });
  }, []);

  const handleSubmit = async () => {
    const requiredFields: (keyof adminWorkflowTypesInputForm)[] = [
      "companyVatNumber",
      "companyName",
      "referentName",
      "referentMobile",
      "referentEmail",
      "zipFilePath",
      "uploadDate",
      "model",
      "teslaVehicleVIN",
      "accessToken",
      "refreshToken",
    ];

    const missing = requiredFields.filter((f) => {
      if (f === "zipFilePath") {
        return (
          !(formData.zipFilePath instanceof File) ||
          formData.zipFilePath.name.trim() === "" ||
          formData.zipFilePath.size === 0
        );
      }
      return !formData[f];
    });

    if (missing.length > 0) {
      const translatedLabels = missing.map((field) =>
        t(`admin.mainWorkflow.labels.${field}`)
      );
      alert(t("admin.missingFields") + ": " + translatedLabels.join(", "));
      return;
    }

    // ✅ Validazione Partita IVA (regex: 11 cifre)
    const partitaIVARegex = /^[0-9]{11}$/;
    if (!partitaIVARegex.test(formData.companyVatNumber)) {
      alert(t("admin.validation.invalidVat"));
      return;
    }

    // ✅ Validazione Cellulare Referente
    const referentMobileRegex = /^[0-9]{10}$/;
    if (!referentMobileRegex.test(formData.referentMobile)) {
      alert(t("admin.validation.invalidMobile"));
      return;
    }

    // ✅ Validazione Email Aziendale
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(formData.referentEmail)) {
      alert(t("admin.validation.invalidEmail"));
      return;
    }

    // ✅ Validazione VIN Tesla (regex: 17 caratteri alfanumerici, senza I/O/Q)
    const vinRegex = /^[A-HJ-NPR-Z0-9]{17}$/;
    if (!vinRegex.test(formData.teslaVehicleVIN)) {
      alert(t("admin.validation.invalidTeslaVehicleVIN"));
      return;
    }

    // ✅ Validazione Data Firma
    const firmaDate = parseISO(formData.uploadDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    if (!isValid(firmaDate) || isAfter(firmaDate, today)) {
      alert(t("admin.mainWorkflow.validation.invalidSignatureDate"));
      return;
    }

    // ✅ Validazione Access Token (JWT)
    const jwtAccessTokenRegex =
      /^[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+$/;
    if (!jwtAccessTokenRegex.test(formData.accessToken)) {
      alert(t("admin.mainWorkflow.validation.invalidAccessToken"));
      return;
    }

    // ✅ Validazione Refresh Token
    const refreshTokenRegex = /^[A-Za-z0-9\-_.]{100,}$/;
    if (!refreshTokenRegex.test(formData.refreshToken)) {
      alert(t("admin.mainWorkflow.validation.invalidRefreshToken"));
      return;
    }

    console.log("Submitting", formData);

    // ✅ 1. Genera URL dal file PDF
    const pdfFileUrl =
      formData.zipFilePath instanceof File
        ? URL.createObjectURL(formData.zipFilePath)
        : "";

    // ✅ 2. Aggiungi ai dati di tabella
    setWorkflowData((prev) => [
      ...prev,
      {
        id: Date.now(), // 👈 ID temporaneo generato via timestamp
        ...formData,
        zipFilePath: pdfFileUrl,
      },
    ]);
    setCurrentPage(1);

    // ✅ 3. Mostra alert di successo
    alert(t("admin.mainWorkflow.button.successAddNewTesla"));

    // ✅ 4. Reset del form
    setFormData({
      companyVatNumber: "",
      companyName: "",
      referentName: "",
      referentMobile: "",
      referentEmail: "",
      zipFilePath: new File([""], ""),
      uploadDate: "",
      model: "",
      teslaVehicleVIN: "",
      accessToken: "",
      refreshToken: "",
      isTeslaActive: true,
      isTeslaFetchingData: true,
    });

    setShowForm(false);
  };

  return (
    <div>
      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl  font-bold text-polarNight dark:text-softWhite">
          {t("admin.mainWorkflow.tableHeader")}
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
            ? t("admin.mainWorkflow.button.undoAddNewTesla")
            : t("admin.mainWorkflow.button.addNewTesla")}
        </button>
      </div>

      {showForm && (
        <AdminMainWorkflowInputForm
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
            <th className="p-4">
              {t("admin.mainWorkflow.headers.isTeslaActive")}
            </th>
            <th className="p-4">
              {t("admin.mainWorkflow.headers.isTeslaFetchingData")}
            </th>
            <th className="p-4">
              {t("admin.mainWorkflow.headers.companyName")}
            </th>
            <th className="p-4">
              {t("admin.mainWorkflow.headers.companyVatNumber")}
            </th>
            <th className="p-4">{t("admin.mainWorkflow.headers.model")}</th>
            <th className="p-4">
              {t("admin.mainWorkflow.headers.teslaVehicleVIN")}
            </th>
          </tr>
        </thead>
        <tbody>
          {currentPageData.map((entry, index) => (
            <tr
              key={index}
              className="border-b border-gray-300 dark:border-gray-600"
            >
              <td className="p-4 space-x-2 inline-flex">
                <button
                  className="p-2 bg-purple-500 text-softWhite rounded hover:bg-purple-600"
                  title={t("admin.mainWorkflow.button.pdfUserAndTesla")}
                >
                  <UserSearch size={16} />
                </button>
                <button
                  className="p-2 bg-yellow-500 text-softWhite rounded hover:bg-yellow-600"
                  title={t("admin.mainWorkflow.button.zipPdfReports")}
                >
                  <FileArchive size={16} />
                </button>
                <button
                  className="p-2 bg-orange-500 text-softWhite rounded hover:bg-orange-600"
                  title={t("admin.mainWorkflow.button.zipConsents")}
                >
                  <FileArchive size={16} />
                </button>
              </td>
              <td className="p-4 align-middle">
                <TeslaStatusToggle
                  id={entry.id}
                  isActive={entry.isTeslaActive}
                  isFetching={entry.isTeslaFetchingData}
                  field="IsActive" // 👈 nuovo
                  onStatusChange={(newIsActive, newIsFetching) => {
                    const updated = [...workflowData];
                    updated[index].isTeslaActive = newIsActive;
                    updated[index].isTeslaFetchingData = newIsFetching;
                    setWorkflowData(updated);
                  }}
                />
              </td>
              <td className="p-4 align-middle">
                <TeslaStatusToggle
                  id={entry.id}
                  isActive={entry.isTeslaActive}
                  isFetching={entry.isTeslaFetchingData}
                  field="IsFetching" // 👈 nuovo
                  onStatusChange={(newIsActive, newIsFetching) => {
                    const updated = [...workflowData];
                    updated[index].isTeslaActive = newIsActive;
                    updated[index].isTeslaFetchingData = newIsFetching;
                    setWorkflowData(updated);
                  }}
                />
              </td>
              <td className="p-4">{entry.companyName}</td>
              <td className="p-4">{entry.companyVatNumber}</td>
              <td className="p-4">{entry.model}</td>
              <td className="p-4">{entry.teslaVehicleVIN}</td>
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
