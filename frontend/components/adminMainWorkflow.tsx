import { useEffect, useState } from "react";
import { useTranslation } from "next-i18next";
import { FileArchive, UserSearch, Link, MessageSquare } from "lucide-react";
import { FuelType } from "@/types/fuelTypes";
import {
  adminWorkflowTypesInputForm,
  WorkflowRow,
} from "@/types/adminWorkflowTypes";
import { parseISO, isAfter, isValid } from "date-fns";
import { logFrontendEvent } from "@/utils/logger";
import AdminLoader from "@/components/adminLoader";
import SearchBar from "@/components/searchBar";
import AdminMainWorkflowInputForm from "@/components/adminMainWorkflowAddForm";
import PaginationControls from "@/components/paginationControls";
import VehicleStatusToggle from "./vehicleStatusToggle";
import Chip from "./chip";
import AdminSmsProfileModal from "./adminSmsProfileModal";
import AdminSmsGdprModal from "./adminSmsGdprModal";
import AdminSmsAuditModal from "./adminSmsAuditModal";

export default function AdminMainWorkflow() {
  const { t } = useTranslation("");
  const [workflowData, setWorkflowData] = useState<WorkflowRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isStatusChanging, setIsStatusChanging] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [generatingProfileId, setGeneratingProfileId] = useState<number | null>(
    null
  );
  const [smsModalOpen, setSmsModalOpen] = useState(false);
  const [smsGdprModalOpen, setSmsGdprModalOpen] = useState(false);
  const [smsAuditModalOpen, setSmsAuditModalOpen] = useState(false);
  const [selectedVehicleForSms, setSelectedVehicleForSms] = useState<{
    id: number;
    vin: string;
    brand: string;
    companyName: string;
    isActive: boolean;
  } | null>(null);

  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [query, setQuery] = useState("");
  const pageSize = 5;

  const [formData, setFormData] = useState<adminWorkflowTypesInputForm>({
    companyId: 0,
    companyVatNumber: "",
    companyName: "",
    referentName: "",
    vehicleMobileNumber: "",
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

  const [searchType, setSearchType] = useState<"id" | "status">("id");

const fetchWorkflowData = async (page: number, searchQuery: string = "") => {
    try {
      setLoading(true);
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      });
      if (searchQuery) {
        params.append("search", searchQuery);
        const type = searchType === "id" ? "vin" : "company";
        params.append("searchType", type);
      }

      const res = await fetch(`/api/clientvehicles?${params}`);
      if (!res.ok) throw new Error("HTTP " + res.status);

      const response = await res.json();

      const mapped: WorkflowRow[] = response.data.map(
        (entry: {
          id: number;
          vin: string;
          fuelType: string;
          brand: string;
          model: string;
          trim?: string;
          color?: string;
          isActive: boolean;
          isFetching: boolean;
          firstActivationAt?: string;
          clientOAuthAuthorized?: boolean;
          referentName?: string;
          vehicleMobileNumber?: string;
          referentEmail?: string;
          clientCompany?: {
            id: number;
            vatNumber: string;
            name: string;
          };
        }) => ({
          id: entry.id,
          companyId: entry.clientCompany?.id ?? 0,
          companyVatNumber: entry.clientCompany?.vatNumber ?? "",
          companyName: entry.clientCompany?.name ?? "",
          referentName: entry.referentName ?? "",
          vehicleMobileNumber: entry.vehicleMobileNumber ?? "",
          referentEmail: entry.referentEmail ?? "",
          zipFilePath: "",
          uploadDate: entry.firstActivationAt ?? "",
          model: entry.model ?? "",
          fuelType: entry.fuelType ?? "",
          vehicleVIN: entry.vin ?? "",
          color: entry.color ?? "",
          trim: entry.trim ?? "",
          accessToken: "",
          refreshToken: "",
          brand: entry.brand ?? "",
          isVehicleActive: entry.isActive,
          isVehicleFetchingData: entry.isFetching,
          clientOAuthAuthorized: entry.clientOAuthAuthorized ?? false,
        })
      );

      setWorkflowData(mapped);
      setTotalCount(response.totalCount);
      setTotalPages(response.totalPages);
      setCurrentPage(response.page);

      logFrontendEvent(
        "AdminMainWorkflow",
        "INFO",
        "Workflow data loaded",
        `Page: ${response.page}, Total: ${response.totalCount}`
      );
    } catch (err) {
      logFrontendEvent(
        "AdminMainWorkflow",
        "ERROR",
        "Failed to load workflow data",
        String(err)
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchWorkflowData(currentPage, query);
  }, [currentPage, query]);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchWorkflowData(currentPage, query);
    setIsRefreshing(false);
  };

  const handleSubmit = async () => {
    logFrontendEvent("AdminMainWorkflow", "INFO", "Form submission triggered");

    const requiredFields: (keyof adminWorkflowTypesInputForm)[] = [
      "companyVatNumber",
      "companyName",
      "referentName",
      "vehicleMobileNumber",
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
      const translatedLabels = missing.map((field) =>
        t(`admin.mainWorkflow.labels.${field}`)
      );
      alert(t("admin.missingFields") + ": " + translatedLabels.join(", "));
      return;
    }

    if (!formData.fuelType) {
      alert(t("admin.clientVehicle.validation.fuelTypeRequired"));
      return;
    }

    const partitaIVARegex = /^[0-9]{11}$/;
    if (!partitaIVARegex.test(formData.companyVatNumber)) {
      alert(t("admin.validation.invalidVat"));
      return;
    }

    // Accetta 10 cifre (3926321311) o +39 seguito da 10 cifre (+393926321311)
    const vehicleMobileNumberRegex = /^(\+39)?[0-9]{10}$/;
    if (!vehicleMobileNumberRegex.test(formData.vehicleMobileNumber)) {
      alert(t("admin.validation.invalidMobile"));
      return;
    }

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(formData.referentEmail)) {
      alert(t("admin.validation.invalidEmail"));
      return;
    }

    const vinRegex = /^[A-HJ-NPR-Z0-9]{17}$/;
    if (!vinRegex.test(formData.vehicleVIN)) {
      alert(t("admin.validation.invalidVehicleVIN"));
      return;
    }

    const firmaDate = parseISO(formData.uploadDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    if (!isValid(firmaDate) || isAfter(firmaDate, today)) {
      alert(t("admin.mainWorkflow.validation.invalidSignatureDate"));
      return;
    }

    setIsSubmitting(true);
    try {
      const formDataToSend = new FormData();
      formDataToSend.append("CompanyName", formData.companyName);
      formDataToSend.append("CompanyVatNumber", formData.companyVatNumber);
      formDataToSend.append("ReferentName", formData.referentName);
      formDataToSend.append("ReferentEmail", formData.referentEmail);
      formDataToSend.append(
        "VehicleMobileNumber",
        formData.vehicleMobileNumber
      );
      formDataToSend.append("VehicleVIN", formData.vehicleVIN);
      formDataToSend.append("VehicleFuelType", formData.fuelType);
      formDataToSend.append("VehicleBrand", formData.brand);
      formDataToSend.append("VehicleModel", formData.model);
      formDataToSend.append("VehicleTrim", formData.trim ?? "");
      formDataToSend.append("VehicleColor", formData.color ?? "");
      formDataToSend.append("UploadDate", formData.uploadDate);
      formDataToSend.append("ConsentZip", formData.zipFilePath);

      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 120000); // 2 minuti timeout

      const response = await fetch(`/api/adminfullclientinsert`, {
        method: "POST",
        body: formDataToSend,
        signal: controller.signal,
      });

      clearTimeout(timeoutId);

      if (!response.ok) {
        let errorCode = "";
        const responseText = await response.text();
        console.error(`[AdminMainWorkflow] HTTP ${response.status}: ${responseText}`);
        try {
          const json = JSON.parse(responseText);
          errorCode = json.errorCode || "";
        } catch {
          alert(`${t("admin.genericError")} HTTP ${response.status} - ${responseText || "Nessuna risposta dal server"}`);
          return;
        }

        switch (errorCode) {
          case "VEHICLE_ALREADY_ASSOCIATED_TO_SAME_COMPANY":
            alert(t("admin.vehicleAlreadyAssociatedToSameCompany"));
            return;
          case "VEHICLE_ALREADY_ASSOCIATED_TO_ANOTHER_COMPANY":
            alert(t("admin.vehicleAlreadyAssociatedToAnotherCompany"));
            return;
          case "DUPLICATE_CONSENT_HASH":
            alert(t("admin.duplicatePdfHash"));
            return;
          case "INVALID_ZIP_FORMAT":
            alert(t("admin.validation.invalidZipType"));
            return;
          default:
            alert(`${t("admin.genericError")} ${errorCode}`);
            return;
        }
      }

      alert(t("admin.mainWorkflow.button.successAddNewVehicle"));
      await fetchWorkflowData(currentPage, query);

      setFormData({
        companyId: 0,
        companyVatNumber: "",
        companyName: "",
        referentName: "",
        vehicleMobileNumber: "",
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
    } catch (error) {
      const errorMessage = String(error);

      // Gestione specifica per errore file modificato durante upload
      if (errorMessage.includes("ERR_UPLOAD_FILE_CHANGED") ||
          errorMessage.includes("upload file changed") ||
          errorMessage.includes("Failed to fetch")) {
        alert(t("admin.fileUploadChanged"));
      } else {
        alert(t("admin.mainWorkflow.insertError", { error: errorMessage }));
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDownloadAllConsents = async (
    companyVatNumber: string,
    companyName: string
  ) => {
    try {
      if (!companyVatNumber || !companyName) {
        alert(t("admin.mainWorkflow.alerts.missingCompanyData"));
        return;
      }

      const confirmDownload = confirm(
        `${t(
          "admin.mainWorkflow.alerts.confirmDownloadAllConsents"
        )} ${companyName} (${companyVatNumber})?`
      );
      if (!confirmDownload) return;

      setIsStatusChanging(true);

      const response = await fetch(
        `/api/clientconsents/download-all-by-company?vatNumber=${encodeURIComponent(
          companyVatNumber
        )}`,
        { method: "GET" }
      );
      const contentType = response.headers.get("content-type");

      if (contentType && contentType.includes("application/json")) {
        const result = await response.json();
        if (!result.success)
          throw new Error(result.message || "Download failed");
        if (!result.hasData) {
          alert(
            result.message || t("admin.mainWorkflow.alerts.noConsentsFound")
          );
          return;
        }
      }

      if (!response.ok) throw new Error("HTTP " + response.status);
      if (!contentType?.includes("application/zip"))
        throw new Error("Server did not return a ZIP file");

      const blob = await response.blob();
      if (blob.size < 22)
        throw new Error("Downloaded file appears to be empty or corrupted");

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

      alert(t("admin.mainWorkflow.alerts.allConsentsDownloaded"));
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Download failed";
      alert(
        `${t(
          "admin.mainWorkflow.alerts.downloadAllConsentsFail"
        )}: ${errorMessage}`
      );
    } finally {
      setIsStatusChanging(false);
    }
  };

  const handleGenerateClientProfile = async (
    companyId: number,
    companyName: string,
    vatNumber: string
  ) => {
    try {
      if (!companyId || !companyName) {
        alert(t("admin.mainWorkflow.alerts.missingCompanyData"));
        return;
      }

      const confirmGeneration = confirm(
        `${t(
          "admin.mainWorkflow.alerts.confirmGenerateProfile"
        )} ${companyName} (${vatNumber})?`
      );
      if (!confirmGeneration) return;

      setGeneratingProfileId(companyId);

      const response = await fetch(
        `/api/ClientProfile/${companyId}/generate-profile-pdf`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
        }
      );

      const contentType = response.headers.get("content-type");
      if (contentType && contentType.includes("application/json")) {
        const result = await response.json();
        if (!response.ok)
          throw new Error(result.message || "Errore nella generazione del PDF");
        if (result.message) {
          alert(result.message);
          return;
        }
      }

      if (!response.ok) throw new Error("HTTP " + response.status);
      if (!contentType?.includes("application/pdf"))
        throw new Error("Il server non ha restituito un file PDF valido");

      const blob = await response.blob();
      if (blob.size < 100)
        throw new Error("Il file PDF generato sembra essere vuoto o corrotto");

      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      const timestamp = new Date()
        .toISOString()
        .slice(0, 19)
        .replace(/[:-]/g, "");
      const sanitizedCompanyName = companyName.replace(/[^a-zA-Z0-9]/g, "_");
      link.download = `Profilo_Cliente_${sanitizedCompanyName}_${vatNumber}_${timestamp}.pdf`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      alert(t("admin.mainWorkflow.alerts.clientProfileGenerated"));
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Generazione PDF fallita";
      alert(
        `${t("admin.mainWorkflow.alerts.generateProfileFail")}: ${errorMessage}`
      );
    } finally {
      setGeneratingProfileId(null);
    }
  };

  return (
    <div className="relative">
      {(loading || isRefreshing || isStatusChanging || isSubmitting) && <AdminLoader local />}

      {/* Header responsive */}
      <div className="flex flex-col sm:flex-row sm:items-center gap-3 mb-6 sm:mb-12">
        <h1 className="text-xl sm:text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.mainWorkflow.tableHeader")} ➜ {totalCount}
        </h1>
        <button
          className={`${
            showForm
              ? "bg-dataRed hover:bg-red-600 active:bg-red-700"
              : "bg-blue-500 hover:bg-blue-600 active:bg-blue-700"
          } text-softWhite px-4 sm:px-6 py-3 sm:py-2 rounded text-sm sm:text-base font-medium transition-colors w-full sm:w-auto`}
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
          isSubmitting={isSubmitting}
        />
      )}

      {/* Toolbar SOLO per mobile - visibile solo sotto lg */}
      <div className="lg:hidden grid grid-cols-3 gap-2 mb-4 p-3 bg-gray-200 dark:bg-gray-700 rounded-lg">
        <button
          onClick={handleRefresh}
          disabled={isRefreshing}
          className="px-2 py-3 bg-blue-500 text-white rounded text-xs font-medium hover:bg-blue-600 active:bg-blue-700 disabled:opacity-50 transition-colors"
        >
          {t("admin.tableRefreshButton")}
        </button>
        <button
          onClick={() => setSmsGdprModalOpen(true)}
          className="px-2 py-3 bg-green-500 text-white rounded text-xs font-medium hover:bg-green-600 active:bg-green-700 transition-colors"
          title={t("admin.smsManagement.buttonGdpr")}
        >
          🔐 GDPR
        </button>
        <button
          onClick={() => setSmsAuditModalOpen(true)}
          className="px-2 py-3 bg-green-500 text-white rounded text-xs font-medium hover:bg-green-600 active:bg-green-700 transition-colors"
          title={t("admin.smsManagement.titleAudit")}
        >
          📊 AUDIT
        </button>
      </div>

      {/* Vista MOBILE: Card Layout */}
      <div className="lg:hidden space-y-4">
        {workflowData.map((entry, index) => (
          <div
            key={entry.id}
            className="bg-softWhite dark:bg-gray-800 rounded-lg shadow-md border border-gray-300 dark:border-gray-600 overflow-hidden"
          >
            {/* Header Card con info veicolo */}
            <div className="p-3 bg-gray-100 dark:bg-gray-700">
              <div className="flex items-start justify-between gap-2 mb-1">
                <span className="font-bold text-base text-polarNight dark:text-softWhite">
                  {entry.brand} {entry.model}
                </span>
                <Chip
                  className={`text-[10px] whitespace-nowrap px-2 py-1 ${
                    entry.clientOAuthAuthorized
                      ? "bg-green-100 text-green-700 border-green-500"
                      : "bg-red-100 text-red-700 border-red-500"
                  }`}
                >
                  {entry.clientOAuthAuthorized ? "✓ Auth" : "⏳ Pending"}
                </Chip>
              </div>
              <div className="text-xs text-gray-500 dark:text-gray-400 font-mono break-all">
                VIN: {entry.vehicleVIN}
              </div>
            </div>

            {/* Dettagli Azienda */}
            <div className="p-3 text-sm border-t border-gray-200 dark:border-gray-600">
              <div className="flex justify-between items-center mb-1">
                <span className="text-gray-500 dark:text-gray-400 text-xs">Azienda:</span>
                <span className="font-medium text-polarNight dark:text-softWhite text-right text-xs truncate max-w-[65%]">
                  {entry.companyName}
                </span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-gray-500 dark:text-gray-400 text-xs">P.IVA:</span>
                <span className="font-mono text-polarNight dark:text-softWhite text-xs">
                  {entry.companyVatNumber}
                </span>
              </div>
            </div>

            {/* Toggle Status - Griglia fissa 2 colonne */}
            <div className="p-3 bg-gray-100 dark:bg-gray-700 border-t border-gray-200 dark:border-gray-600">
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div className="flex flex-col items-center">
                  <span className="text-[10px] text-gray-500 dark:text-gray-400 mb-1 text-center">
                    Veicolo Attivo
                  </span>
                  <VehicleStatusToggle
                    id={entry.id}
                    isActive={entry.isVehicleActive}
                    isFetching={entry.isVehicleFetchingData}
                    field="IsActive"
                    onStatusChange={(newIsActive, newIsFetching) => {
                      setWorkflowData((prev) =>
                        prev.map((w, i) =>
                          i === index
                            ? {
                                ...w,
                                isVehicleActive: newIsActive,
                                isVehicleFetchingData: newIsFetching,
                              }
                            : w
                        )
                      );
                    }}
                    setLoading={setIsStatusChanging}
                    refreshWorkflowData={async () =>
                      await fetchWorkflowData(currentPage, query)
                    }
                    disabled={!entry.clientOAuthAuthorized}
                  />
                </div>
                <div className="flex flex-col items-center">
                  <span className="text-[10px] text-gray-500 dark:text-gray-400 mb-1 text-center">
                    Fetch Dati
                  </span>
                  <VehicleStatusToggle
                    id={entry.id}
                    isActive={entry.isVehicleActive}
                    isFetching={entry.isVehicleFetchingData}
                    field="IsFetching"
                    onStatusChange={(newIsActive, newIsFetching) => {
                      setWorkflowData((prev) =>
                        prev.map((w, i) =>
                          i === index
                            ? {
                                ...w,
                                isVehicleActive: newIsActive,
                                isVehicleFetchingData: newIsFetching,
                              }
                            : w
                        )
                      );
                    }}
                    setLoading={setIsStatusChanging}
                    refreshWorkflowData={async () =>
                      await fetchWorkflowData(currentPage, query)
                    }
                    disabled={!entry.clientOAuthAuthorized}
                  />
                </div>
              </div>
            </div>

            {/* Bottoni Azioni - Grid 2x2 con style inline per garantire funzionamento */}
            <div className="p-3 border-t border-gray-200 dark:border-gray-600">
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                <button
                  style={{ minHeight: '44px' }}
                  className={`flex items-center justify-center gap-1 p-2 ${
                    !entry.clientOAuthAuthorized
                      ? "bg-cyan-500 active:bg-cyan-700"
                      : "bg-slate-400 cursor-not-allowed opacity-40"
                  } text-white rounded-lg`}
                  disabled={entry.clientOAuthAuthorized}
                  onClick={async () => {
                    if (entry.clientOAuthAuthorized || !entry.brand) return;
                    try {
                      const res = await fetch(
                        `/api/VehicleOAuth/GenerateUrl?brand=${entry.brand.toLowerCase()}&vin=${entry.vehicleVIN}`
                      );
                      const data = await res.json();
                      if (data?.url) {
                        await navigator.clipboard.writeText(data.url);
                        alert(t("admin.mainWorkflow.alerts.urlGenerationConfirm"));
                      } else {
                        alert(t("admin.mainWorkflow.alerts.urlGenerationFail"));
                      }
                    } catch {
                      alert(t("admin.mainWorkflow.alerts.urlGenerationOAuthFail"));
                    }
                  }}
                >
                  <Link size={16} />
                  <span className="text-xs font-medium">OAuth</span>
                </button>
                <button
                  style={{ minHeight: '44px' }}
                  className={`flex items-center justify-center gap-1 p-2 ${
                    entry.isVehicleActive
                      ? "bg-green-500 active:bg-green-700"
                      : "bg-gray-400 cursor-not-allowed opacity-40"
                  } text-white rounded-lg`}
                  disabled={!entry.isVehicleActive}
                  onClick={() => {
                    if (entry.isVehicleActive) {
                      setSelectedVehicleForSms({
                        id: entry.id,
                        brand: entry.brand,
                        vin: entry.vehicleVIN,
                        companyName: entry.companyName,
                        isActive: entry.isVehicleActive,
                      });
                      setSmsModalOpen(true);
                    }
                  }}
                >
                  <MessageSquare size={16} />
                  <span className="text-xs font-medium">SMS</span>
                </button>
                <button
                  style={{ minHeight: '44px' }}
                  className="flex items-center justify-center gap-1 p-2 text-white rounded-lg bg-purple-500 active:bg-purple-700 disabled:bg-gray-400 disabled:opacity-40"
                  disabled={generatingProfileId === entry.companyId}
                  onClick={() =>
                    handleGenerateClientProfile(
                      entry.companyId,
                      entry.companyName,
                      entry.companyVatNumber
                    )
                  }
                >
                  {generatingProfileId === entry.companyId ? (
                    <AdminLoader inline />
                  ) : (
                    <>
                      <UserSearch size={16} />
                      <span className="text-xs font-medium">Profilo</span>
                    </>
                  )}
                </button>
                <button
                  style={{ minHeight: '44px' }}
                  className="flex items-center justify-center gap-1 p-2 bg-yellow-500 active:bg-yellow-700 text-white rounded-lg"
                  onClick={() =>
                    handleDownloadAllConsents(
                      entry.companyVatNumber,
                      entry.companyName
                    )
                  }
                >
                  <FileArchive size={16} />
                  <span className="text-xs font-medium">ZIP</span>
                </button>
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Vista DESKTOP: Table Layout */}
      <table className="hidden lg:table w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
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
              </button>{" "}
              <button
                onClick={() => setSmsGdprModalOpen(true)}
                className="px-2 bg-green-500 text-white rounded text-sm hover:bg-green-600"
                title={t("admin.smsManagement.buttonGdpr")}
              >
                <span className="uppercase text-xs tracking-widest">
                  🔐 {t("admin.smsManagement.buttonGdprShort")}
                </span>
              </button>{" "}
              <button
                onClick={() => setSmsAuditModalOpen(true)}
                className="px-2 bg-green-500 text-white rounded text-sm hover:bg-green-600"
                title={t("admin.smsManagement.titleAudit")}
              >
                <span className="uppercase text-xs tracking-widest">
                  📊 AUDIT
                </span>
              </button>
            </th>
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
          {workflowData.map((entry, index) => (
            <tr
              key={entry.id}
              className="border-b border-gray-300 dark:border-gray-600"
            >
              <td className="p-4 space-x-2 inline-flex">
                <button
                  className={`p-2 ${
                    !entry.clientOAuthAuthorized
                      ? "bg-cyan-500 hover:bg-cyan-600"
                      : "bg-slate-400 cursor-not-allowed opacity-20"
                  } text-softWhite rounded`}
                  disabled={entry.clientOAuthAuthorized}
                  title={t("admin.mainWorkflow.alerts.urlGenerationTooltip")}
                  onClick={async () => {
                    if (entry.clientOAuthAuthorized || !entry.brand) return;
                    try {
                      const res = await fetch(
                        `/api/VehicleOAuth/GenerateUrl?brand=${entry.brand.toLowerCase()}&vin=${
                          entry.vehicleVIN
                        }`
                      );
                      const data = await res.json();
                      if (data?.url) {
                        await navigator.clipboard.writeText(data.url);
                        alert(
                          t("admin.mainWorkflow.alerts.urlGenerationConfirm")
                        );
                      } else {
                        alert(t("admin.mainWorkflow.alerts.urlGenerationFail"));
                      }
                    } catch {
                      alert(
                        t("admin.mainWorkflow.alerts.urlGenerationOAuthFail")
                      );
                    }
                  }}
                >
                  <Link size={16} />
                </button>
                <button
                  className={`p-2 ${
                    entry.isVehicleActive
                      ? "bg-green-500 hover:bg-green-600"
                      : "bg-gray-400 cursor-not-allowed opacity-20"
                  } text-softWhite rounded`}
                  disabled={!entry.isVehicleActive}
                  title={t("admin.mainWorkflow.alerts.adaptiveSmsTooltip")}
                  onClick={() => {
                    if (entry.isVehicleActive) {
                      setSelectedVehicleForSms({
                        id: entry.id,
                        brand: entry.brand,
                        vin: entry.vehicleVIN,
                        companyName: entry.companyName,
                        isActive: entry.isVehicleActive,
                      });
                      setSmsModalOpen(true);
                    }
                  }}
                >
                  <MessageSquare size={16} />
                </button>
                <button
                  className="p-2 text-softWhite rounded bg-purple-500 hover:bg-purple-600 disabled:bg-gray-400 disabled:opacity-20"
                  disabled={generatingProfileId === entry.companyId}
                  title={t("admin.mainWorkflow.alerts.clientProfileTooltip")}
                  onClick={() =>
                    handleGenerateClientProfile(
                      entry.companyId,
                      entry.companyName,
                      entry.companyVatNumber
                    )
                  }
                >
                  {generatingProfileId === entry.companyId ? (
                    <AdminLoader inline />
                  ) : (
                    <UserSearch size={16} />
                  )}
                </button>
                <button
                  className="p-2 bg-yellow-500 hover:bg-yellow-600 text-softWhite rounded"
                  title={t("admin.mainWorkflow.alerts.clientZipConsentsTooltip")}
                  onClick={() =>
                    handleDownloadAllConsents(
                      entry.companyVatNumber,
                      entry.companyName
                    )
                  }
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
                    setWorkflowData((prev) =>
                      prev.map((w, i) =>
                        i === index
                          ? {
                              ...w,
                              isVehicleActive: newIsActive,
                              isVehicleFetchingData: newIsFetching,
                            }
                          : w
                      )
                    );
                  }}
                  setLoading={setIsStatusChanging}
                  refreshWorkflowData={async () =>
                    await fetchWorkflowData(currentPage, query)
                  }
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
                    setWorkflowData((prev) =>
                      prev.map((w, i) =>
                        i === index
                          ? {
                              ...w,
                              isVehicleActive: newIsActive,
                              isVehicleFetchingData: newIsFetching,
                            }
                          : w
                      )
                    );
                  }}
                  setLoading={setIsStatusChanging}
                  refreshWorkflowData={async () =>
                    await fetchWorkflowData(currentPage, query)
                  }
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

      {/* Controlli Paginazione e Ricerca - Full width su desktop */}
      <div className="flex flex-col lg:flex-row items-stretch lg:items-center gap-3 lg:gap-4 mt-6 w-full">
        <div className="order-2 lg:order-1 shrink-0">
          <PaginationControls
            currentPage={currentPage}
            totalPages={totalPages}
            onPrev={() => setCurrentPage((p) => Math.max(1, p - 1))}
            onNext={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
          />
        </div>
        <div className="order-1 lg:order-2 flex-1 min-w-0">
          <SearchBar
            query={query}
            setQuery={setQuery}
            resetPage={() => setCurrentPage(1)}
            searchMode="vin-or-company"
            externalSearchType={searchType}
            onSearchTypeChange={(type) => {
              if (type === "id" || type === "status") {
                setSearchType(type);
              }
            }}
            vatLabel={t("admin.vehicleVIN")}
            companyLabel={t("admin.clientCompany.name")}
          />
        </div>
      </div>

      {selectedVehicleForSms && (
        <AdminSmsProfileModal
          isOpen={smsModalOpen}
          onClose={() => {
            setSmsModalOpen(false);
            setSelectedVehicleForSms(null);
          }}
          vehicleId={selectedVehicleForSms.id}
          vehicleVin={selectedVehicleForSms.vin}
          vehicleBrand={selectedVehicleForSms.brand}
          companyName={selectedVehicleForSms.companyName}
          isVehicleActive={selectedVehicleForSms.isActive}
        />
      )}

      <AdminSmsGdprModal
        isOpen={smsGdprModalOpen}
        onClose={() => setSmsGdprModalOpen(false)}
      />

      <AdminSmsAuditModal
        isOpen={smsAuditModalOpen}
        onClose={() => setSmsAuditModalOpen(false)}
      />
    </div>
  );
}
