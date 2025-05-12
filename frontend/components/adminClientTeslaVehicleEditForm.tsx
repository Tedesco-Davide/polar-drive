import { useState } from "react";
import { ClientTeslaVehicle } from "@/types/teslaVehicleInterfaces";
import { TFunction } from "i18next";
import { API_BASE_URL } from "@/utils/api";
import { formatDateToDisplay } from "@/utils/date";
import { isAfter, isValid, parseISO } from "date-fns";
import axios from "axios";

type Props = {
  vehicle: ClientTeslaVehicle;
  onClose: () => void;
  onSave: (updatedVehicle: ClientTeslaVehicle) => void;
  t: TFunction;
  refreshWorkflowData: () => Promise<void>;
};

export default function AdminClientTeslaVehicleEditForm({
  vehicle,
  onClose,
  onSave,
  t,
  refreshWorkflowData,
}: Props) {
  const [formData, setFormData] = useState<ClientTeslaVehicle>({
    ...vehicle,
    firstActivationAt: vehicle.firstActivationAt ?? "",
  });

  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    const { name, value } = e.target;
    if (name === "firstActivationAt") return;
    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  const handleSubmit = async () => {
    console.log("formData.id:", formData.id);
    const vinRegex = /^[A-HJ-NPR-Z0-9]{17}$/;

    if (!vinRegex.test(formData.vin.trim())) {
      alert(t("admin.clientTeslaVehicle.validation.invalidTeslaVehicleVIN"));
      return;
    }

    if (!formData.model.trim()) {
      alert(t("admin.clientTeslaVehicle.validation.modelRequired"));
      return;
    }

    if (formData.firstActivationAt) {
      const parsedDate = parseISO(formData.firstActivationAt);
      const today = new Date();
      today.setHours(0, 0, 0, 0);

      if (!isValid(parsedDate) || isAfter(parsedDate, today)) {
        alert("Data prima attivazione non valida o nel futuro");
        return;
      }
    }

    try {
      await axios.put(
        `${API_BASE_URL}/api/ClientTeslaVehicles/${formData.id}`,
        formData
      );

      // 🔄 Aggiorna la tabella Gestione principale workflow
      await refreshWorkflowData();

      alert(t("admin.successEditRow"));
      onSave(formData);
      onClose();
    } catch (err) {
      console.error(t("admin.genericApiError"), err);
      alert(err instanceof Error ? err.message : t("admin.genericApiError"));
    }
  };

  return (
    <div className="bg-softWhite dark:bg-gray-800 p-6 rounded-lg shadow-lg border border-gray-300 dark:border-gray-600">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            VIN
          </span>
          <input
            name="vin"
            value={formData.vin}
            onChange={handleChange}
            className="input"
            maxLength={17}
            required
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientTeslaVehicle.model")}
          </span>
          <select
            name="model"
            value={formData.model}
            onChange={handleChange}
            className="input appearance-none cursor-pointer bg-white dark:bg-gray-700 dark:text-softWhite"
            required
          >
            <option value="">{t("admin.basicPlaceholder")}</option>
            <option value="Model 3">Model 3</option>
            <option value="Model Y">Model Y</option>
            <option value="Model S">Model S</option>
            <option value="Model X">Model X</option>
          </select>
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientTeslaVehicle.trim")}
          </span>
          <input
            name="trim"
            value={formData.trim}
            onChange={handleChange}
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientTeslaVehicle.color")}
          </span>
          <input
            name="color"
            value={formData.color}
            onChange={handleChange}
            className="input"
          />
        </label>

        {/* Read-only visualizzazione stati attivazione */}
        <div className="flex items-center gap-2">
          <span className="text-2xl text-gray-600 dark:text-gray-300">
            {t("admin.clientTeslaVehicle.isActive")}
          </span>
          <span className="text-2xl">
            {formData.isActive ? `✅ ${t("admin.yes")}` : `🛑 ${t("admin.no")}`}
          </span>

          <span className="text-2xl text-gray-600 dark:text-gray-300">
            {t("admin.clientTeslaVehicle.isFetching")}
          </span>
          <span className="text-2xl">
            {formData.isFetching
              ? `✅ ${t("admin.yes")}`
              : `🛑 ${t("admin.no")}`}
          </span>

          <span className="text-2xl text-gray-600 dark:text-gray-300">
            {t("admin.clientTeslaVehicle.firstActivationAt")}
          </span>
          <span className="text-2xl">
            {formData.firstActivationAt
              ? formatDateToDisplay(formData.firstActivationAt)
              : "—"}
          </span>
        </div>
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
