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
import AdminMainWorkflowInputForm from "@/components/adminMainWorkflowInputForm";
import PaginationControls from "@/components/paginationControls";
import VehicleStatusToggle from "./vehicleStatusToggle";
import Chip from "./chip";
import AdminSmsManagementModal from "./adminSmsManagementModal";

export default function AdminMainWorkflow() {
  const { t } = useTranslation("");
  const [workflowData, setWorkflowData] = useState<WorkflowRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isStatusChanging, setIsStatusChanging] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [generatingProfileId, setGeneratingProfileId] = useState<number | null>(
    null
  );
  const [smsModalOpen, setSmsModalOpen] = useState(false);
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

    const vehicleMobileNumberRegex = /^[0-9]{10}$/;
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

      const response = await fetch(`/api/adminfullclientinsert`, {
        method: "POST",
        body: formDataToSend,
      });

      if (!response.ok) {
        let errorCode = "";
        try {
          const json = await response.json();
          errorCode = json.errorCode || "";
        } catch {
          alert(`${t("admin.genericError")} ${await response.text()}`);
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
      alert(`Errore durante l'inserimento: ${error}`);
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
      {(loading || isRefreshing || isStatusChanging) && <AdminLoader local />}

      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.mainWorkflow.tableHeader")} ➜ {totalCount}
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
              {t("admin.actions")}
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
          searchMode="vin-or-company"
          externalSearchType={searchType}
          onSearchTypeChange={setSearchType}
          vatLabel={t("admin.vehicleVIN")}
          companyLabel={t("admin.clientCompany.name")}
        />
      </div>

      {selectedVehicleForSms && (
        <AdminSmsManagementModal
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
    </div>
  );
}
