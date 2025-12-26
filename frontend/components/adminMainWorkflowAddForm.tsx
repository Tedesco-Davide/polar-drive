import { TFunction } from "i18next";
import { adminWorkflowTypesInputForm } from "@/types/adminWorkflowTypes";
import { formatDateToSave } from "@/utils/date";
import { fuelTypeOptions } from "@/types/fuelTypes";
import { vehicleOptions } from "@/types/vehicleOptions";
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
};

export default function AdminMainWorkflowInputForm({
  formData,
  setFormData,
  onSubmit,
  t,
}: Props) {
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
        vehicleOptions[formData.brand]?.models[value]?.fuelType ?? "";

      setFormData((prev) => ({
        ...prev,
        model: value,
        fuelType: detectedFuelType,
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
    <div className="bg-softWhite dark:bg-gray-800 p-6 rounded-lg shadow-lg mb-12 border border-gray-300 dark:border-gray-600">
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
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
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.mainWorkflow.labels.companyName")}
          </span>
          <input
            name="companyName"
            value={formData.companyName}
            onChange={handleChange}
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientCompany.referentName")}
          </span>
          <input
            name="referentName"
            value={formData.referentName}
            onChange={handleChange}
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.mainWorkflow.labels.vehicleMobileNumber")}
          </span>
          <input
            maxLength={10}
            pattern="[0-9]*"
            inputMode="numeric"
            name="vehicleMobileNumber"
            value={formData.vehicleMobileNumber}
            onChange={handleChange}
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.mainWorkflow.labels.referentEmail")}
          </span>
          <input
            name="referentEmail"
            value={formData.referentEmail}
            onChange={handleChange}
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.uploadZipSignedActivation")}
          </span>
          <label className="flex flex-col">
            <input
              name="zipFilePath"
              type="file"
              accept=".zip"
              onChange={handleZipUpload}
              className="input text-[12px]"
            />
          </label>
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.mainWorkflow.labels.uploadDate")}
          </span>
          <DatePicker
            className="input appearance-none cursor-text bg-gray-800 text-softWhite border border-gray-600 rounded px-3 py-2 w-full"
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
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientVehicle.fuelType")}
          </span>
          <select
            name="fuelType"
            value={formData.fuelType}
            onChange={handleChange}
            className="input cursor-pointer bg-softWhite dark:bg-gray-700 dark:text-softWhite"
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
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientVehicle.brand")}
          </span>
          <select
            name="brand"
            value={formData.brand}
            onChange={handleChange}
            className="input cursor-pointer bg-softWhite dark:bg-gray-700 dark:text-softWhite"
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
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.mainWorkflow.labels.model")}
          </span>
          <select
            name="model"
            value={formData.model}
            onChange={handleChange}
            className="input cursor-pointer bg-softWhite dark:bg-gray-700 dark:text-softWhite"
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
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.mainWorkflow.labels.vehicleVIN")}
          </span>
          <input
            maxLength={17}
            name="vehicleVIN"
            value={formData.vehicleVIN}
            onChange={handleChange}
            className="input"
          />
        </label>
      </div>
      <button
        className="mt-6 bg-green-700 text-softWhite px-6 py-2 rounded hover:bg-green-600"
        onClick={onSubmit}
      >
        {t("admin.mainWorkflow.button.confirmAddNewVehicle")}
      </button>
    </div>
  );
}
