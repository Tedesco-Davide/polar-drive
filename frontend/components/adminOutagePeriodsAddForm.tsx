import { TFunction } from "i18next";
import { formatDateToSave } from "@/utils/date";
import { OutageFormData } from "@/types/outagePeriodTypes";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";

type Props = {
  formData: OutageFormData;
  setFormData: React.Dispatch<React.SetStateAction<OutageFormData>>;
  onSubmit: () => void;
  t: TFunction;
};

export default function AdminOutagePeriodsAddForm({
  formData,
  setFormData,
  onSubmit,
  t,
}: Props) {
  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    const { name, value, files } = e.target as HTMLInputElement;

    if (name === "zipFilePath" && files?.[0]) {
      const file = files[0];
      if (
        file.type !== "application/zip" &&
        file.type !== "application/x-zip-compressed"
      ) {
        alert(t("admin.validation.invalidZipType"));
        e.target.value = "";
        return;
      }
      const maxSize = 50 * 1024 * 1024;
      if (file.size > maxSize) {
        alert(t("admin.outagePeriods.validation.zipTooLarge"));
        e.target.value = "";
        return;
      }
      setFormData({
        ...formData,
        [name]: file,
      });
      return;
    }

    if (name === "autoDetected") {
      setFormData({
        ...formData,
        autoDetected: value === "true",
      });
      return;
    }

    setFormData({
      ...formData,
      [name]: value,
    });
  };

  return (
    <div className="bg-softWhite dark:bg-gray-800 p-6 rounded-lg shadow-lg mb-12 border border-gray-300 dark:border-gray-600">
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {/* Auto Detected */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.autoDetected")}
          </span>
          <select
            name="autoDetected"
            value={formData.autoDetected ? "true" : "false"}
            onChange={handleChange}
            className="input cursor-pointer"
          >
            <option value="false">{t("admin.no")}</option>
            <option value="true">{t("admin.yes")}</option>
          </select>
        </label>

        {/* Status */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.status")}
          </span>
          <select
            name="status"
            value={formData.status}
            onChange={handleChange}
            className="input cursor-pointer"
          >
            <option value="">{t("admin.basicPlaceholder")}</option>{" "}
            {/* 🆕 basicPlaceholder */}
            <option value="OUTAGE-ONGOING">OUTAGE-ONGOING</option>
            <option value="OUTAGE-RESOLVED">OUTAGE-RESOLVED</option>
          </select>
        </label>

        {/* Outage Type */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.outageType")}
          </span>
          <select
            name="outageType"
            value={formData.outageType}
            onChange={handleChange}
            className="input cursor-pointer"
          >
            <option value="">{t("admin.basicPlaceholder")}</option>{" "}
            {/* 🆕 basicPlaceholder */}
            <option value="Outage Veichle">Outage Veichle</option>
            <option value="Outage Fleet Api">Outage Fleet Api</option>
          </select>
        </label>

        {/* Outage Start */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.outageStart")}
          </span>
          <DatePicker
            className="input appearance-none cursor-text bg-gray-800 text-softWhite border border-gray-600 rounded px-3 py-2 w-full"
            selected={
              formData.outageStart ? new Date(formData.outageStart) : null
            }
            onChange={(date: Date | null) => {
              if (!date) return;
              const formatted = formatDateToSave(date);
              setFormData({
                ...formData,
                outageStart: formatted,
              });
            }}
            dateFormat="dd/MM/yyyy"
            placeholderText="dd/MM/yyyy"
          />
        </label>

        {/* Outage End */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.outageEnd")}
          </span>
          <DatePicker
            className="input appearance-none cursor-text bg-gray-800 text-softWhite border border-gray-600 rounded px-3 py-2 w-full"
            selected={formData.outageEnd ? new Date(formData.outageEnd) : null}
            onChange={(date: Date | null) => {
              if (!date) return;
              const formatted = formatDateToSave(date);
              setFormData({
                ...formData,
                outageEnd: formatted,
              });
            }}
            dateFormat="dd/MM/yyyy"
            placeholderText="dd/MM/yyyy"
          />
        </label>

        {/* Company VAT Number */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.companyVatNumber")}
          </span>
          <input
            name="companyVatNumber"
            value={formData.companyVatNumber || ""}
            onChange={handleChange}
            className="input"
          />
        </label>

        {/* Tesla VIN */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.teslaVehicleVIN")}
          </span>
          <input
            name="vin"
            value={formData.vin || ""}
            onChange={handleChange}
            className="input"
          />
        </label>

        {/* Upload ZIP */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.uploadZipOutage")}
          </span>
          <input
            name="zipFilePath"
            type="file"
            onChange={handleChange}
            className="input text-[12px]"
          />
        </label>
      </div>

      {/* Confirm Button */}
      <button
        className="mt-6 bg-green-700 text-softWhite px-6 py-2 rounded hover:bg-green-600"
        onClick={onSubmit}
      >
        {t("admin.outagePeriods.confirmAddNewOutage")}
      </button>
    </div>
  );
}
