import { TFunction } from "i18next";
import { formatDateToSave } from "@/utils/date";
import { ClientConsent } from "@/types/clientConsentInterfaces";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";

type Props = {
  formData: ClientConsent;
  setFormData: React.Dispatch<React.SetStateAction<ClientConsent>>;
  onSubmit: () => void;
  t: TFunction;
};

export default function AdminClientConsentAddForm({
  formData,
  setFormData,
  onSubmit,
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

  const handleZipUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // ✅ Validazione estensione ZIP
    if (!file.name.endsWith(".zip")) {
      alert("Carica un file .zip valido.");
      return;
    }

    // ✅ Validazione VIN (lunghezza esatta 17)
    if (formData.teslaVehicleVIN.length !== 17) {
      alert("VIN non valido o non compilato. Deve essere lungo 17 caratteri.");
      return;
    }

    // ✅ Validazione Partita IVA (esattamente 11 cifre)
    if (!/^[0-9]{11}$/.test(formData.companyVatNumber.trim())) {
      alert(t("admin.validation.invalidVat"));
      return;
    }

    // ✅ Prepara il FormData
    const uploadForm = new FormData();
    uploadForm.append("zipFile", file);
    uploadForm.append("clientCompanyId", formData.clientCompanyId.toString());
    uploadForm.append("teslaVehicleId", formData.teslaVehicleId.toString());
    uploadForm.append("consentType", formData.consentType);
    uploadForm.append("uploadDate", formData.uploadDate);
    uploadForm.append("companyVatNumber", formData.companyVatNumber);
    uploadForm.append("teslaVehicleVIN", formData.teslaVehicleVIN);

    try {
      const res = await fetch("/api/upload-consent-zip", {
        method: "POST",
        body: uploadForm,
      });

      if (res.status === 409) {
        const conflict = await res.json();
        alert("File già esistente. ID esistente: " + conflict.existingId);
        return;
      }

      if (!res.ok) throw new Error("Errore durante l’upload del file.");

      await res.json(); // { id: number }
      const savedPath = `pdfs/consents/${formData.teslaVehicleVIN}.zip`;
      setFormData({ ...formData, zipFilePath: savedPath });
    } catch (err) {
      console.error("Errore upload ZIP:", err);
      alert("Errore durante il caricamento del file ZIP.");
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
        onClick={onSubmit}
      >
        {t("admin.clientConsents.confirmAddNewConsent")}
      </button>
    </div>
  );
}
