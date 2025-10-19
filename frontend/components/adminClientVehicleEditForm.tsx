import { useState } from "react";
import { ClientVehicle } from "@/types/vehicleInterfaces";
import { TFunction } from "i18next";
import { formatDateToDisplay } from "@/utils/date";
import { vehicleOptions } from "@/types/vehicleOptions";
import { fuelTypeOptions } from "@/types/fuelTypes";
import { logFrontendEvent } from "@/utils/logger";
import axios from "axios";
import { CircleCheck, CircleX } from "lucide-react";

type Props = {
  vehicle: ClientVehicle;
  onClose: () => void;
  onSave: (updatedVehicle: ClientVehicle) => void;
  t: TFunction;
  refreshWorkflowData: () => Promise<void>;
};

export default function AdminClientVehicleEditForm({
  vehicle,
  onClose,
  onSave,
  t,
  refreshWorkflowData,
}: Props) {
  const [formData, setFormData] = useState<ClientVehicle>({
    ...vehicle,
    firstActivationAt: vehicle.firstActivationAt ?? "",
  });

  const _vehicleOptions = Object.keys(vehicleOptions);

  const modelOptions = formData.brand
    ? Object.keys(vehicleOptions[formData.brand]?.models || {})
    : [];

  const colorOptions =
    formData.brand && formData.model
      ? vehicleOptions[formData.brand]?.models[formData.model]?.colors || []
      : [];

  const trimOptions =
    formData.brand && formData.model
      ? vehicleOptions[formData.brand]?.models[formData.model]?.trims || []
      : [];

  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    const { name, value } = e.target;

    if (name === "brand") {
      setFormData((prev) => ({
        ...prev,
        brand: value,
        model: "",
        trim: "",
        color: "",
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
        trim: "",
        color: "",
      }));
      return;
    }

    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  const handleSubmit = async () => {
    const vinRegex = /^[A-HJ-NPR-Z0-9]{17}$/;

    if (
      !formData.vin ||
      formData.vin.length !== 17 ||
      !vinRegex.test(formData.vin.trim())
    ) {
      alert(t("admin.clientVehicle.validation.invalidVehicleVIN"));
      return;
    }

    if (!formData.fuelType) {
      alert(t("admin.clientVehicle.validation.fuelTypeRequired"));
      return;
    }

    if (!formData.brand?.trim()) {
      alert(t("admin.clientVehicle.validation.brandRequired"));
      return;
    }

    if (!formData.model.trim()) {
      alert(t("admin.clientVehicle.validation.modelRequired"));
      return;
    }

    try {
      await logFrontendEvent(
        "AdminClientVehicleEditForm",
        "INFO",
        "Attempting to update client vehicle",
        JSON.stringify(formData)
      );

      await axios.put(
        `/api/ClientVehicles/${formData.id}`,
        formData
      );

      await logFrontendEvent(
        "AdminClientVehicleEditForm",
        "INFO",
        "Client vehicle updated successfully",
        "VehicleId=" + formData.id + ", VIN=" + formData.vin
      );

      // 🔄 Update workflow table
      await refreshWorkflowData();

      alert(t("admin.successEditRow"));
      onSave(formData);
      onClose();
    } catch (err) {
      const errDetails = err instanceof Error ? err.message : String(err);

      await logFrontendEvent(
        "AdminClientVehicleEditForm",
        "ERROR",
        "Exception thrown during vehicle update",
        errDetails
      );

      console.error(t("admin.genericApiError"), err);
      alert(err instanceof Error ? err.message : t("admin.genericApiError"));
    }
  };

  return (
    <div className="bg-softWhite dark:bg-gray-800 p-6 rounded-lg shadow-lg border border-gray-300 dark:border-gray-600">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.vehicleVIN")}
          </span>
          <input
            name="vin"
            value={formData.vin}
            className="input bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-400 cursor-not-allowed"
            maxLength={17}
            disabled
            readOnly
          />
        </label>

        {/* Fuel Type */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientVehicle.fuelType")}
          </span>
          <select
            name="fuelType"
            value={formData.fuelType}
            onChange={handleChange}
            className="input appearance-none cursor-pointer bg-white dark:bg-gray-700 dark:text-softWhite"
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
            className="input appearance-none cursor-pointer bg-white dark:bg-gray-700 dark:text-softWhite"
            required
          >
            <option value="">{t("admin.basicPlaceholder")}</option>
            {_vehicleOptions.map((brand) => (
              <option key={brand} value={brand}>
                {brand}
              </option>
            ))}
          </select>
        </label>

        {/* Model */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientVehicle.model")}
          </span>
          <select
            name="model"
            value={formData.model}
            onChange={handleChange}
            className="input appearance-none cursor-pointer bg-white dark:bg-gray-700 dark:text-softWhite"
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

        {/* Trim */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientVehicle.trim")}
          </span>
          <select
            name="trim"
            value={formData.trim || ""}
            onChange={handleChange}
            className="input appearance-none cursor-pointer bg-white dark:bg-gray-700 dark:text-softWhite"
            required
            disabled={!formData.model}
          >
            <option value="">{t("admin.basicPlaceholder")}</option>
            {trimOptions.map((trim) => (
              <option key={trim} value={trim}>
                {trim}
              </option>
            ))}
          </select>
        </label>

        {/* Color */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientVehicle.color")}
          </span>
          <select
            name="color"
            value={formData.color}
            onChange={handleChange}
            className="input appearance-none cursor-pointer bg-white dark:bg-gray-700 dark:text-softWhite"
            required
            disabled={!formData.model}
          >
            <option value="">{t("admin.basicPlaceholder")}</option>
            {colorOptions.map((color) => (
              <option key={color} value={color}>
                {color}
              </option>
            ))}
          </select>
        </label>

        {/* ✅ Blocchi read-only in riga completa */}
        <label className="flex flex-col md:flex-row md:items-center gap-2 md:gap-6 col-span-full px-1">
          <div className="flex items-center gap-2">
            <span className="text-xl text-gray-600 dark:text-gray-300">
              {t("admin.clientVehicle.isActive")}
            </span>
            {formData.isActive ? (
              <div className="flex items-center text-green-600 gap-1">
                <CircleCheck size={30} />
                <span className="text-xl">{t("admin.yes")}</span>
              </div>
            ) : (
              <div className="flex items-center text-red-600 gap-1">
                <CircleX size={30} />
                <span className="text-xl">{t("admin.no")}</span>
              </div>
            )}
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xl text-gray-600 dark:text-gray-300">
              {t("admin.clientVehicle.isFetching")}
            </span>
            {formData.isFetching ? (
              <div className="flex items-center text-green-600 gap-1">
                <CircleCheck size={30} />
                <span className="text-xl">{t("admin.yes")}</span>
              </div>
            ) : (
              <div className="flex items-center text-red-600 gap-1">
                <CircleX size={30} />
                <span className="text-xl">{t("admin.no")}</span>
              </div>
            )}
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xl text-gray-600 dark:text-gray-300">
              {t("admin.clientVehicle.firstActivationAt")}
            </span>
            <span className="text-xl">
              {formData.firstActivationAt
                ? formatDateToDisplay(formData.firstActivationAt)
                : "—"}
            </span>
          </div>
        </label>
      </div>

      <div className="mt-6 flex md:flex-row flex-col gap-4">
        <button
          className="bg-green-700 text-softWhite px-6 py-2 rounded hover:bg-green-600"
          onClick={handleSubmit}
        >
          {t("admin.confirmEditRow")}
        </button>
        <button
          className="bg-gray-400 text-white px-6 py-2 rounded hover:bg-gray-500"
          onClick={onClose}
        >
          {t("admin.cancelEditRow")}
        </button>
      </div>
    </div>
  );
}
