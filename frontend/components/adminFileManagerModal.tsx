import { useState, useEffect } from "react";
import { TFunction } from "i18next";
import { Plus, Minus } from "lucide-react";
import { logFrontendEvent } from "@/utils/logger";
import { isAfter, isValid, parseISO } from "date-fns";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";

interface AdminFileManagerModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  t: TFunction;
}

interface AdminFileManagerModalRequest {
  periodStart: string;
  periodEnd: string;
  companies: string[];
  vins: string[];
  brands: string[];
  requestedBy: string;
}

export default function AdminFileManagerModal({
  isOpen,
  onSuccess,
  t,
}: AdminFileManagerModalProps) {
  const [formData, setFormData] = useState<AdminFileManagerModalRequest>({
    periodStart: "",
    periodEnd: "",
    companies: [],
    vins: [],
    brands: [],
    requestedBy: "",
  });

  const [availableCompanies, setAvailableCompanies] = useState<string[]>([]);
  const [availableBrands, setAvailableBrands] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [currentCompany, setCurrentCompany] = useState("");
  const [currentVin, setCurrentVin] = useState("");
  const [currentBrand, setCurrentBrand] = useState("");

  const formatDateForInput = (date: Date): string => {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return year + "-" + month + "-" + day;
  };

  // Carica liste disponibili
  useEffect(() => {
    if (isOpen) {
      loadAvailableFilters();
    }
  }, [isOpen]);

  const loadAvailableFilters = async () => {
    try {
      const [companiesRes, brandsRes] = await Promise.all([
        fetch(`/api/filemanager/available-companies`),
        fetch(`/api/filemanager/available-brands`),
      ]);

      const companies = await companiesRes.json();
      const brands = await brandsRes.json();

      setAvailableCompanies(companies);
      setAvailableBrands(brands);
    } catch (error) {
      logFrontendEvent(
        "AdminFileManagerModal",
        "ERROR",
        "Failed to load available filters",
        String(error)
      );
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    // ✅ VALIDAZIONI OBBLIGATORIE
    if (!formData.periodStart) {
      alert(t("admin.filemanager.modal.startDateRequired"));
      return;
    }

    if (!formData.periodEnd) {
      alert(t("admin.filemanager.modal.endDateRequired"));
      return;
    }

    if (!formData.requestedBy || formData.requestedBy.trim() === "") {
      alert(t("admin.filemanager.modal.requestedByRequired"));
      return;
    }

    // ✅ VALIDAZIONI DATE
    const parsedStart = parseISO(formData.periodStart);
    const parsedEnd = parseISO(formData.periodEnd);
    const now = new Date();

    // Controllo validità date
    if (!isValid(parsedStart)) {
      alert(t("admin.filemanager.modal.invalidStartDate"));
      return;
    }

    if (!isValid(parsedEnd)) {
      alert(t("admin.filemanager.modal.invalidEndDate"));
      return;
    }

    // ✅ Data inizio non può essere nel futuro
    if (isAfter(parsedStart, now)) {
      alert(t("admin.filemanager.modal.startDateInFuture"));
      return;
    }

    // ✅ Data fine non può essere nel futuro
    if (isAfter(parsedEnd, now)) {
      alert(t("admin.filemanager.modal.endDateInFuture"));
      return;
    }

    // ✅ Data fine deve essere successiva alla data inizio
    if (parsedEnd < parsedStart) {
      alert(t("admin.filemanager.modal.endBeforeStart"));
      return;
    }

    // ✅ Controllo periodo massimo (es. 1 anno per evitare ZIP troppo grandi)
    const oneYearInMs = 365 * 24 * 60 * 60 * 1000;
    if (parsedEnd.getTime() - parsedStart.getTime() > oneYearInMs) {
      alert(t("admin.filemanager.modal.periodTooLong"));
      return;
    }

    setIsLoading(true);

    try {
      await logFrontendEvent(
        "AdminFileManagerModal",
        "INFO",
        "Attempting to create PDF download request",
        "Period: " + formData.periodStart + " to " + formData.periodEnd + ", RequestedBy: " + formData.requestedBy
      );

      const response = await fetch(
        `/api/filemanager/filemanager-download`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            periodStart: formData.periodStart,
            periodEnd: formData.periodEnd,
            companies:
              formData.companies.length > 0 ? formData.companies : null,
            vins: formData.vins.length > 0 ? formData.vins : null,
            brands: formData.brands.length > 0 ? formData.brands : null,
            requestedBy: formData.requestedBy.trim(),
            note: null,
          }),
        }
      );

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error("Errore " + response.status + ": " + errorText);
      }

      logFrontendEvent(
        "AdminFileManagerModal",
        "INFO",
        "PDF download request created successfully",
        "Period: " + formData.periodStart + " to " + formData.periodEnd
      );

      alert(t("admin.filemanager.modal.requestCreated"));

      onSuccess();
      resetForm();
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);

      logFrontendEvent(
        "AdminFileManagerModal",
        "ERROR",
        "Failed to create PDF download request",
        errorMessage
      );

      alert(`${t("admin.filemanager.error.requestFailed ")} ${errorMessage}`);
    } finally {
      setIsLoading(false);
    }
  };

  const resetForm = () => {
    setFormData({
      periodStart: "",
      periodEnd: "",
      companies: [],
      vins: [],
      brands: [],
      requestedBy: "",
    });
    setCurrentCompany("");
    setCurrentVin("");
    setCurrentBrand("");
  };

  const addCompany = () => {
    if (currentCompany && !formData.companies.includes(currentCompany)) {
      setFormData((prev) => ({
        ...prev,
        companies: [...prev.companies, currentCompany],
      }));
      setCurrentCompany("");
    }
  };

  const removeCompany = (company: string) => {
    setFormData((prev) => ({
      ...prev,
      companies: prev.companies.filter((c) => c !== company),
    }));
  };

  const addVin = () => {
    const vinToAdd = currentVin.trim().toUpperCase();

    // ✅ Validazione VIN (17 caratteri alfanumerici)
    if (vinToAdd.length !== 17) {
      alert(t("admin.filemanager.modal.invalidVehicleVIN"));
      return;
    }

    if (!/^[A-HJ-NPR-Z0-9]{17}$/i.test(vinToAdd)) {
      alert(t("admin.filemanager.modal.invalidVehicleVINchar"));
      return;
    }

    if (!formData.vins.includes(vinToAdd)) {
      setFormData((prev) => ({
        ...prev,
        vins: [...prev.vins, vinToAdd],
      }));
      setCurrentVin("");
    }
  };

  const removeVin = (vin: string) => {
    setFormData((prev) => ({
      ...prev,
      vins: prev.vins.filter((v) => v !== vin),
    }));
  };

  const addBrand = () => {
    if (currentBrand && !formData.brands.includes(currentBrand)) {
      setFormData((prev) => ({
        ...prev,
        brands: [...prev.brands, currentBrand],
      }));
      setCurrentBrand("");
    }
  };

  const removeBrand = (brand: string) => {
    setFormData((prev) => ({
      ...prev,
      brands: prev.brands.filter((b) => b !== brand),
    }));
  };

  if (!isOpen) return null;

  return (
    <div className="bg-softWhite dark:bg-gray-800 p-6 rounded-lg shadow-lg mb-12 border border-gray-300 dark:border-gray-600">
      <form onSubmit={handleSubmit} className="space-y-6">
        {/* Grid principale per i campi del form */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {/* Periodo Start - OBBLIGATORIO */}
          <label className="flex flex-col">
            <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
              {t("admin.filemanager.modal.periodStart")}
            </span>
            <DatePicker
              className="input appearance-none cursor-text bg-gray-800 text-softWhite border border-gray-600 rounded px-3 py-2 w-full"
              selected={
                formData.periodStart
                  ? new Date(formData.periodStart + "T12:00:00")
                  : null
              }
              onChange={(date: Date | null) => {
                if (!date) return;
                setFormData((prev) => ({
                  ...prev,
                  periodStart: formatDateForInput(date),
                }));
              }}
              dateFormat="dd/MM/yyyy"
              placeholderText="dd/MM/yyyy"
              maxDate={new Date()}
              required
            />
          </label>

          {/* Periodo End - OBBLIGATORIO */}
          <label className="flex flex-col">
            <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
              {t("admin.filemanager.modal.periodEnd")}
            </span>
            <DatePicker
              className="input appearance-none cursor-text bg-gray-800 text-softWhite border border-gray-600 rounded px-3 py-2 w-full"
              selected={
                formData.periodEnd
                  ? new Date(formData.periodEnd + "T12:00:00")
                  : null
              }
              onChange={(date: Date | null) => {
                if (!date) return;
                setFormData((prev) => ({
                  ...prev,
                  periodEnd: formatDateForInput(date),
                }));
              }}
              dateFormat="dd/MM/yyyy"
              placeholderText="dd/MM/yyyy"
              minDate={
                formData.periodStart
                  ? new Date(formData.periodStart + "T12:00:00")
                  : undefined
              }
              maxDate={new Date()}
              required
            />
          </label>

          {/* Richiesto da - OBBLIGATORIO */}
          <label className="flex flex-col">
            <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
              {t("admin.filemanager.modal.requestedBy")}
            </span>
            <input
              type="text"
              value={formData.requestedBy}
              onChange={(e) =>
                setFormData((prev) => ({
                  ...prev,
                  requestedBy: e.target.value,
                }))
              }
              className="input"
              maxLength={100}
              required
            />
          </label>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 items-end">
          {/* Filtro Aziende - Select */}
          <label className="flex flex-col">
            <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
              {t("admin.filemanager.modal.companies")}
            </span>
            <div className="flex gap-2">
              <select
                value={currentCompany}
                onChange={(e) => setCurrentCompany(e.target.value)}
                className="input cursor-pointer w-full"
              >
                <option value="">{t("admin.basicPlaceholder")}</option>
                {availableCompanies.map((company) => (
                  <option key={company} value={company}>
                    {company}
                  </option>
                ))}
              </select>
              <button
                type="button"
                onClick={addCompany}
                disabled={!currentCompany}
                className="text-softWhite hover:bg-green-600 bg-green-700 disabled:bg-gray-400 flex items-center justify-center p-2 rounded"
              >
                <Plus size={18} />
              </button>
            </div>
          </label>

          {/* Brand Select */}
          <label className="flex flex-col">
            <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
              {t("admin.filemanager.modal.brand")}
            </span>
            <div className="flex gap-2">
              <select
                value={currentBrand}
                onChange={(e) => setCurrentBrand(e.target.value)}
                className="input cursor-pointer w-full"
              >
                <option value="">{t("admin.basicPlaceholder")}</option>
                {availableBrands.map((brand) => (
                  <option key={brand} value={brand}>
                    {brand}
                  </option>
                ))}
              </select>
              <button
                type="button"
                onClick={addBrand}
                disabled={!currentBrand}
                className="text-softWhite hover:bg-green-600 bg-green-700 disabled:bg-gray-400 flex items-center justify-center p-2 rounded"
              >
                <Plus size={18} />
              </button>
            </div>
          </label>

          {/* VIN Input */}
          <label className="flex flex-col">
            <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
              {t("admin.filemanager.modal.vin")}
            </span>
            <div className="flex gap-2">
              <input
                type="text"
                value={currentVin}
                onChange={(e) => setCurrentVin(e.target.value.toUpperCase())}
                className="input w-full"
                maxLength={17}
                pattern="[A-HJ-NPR-Z0-9]*"
              />
              <button
                type="button"
                onClick={addVin}
                disabled={!currentVin || currentVin.length !== 17}
                className="text-softWhite hover:bg-green-600 bg-green-700 disabled:bg-gray-400 flex items-center justify-center p-2 rounded"
              >
                <Plus size={18} />
              </button>
            </div>
          </label>
        </div>

        <div className="flex items-stretch">
          {/* Riepilogo */}
          <div className="bg-gray-50 dark:bg-gray-700 p-4 rounded-lg border border-gray-300 dark:border-gray-600 flex flex-col justify-around items-stretch mr-4">
            <h3 className="font-medium text-gray-900 dark:text-white mb-2">
              📋 {t("admin.filemanager.modal.recap.title")}
            </h3>
            <ul className="text-sm text-gray-700 dark:text-gray-300 space-y-1">
              <li>
                📅 {t("admin.filemanager.modal.recap.period")}:{" "}
                {formData.periodStart || "---"} → {formData.periodEnd || "---"}
              </li>
              <li>
                👤 {t("admin.filemanager.modal.recap.requestedBy")}:{" "}
                {formData.requestedBy || "---"}
              </li>
              <li>
                🏢 {t("admin.filemanager.modal.recap.companies")}:{" "}
                {formData.companies.length > 0
                  ? formData.companies.join(", ")
                  : t("admin.filemanager.modal.recap.allCompanies")}
              </li>
              <li>
                🚗 {t("admin.filemanager.modal.recap.vin")}:{" "}
                {formData.vins.length > 0
                  ? formData.vins.join(", ")
                  : t("admin.filemanager.modal.recap.allVin")}
              </li>
              <li>
                🏷️ {t("admin.filemanager.modal.recap.brand")}:{" "}
                {formData.brands.length > 0
                  ? formData.brands.join(", ")
                  : t("admin.filemanager.modal.recap.allBrand")}
              </li>
            </ul>
          </div>

          {/* Sezione Tags selezionate */}
          <div className="space-y-4">
            {/* Companies Tags */}
            {formData.companies.length > 0 && (
              <div>
                <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  {t("admin.filemanager.modal.recap.selectedCompanies")}{" "}
                </h4>
                <div className="flex flex-wrap gap-2">
                  {formData.companies.map((company) => (
                    <span
                      key={company}
                      className="inline-flex items-center gap-1 px-3 py-1 bg-blue-100 dark:bg-blue-900 text-blue-700 dark:text-blue-300 rounded-full text-sm"
                    >
                      {company}
                      <button
                        type="button"
                        onClick={() => removeCompany(company)}
                      >
                        <Minus size={14} />
                      </button>
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* Brands Tags */}
            {formData.brands.length > 0 && (
              <div>
                <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  {t("admin.filemanager.modal.recap.selectedBrand")}{" "}
                </h4>
                <div className="flex flex-wrap gap-2">
                  {formData.brands.map((brand) => (
                    <span
                      key={brand}
                      className="inline-flex items-center gap-1 px-3 py-1 bg-purple-100 dark:bg-purple-900 text-purple-700 dark:text-purple-300 rounded-full text-sm"
                    >
                      {brand}
                      <button type="button" onClick={() => removeBrand(brand)}>
                        <Minus size={14} />
                      </button>
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* VINs Tags */}
            {formData.vins.length > 0 && (
              <div>
                <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  {t("admin.filemanager.modal.recap.selectedVin")}{" "}
                </h4>
                <div className="flex flex-wrap gap-2">
                  {formData.vins.map((vin) => (
                    <span
                      key={vin}
                      className="inline-flex items-center gap-1 px-3 py-1 bg-orange-100 dark:bg-orange-900 text-orange-700 dark:text-orange-300 rounded-full text-sm"
                    >
                      {vin}
                      <button type="button" onClick={() => removeVin(vin)}>
                        <Minus size={14} />
                      </button>
                    </span>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Bottoni */}
        <div className="flex gap-3">
          <button
            type="submit"
            disabled={
              isLoading ||
              !formData.periodStart ||
              !formData.periodEnd ||
              !formData.requestedBy.trim()
            }
            className="bg-green-700 text-softWhite px-6 py-2 rounded hover:bg-green-600 disabled:bg-gray-400 flex items-center gap-2"
          >
            {isLoading ? (
              <>{t("admin.filemanager.modal.recap.elaborating")}</>
            ) : (
              <>{t("admin.filemanager.modal.recap.createRequest")}</>
            )}
          </button>
        </div>
      </form>
    </div>
  );
}
