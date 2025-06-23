import { useState, useEffect } from "react";
import { TFunction } from "i18next";
import {
  X,
  Calendar,
  Building,
  Car,
  FileArchive,
  Plus,
  Minus,
} from "lucide-react";
import { API_BASE_URL } from "@/utils/api";
import { logFrontendEvent } from "@/utils/logger";

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
      // Imposta date predefinite (ultimo mese)
      const endDate = new Date();
      const startDate = new Date();
      startDate.setMonth(startDate.getMonth() - 1);

      setFormData((prev) => ({
        ...prev,
        periodStart: startDate.toISOString().split("T")[0],
        periodEnd: endDate.toISOString().split("T")[0],
      }));
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
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white dark:bg-gray-800 rounded-lg p-6 w-full max-w-2xl max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between mb-6">
          <h2 className="text-xl font-bold text-gray-900 dark:text-white flex items-center gap-2">
            <FileArchive className="text-blue-500" />
            {t("admin.filemanager.createDownload", "Crea Nuovo Download PDF")}
          </h2>
          <button
            onClick={onClose}
            className="text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
          >
            <X size={24} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Periodo */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                <Calendar size={16} className="inline mr-1" />
                {t("admin.filemanager.periodStart", "Data Inizio")} *
              </label>
              <input
                type="date"
                value={formData.periodStart}
                onChange={(e) =>
                  setFormData((prev) => ({
                    ...prev,
                    periodStart: e.target.value,
                  }))
                }
                className="w-full p-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:border-gray-600 dark:text-white"
                required
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                <Calendar size={16} className="inline mr-1" />
                {t("admin.filemanager.periodEnd", "Data Fine")} *
              </label>
              <input
                type="date"
                value={formData.periodEnd}
                onChange={(e) =>
                  setFormData((prev) => ({
                    ...prev,
                    periodEnd: e.target.value,
                  }))
                }
                className="w-full p-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:border-gray-600 dark:text-white"
                required
              />
            </div>
          </div>

          {/* Filtro Aziende */}
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
              <Building size={16} className="inline mr-1" />
              {t("admin.filemanager.companies", "Aziende")}
              <span className="text-gray-500 text-xs ml-1">
                (opzionale - se vuoto, include tutte)
              </span>
            </label>
            <div className="flex gap-2 mb-2">
              <select
                value={currentCompany}
                onChange={(e) => setCurrentCompany(e.target.value)}
                className="flex-1 p-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:border-gray-600 dark:text-white"
              >
                <option value="">Seleziona un azienda...</option>
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
                className="px-3 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 disabled:bg-gray-400"
              >
                <Plus size={16} />
              </button>
            </div>
            <div className="flex flex-wrap gap-2">
              {formData.companies.map((company) => (
                <span
                  key={company}
                  className="inline-flex items-center gap-1 px-2 py-1 bg-blue-100 text-blue-700 rounded text-sm"
                >
                  {company}
                  <button type="button" onClick={() => removeCompany(company)}>
                    <Minus size={14} />
                  </button>
                </span>
              ))}
            </div>
          </div>

          {/* Filtro VIN */}
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
              <Car size={16} className="inline mr-1" />
              {t("admin.filemanager.vins", "VIN")}
              <span className="text-gray-500 text-xs ml-1">
                (opzionale - se vuoto, include tutti)
              </span>
            </label>
            <div className="flex gap-2 mb-2">
              <input
                type="text"
                value={currentVin}
                onChange={(e) => setCurrentVin(e.target.value)}
                placeholder="Inserisci VIN..."
                className="flex-1 p-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:border-gray-600 dark:text-white"
                maxLength={17}
              />
              <button
                type="button"
                onClick={addVin}
                disabled={!currentVin}
                className="px-3 py-2 bg-orange-500 text-white rounded-lg hover:bg-orange-600 disabled:bg-gray-400"
              >
                <Plus size={16} />
              </button>
            </div>
            <div className="flex flex-wrap gap-2">
              {formData.vins.map((vin) => (
                <span
                  key={vin}
                  className="inline-flex items-center gap-1 px-2 py-1 bg-orange-100 text-orange-700 rounded text-sm font-mono"
                >
                  {vin}
                  <button type="button" onClick={() => removeVin(vin)}>
                    <Minus size={14} />
                  </button>
                </span>
              ))}
            </div>
          </div>

          {/* Filtro Brand */}
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
              🏷️ {t("admin.filemanager.brands", "Brand")}
              <span className="text-gray-500 text-xs ml-1">
                (opzionale - se vuoto, include tutti)
              </span>
            </label>
            <div className="flex gap-2 mb-2">
              <select
                value={currentBrand}
                onChange={(e) => setCurrentBrand(e.target.value)}
                className="flex-1 p-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:border-gray-600 dark:text-white"
              >
                <option value="">Seleziona un brand...</option>
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
                className="px-3 py-2 bg-purple-500 text-white rounded-lg hover:bg-purple-600 disabled:bg-gray-400"
              >
                <Plus size={16} />
              </button>
            </div>
            <div className="flex flex-wrap gap-2">
              {formData.brands.map((brand) => (
                <span
                  key={brand}
                  className="inline-flex items-center gap-1 px-2 py-1 bg-purple-100 text-purple-700 rounded text-sm"
                >
                  {brand}
                  <button type="button" onClick={() => removeBrand(brand)}>
                    <Minus size={14} />
                  </button>
                </span>
              ))}
            </div>
          </div>

          {/* Richiesto da */}
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
              👤 {t("admin.filemanager.requestedBy", "Richiesto da")}
            </label>
            <input
              type="text"
              value={formData.requestedBy}
              onChange={(e) =>
                setFormData((prev) => ({
                  ...prev,
                  requestedBy: e.target.value,
                }))
              }
              placeholder="Es. Mario Rossi, Supporto Clienti..."
              className="w-full p-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:border-gray-600 dark:text-white"
            />
          </div>

          {/* Riepilogo */}
          <div className="bg-gray-50 dark:bg-gray-700 p-4 rounded-lg">
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

          {/* Bottoni */}
          <div className="flex justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-gray-700 bg-gray-200 rounded-lg hover:bg-gray-300 dark:bg-gray-600 dark:text-gray-300 dark:hover:bg-gray-500"
            >
              {t("common.cancel", "Annulla")}
            </button>
            <button
              type="submit"
              disabled={isLoading}
              className="px-4 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 disabled:bg-gray-400 flex items-center gap-2"
            >
              {isLoading ? (
                <>🔄 {t("common.processing", "Elaborazione...")}</>
              ) : (
                <>
                  <FileArchive size={16} />
                  {t("admin.filemanager.createRequest", "Crea Richiesta")}
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
