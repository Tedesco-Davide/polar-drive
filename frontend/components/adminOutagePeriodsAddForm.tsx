import { TFunction } from "i18next";
import { formatDateToSave } from "@/utils/date";
import { API_BASE_URL } from "@/utils/api";
import { OutageFormData } from "@/types/outagePeriodTypes";
import { isAfter, isValid, parseISO } from "date-fns";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";

type Props = {
  formData: OutageFormData;
  setFormData: React.Dispatch<React.SetStateAction<OutageFormData>>;
  t: TFunction;
  refreshOutagePeriods: () => Promise<void>;
  onSubmitSuccess: () => void;
};

export default function AdminOutagePeriodsAddForm({
  formData,
  setFormData,
  t,
  refreshOutagePeriods,
  onSubmitSuccess,
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
        return;
      }
      const maxSize = 50 * 1024 * 1024;
      if (file.size > maxSize) {
        alert(t("admin.outagePeriods.validation.zipTooLarge"));
        return;
      }
      setFormData({ ...formData, zipFilePath: file });
      return;
    }

    if (name === "autoDetected") {
      setFormData({ ...formData, autoDetected: value === "true" });
      return;
    }

    setFormData({ ...formData, [name]: value });
  };

  const handleSubmit = async () => {
    const {
      status,
      outageType,
      outageStart,
      outageEnd,
      vin,
      companyVatNumber,
      zipFilePath,
    } = formData;

    // Validazioni base
    if (!status)
      return alert(t("admin.outagePeriods.validation.statusRequired"));
    if (!outageType)
      return alert(t("admin.outagePeriods.validation.outageTypeRequired"));
    if (!outageStart)
      return alert(t("admin.outagePeriods.validation.startDateRequired"));

    const parsedStart = parseISO(outageStart);
    if (!isValid(parsedStart) || isAfter(parsedStart, new Date()))
      return alert(t("admin.outagePeriods.validation.startDateInFuture"));

    const isGenericOutage = !vin && !companyVatNumber;

    let clientCompanyId: number | null = null;
    let teslaVehicleId: number | null = null;

    if (!isGenericOutage) {
      const resolveRes = await fetch(
        `${API_BASE_URL}/api/clientconsents/resolve-ids?vatNumber=${companyVatNumber}&vin=${vin}`
      );
      if (!resolveRes.ok) {
        alert(t("admin.clientConsents.validation.resolveVATandVIN"));
        return;
      }
      const resolved = await resolveRes.json();
      clientCompanyId = resolved.clientCompanyId;
      teslaVehicleId = resolved.teslaVehicleId;
    }

    const payload = new FormData();
    if (zipFilePath) payload.append("zipFile", zipFilePath);
    payload.append("outageType", outageType);
    payload.append("autoDetected", formData.autoDetected ? "true" : "false");
    payload.append("status", status);
    payload.append("outageStart", outageStart);
    if (outageEnd) payload.append("outageEnd", outageEnd);
    if (vin) payload.append("vin", vin);
    if (companyVatNumber) payload.append("companyVatNumber", companyVatNumber);
    if (clientCompanyId !== null)
      payload.append("clientCompanyId", clientCompanyId.toString());
    if (teslaVehicleId !== null)
      payload.append("teslaVehicleId", teslaVehicleId.toString());

    const res = await fetch(`${API_BASE_URL}/api/uploadoutagezip`, {
      method: "POST",
      body: payload,
    });

    if (!res.ok) {
      const errMsg = await res.text();
      console.error("Upload outage error", errMsg);
      alert(t("admin.outagePeriods.genericUploadError") + ": " + errMsg);
      return;
    }

    alert(t("admin.outagePeriods.successAddNewOutage"));
    await refreshOutagePeriods();
    onSubmitSuccess();
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
            <option value="Outage Vehicle">Outage Vehicle</option>
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
        onClick={handleSubmit}
      >
        {t("admin.outagePeriods.confirmAddNewOutage")}
      </button>
    </div>
  );
}
