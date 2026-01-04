import { useEffect, useState, useCallback, useMemo } from "react";
import { useTranslation } from "next-i18next";
import { Search, ChevronDown } from "lucide-react";

import { logFrontendEvent } from "@/utils/logger";

interface GdprConsent {
  id: number;
  phoneNumber: string;
  adaptiveSurnameName: string;
  brand: string;
  requestedAt: string;
  consentGivenAt: string | null;
  consentAccepted: boolean;
}

interface AdminSmsGdprModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function AdminSmsGdprModal({
  isOpen,
  onClose,
}: AdminSmsGdprModalProps) {
  const { t } = useTranslation("");
  const [loading, setLoading] = useState(false);
  const [gdprConsents, setGdprConsents] = useState<GdprConsent[]>([]);
  const [searchFilter, setSearchFilter] = useState("");
  const [availableBrands, setAvailableBrands] = useState<string[]>([]);
  const [selectedBrand, setSelectedBrand] = useState<string>("");
  const [loadingBrands, setLoadingBrands] = useState(false);
  const [polarDriveMobileNumber, setPolarDriveMobileNumber] = useState<string>("");

  // Carica i brand disponibili
  const loadBrands = useCallback(async () => {
    try {
      setLoadingBrands(true);
      const response = await fetch("/api/Sms/gdpr/brands");
      if (response.ok) {
        const data = await response.json();
        setAvailableBrands(data.brands || []);
        setPolarDriveMobileNumber(data.polarDriveMobileNumber || "");
        // Seleziona il primo brand se non c'è già una selezione
        if (data.brands?.length > 0 && !selectedBrand) {
          setSelectedBrand(data.brands[0]);
        }
      }
    } catch (error) {
      logFrontendEvent(
        "AdminSmsGdprModal",
        "ERROR",
        "Failed to load available brands",
        error instanceof Error ? error.message : String(error)
      );
    } finally {
      setLoadingBrands(false);
    }
  }, [selectedBrand]);

  // Carica i consensi GDPR per il brand selezionato
  const loadConsents = useCallback(async () => {
    if (!selectedBrand) return;

    try {
      setLoading(true);
      const gdprResponse = await fetch(`/api/Sms/gdpr/consents?brand=${selectedBrand}`);
      if (gdprResponse.ok) {
        const consents = await gdprResponse.json();
        setGdprConsents(consents);
      }

      logFrontendEvent(
        "AdminSmsGdprModal",
        "INFO",
        "GDPR consents loaded successfully",
        "Brand: " + selectedBrand + ", Consents: " + gdprConsents.length
      );
    } catch (error) {
      logFrontendEvent(
        "AdminSmsGdprModal",
        "ERROR",
        "Failed to load GDPR consents",
        error instanceof Error ? error.message : String(error)
      );
    } finally {
      setLoading(false);
    }
  }, [selectedBrand, gdprConsents.length]);

  // Carica i brand quando la modal si apre
  useEffect(() => {
    if (isOpen) {
      loadBrands();
    }
  }, [isOpen, loadBrands]);

  // Carica i consensi quando cambia il brand selezionato
  useEffect(() => {
    if (isOpen && selectedBrand) {
      loadConsents();
    }
  }, [isOpen, selectedBrand, loadConsents]);

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString("it-IT");
  };

  // Filtra i consensi in base alla ricerca
  const filteredConsents = useMemo(() => {
    if (!searchFilter.trim()) return gdprConsents;
    
    const lowerSearch = searchFilter.toLowerCase();
    return gdprConsents.filter(
      (consent) =>
        consent.phoneNumber.toLowerCase().includes(lowerSearch) ||
        consent.adaptiveSurnameName.toLowerCase().includes(lowerSearch) ||
        consent.brand.toLowerCase().includes(lowerSearch) ||
        consent.id.toString().includes(lowerSearch)
    );
  }, [gdprConsents, searchFilter]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/10 backdrop-blur-sm note-modal p-4">
      <div className="w-full max-w-7xl max-h-[80vh] p-6 relative flex flex-col bg-softWhite dark:bg-gray-800 border border-gray-300 dark:border-gray-600 shadow-lg rounded-lg">
        {/* Header */}
        <div className="flex items-start justify-between mb-4">
          <div className="flex-1">
            <h2 className="text-xl font-semibold text-polarNight dark:text-softWhite mb-2">
              🔐 {t("admin.smsManagement.titleGdpr")}
            </h2>
            {/* Brand Selector e Cellulare Operativo PolarDrive */}
            <div className="flex items-center gap-6 flex-wrap">
              <div className="flex items-center gap-2">
                <label className="text-sm text-gray-600 dark:text-gray-400">
                  {t("admin.smsManagement.brandLabel")}:
                </label>
                <div className="relative">
                  <select
                    value={selectedBrand}
                    onChange={(e) => setSelectedBrand(e.target.value)}
                    disabled={loadingBrands}
                    className="appearance-none bg-white dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-lg px-4 py-2 pr-10 text-sm text-polarNight dark:text-softWhite focus:outline-none focus:ring-2 focus:ring-blue-500 cursor-pointer min-w-[150px]"
                  >
                    {loadingBrands ? (
                      <option value="">{t("admin.smsManagement.loadingBrands")}</option>
                    ) : availableBrands.length === 0 ? (
                      <option value="">{t("admin.smsManagement.noBrandsAvailable")}</option>
                    ) : (
                      availableBrands.map((brand) => (
                        <option key={brand} value={brand}>
                          {brand}
                        </option>
                      ))
                    )}
                  </select>
                  <ChevronDown
                    className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 pointer-events-none"
                    size={16}
                  />
                </div>
              </div>
              {/* Cellulare Operativo PolarDrive */}
              {polarDriveMobileNumber && (
                <div className="flex items-center gap-2 px-3 py-1.5 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-700 rounded-lg">
                  <span className="text-sm text-gray-600 dark:text-gray-400">
                    {t("admin.smsManagement.polarDriveMobileLabel")}:
                  </span>
                  <span className="text-sm font-semibold text-blue-600 dark:text-blue-400">
                    {polarDriveMobileNumber}
                  </span>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Search Bar */}
        <div className="mb-4">
          <div className="relative">
            <Search
              className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400"
              size={20}
            />
            <input
              type="text"
              placeholder={t("admin.smsManagement.searchGdprPlaceholder")}
              value={searchFilter}
              onChange={(e) => setSearchFilter(e.target.value)}
              className="w-full pl-10 pr-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-polarNight dark:text-softWhite focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
            {t("admin.smsManagement.resultsCount", { filtered: filteredConsents.length, total: gdprConsents.length })}
          </p>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto min-h-0 border border-gray-300 dark:border-gray-600 rounded-lg">
          {filteredConsents.length === 0 ? (
            <p className="text-gray-500 py-8 text-center">
              {searchFilter
                ? t("admin.smsManagement.noResultsFound")
                : `${t("admin.smsManagement.noConsentsFound")} ${selectedBrand}`}
            </p>
          ) : (
            <table className="w-full">
              <thead className="bg-gray-100 dark:bg-gray-700 sticky top-0">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    ID
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    <div className="flex items-center gap-2">
                      {t("admin.smsManagement.numberHeader")}
                    </div>
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    {t("admin.smsManagement.nameHeader")}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    {t("admin.smsManagement.gdprConsentLabel")}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    {t("admin.smsManagement.brandHeader")}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    {t("admin.smsManagement.requestedHeader")}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    {t("admin.smsManagement.acceptedHeader")}
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
                {filteredConsents.map((consent) => (
                  <tr
                    key={consent.id}
                    className={`${
                      consent.consentAccepted
                        ? "bg-green-50 dark:bg-green-900/10 hover:bg-green-100 dark:hover:bg-green-900/20"
                        : "bg-red-50 dark:bg-red-900/10 hover:bg-red-100 dark:hover:bg-red-900/20"
                    } transition-colors`}
                  >
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      #{consent.id}
                    </td>
                    <td className="px-4 py-3 text-sm text-polarNight dark:text-softWhite">
                      {consent.phoneNumber}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      {consent.adaptiveSurnameName}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={`px-3 py-1 rounded-full text-xs font-medium ${
                          consent.consentAccepted
                            ? "bg-green-100 text-green-700 dark:bg-green-900/40 dark:text-green-300"
                            : "bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300"
                        }`}
                      >
                        {consent.consentAccepted
                          ? t("admin.smsManagement.gdprStatusActive")
                          : t("admin.smsManagement.gdprStatusRevoked")}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      {consent.brand}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      {formatDate(consent.requestedAt)}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      {consent.consentGivenAt
                        ? formatDate(consent.consentGivenAt)
                        : "-"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Loading */}
        {loading && (
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
          </div>
        )}

        {/* Footer */}
        <div className="mt-6 flex flex-shrink-0 pt-4 border-t border-gray-200 dark:border-gray-700">
          <button
            className="bg-gray-400 text-white px-6 py-2 rounded hover:bg-gray-500"
            onClick={() => {
              logFrontendEvent("SmsGdprModal", "INFO", "SMS GDPR modal closed");
              onClose();
            }}
          >
            {t("admin.close")}
          </button>
        </div>
      </div>
    </div>
  );
}
