import { useEffect, useState } from "react";
import { useTranslation } from "next-i18next";
import { FileArchive, UserSearch } from "lucide-react";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { FuelType } from "@/types/fuelTypes";
import { API_BASE_URL } from "@/utils/api";
import {
  adminWorkflowTypesInputForm,
  WorkflowRow,
} from "@/types/adminWorkflowTypes";
import { parseISO, isAfter, isValid } from "date-fns";
import AdminLoader from "@/components/adminLoader";
import SearchBar from "@/components/searchBar";
import AdminMainWorkflowInputForm from "@/components/adminMainWorkflowInputForm";
import PaginationControls from "@/components/paginationControls";
import VehicleStatusToggle from "./vehicleStatusToggle";

export default function AdminMainWorkflow({
  workflowData,
  refreshWorkflowData,
}: {
  workflowData: WorkflowRow[];
  refreshWorkflowData: () => Promise<void>;
}) {
  const [formData, setFormData] = useState<adminWorkflowTypesInputForm>({
    companyVatNumber: "",
    companyName: "",
    referentName: "",
    referentMobile: "",
    referentEmail: "",
    zipFilePath: new File([""], ""),
    uploadDate: "",
    model: "",
    fuelType: FuelType.Electric,
    vehicleVIN: "",
    brand: "",
    accessToken: "",
    refreshToken: "",
    isVehicleActive: true,
    isVehicleFetchingData: true,
  });

  const [internalWorkflowData, setInternalWorkflowData] =
    useState<WorkflowRow[]>(workflowData);
  const [showForm, setShowForm] = useState(false);
  const [isStatusChanging, setIsStatusChanging] = useState(false);
  const { t } = useTranslation("");

  const { query, setQuery, filteredData } = useSearchFilter<WorkflowRow>(
    internalWorkflowData,
    [
      "companyVatNumber",
      "companyName",
      "referentMobile",
      "referentEmail",
      "vehicleVIN",
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
    setInternalWorkflowData(workflowData);
    setCurrentPage(1);
  }, [workflowData, setCurrentPage]);

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
      "fuelType",
      "vehicleVIN",
      "brand",
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

    // ✅ Validazione Alimentazaione
    if (!formData.fuelType) {
      alert(t("admin.clientVehicle.validation.fuelTypeRequired"));
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

    // ✅ Validazione VIN Vehicle (regex: 17 caratteri alfanumerici, senza I/O/Q)
    const vinRegex = /^[A-HJ-NPR-Z0-9]{17}$/;
    if (!vinRegex.test(formData.vehicleVIN)) {
      alert(t("admin.validation.invalidVehicleVIN"));
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

    setCurrentPage(1);

    // ✅ Invio dati al backend
    try {
      const formDataToSend = new FormData();
      formDataToSend.append("companyVatNumber", formData.companyVatNumber);
      formDataToSend.append("companyName", formData.companyName);
      formDataToSend.append("referentName", formData.referentName);
      formDataToSend.append("referentMobile", formData.referentMobile);
      formDataToSend.append("referentEmail", formData.referentEmail);
      formDataToSend.append("ConsentZip", formData.zipFilePath);
      formDataToSend.append("uploadDate", formData.uploadDate);
      formDataToSend.append("model", formData.model);
      formDataToSend.append("fuelType", formData.fuelType);
      formDataToSend.append("vehicleVIN", formData.vehicleVIN);
      formDataToSend.append("brand", formData.brand);
      formDataToSend.append("accessToken", formData.accessToken);
      formDataToSend.append("refreshToken", formData.refreshToken);

      const response = await fetch(
        `${API_BASE_URL}/api/adminfullclientinsert`,
        {
          method: "POST",
          body: formDataToSend,
        }
      );

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(errorText);
      }

      // ✅ Mostri alert ed aggiorni i dati
      try {
        alert(t("admin.mainWorkflow.button.successAddNewVehicle"));
        await refreshWorkflowData();
      } catch (refreshErr) {
        console.warn("Refresh fallito dopo insert:", refreshErr);
      }
    } catch (error) {
      alert(`Errore durante l'inserimento: ${error}`);
      console.error("Errore POST:", error);
      return;
    }

    // ✅ Reset del form
    setFormData({
      companyVatNumber: "",
      companyName: "",
      referentName: "",
      referentMobile: "",
      referentEmail: "",
      zipFilePath: new File([""], ""),
      uploadDate: "",
      model: "",
      fuelType: FuelType.Electric,
      vehicleVIN: "",
      brand: "",
      accessToken: "",
      refreshToken: "",
      isVehicleActive: true,
      isVehicleFetchingData: true,
    });

    setShowForm(false);
  };

  return (
    <div>
      {isStatusChanging && <AdminLoader />}
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
            ? t("admin.mainWorkflow.button.undoAddNewVehicle")
            : t("admin.mainWorkflow.button.addNewVehicle")}
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
              {t("admin.mainWorkflow.headers.isVehicleActive")}
            </th>
            <th className="p-4">
              {t("admin.mainWorkflow.headers.isVehicleFetchingData")}
            </th>
            <th className="p-4">
              {t("admin.mainWorkflow.headers.companyName")}
            </th>
            <th className="p-4">
              {t("admin.mainWorkflow.headers.companyVatNumber")}
            </th>
            <th className="p-4">{t("admin.mainWorkflow.headers.model")}</th>
            <th className="p-4">
              {t("admin.mainWorkflow.headers.vehicleVIN")}
            </th>
          </tr>
        </thead>
        <tbody>
          {currentPageData.map((entry, index) => (
            <tr
              key={entry.id}
              className="border-b border-gray-300 dark:border-gray-600"
            >
              <td className="p-4 space-x-2 inline-flex">
                <button
                  className="p-2 bg-purple-500 text-softWhite rounded hover:bg-purple-600"
                  title={t("admin.mainWorkflow.button.pdfUserAndVehicle")}
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
                <VehicleStatusToggle
                  id={entry.id}
                  isActive={entry.isVehicleActive}
                  isFetching={entry.isVehicleFetchingData}
                  field="IsActive"
                  onStatusChange={(newIsActive, newIsFetching) => {
                    const updated = [...workflowData];
                    updated[index].isVehicleActive = newIsActive;
                    updated[index].isVehicleFetchingData = newIsFetching;
                    setInternalWorkflowData(updated);
                  }}
                  setLoading={setIsStatusChanging}
                  refreshWorkflowData={refreshWorkflowData}
                />
              </td>
              <td className="p-4 align-middle">
                <VehicleStatusToggle
                  id={entry.id}
                  isActive={entry.isVehicleActive}
                  isFetching={entry.isVehicleFetchingData}
                  field="IsFetching"
                  onStatusChange={(newIsActive, newIsFetching) => {
                    const updated = [...workflowData];
                    updated[index].isVehicleActive = newIsActive;
                    updated[index].isVehicleFetchingData = newIsFetching;
                    setInternalWorkflowData(updated);
                  }}
                  setLoading={setIsStatusChanging}
                  refreshWorkflowData={refreshWorkflowData}
                />
              </td>
              <td className="p-4">{entry.companyName}</td>
              <td className="p-4">{entry.companyVatNumber}</td>
              <td className="p-4">{entry.model}</td>
              <td className="p-4">{entry.vehicleVIN}</td>
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
