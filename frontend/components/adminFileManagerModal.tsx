import { useState, useEffect } from "react";
import { TFunction } from "i18next";
import { Plus, Minus } from "lucide-react";
import { API_BASE_URL } from "@/utils/api";
import { logFrontendEvent } from "@/utils/logger";
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
  onClose,
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

  // Carica liste disponibili
  useEffect(() => {
    if (isOpen) {
      loadAvailableFilters();
    }
  }, [isOpen]);

  const loadAvailableFilters = async () => {
    try {
      const [companiesRes, brandsRes] = await Promise.all([
        fetch(`${API_BASE_URL}/api/filemanager/available-companies`),
        fetch(`${API_BASE_URL}/api/filemanager/available-brands`),
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

    if (!formData.periodStart || !formData.periodEnd) {
      alert("Seleziona il periodo per i PDF da includere");
      return;
    }

    if (new Date(formData.periodStart) > new Date(formData.periodEnd)) {
      alert("La data di inizio deve essere precedente alla data di fine");
      return;
    }

    setIsLoading(true);

    try {
      const response = await fetch(
        `${API_BASE_URL}/api/filemanager/filemanager-download`,
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
            requestedBy: formData.requestedBy || "Admin",
          }),
        }
      );

      if (!response.ok) {
        throw new Error(`Errore ${response.status}: ${response.statusText}`);
      }

      logFrontendEvent(
        "AdminFileManagerModal",
        "INFO",
        "PDF download request created successfully",
        `Period: ${formData.periodStart} to ${formData.periodEnd}`
      );

      alert(
        "✅ Richiesta di download PDF creata con successo! Il file ZIP sarà generato in background."
      );
      onSuccess();
      onClose();
      resetForm();
    } catch (error) {
      logFrontendEvent(
        "AdminFileManagerModal",
        "ERROR",
        "Failed to create PDF download request",
        String(error)
      );
      alert(`❌ Errore durante la creazione della richiesta: ${error}`);
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
    if (currentVin && !formData.vins.includes(currentVin.toUpperCase())) {
      setFormData((prev) => ({
        ...prev,
        vins: [...prev.vins, currentVin.toUpperCase()],
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
          {/* Periodo Start */}
          <label className="flex flex-col">
            <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
              {t("admin.filemanager.periodStart", "Data Inizio")} *
            </span>
            <DatePicker
              className="input appearance-none cursor-text bg-gray-800 text-softWhite border border-gray-600 rounded px-3 py-2 w-full"
              selected={
                formData.periodStart ? new Date(formData.periodStart) : null
              }
              onChange={(date: Date | null) => {
                if (!date) return;
                setFormData((prev) => ({
                  ...prev,
                  periodStart: date.toISOString().split("T")[0],
                }));
              }}
              dateFormat="dd/MM/yyyy"
              placeholderText="dd/MM/yyyy"
              required
            />
          </label>
          {/* Periodo End */}
          <label className="flex flex-col">
            <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
              {t("admin.filemanager.periodEnd", "Data Fine")} *
            </span>
            <DatePicker
              className="input appearance-none cursor-text bg-gray-800 text-softWhite border border-gray-600 rounded px-3 py-2 w-full"
              selected={
                formData.periodEnd ? new Date(formData.periodEnd) : null
              }
              onChange={(date: Date | null) => {
                if (!date) return;
                setFormData((prev) => ({
                  ...prev,
                  periodEnd: date.toISOString().split("T")[0],
                }));
              }}
              dateFormat="dd/MM/yyyy"
              placeholderText="dd/MM/yyyy"
              required
            />
          </label>
          {/* Richiesto da */}
          <label className="flex flex-col">
            <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
              {t("admin.filemanager.requestedBy", "Richiesto da")}
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
            />
          </label>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 items-end">
          {/* Filtro Aziende - Select */}
          <label className="flex flex-col">
            <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
              {t("admin.filemanager.companies", "Aziende")}
            </span>
            <div className="flex gap-2">
              <select
                value={currentCompany}
                onChange={(e) => setCurrentCompany(e.target.value)}
                className="input cursor-pointer w-full"
              >
                <option value="">
                  {t("admin.basicPlaceholder", "Seleziona...")}
                </option>
                {availableCompanies.map((company) => (
                  <option key={company} value={company}>
                    {company}
                  </option>
                ))}
              </select>
              <button
                onClick={addCompany}
                disabled={!currentCompany}
                className="text-softWhite hover:bg-green-600 bg-green-700 disabled:bg-gray-400 flex items-center justify-center p-2 rounded"
              >
                <Plus size={18} />
              </button>
            </div>
          </label>

          {/* VIN Input */}
          <label className="flex flex-col">
            <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
              {t("admin.filemanager.vins", "VIN")}
            </span>
            <div className="flex gap-2">
              <input
                type="text"
                value={currentVin}
                onChange={(e) => setCurrentVin(e.target.value)}
                className="input w-full"
                maxLength={17}
              />
              <button
                type="button"
                onClick={addVin}
                disabled={!currentVin}
                className="text-softWhite hover:bg-green-600 bg-green-700 disabled:bg-gray-400 flex items-center justify-center p-2 rounded"
              >
                <Plus size={18} />
              </button>
            </div>
          </label>

          <label className="flex flex-col">
            <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
              {t("admin.filemanager.brands", "Brand")}
            </span>
            <div className="flex gap-2">
              <select
                value={currentBrand}
                onChange={(e) => setCurrentBrand(e.target.value)}
                className="input cursor-pointer w-full"
              >
                <option value="">
                  {t("admin.basicPlaceholder", "Seleziona...")}
                </option>
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
        </div>

        <div className="flex items-center items-stretch">
          {/* Riepilogo */}
          <div className="bg-gray-50 dark:bg-gray-700 p-4 rounded-lg border border-gray-300 dark:border-gray-600 flex flex-col justify-around items-stretch mr-4">
            <h3 className="font-medium text-gray-900 dark:text-white mb-2">
              📋 Riepilogo Richiesta:
            </h3>
            <ul className="text-sm text-gray-700 dark:text-gray-300 space-y-1">
              <li>
                📅 Periodo: {formData.periodStart} → {formData.periodEnd}
              </li>
              <li>
                🏢 Aziende:{" "}
                {formData.companies.length > 0
                  ? formData.companies.join(", ")
                  : "Tutte"}
              </li>
              <li>
                🚗 VIN:{" "}
                {formData.vins.length > 0 ? formData.vins.join(", ") : "Tutti"}
              </li>
              <li>
                🏷️ Brand:{" "}
                {formData.brands.length > 0
                  ? formData.brands.join(", ")
                  : "Tutti"}
              </li>
            </ul>
          </div>
          {/* Sezione Tags selezionate */}
          <div className="space-y-4">
            {/* Companies Tags */}
            {formData.companies.length > 0 && (
              <div>
                <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  Aziende Selezionate:
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

            {/* VINs Tags */}
            {formData.vins.length > 0 && (
              <div>
                <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  VIN Selezionati:
                </h4>
                <div className="flex flex-wrap gap-2">
                  {formData.vins.map((vin) => (
                    <span
                      key={vin}
                      className="inline-flex items-center gap-1 px-3 py-1 bg-orange-100 dark:bg-orange-900 text-orange-700 dark:text-orange-300 rounded-full text-sm font-mono"
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

            {/* Brands Tags */}
            {formData.brands.length > 0 && (
              <div>
                <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  Brand Selezionati:
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
          </div>
        </div>

        {/* Bottoni */}
        <div className="flex gap-3">
          <button
            type="submit"
            disabled={isLoading}
            className="bg-green-700 text-softWhite px-6 py-2 rounded hover:bg-green-600 disabled:bg-gray-400 flex items-center gap-2"
          >
            {isLoading ? (
              <>🔄 {t("common.processing", "Elaborazione...")}</>
            ) : (
              <>{t("admin.filemanager.createRequest", "Crea Richiesta")}</>
            )}
          </button>
        </div>
      </form>
    </div>
  );
}
