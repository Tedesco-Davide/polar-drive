import { TFunction } from "i18next";
import { adminWorkflowTypesInputForm } from "@/types/adminWorkflowTypes";
import { formatDateToSave } from "@/utils/date";
import { fuelTypeOptions } from "@/types/fuelTypes";
import { useVehicleOptions } from "@/utils/useVehicleOptions";
import { logFrontendEvent } from "@/utils/logger";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";

type Props = {
  formData: adminWorkflowTypesInputForm;
  setFormData: React.Dispatch<
    React.SetStateAction<adminWorkflowTypesInputForm>
  >;
  onSubmit: () => void;
  t: TFunction;
  isSubmitting?: boolean;
};

export default function AdminMainWorkflowInputForm({
  formData,
  setFormData,
  onSubmit,
  t,
  isSubmitting = false,
}: Props) {
  const { options: vehicleOptions, loading: loadingOptions } = useVehicleOptions();
  const brandOptions = Object.keys(vehicleOptions);
  
  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    const { name, value, files } = e.target as HTMLInputElement;

    if (name === "brand") {
      setFormData((prev) => ({
        ...prev,
        brand: value,
        model: "",
      }));
      return;
    }

    if (name === "model") {
      const detectedFuelType =
        vehicleOptions[formData.brand]?.models[value]?.fuelType;

      setFormData((prev) => ({
        ...prev,
        model: value,
        // Preserva il fuelType esistente se non riesce a rilevarlo dalle opzioni
        fuelType: detectedFuelType ?? prev.fuelType,
      }));
      return;
    }

    // ✅ ZIP upload: controlli specifici
    if (name === "zipFilePath" && files?.[0]) {
      const file = files[0];

      const allowedTypes = ["application/zip", "application/x-zip-compressed"];
      const maxSize = 50 * 1024 * 1024; // 50MB

      if (!allowedTypes.includes(file.type)) {
        alert(t("admin.validation.invalidZipType"));
        e.target.value = "";
        return;
      }

      if (file.size > maxSize) {
        alert(t("admin.validation.zipTooLarge"));
        e.target.value = "";
        return;
      }

      setFormData({
        ...formData,
        zipFilePath: file,
      });
      return;
    }

    // ✅ Per tutti gli altri campi (input testo, select, ecc.)
    setFormData({
      ...formData,
      [name]: files ? files[0] : value,
    });
  };

  const handleZipUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // ✅ Extension check
    if (!file.name.toLowerCase().endsWith(".zip")) {
      alert(t("admin.validation.invalidZipType"));
      await logFrontendEvent(
        "AdminMainWorkflowInputForm",
        "WARNING",
        "ZIP file upload rejected: invalid extension",
        file.name
      );
      return;
    }

    try {
      // ✅ All OK
      await logFrontendEvent(
        "AdminMainWorkflowInputForm",
        "INFO",
        "ZIP file validated and accepted",
        file.name
      );

      setFormData((prev) => ({
        ...prev,
        zipFilePath: file,
      }));
    } catch (err) {
      const errorDetails = err instanceof Error ? err.message : String(err);
      console.error("ZIP read error:", err);

      await logFrontendEvent(
        "AdminMainWorkflowInputForm",
        "ERROR",
        "Error while reading uploaded ZIP file",
        errorDetails
      );

      alert(t("admin.validation.invalidZipCorrupted"));
    }
  };

  const modelOptions =
    formData.brand && vehicleOptions[formData.brand]
      ? Object.keys(vehicleOptions[formData.brand].models)
      : [];

  return (
    <div className="bg-softWhite dark:bg-gray-800 p-4 sm:p-6 rounded-lg shadow-lg mb-8 sm:mb-12 border border-gray-300 dark:border-gray-600">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 sm:gap-4">
        {/* P.IVA */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.mainWorkflow.labels.companyVatNumber")}
          </span>
          <input
            maxLength={11}
            pattern="[0-9]*"
            inputMode="numeric"
            name="companyVatNumber"
            value={formData.companyVatNumber}
            onChange={handleChange}
            className="input h-12 text-base"
            autoComplete="off"
          />
        </label>
        {/* Nome Azienda */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.mainWorkflow.labels.companyName")}
          </span>
          <input
            name="companyName"
            value={formData.companyName}
            onChange={handleChange}
            className="input h-12 text-base"
            autoComplete="off"
          />
        </label>
        {/* Nome Referente */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientCompany.referentName")}
          </span>
          <input
            name="referentName"
            value={formData.referentName}
            onChange={handleChange}
            className="input h-12 text-base"
            autoComplete="off"
          />
        </label>
        {/* Telefono Veicolo */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.mainWorkflow.labels.vehicleMobileNumber")}
          </span>
          <input
            maxLength={10}
            pattern="[0-9]*"
            inputMode="tel"
            name="vehicleMobileNumber"
            value={formData.vehicleMobileNumber}
            onChange={handleChange}
            className="input h-12 text-base"
            autoComplete="tel"
          />
        </label>
        {/* Email Referente */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.mainWorkflow.labels.referentEmail")}
          </span>
          <input
            name="referentEmail"
            type="email"
            inputMode="email"
            value={formData.referentEmail}
            onChange={handleChange}
            className="input h-12 text-base"
            autoComplete="email"
          />
        </label>
        {/* Upload ZIP */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.uploadZipSignedActivation")}
          </span>
          <div className="relative">
            <input
              name="zipFilePath"
              type="file"
              accept=".zip"
              onChange={handleZipUpload}
              className="input w-full h-12 text-sm file:mr-2 file:py-1 file:px-3 file:rounded file:border-0 file:text-sm file:bg-blue-500 file:text-white hover:file:bg-blue-600 file:cursor-pointer"
            />
          </div>
        </label>
        {/* Data Firma */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.mainWorkflow.labels.uploadDate")}
          </span>
          <DatePicker
            className="input h-12 text-base appearance-none cursor-text bg-gray-800 text-softWhite border border-gray-600 rounded px-3 py-2 w-full"
            selected={
              formData.uploadDate ? new Date(formData.uploadDate) : null
            }
            onChange={(date: Date | null) => {
              if (!date) return;
              const formatted = formatDateToSave(date);
              setFormData({
                ...formData,
                uploadDate: formatted,
              });
            }}
            dateFormat="dd/MM/yyyy"
            placeholderText="dd/MM/yyyy"
            withPortal
          />
        </label>
        {/* Tipo Carburante */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientVehicle.fuelType")}
          </span>
          <select
            name="fuelType"
            value={formData.fuelType}
            onChange={handleChange}
            className="input h-12 text-base cursor-pointer bg-softWhite dark:bg-gray-700 dark:text-softWhite"
            required
          >
            <option value="">{t("admin.basicPlaceholder")}</option>
            {fuelTypeOptions.map(({ value, label }) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        {/* Brand */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientVehicle.brand")}
          </span>
          <select
            name="brand"
            value={formData.brand}
            onChange={handleChange}
            className="input h-12 text-base cursor-pointer bg-softWhite dark:bg-gray-700 dark:text-softWhite"
            required
          >
            <option value="">{t("admin.basicPlaceholder")}</option>
            {brandOptions.map((brand) => (
              <option key={brand} value={brand}>
                {brand}
              </option>
            ))}
          </select>
        </label>
        {/* Modello */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.mainWorkflow.labels.model")}
          </span>
          <select
            name="model"
            value={formData.model}
            onChange={handleChange}
            className="input h-12 text-base cursor-pointer bg-softWhite dark:bg-gray-700 dark:text-softWhite"
            required
            disabled={!formData.brand}
          >
            <option value="">{t("admin.basicPlaceholder")}</option>
            {modelOptions.map((model) => (
              <option key={model} value={model}>
                {model}
              </option>
            ))}
          </select>
        </label>
        {/* VIN */}
        <label className="flex flex-col sm:col-span-2 lg:col-span-1">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.mainWorkflow.labels.vehicleVIN")}
          </span>
          <input
            maxLength={17}
            name="vehicleVIN"
            value={formData.vehicleVIN}
            onChange={handleChange}
            className="input h-12 text-base uppercase"
            autoComplete="off"
            autoCapitalize="characters"
          />
        </label>
      </div>
      {/* Submit Button - Full width su mobile */}
      <button
        className="mt-6 w-full sm:w-auto bg-green-700 text-softWhite px-6 py-3 sm:py-2 rounded text-base font-medium hover:bg-green-600 disabled:opacity-50 disabled:cursor-not-allowed active:bg-green-800 transition-colors"
        onClick={onSubmit}
        disabled={isSubmitting}
      >
        {isSubmitting ? t("admin.loading") : t("admin.mainWorkflow.button.confirmAddNewVehicle")}
      </button>
    </div>
  );
}
