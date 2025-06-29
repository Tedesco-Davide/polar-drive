import { useEffect, useState } from "react";
import { useTranslation } from "next-i18next";
import { FileArchive, UserSearch, Link } from "lucide-react";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { FuelType } from "@/types/fuelTypes";
import { API_BASE_URL } from "@/utils/api";
import {
  adminWorkflowTypesInputForm,
  WorkflowRow,
} from "@/types/adminWorkflowTypes";
import { parseISO, isAfter, isValid } from "date-fns";
import { logFrontendEvent } from "@/utils/logger";
import AdminLoader from "@/components/adminLoader";
import SearchBar from "@/components/searchBar";
import AdminMainWorkflowInputForm from "@/components/adminMainWorkflowInputForm";
import PaginationControls from "@/components/paginationControls";
import VehicleStatusToggle from "./vehicleStatusToggle";
import Chip from "./chip";

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
    trim: "",
    color: "",
    isVehicleActive: true,
    isVehicleFetchingData: true,
    clientOAuthAuthorized: false,
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

  useEffect(() => {
    logFrontendEvent(
      "AdminMainWorkflow",
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
  } = usePagination<WorkflowRow>(filteredData, 5);

  useEffect(() => {
    logFrontendEvent(
      "AdminMainWorkflow",
      "DEBUG",
      "Pagination or search interaction",
      `Current page: ${currentPage}, Query: ${query}`
    );
  }, [currentPage, query]);

  useEffect(() => {
    setInternalWorkflowData(workflowData);
    setCurrentPage(1);
  }, [workflowData, setCurrentPage]);

  useEffect(() => {
    logFrontendEvent(
      "AdminMainWorkflow",
      "INFO",
      "Component mounted and initialized with workflow data"
    );
  }, []);

  const handleSubmit = async () => {
    logFrontendEvent("AdminMainWorkflow", "INFO", "Form submission triggered");

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
      logFrontendEvent(
        "AdminMainWorkflow",
        "WARNING",
        "Form validation failed: missing required fields",
        JSON.stringify(missing)
      );
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

    setCurrentPage(1);

    // ✅ Invio dati al backend
    try {
      const formDataToSend = new FormData();
      formDataToSend.append("CompanyName", formData.companyName);
      formDataToSend.append("CompanyVatNumber", formData.companyVatNumber);
      formDataToSend.append("ReferentName", formData.referentName);
      formDataToSend.append("ReferentEmail", formData.referentEmail);
      formDataToSend.append("ReferentMobile", formData.referentMobile);
      formDataToSend.append("VehicleVIN", formData.vehicleVIN);
      formDataToSend.append("VehicleFuelType", formData.fuelType);
      formDataToSend.append("VehicleBrand", formData.brand);
      formDataToSend.append("VehicleModel", formData.model);
      formDataToSend.append("VehicleTrim", formData.trim ?? "");
      formDataToSend.append("VehicleColor", formData.color ?? "");
      formDataToSend.append("UploadDate", formData.uploadDate);
      formDataToSend.append("ConsentZip", formData.zipFilePath);

      const response = await fetch(
        `${API_BASE_URL}/api/adminfullclientinsert`,
        {
          method: "POST",
          body: formDataToSend,
        }
      );

      if (!response.ok) {
        let errorCode = "";
        try {
          const json = await response.json();
          errorCode = json.errorCode || "";
        } catch {
          logFrontendEvent(
            "AdminMainWorkflow",
            "ERROR",
            "Error during form submission",
            `Status: ${response.status}, VIN: ${formData.vehicleVIN}`
          );
          alert(`${t("admin.genericError")} ${await response.text()}`);
          return;
        }

        switch (errorCode) {
          case "VEHICLE_ALREADY_ASSOCIATED_TO_SAME_COMPANY":
            logFrontendEvent(
              "AdminMainWorkflow",
              "WARNING",
              "Form submission failed with known backend error",
              `ErrorCode: ${errorCode}`
            );
            alert(t("admin.vehicleAlreadyAssociatedToSameCompany"));
            return;
          case "VEHICLE_ALREADY_ASSOCIATED_TO_ANOTHER_COMPANY":
            logFrontendEvent(
              "AdminMainWorkflow",
              "WARNING",
              "Form submission failed with known backend error",
              `ErrorCode: ${errorCode}`
            );
            alert(t("admin.vehicleAlreadyAssociatedToAnotherCompany"));
            return;
          case "DUPLICATE_CONSENT_HASH":
            logFrontendEvent(
              "AdminMainWorkflow",
              "WARNING",
              "Form submission failed with known backend error",
              `ErrorCode: ${errorCode}`
            );
            alert(t("admin.duplicatePdfHash"));
            return;
          case "INVALID_ZIP_FORMAT":
            logFrontendEvent(
              "AdminMainWorkflow",
              "WARNING",
              "Form submission failed with known backend error",
              `ErrorCode: ${errorCode}`
            );
            alert(t("admin.validation.invalidZipType"));
            return;
          default:
            logFrontendEvent(
              "AdminMainWorkflow",
              "WARNING",
              "Form submission failed with known backend error",
              `ErrorCode: ${errorCode}`
            );
            alert(`${t("admin.genericError")} ${errorCode}`);
            return;
        }
      }

      // ✅ Mostri alert ed aggiorni i dati
      try {
        alert(t("admin.mainWorkflow.button.successAddNewVehicle"));
        await refreshWorkflowData();
      } catch (error) {
        logFrontendEvent(
          "AdminMainWorkflow",
          "ERROR",
          "Error during workflow refresh after insert",
          error instanceof Error ? error.message : String(error)
        );
        alert(t("admin.genericError"));
        console.error("Errore POST:", error);
      }
    } catch (error) {
      alert(`Errore durante l'inserimento: ${error}`);
      console.error("Errore POST:", error);
      return;
    }

    logFrontendEvent(
      "AdminMainWorkflow",
      "INFO",
      "Form reset after successful submission"
    );

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
      trim: "",
      color: "",
      isVehicleActive: true,
      isVehicleFetchingData: true,
      clientOAuthAuthorized: false,
    });

    setShowForm(false);
  };

  const handleDownloadAllConsents = async (
    companyVatNumber: string,
    companyName: string,
    vehicleVin: string
  ) => {
    try {
      // Validazione preliminare
      if (!companyVatNumber || !companyName) {
        alert(t("admin.mainWorkflow.alerts.missingCompanyData"));
        return;
      }

      logFrontendEvent(
        "AdminMainWorkflow",
        "INFO",
        "Download all consents triggered",
        `Company VAT: ${companyVatNumber}, VIN: ${vehicleVin}`
      );

      // Conferma dall'utente per operazioni su grandi volumi
      const confirmDownload = confirm(
        `${t(
          "admin.mainWorkflow.alerts.confirmDownloadAllConsents"
        )} ${companyName} (${companyVatNumber})?`
      );

      if (!confirmDownload) {
        logFrontendEvent(
          "AdminMainWorkflow",
          "INFO",
          "Download all consents cancelled by user",
          `Company VAT: ${companyVatNumber}`
        );
        return;
      }

      setIsStatusChanging(true);

      const response = await fetch(
        `${API_BASE_URL}/api/clientconsents/download-all-by-company?vatNumber=${encodeURIComponent(
          companyVatNumber
        )}`,
        {
          method: "GET",
          headers: {
            Accept: "application/zip",
          },
        }
      );

      if (!response.ok) {
        let errorMessage = "Unknown error";
        try {
          const errorText = await response.text();
          errorMessage = errorText;
        } catch {
          errorMessage = `HTTP ${response.status}: ${response.statusText}`;
        }
        throw new Error(errorMessage);
      }

      // Verifica che sia effettivamente un file ZIP
      const contentType = response.headers.get("content-type");
      if (!contentType?.includes("application/zip")) {
        throw new Error("Server did not return a ZIP file");
      }

      const blob = await response.blob();

      // Verifica dimensione minima del file (dovrebbe essere > 22 bytes per un ZIP vuoto)
      if (blob.size < 22) {
        throw new Error("Downloaded file appears to be empty or corrupted");
      }

      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;

      const timestamp = new Date()
        .toISOString()
        .slice(0, 19)
        .replace(/[:-]/g, "");
      const sanitizedCompanyName = companyName.replace(/[^a-zA-Z0-9]/g, "_");
      link.download = `consensi_${sanitizedCompanyName}_${companyVatNumber}_${timestamp}.zip`;

      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      logFrontendEvent(
        "AdminMainWorkflow",
        "INFO",
        "All consents downloaded successfully",
        `Company VAT: ${companyVatNumber}, File size: ${blob.size} bytes`
      );

      alert(t("admin.mainWorkflow.alerts.allConsentsDownloaded"));
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Download failed";
      logFrontendEvent(
        "AdminMainWorkflow",
        "ERROR",
        "Failed to download all consents",
        `Company VAT: ${companyVatNumber}, Error: ${errorMessage}`
      );

      // Messaggi di errore più specifici
      if (errorMessage.includes("not found")) {
        alert(t("admin.mainWorkflow.alerts.noConsentsFound"));
      } else if (errorMessage.includes("HTTP 500")) {
        alert(t("admin.mainWorkflow.alerts.serverErrorDownload"));
      } else {
        alert(
          `${t(
            "admin.mainWorkflow.alerts.downloadAllConsentsFail"
          )}: ${errorMessage}`
        );
      }
    } finally {
      setIsStatusChanging(false);
    }
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
          onClick={() => {
            const newValue = !showForm;
            setShowForm(newValue);
            logFrontendEvent(
              "AdminMainWorkflow",
              "INFO",
              "Form visibility toggled",
              `Now showing form: ${newValue}`
            );
          }}
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
              {t("admin.mainWorkflow.headers.isVehicleAuthorized")}
            </th>
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
                  className={`p-2 ${
                    !entry.clientOAuthAuthorized
                      ? "bg-cyan-500 hover:bg-cyan-600"
                      : "bg-slate-500 cursor-not-allowed opacity-20 text-slate-200"
                  } text-softWhite rounded`}
                  disabled={entry.clientOAuthAuthorized}
                  title={t("admin.mainWorkflow.alerts.urlGenerationTooltip")}
                  onClick={async () => {
                    try {
                      if (entry.clientOAuthAuthorized) return;
                      if (!entry.brand) {
                        alert(
                          t(
                            "admin.mainWorkflow.alerts.urlGenerationMissingBrand"
                          )
                        );
                        return;
                      }
                      const brand = entry.brand.toLowerCase();
                      const res = await fetch(
                        `${API_BASE_URL}/api/VehicleOAuth/GenerateUrl?brand=${brand}&vin=${entry.vehicleVIN}`
                      );
                      const data = await res.json();
                      if (data?.url) {
                        await navigator.clipboard.writeText(data.url);
                        logFrontendEvent(
                          "AdminMainWorkflow",
                          "INFO",
                          "OAuth URL generated and copied to clipboard",
                          `VIN: ${entry.vehicleVIN}`
                        );
                        alert(
                          t("admin.mainWorkflow.alerts.urlGenerationConfirm")
                        );
                      } else {
                        alert(t("admin.mainWorkflow.alerts.urlGenerationFail"));
                      }
                    } catch (err) {
                      logFrontendEvent(
                        "AdminMainWorkflow",
                        "ERROR",
                        "OAuth URL generation failed",
                        err instanceof Error ? err.message : String(err)
                      );
                      console.error("Errore OAuth:", err);
                      alert(
                        t("admin.mainWorkflow.alerts.urlGenerationOAuthFail")
                      );
                    }
                  }}
                >
                  <Link size={16} />
                </button>
                <button
                  className="p-2 bg-purple-500 hover:bg-purple-600 text-softWhite rounded"
                  title={t("admin.mainWorkflow.button.pdfUserAndVehicle")}
                  onClick={() => {
                    alert("TODO");
                    // azione che fa i check interni se non ci sono dati
                  }}
                >
                  <UserSearch size={16} />
                </button>
                <button
                  className="p-2 bg-yellow-500 hover:bg-yellow-600 text-softWhite rounded"
                  title={t("admin.mainWorkflow.button.zipPdfReports")}
                  onClick={() => {
                    alert("TODO");
                    // azione che fa i check interni se non ci sono dati
                  }}
                >
                  <FileArchive size={16} />
                </button>
                <button
                  className="p-2 bg-orange-500 hover:bg-orange-600 text-softWhite rounded"
                  title={t("admin.mainWorkflow.button.zipConsents")}
                  onClick={() => {
                    handleDownloadAllConsents(
                      entry.companyVatNumber,
                      entry.companyName,
                      entry.vehicleVIN
                    );
                  }}
                >
                  <FileArchive size={16} />
                </button>
              </td>
              <td className="p-4">
                <Chip
                  className={`w-[155px] ${
                    entry.clientOAuthAuthorized
                      ? "bg-green-100 text-green-700 border-green-500"
                      : "bg-red-100 text-red-700 border-red-500"
                  }`}
                >
                  {entry.clientOAuthAuthorized
                    ? t("admin.authorized")
                    : t("admin.pendingAuthorization")}
                </Chip>
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
                    logFrontendEvent(
                      "AdminMainWorkflow",
                      "INFO",
                      "Vehicle status changed",
                      `VIN: ${entry.vehicleVIN}, Active: ${newIsActive}, Fetching: ${newIsFetching}`
                    );
                  }}
                  setLoading={setIsStatusChanging}
                  refreshWorkflowData={refreshWorkflowData}
                  disabled={!entry.clientOAuthAuthorized}
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
                    logFrontendEvent(
                      "AdminMainWorkflow",
                      "INFO",
                      "Vehicle status changed",
                      `VIN: ${entry.vehicleVIN}, Active: ${newIsActive}, Fetching: ${newIsFetching}`
                    );
                  }}
                  setLoading={setIsStatusChanging}
                  refreshWorkflowData={refreshWorkflowData}
                  disabled={!entry.clientOAuthAuthorized}
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
