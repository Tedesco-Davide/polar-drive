import React, { useState, useEffect } from "react";
import { TFunction } from "i18next";
import { parseISO, isValid, isAfter } from "date-fns";
import { API_BASE_URL } from "@/utils/api";
import { logFrontendEvent } from "@/utils/logger";
import { formatOutageDateTimeToSave } from "@/utils/date";
import { OutageFormData } from "@/types/outagePeriodTypes";
import "react-datepicker/dist/react-datepicker.css";
import DatePicker from "react-datepicker";
import JSZip from "jszip";

interface Props {
  t: TFunction;
  onSubmitSuccess: () => void;
  refreshOutagePeriods: () => Promise<void>;
}

// ✅ Costanti (dovrebbero corrispondere al backend)
const OUTAGE_TYPES = ["Outage Vehicle", "Outage Fleet Api"];
const VALID_BRANDS = ["Tesla"];

export default function AdminOutagePeriodsAddForm({
  t,
  onSubmitSuccess,
  refreshOutagePeriods,
}: Props) {
  const [formData, setFormData] = useState<OutageFormData>({
    outageType: "",
    outageBrand: "",
    outageStart: "",
    outageEnd: undefined,
    companyVatNumber: "",
    vin: "",
    notes: "",
    zipFile: null,
  });

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [resolvedIds, setResolvedIds] = useState<{
    clientCompanyId: number | null;
    vehicleId: number | null;
  }>({ clientCompanyId: null, vehicleId: null });

  // ✅ Filtra veicoli quando cambia company o brand
  useEffect(() => {
    if (formData.outageType === "Outage Fleet Api") {
      // Reset campi veicolo per Outage Fleet Api
      setFormData((prev) => ({
        ...prev,
        companyVatNumber: "",
        vin: "",
      }));
      setResolvedIds({ clientCompanyId: null, vehicleId: null });
    }
    // Per "Outage Vehicle" non facciamo nulla, i campi rimangono editabili
  }, [formData.outageType]);

  // ✅ Risolvi IDs quando cambiano VAT e VIN
  useEffect(() => {
    if (
      formData.outageType === "Outage Vehicle" &&
      formData.companyVatNumber &&
      formData.vin
    ) {
      resolveCompanyAndVehicleIds();
    } else {
      setResolvedIds({ clientCompanyId: null, vehicleId: null });
    }
  }, [formData.companyVatNumber, formData.vin, formData.outageType]);

  const resolveCompanyAndVehicleIds = async () => {
    try {
      const response = await fetch(
        `${API_BASE_URL}/api/clientconsents/resolve-ids?vatNumber=${formData.companyVatNumber}&vin=${formData.vin}`
      );

      if (response.ok) {
        const resolved = await response.json();
        setResolvedIds({
          clientCompanyId: resolved.clientCompanyId,
          vehicleId: resolved.vehicleId,
        });

        // ✅ Verifica brand coerente
        const vehicleBrand = (resolved.vehicleBrand || "").trim();
        const selectedBrand = formData.outageBrand.trim();
        if (vehicleBrand && selectedBrand && vehicleBrand !== selectedBrand) {
          alert(
            `${t("admin.brandMismatch")}\n\n${t(
              "admin.expectedResult"
            )}: ${vehicleBrand}\n${t("admin.insertedValue")}: ${selectedBrand}`
          );
        }
      } else {
        setResolvedIds({ clientCompanyId: null, vehicleId: null });
      }
    } catch (error) {
      console.error("Error resolveCompanyAndVehicleIds", error);
      setResolvedIds({ clientCompanyId: null, vehicleId: null });
    }
  };

  const handleChange = (
    e: React.ChangeEvent<
      HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement
    >
  ) => {
    const { name, value, files } = e.target as HTMLInputElement;

    if (name === "zipFile" && files?.[0]) {
      const file = files[0];

      if (!file.name.endsWith(".zip")) {
        alert(t("admin.validation.invalidZipType"));
        return;
      }

      const maxSize = 50 * 1024 * 1024; // 50MB
      if (file.size > maxSize) {
        alert(t("admin.outagePeriods.validation.zipTooLarge"));
        return;
      }

      setFormData((prev) => ({ ...prev, zipFile: file }));
      return;
    }

    setFormData((prev) => ({ ...prev, [name]: value }));
  };

  const validateForm = (): boolean => {
    // ✅ Validazioni base
    if (!formData.outageType) {
      alert(t("admin.outagePeriods.validation.outageTypeRequired"));
      return false;
    }

    if (!formData.outageBrand) {
      alert(t("admin.outagePeriods.validation.outageBrandRequired"));
      return false;
    }

    if (!formData.outageStart) {
      alert(t("admin.outagePeriods.validation.startDateRequired"));
      return false;
    }

    const parsedStart = parseISO(formData.outageStart);
    if (!isValid(parsedStart) || isAfter(parsedStart, new Date())) {
      alert(t("admin.outagePeriods.validation.startDateInFuture"));
      return false;
    }

    // ✅ Validazione data fine
    if (formData.outageEnd) {
      const parsedEnd = parseISO(formData.outageEnd);
      const now = new Date();

      if (!isValid(parsedEnd)) {
        alert(t("admin.outagePeriods.validation.invalidEndDate"));
        return false;
      }

      if (parsedEnd < parsedStart) {
        alert(t("admin.outagePeriods.validation.outageEndBeforeStart"));
        return false;
      }

      if (parsedEnd > now) {
        alert(t("admin.outagePeriods.validation.outageEndInFuture"));
        return false;
      }
    }

    // ✅ Validazioni specifiche per tipo outage
    if (formData.outageType === "Outage Vehicle") {
      if (!formData.vin || !formData.companyVatNumber) {
        alert(t("admin.resolveVATandVIN"));
        return false;
      }

      if (!resolvedIds.clientCompanyId || !resolvedIds.vehicleId) {
        alert(t("admin.resolveVATandVIN"));
        return false;
      }
    }

    if (formData.outageType === "Outage Fleet Api") {
      if (formData.vin || formData.companyVatNumber) {
        alert(t("admin.outagePeriods.fleetApiMustNotHaveVinOrVat"));
        return false;
      }
    }

    return true;
  };

  const validateZipFile = async (file: File): Promise<boolean> => {
    try {
      const zipBuffer = await file.arrayBuffer();
      const zip = new JSZip();
      const contents = await zip.loadAsync(zipBuffer);

      const pdfFound = Object.keys(contents.files).some(
        (filename) =>
          filename.toLowerCase().endsWith(".pdf") &&
          !contents.files[filename].dir
      );

      if (!pdfFound) {
        alert(t("admin.outagePeriods.invalidZipTypeRequiredOutages"));
        return false;
      }

      return true;
    } catch (error) {
      console.error("Errore validation .zip file", error);
      alert(t("admin.validation.invalidZipFile"));
      return false;
    }
  };

  const handleSubmit = async () => {
    if (isSubmitting) return;

    try {
      setIsSubmitting(true);

      await logFrontendEvent(
        "AdminOutagePeriodsAddForm",
        "INFO",
        "Attempting to submit new outage period"
      );

      if (!validateForm()) {
        return;
      }

      // ✅ Validazione ZIP se presente
      if (formData.zipFile) {
        const isValidZip = await validateZipFile(formData.zipFile);
        if (!isValidZip) {
          return;
        }
      }

      // ✅ Prepara payload per la nuova API
      const payload = {
        outageType: formData.outageType,
        outageBrand: formData.outageBrand,
        outageStart: formData.outageStart,
        outageEnd: formData.outageEnd || null,
        vehicleId:
          formData.outageType === "Outage Vehicle"
            ? resolvedIds.vehicleId
            : null,
        clientCompanyId:
          formData.outageType === "Outage Vehicle"
            ? resolvedIds.clientCompanyId
            : null,
        notes: formData.notes || "Inserito manualmente",
      };

      // ✅ Crea l'outage prima
      const outageResponse = await fetch(`${API_BASE_URL}/api/outageperiods`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(payload),
      });

      if (!outageResponse.ok) {
        const errorText = await outageResponse.text();
        throw new Error(errorText);
      }

      const newOutage = await outageResponse.json();
      const outageId = newOutage.id;

      // ✅ Upload ZIP se presente
      if (formData.zipFile) {
        const zipFormData = new FormData();
        zipFormData.append("zipFile", formData.zipFile);

        const zipResponse = await fetch(
          `${API_BASE_URL}/api/outageperiods/${outageId}/upload-zip`,
          {
            method: "POST",
            body: zipFormData,
          }
        );

        if (!zipResponse.ok) {
          console.warn("ZIP upload failed, but outage was created");
        }
      }

      await logFrontendEvent(
        "AdminOutagePeriodsAddForm",
        "INFO",
        "Outage period successfully created",
        `Outage ID: ${outageId}`
      );

      alert(t("admin.outagePeriods.successAddNewOutage"));
      await refreshOutagePeriods();
      onSubmitSuccess();

      // ✅ Reset form
      setFormData({
        outageType: "",
        outageBrand: "",
        outageStart: "",
        outageEnd: undefined,
        companyVatNumber: "",
        vin: "",
        notes: "",
        zipFile: null,
      });
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Unknown error";

      await logFrontendEvent(
        "AdminOutagePeriodsAddForm",
        "ERROR",
        "Failed to create outage",
        errorMessage
      );

      alert(`${t("admin.outagePeriods.genericUploadError")}: ${errorMessage}`);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="bg-softWhite dark:bg-gray-800 p-6 rounded-lg shadow-lg mb-12 border border-gray-300 dark:border-gray-600">
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
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
            required
          >
            <option value="">{t("admin.basicPlaceholder")}</option>
            {OUTAGE_TYPES.map((type) => (
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
            {VALID_BRANDS.map((brand) => (
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
            timeFormat="HH:mm"
            timeCaption="Orario"
            className="input appearance-none cursor-text bg-gray-800 text-softWhite border border-gray-600 rounded px-3 py-2 w-full"
            selected={
              formData.outageStart ? new Date(formData.outageStart) : null
            }
            onChange={(date: Date | null) => {
              if (!date) return;
              const formatted = formatOutageDateTimeToSave(date);
              setFormData((prev) => ({ ...prev, outageStart: formatted }));
            }}
            dateFormat="dd/MM/yyyy HH:mm"
            placeholderText="dd/MM/yyyy HH:mm"
            maxDate={new Date()}
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
            timeFormat="HH:mm"
            timeCaption="Orario"
            className="input appearance-none cursor-text bg-gray-800 text-softWhite border border-gray-600 rounded px-3 py-2 w-full"
            selected={formData.outageEnd ? new Date(formData.outageEnd) : null}
            onChange={(date: Date | null) => {
              if (!date) {
                setFormData((prev) => ({ ...prev, outageEnd: undefined }));
                return;
              }
              const formatted = formatOutageDateTimeToSave(date);
              setFormData((prev) => ({ ...prev, outageEnd: formatted }));
            }}
            dateFormat="dd/MM/yyyy HH:mm"
            placeholderText="dd/MM/yyyy HH:mm"
            maxDate={new Date()}
            minDate={
              formData.outageStart ? new Date(formData.outageStart) : undefined
            }
          />
        </label>

        {/* Company VAT Number - Solo per Outage Vehicle */}
        <label
          className={`flex flex-col ${
            formData.outageType === "Outage Fleet Api" ? "opacity-50" : ""
          }`}
        >
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.companyVatNumber")}
            {formData.outageType === "Outage Vehicle"}
          </span>
          <input
            name="companyVatNumber"
            value={formData.companyVatNumber}
            onChange={handleChange}
            className="input"
            disabled={formData.outageType === "Outage Fleet Api"}
            placeholder={formData.outageType === "Outage Fleet Api" ? "—" : ""}
          />
          {resolvedIds.clientCompanyId && (
            <span className="text-xs text-green-600 mt-1">
              ✅ Company trovata (ID: {resolvedIds.clientCompanyId})
            </span>
          )}
        </label>

        {/* Vehicle VIN - Solo per Outage Vehicle */}
        <label
          className={`flex flex-col ${
            formData.outageType === "Outage Fleet Api" ? "opacity-50" : ""
          }`}
        >
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.vehicleVIN")}
            {formData.outageType === "Outage Vehicle"}
          </span>
          <input
            name="vin"
            value={formData.vin}
            onChange={handleChange}
            className="input"
            disabled={formData.outageType === "Outage Fleet Api"}
            placeholder={formData.outageType === "Outage Fleet Api" ? "—" : ""}
          />
          {resolvedIds.vehicleId && (
            <span className="text-xs text-green-600 mt-1">
              ✅ Veicolo trovato (ID: {resolvedIds.vehicleId})
            </span>
          )}
        </label>

        {/* Upload ZIP */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.outagePeriods.uploadZipOutage")}
          </span>
          <input
            name="zipFile"
            type="file"
            accept=".zip"
            onChange={handleChange}
            className="input text-[12px]"
          />
          {formData.zipFile && (
            <span className="text-xs mt-1 text-green-600">
              ✅ {formData.zipFile.name}
            </span>
          )}
        </label>

        {/* Notes */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.notes.addNote")}
          </span>
          <textarea
            name="notes"
            value={formData.notes}
            onChange={handleChange}
            className="input h-[42px] resize-none"
          />
        </label>
      </div>

      {/* Informazioni di stato */}
      {formData.outageType === "Outage Fleet Api" && (
        <div className="mt-4 p-3 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
          <p className="text-sm text-blue-700 dark:text-blue-300">
            ℹ️ Per gli outages di tipo Fleet Api non è necessario specificare
            azienda o veicolo specifico.
          </p>
        </div>
      )}

      {formData.outageType === "Outage Vehicle" &&
        (!formData.companyVatNumber || !formData.vin) && (
          <div className="mt-4 p-3 bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 rounded-lg">
            <p className="text-sm text-yellow-700 dark:text-yellow-300">
              ⚠️ Per gli outages di tipo Vehicle è obbligatorio specificare sia
              Partita IVA che VIN del veicolo.
            </p>
          </div>
        )}

      {/* Confirm Button */}
      <button
        className={`mt-6 px-6 py-2 rounded font-medium transition-colors ${
          isSubmitting
            ? "bg-gray-400 cursor-not-allowed text-white"
            : "bg-green-700 hover:bg-green-600 text-softWhite"
        }`}
        onClick={handleSubmit}
        disabled={isSubmitting}
      >
        {isSubmitting
          ? t("admin.processing") || "Elaborazione..."
          : t("admin.outagePeriods.confirmAddNewOutage")}
      </button>
    </div>
  );
}
