import { TFunction } from "i18next";
import { formatDateToSave } from "@/utils/date";
import { isAfter, isValid, parseISO } from "date-fns";
import { logFrontendEvent } from "@/utils/logger";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";
import { useState, useEffect, useRef } from "react";

type Props = {
  t: TFunction;
  onSubmitSuccess: () => void;
  refreshClientConsents: () => Promise<void>;
};

const VALID_CONSENT_TYPES = [
  "Consent Deactivation",
  "Consent Stop Data Fetching",
  "Consent Reactivation",
];

export default function AdminAddFormClientConsent({
  t,
  onSubmitSuccess,
  refreshClientConsents,
}: Props) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [formData, setFormData] = useState<{
    consentType: string;
    companyVatNumber: string;
    vehicleVIN: string;
    uploadDate: string;
    notes: string;
    zipFile: File | null;
  }>({
    consentType: "",
    companyVatNumber: "",
    vehicleVIN: "",
    uploadDate: "",
    notes: "",
    zipFile: null,
  });

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [resolvedIds, setResolvedIds] = useState<{
    clientCompanyId: number | null;
    vehicleId: number | null;
  }>({ clientCompanyId: null, vehicleId: null });

  // ✅ Risolvi IDs quando cambiano VAT e VIN
  useEffect(() => {
    const resolveCompanyAndVehicleIds = async () => {
      try {
        const response = await fetch(
        `/api/clientconsents/resolve-ids?vatNumber=${formData.companyVatNumber}&vin=${formData.vehicleVIN}`
        );

        if (response.ok) {
          const resolved = await response.json();
          setResolvedIds({
            clientCompanyId: resolved.clientCompanyId,
            vehicleId: resolved.vehicleId,
          });
        } else {
          setResolvedIds({ clientCompanyId: null, vehicleId: null });
        }
      } catch (error) {
        console.error("Error resolveCompanyAndVehicleIds", error);
        setResolvedIds({ clientCompanyId: null, vehicleId: null });
      }
    };

    if (formData.companyVatNumber && formData.vehicleVIN) {
      resolveCompanyAndVehicleIds();
    } else {
      setResolvedIds({ clientCompanyId: null, vehicleId: null });
    }
  }, [formData.companyVatNumber, formData.vehicleVIN]);

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
        alert(t("admin.validation.zipTooLarge"));
        return;
      }

      setFormData((prev) => ({ ...prev, zipFile: file }));
      return;
    }

    setFormData((prev) => ({ ...prev, [name]: value }));
  };

  const validateForm = (): boolean => {
    // ✅ Validazioni base
    if (!formData.consentType) {
      alert(t("admin.clientConsents.validation.consentTypeRequired"));
      return false;
    }

    if (!formData.companyVatNumber) {
      alert(t("admin.clientConsents.validation.companyVatNumberRequired"));
      return false;
    }

    if (!formData.vehicleVIN) {
      alert(t("admin.clientConsents.validation.vehicleVINRequired"));
      return false;
    }

    if (!formData.uploadDate) {
      alert(t("admin.clientConsents.validation.uploadDateRequired"));
      return false;
    }

    // ✅ Validazione regex VIN / P.IVA
    if (!/^[0-9]{11}$/.test(formData.companyVatNumber)) {
      alert(t("admin.validation.invalidVat"));
      return false;
    }

    if (!/^[A-HJ-NPR-Z0-9]{17}$/.test(formData.vehicleVIN)) {
      alert(t("admin.validation.invalidVehicleVIN"));
      return false;
    }

    // ✅ Validazione data corretta e non futura
    const uploadDate = parseISO(formData.uploadDate);
    const today = new Date();
    today.setHours(23, 59, 59, 999); // Fine giornata per permettere oggi

    if (!isValid(uploadDate) || isAfter(uploadDate, today)) {
      alert(t("admin.clientConsents.validation.invalidUploadDate"));
      return false;
    }

    // ✅ Validazione IDs risolti
    if (!resolvedIds.clientCompanyId || !resolvedIds.vehicleId) {
      alert(t("admin.resolveVATandVIN"));
      return false;
    }

    // ✅ Validazione ZIP obbligatorio
    if (!formData.zipFile) {
      alert(t("admin.clientConsents.validation.zipFileRequired"));
      return false;
    }

    return true;
  };

    const handleSubmit = async () => {
    if (isSubmitting) return;

    try {
        setIsSubmitting(true);

        await logFrontendEvent(
        "AdminClientConsentAddForm",
        "INFO",
        "Attempting to submit new consent"
        );

        if (!validateForm()) return;

        // ✅ Una singola chiamata atomica con FormData
        const formDataToSend = new FormData();
        formDataToSend.append("clientCompanyId", resolvedIds.clientCompanyId!.toString());
        formDataToSend.append("vehicleId", resolvedIds.vehicleId!.toString());
        formDataToSend.append("consentType", formData.consentType);
        formDataToSend.append("uploadDate", formData.uploadDate);
        formDataToSend.append("notes", formData.notes);
        formDataToSend.append("zipFile", formData.zipFile!);

        const response = await fetch(`/api/clientconsents`, {
        method: "POST",
        body: formDataToSend, // NO Content-Type header con FormData
        });

        if (!response.ok) {
        const errorText = await response.text();
        throw new Error(errorText);
        }

        const newConsent = await response.json();

        await logFrontendEvent(
        "AdminClientConsentAddForm",
        "INFO",
        "Consent successfully created",
        "Consent ID: " + newConsent.id
        );

        alert(t("admin.clientConsents.successAddNewConsent"));
        await refreshClientConsents();
        onSubmitSuccess();

        // Reset form
        setFormData({
        consentType: "",
        companyVatNumber: "",
        vehicleVIN: "",
        uploadDate: "",
        notes: "",
        zipFile: null,
        });
        setResolvedIds({ clientCompanyId: null, vehicleId: null });

      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }

    } catch (error) {
        const errorMessage = error instanceof Error ? error.message : "Unknown error";
        await logFrontendEvent(
        "AdminClientConsentAddForm",
        "ERROR",
        "Failed to create consent",
        errorMessage
        );
        alert(`${t("admin.genericUploadError")}: ${errorMessage}`);
        setFormData((prev) => ({ ...prev, zipFile: null }));
        if (fileInputRef.current) {
            fileInputRef.current.value = "";
        }        
    } finally {
        setIsSubmitting(false);
    }
    };

  return (
    <div className="bg-softWhite dark:bg-gray-800 p-6 rounded-lg shadow-lg mb-12 border border-gray-300 dark:border-gray-600">
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {/* Consent Type */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientConsents.consentType")}
          </span>
          <select
            name="consentType"
            value={formData.consentType}
            onChange={handleChange}
            className="input cursor-pointer"
            required
          >
            <option value="">{t("admin.basicPlaceholder")}</option>
            {VALID_CONSENT_TYPES.map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        </label>

        {/* Company VAT Number */}
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
            required
          />
          {resolvedIds.clientCompanyId && (
            <span className="text-xs text-green-600 mt-1">
              ✅ {t("admin.companyFound")} (ID: {resolvedIds.clientCompanyId})
            </span>
          )}
        </label>

        {/* Vehicle VIN */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientConsents.vehicleVIN")}
          </span>
          <input
            name="vehicleVIN"
            value={formData.vehicleVIN}
            onChange={handleChange}
            className="input"
            maxLength={17}
            pattern="[A-HJ-NPR-Z0-9]*"
            inputMode="text"
            required
          />
          {resolvedIds.vehicleId && (
            <span className="text-xs text-green-600 mt-1">
              ✅ {t("admin.vehicleFound")} (ID: {resolvedIds.vehicleId})
            </span>
          )}
        </label>

        {/* Upload Date */}
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
              setFormData((prev) => ({ ...prev, uploadDate: formatted }));
            }}
            dateFormat="dd/MM/yyyy"
            placeholderText="dd/MM/yyyy"
            maxDate={new Date()}
            required
          />
        </label>

        {/* Upload ZIP */}
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.uploadZipSignedConsentGeneric")}
          </span>
          <input
            ref={fileInputRef}
            name="zipFile"
            type="file"
            accept=".zip"
            onChange={handleChange}
            className="input text-[12px]"
            required
          />
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
      {formData.companyVatNumber &&
        formData.vehicleVIN &&
        (!resolvedIds.clientCompanyId || !resolvedIds.vehicleId) && (
          <div className="mt-4 p-3 bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 rounded-lg">
            <p className="text-sm text-yellow-700 dark:text-yellow-300">
              ⚠️ {t("admin.resolveVATandVIN")}
            </p>
          </div>
        )}

      {/* Confirm Button */}
      <button
        className={`mt-6 px-6 py-2 rounded font-medium transition-colors ${
          isSubmitting
            ? "bg-gray-400 cursor-not-allowed text-white opacity-20"
            : "bg-green-700 hover:bg-green-600 text-softWhite"
        }`}
        onClick={handleSubmit}
        disabled={isSubmitting}
      >
        {isSubmitting
          ? t("admin.processing")
          : t("admin.clientConsents.confirmAddNewConsent")}
      </button>
    </div>
  );
}
