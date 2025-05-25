import { TFunction } from "i18next";
import { formatOutageDateTimeToSave } from "@/utils/date";
import { API_BASE_URL } from "@/utils/api";
import { OutageFormData, UploadOutageResult } from "@/types/outagePeriodTypes";
import { isAfter, isValid, parseISO } from "date-fns";
import { OutagePeriod } from "@/types/outagePeriodInterfaces";
import { vehicleOptions } from "@/types/vehicleOptions";
import { outageStatusOptions, outageTypeOptions } from "@/types/outageOptions";
import "react-datepicker/dist/react-datepicker.css";
import DatePicker from "react-datepicker";
import JSZip from "jszip";

const brandOptions = Object.keys(vehicleOptions);

type Props = {
  formData: OutageFormData;
  setFormData: React.Dispatch<React.SetStateAction<OutageFormData>>;
  t: TFunction;
  refreshOutagePeriods: () => Promise<OutagePeriod[]>;
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
      outageBrand,
      outageStart,
      outageEnd,
      vin,
      companyVatNumber,
      zipFilePath,
    } = formData;

    const payload = new FormData();
    const parsedStart = parseISO(outageStart);
    const isGenericOutage = !vin && !companyVatNumber;
    let clientCompanyId: number | null = null;
    let vehicleId: number | null = null;

    // Validazioni base
    if (!status)
      return alert(t("admin.outagePeriods.validation.statusRequired"));

    if (!outageType)
      return alert(t("admin.outagePeriods.validation.outageTypeRequired"));

    if (!outageBrand) {
      return alert(t("admin.outagePeriods.validation.outageBrandRequired"));
    }

    if (!outageStart)
      return alert(t("admin.outagePeriods.validation.startDateRequired"));

    // ⛔ Se stato è OUTAGE-ONGOING, non deve esserci una data di fine!
    if (status === "OUTAGE-ONGOING" && outageEnd) {
      alert(t("admin.outagePeriods.validation.ongoingCannotHaveEndDate"));
      return;
    }

    // ⛔ OUTAGE-RESOLVED richiede anche la data di fine
    if (status === "OUTAGE-RESOLVED" && !outageEnd) {
      alert(t("admin.outagePeriods.validation.outageEndRequiredForResolved"));
      return;
    }

    // ⛔ END non può essere prima della START
    if (outageEnd) {
      const parsedEnd = parseISO(outageEnd);
      const now = new Date();

      if (!isValid(parsedEnd)) {
        alert(t("admin.outagePeriods.validation.invalidEndDate"));
        return;
      }

      if (parsedEnd < parsedStart) {
        alert(t("admin.outagePeriods.validation.outageEndBeforeStart"));
        return;
      }

      if (parsedEnd > now) {
        alert(t("admin.outagePeriods.validation.outageEndInFuture"));
        return;
      }
    }

    if (!isValid(parsedStart) || isAfter(parsedStart, new Date()))
      return alert(t("admin.outagePeriods.validation.startDateInFuture"));

    if (outageType === "Outage Vehicle") {
      if (!vin || !companyVatNumber) {
        alert(t("admin.resolveVATandVIN"));
        return;
      }
    }

    if (outageType === "Outage Fleet Api" && (vin || companyVatNumber)) {
      alert(t("admin.outagePeriods.fleetApiMustNotHaveVinOrVat"));
      return;
    }

    if (!isGenericOutage) {
      const resolveRes = await fetch(
        `${API_BASE_URL}/api/clientconsents/resolve-ids?vatNumber=${companyVatNumber}&vin=${vin}`
      );
      if (!resolveRes.ok) {
        alert(t("admin.resolveVATandVIN"));
        return;
      }
      const resolved = await resolveRes.json();
      clientCompanyId = resolved.clientCompanyId;
      vehicleId = resolved.vehicleId;

      // ✅ Nuovo controllo brand coerente
      const vehicleBrand = (resolved.vehicleBrand || "").trim();
      const selectedBrand = (outageBrand || "").trim();
      if (vehicleBrand !== selectedBrand) {
        alert(
          `${t("admin.brandMismatch")}\n\n${t(
            "admin.expectedResult"
          )}: ${vehicleBrand}\n${t("admin.insertedValue")}: ${selectedBrand}`
        );
        return;
      }
    }

    if (zipFilePath) {
      // ⛔ Verifica contenuto ZIP (deve avere almeno un PDF)
      const zipBuffer = await zipFilePath.arrayBuffer();
      const zip = new JSZip();
      const contents = await zip.loadAsync(zipBuffer);
      const pdfFound = Object.keys(contents.files).some(
        (filename) =>
          filename.toLowerCase().endsWith(".pdf") &&
          !contents.files[filename].dir
      );
      if (!pdfFound) {
        alert(t("admin.outagePeriods.invalidZipTypeRequiredOutages"));
        return;
      }

      // ✅ Allego al payload solo se valido
      payload.append("zipFile", zipFilePath);
    }

    payload.append("outageType", outageType);
    payload.append("outageBrand", outageBrand);
    payload.append("autoDetected", formData.autoDetected ? "true" : "false");
    payload.append("status", status);
    payload.append("outageStart", outageStart);
    if (outageEnd) {
      payload.append(
        "outageEnd",
        formatOutageDateTimeToSave(parseISO(outageEnd))
      );
    }
    if (vin) payload.append("vin", vin);
    if (companyVatNumber) payload.append("companyVatNumber", companyVatNumber);
    if (clientCompanyId !== null)
      payload.append("clientCompanyId", clientCompanyId.toString());
    if (vehicleId !== null) payload.append("vehicleId", vehicleId.toString());

    const res = await fetch(`${API_BASE_URL}/api/uploadoutagezip`, {
      method: "POST",
      body: payload,
    });

    const responseText = await res.text();

    if (!res.ok) {
      console.error("UPLOAD OUTAGE ERROR", {
        status: res.status,
        statusText: res.statusText,
        error: responseText,
      });
      alert(`${t("admin.outagePeriods.genericUploadError")}: ${responseText}`);
      return;
    }

    let result: UploadOutageResult = {};
    try {
      result = JSON.parse(responseText) as UploadOutageResult;
    } catch (e) {
      console.warn("Warning: JSON parse failed", e);
    }

    if (result && result.isNew) {
      alert(t("admin.outagePeriods.successAddNewOutage"));
    } else {
      alert(t("admin.outagePeriods.addNewOutageExisting"));
    }

    await refreshOutagePeriods();
    onSubmitSuccess();
    setFormData((prev) => ({ ...prev, zipFilePath: null }));
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
            <option value="">{t("admin.basicPlaceholder")}</option>
            {outageStatusOptions.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
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
            <option value="">{t("admin.basicPlaceholder")}</option>
            {outageTypeOptions.map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        </label>
        {/* Outage Brand */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.outageBrand")}
          </span>
          <select
            name="outageBrand"
            value={formData.outageBrand}
            onChange={handleChange}
            className="input cursor-pointer"
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

        {/* Outage Start */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.outageStart")}
          </span>
          <DatePicker
            showTimeSelect
            timeIntervals={10}
            showTimeSelectOnly={false}
            timeFormat="HH:mm"
            timeCaption="Orario"
            className="input appearance-none cursor-text bg-gray-800 text-softWhite border border-gray-600 rounded px-3 py-2 w-full"
            selected={
              formData.outageStart ? new Date(formData.outageStart) : null
            }
            onChange={(date: Date | null) => {
              if (!date) return;
              const formatted = formatOutageDateTimeToSave(date);
              setFormData({
                ...formData,
                outageStart: formatted,
              });
            }}
            dateFormat="dd/MM/yyyy HH:mm"
            placeholderText="dd/MM/yyyy HH:mm"
          />
        </label>

        {/* Outage End */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.outageEnd")}
          </span>
          <DatePicker
            isClearable
            showTimeSelect
            timeIntervals={10}
            showTimeSelectOnly={false}
            timeFormat="HH:mm"
            timeCaption="Orario"
            className="input appearance-none cursor-text bg-gray-800 text-softWhite border border-gray-600 rounded px-3 py-2 w-full"
            selected={formData.outageEnd ? new Date(formData.outageEnd) : null}
            onChange={(date: Date | null) => {
              if (!date) {
                setFormData({ ...formData, outageEnd: undefined });
                return;
              }
              const formatted = formatOutageDateTimeToSave(date);
              setFormData({
                ...formData,
                outageEnd: formatted,
              });
            }}
            dateFormat="dd/MM/yyyy HH:mm"
            placeholderText="dd/MM/yyyy HH:mm"
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

        {/* Vehicled VIN */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.vehicleVIN")}
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
          {formData.zipFilePath && (
            <p className="text-xs mt-1 text-gray-500 dark:text-gray-400">
              {formData.zipFilePath.name}
            </p>
          )}
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
