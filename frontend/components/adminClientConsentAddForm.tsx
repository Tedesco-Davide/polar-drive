import { TFunction } from "i18next";
import { formatDateToSave } from "@/utils/date";
import { ClientConsent } from "@/types/clientConsentInterfaces";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";
import { API_BASE_URL } from "@/utils/api";
import { isAfter, isValid, parseISO } from "date-fns";

type Props = {
  formData: ClientConsent;
  setFormData: React.Dispatch<React.SetStateAction<ClientConsent>>;
  t: TFunction;
};

export default function AdminClientConsentAddForm({
  formData,
  setFormData,
  t,
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

  const handleZipUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.name.endsWith(".zip")) {
      alert("Carica un file .zip valido.");
      return;
    }

    setFormData((prev) => ({
      ...prev,
      zipFile: file,
    }));
  };

  const handleSubmit = async () => {
    const { companyVatNumber, teslaVehicleVIN, consentType, uploadDate } =
      formData;

    // Validazione aggregata
    const requiredFields = [
      "companyVatNumber",
      "teslaVehicleVIN",
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
      alert("Tipo di consenso non valido per l'inserimento manuale.");
      return;
    }

    // Validazione regex VIN / P.IVA
    if (!/^[0-9]{11}$/.test(companyVatNumber)) {
      alert(t("admin.validation.invalidVat"));
      return;
    }

    if (!/^[A-HJ-NPR-Z0-9]{17}$/.test(teslaVehicleVIN)) {
      alert(t("admin.validation.invalidTeslaVehicleVIN"));
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
      alert("Devi selezionare un file ZIP valido.");
      return;
    }

    try {
      const resolveRes = await fetch(
        `${API_BASE_URL}/api/ClientConsents/resolve-ids?vatNumber=${companyVatNumber}&vin=${teslaVehicleVIN}`
      );

      if (!resolveRes.ok) {
        alert("Impossibile risolvere azienda o veicolo.");
        return;
      }

      const { clientCompanyId, teslaVehicleId } = await resolveRes.json();

      const uploadForm = new FormData();
      uploadForm.append("zipFile", formData.zipFile);
      uploadForm.append("clientCompanyId", clientCompanyId.toString());
      uploadForm.append("teslaVehicleId", teslaVehicleId.toString());
      uploadForm.append("consentType", consentType);
      uploadForm.append("uploadDate", uploadDate);
      uploadForm.append("companyVatNumber", companyVatNumber);
      uploadForm.append("teslaVehicleVIN", teslaVehicleVIN);

      const res = await fetch(`${API_BASE_URL}/api/upload-consent-zip`, {
        method: "POST",
        body: uploadForm,
      });

      if (res.status === 409) {
        const conflict = await res.json();
        alert("File già caricato. ID: " + conflict.existingId);
        return;
      }

      if (!res.ok)
        throw new Error("Errore durante upload ZIP + salvataggio consenso.");

      const saved = await res.json();
      alert(t("admin.clientConsents.successAddNewConsent"));

      window.dispatchEvent(
        new CustomEvent("refresh-consents", { detail: saved })
      );

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
        notes: "",
        zipFile: null,
      });
    } catch (err) {
      console.error("Errore:", err);
      alert("Errore durante l’upload del consenso.");
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
            {t("admin.clientConsents.teslaVehicleVIN")}
          </span>
          <input
            name="teslaVehicleVIN"
            value={formData.teslaVehicleVIN}
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
            {t("admin.uploadZipOutageSigned")}
          </span>
          <input
            type="file"
            accept=".zip"
            onChange={handleZipUpload}
            className="input text-[12px]"
            disabled={formData.teslaVehicleVIN.length !== 17}
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
