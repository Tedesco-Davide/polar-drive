import { TFunction } from "i18next";
import { formatDateToSave } from "@/utils/date";
import { ClientConsent } from "@/types/clientConsentInterfaces";
import { API_BASE_URL } from "@/utils/api";
import { isAfter, isValid, parseISO } from "date-fns";
import { logFrontendEvent } from "@/utils/logger";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";

type Props = {
  formData: ClientConsent;
  setFormData: React.Dispatch<React.SetStateAction<ClientConsent>>;
  t: TFunction;
  refreshClientConsents: () => Promise<void>;
};

export default function AdminClientConsentAddForm({
  formData,
  setFormData,
  t,
  refreshClientConsents,
}: Props) {
  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    const { name, value } = e.target;
    setFormData({
      ...formData,
      [name]: value,
    });
  };

  const handleZipUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.name.endsWith(".zip")) {
      alert(t("admin.validation.invalidZipType"));
      return;
    }

    // ✅ Tutto OK
    setFormData((prev) => ({
      ...prev,
      zipFile: file,
    }));
  };

  const handleSubmit = async () => {
    const {
      companyVatNumber,
      vehicleVIN: vehicleVIN,
      consentType,
      uploadDate,
    } = formData;

    // Validazione aggregata
    const requiredFields = [
      "companyVatNumber",
      "vehicleVIN",
      "consentType",
      "uploadDate",
    ] as const;
    const missing = requiredFields.filter((field) => !formData[field]);
    if (missing.length > 0) {
      const translatedLabels = missing.map((field) =>
        t(`admin.clientConsents.${field}`)
      );
      alert(t("admin.missingFields") + ": " + translatedLabels.join(", "));
      return;
    }

    // Validazione consentType accettabile
    const validConsentTypes = new Set([
      "Consent Deactivation",
      "Consent Stop Data Fetching",
      "Consent Reactivation",
    ]);
    if (!validConsentTypes.has(consentType)) {
      alert(t("admin.clientConsents.validation.consentType"));
      return;
    }

    // Validazione regex VIN / P.IVA
    if (!/^[0-9]{11}$/.test(companyVatNumber)) {
      alert(t("admin.validation.invalidVat"));
      return;
    }

    if (!/^[A-HJ-NPR-Z0-9]{17}$/.test(vehicleVIN)) {
      alert(t("admin.validation.invalidVehicleVIN"));
      return;
    }

    // Validazione data corretta e non futura
    const firmaDate = parseISO(uploadDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    if (!isValid(firmaDate) || isAfter(firmaDate, today)) {
      alert(t("admin.mainWorkflow.validation.invalidSignatureDate"));
      return;
    }

    if (!formData.zipFile) {
      alert(t("admin.validation.invalidZipTypeRequiredConsent"));
      return;
    }

    try {
      await logFrontendEvent(
        "AdminClientConsentAddForm",
        "INFO",
        "Attempting to upload a new consent ZIP",
        JSON.stringify(formData)
      );

      const resolveRes = await fetch(
        `${API_BASE_URL}/api/ClientConsents/resolve-ids?vatNumber=${companyVatNumber}&vin=${vehicleVIN}`
      );

      if (!resolveRes.ok) {
        await logFrontendEvent(
          "AdminClientConsentAddForm",
          "WARNING",
          "resolve-ids failed (VAT or VIN not found)",
          `VAT: ${companyVatNumber}, VIN: ${vehicleVIN}`
        );
        alert(t("admin.resolveVATandVIN"));
        return;
      }

      const { clientCompanyId, vehicleId: vehicleId } = await resolveRes.json();

      const uploadForm = new FormData();
      uploadForm.append("zipFile", formData.zipFile);
      uploadForm.append("clientCompanyId", clientCompanyId.toString());
      uploadForm.append("vehicleId", vehicleId.toString());
      uploadForm.append("consentType", consentType);
      uploadForm.append("uploadDate", uploadDate);
      uploadForm.append("companyVatNumber", companyVatNumber);
      uploadForm.append("vehicleVIN", vehicleVIN);

      const res = await fetch(`${API_BASE_URL}/api/uploadconsentzip`, {
        method: "POST",
        body: uploadForm,
      });

      if (res.status === 409) {
        const conflict = await res.json();
        await logFrontendEvent(
          "AdminClientConsentAddForm",
          "WARNING",
          "Duplicate consent hash detected",
          `existingId=${conflict.existingId}`
        );
        alert(
          t("admin.clientConsents.validation.hashIDalready") +
            conflict.existingId
        );
        return;
      }

      if (!res.ok) {
        const errMsg = await res.text();
        await logFrontendEvent(
          "AdminClientConsentAddForm",
          "ERROR",
          "Consent upload failed",
          errMsg
        );
        console.error(
          t("admin.clientConsents.validation.genericError"),
          errMsg
        );
        alert(errMsg);
        return;
      }

      await logFrontendEvent(
        "AdminClientConsentAddForm",
        "INFO",
        "Consent uploaded successfully",
        `CompanyId=${clientCompanyId}, VehicleId=${vehicleId}`
      );

      alert(t("admin.clientConsents.successAddNewConsent"));

      await refreshClientConsents();

      setFormData({
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
        zipFile: null,
      });
    } catch (err) {
      const errMsg = err instanceof Error ? err.message : String(err);
      await logFrontendEvent(
        "AdminClientConsentAddForm",
        "ERROR",
        "Exception thrown during consent upload",
        errMsg
      );
      console.error(t("admin.clientConsents.validation.genericError"), err);
      alert(t("admin.clientConsents.validation.genericError"));
    }
  };

  return (
    <div className="bg-softWhite dark:bg-gray-800 p-6 rounded-lg shadow-lg mb-12 border border-gray-300 dark:border-gray-600">
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientConsents.consentType")}
          </span>
          <select
            name="consentType"
            value={formData.consentType}
            onChange={handleChange}
            className="input cursor-pointer bg-softWhite dark:bg-gray-700 dark:text-softWhite"
          >
            <option value="">{t("admin.basicPlaceholder")}</option>
            <option value="Consent Deactivation">Consent Deactivation</option>
            <option value="Consent Stop Data Fetching">
              Consent Stop Data Fetching
            </option>
            <option value="Consent Reactivation">Consent Reactivation</option>
          </select>
        </label>

        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientConsents.companyVatNumber")}
          </span>
          <input
            name="companyVatNumber"
            value={formData.companyVatNumber}
            onChange={handleChange}
            className="input"
            maxLength={11}
            pattern="[0-9]*"
            inputMode="numeric"
          />
        </label>

        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientConsents.vehicleVIN")}
          </span>
          <input
            name="vehicleVIN"
            value={formData.vehicleVIN}
            onChange={handleChange}
            className="input"
            maxLength={17}
            pattern="[A-HJ-NPR-Z0-9]*"
            inputMode="text"
          />
        </label>

        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientConsents.uploadDate")}
          </span>
          <DatePicker
            className="input appearance-none cursor-text bg-gray-800 text-softWhite border border-gray-600 rounded px-3 py-2 w-full"
            selected={
              formData.uploadDate ? new Date(formData.uploadDate) : null
            }
            onChange={(date: Date | null) => {
              if (!date) return;
              const formatted = formatDateToSave(date);
              setFormData({ ...formData, uploadDate: formatted });
            }}
            dateFormat="dd/MM/yyyy"
            placeholderText="dd/MM/yyyy"
          />
        </label>

        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.uploadZipSignedConsentGeneric")}
          </span>
          <input
            type="file"
            accept=".zip"
            onChange={handleZipUpload}
            className="input text-[12px]"
          />
        </label>
      </div>

      <button
        className="mt-6 bg-green-700 text-softWhite px-6 py-2 rounded hover:bg-green-600"
        onClick={handleSubmit}
      >
        {t("admin.clientConsents.confirmAddNewConsent")}
      </button>
    </div>
  );
}
