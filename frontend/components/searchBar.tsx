import React, { useState, useEffect } from "react";
import { useTranslation } from "next-i18next";
import { Search, X } from "lucide-react";

type SearchBarProps = {
  query: string;
  setQuery: (value: string) => void;
  resetPage: () => void;
  onSearch?: (searchValue: string) => void;
  placeholderKey?: string;
  searchMode?: "id-or-status" | "vin-or-company" | "default";
  externalSearchType?: "id" | "status" | "outageType" | "vin";
  onSearchTypeChange?: (type: "id" | "status" | "outageType" | "vin") => void;
  availableStatuses?: string[];
  statusLabel?: string;
  outageLabel?: string;
  outageTypePlaceholder?: string;
  selectPlaceholder?: string;
  vatLabel?: string;
  companyLabel?: string;
  vinPlaceholder?: string;
  companyPlaceholder?: string;
  availableOutageTypes?: string[];
  showVinFilter?: boolean;
  vinFilterLabel?: string;
};

export default function SearchBar({
  query,
  setQuery,
  resetPage,
  onSearch,
  placeholderKey = "admin.searchPlaceholder",
  searchMode = "default",
  externalSearchType,
  onSearchTypeChange,
  availableStatuses,
  statusLabel,
  outageLabel,
  outageTypePlaceholder,
  selectPlaceholder,
  vatLabel,
  companyLabel,
  vinPlaceholder,
  companyPlaceholder,
  availableOutageTypes,
  showVinFilter = false,
  vinFilterLabel,
}: SearchBarProps) {
  const { t } = useTranslation();
  const [localValue, setLocalValue] = useState(query);
  const [searchType, setSearchType] = useState<"id" | "status" | "outageType" | "vin">(
    externalSearchType || "id"
  );

  const defaultStatuses = availableStatuses || [
    "PDF-READY",
    "REGENERATING",
    "PROCESSING",
    "ERROR",
    "NO-DATA",
  ];

  useEffect(() => {
    setLocalValue(query);
  }, [query]);

  const handleSearch = () => {
    const trimmedValue = localValue.trim();
    // Se stiamo cercando per VIN, aggiungiamo il prefisso "VIN:" per il backend
    const searchValue = searchType === "vin" && trimmedValue ? `VIN:${trimmedValue}` : trimmedValue;
    setQuery(searchValue);
    resetPage();
    onSearch?.(searchValue);
  };

  const handleClear = () => {
    setLocalValue("");
    setQuery("");
    resetPage();
    onSearch?.("");
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter") {
      handleSearch();
    }
  };

  if (searchMode === "id-or-status") {
    return (
      <div className="flex-1 flex gap-2">
        <div className="flex bg-gray-200 dark:bg-gray-700 rounded overflow-hidden">
          <button
            onClick={() => {
              setSearchType("id");
              onSearchTypeChange?.("id");
              setLocalValue("");
            }}
            className={`px-4 py-2 text-sm font-medium transition-colors ${
              searchType === "id"
                ? "bg-blue-500 text-white"
                : "text-gray-600 dark:text-gray-300 hover:bg-gray-300 dark:hover:bg-gray-600"
            }`}
          >
            ID
          </button>
          <button
            onClick={() => {
              setSearchType("status");
              onSearchTypeChange?.("status");
              setLocalValue("");
            }}
            className={`px-4 py-2 text-sm font-medium transition-colors ${
              searchType === "status"
                ? "bg-blue-500 text-white"
                : "text-gray-600 dark:text-gray-300 hover:bg-gray-300 dark:hover:bg-gray-600"
            }`}
          >
            {statusLabel || t("admin.status")}
          </button>
          {showVinFilter && (
            <button
              onClick={() => {
                setSearchType("vin");
                onSearchTypeChange?.("vin");
                setLocalValue("");
              }}
              className={`px-4 py-2 text-sm font-medium transition-colors ${
                searchType === "vin"
                  ? "bg-blue-500 text-white"
                  : "text-gray-600 dark:text-gray-300 hover:bg-gray-300 dark:hover:bg-gray-600"
              }`}
            >
              {vinFilterLabel || "VIN"}
            </button>
          )}
          {availableOutageTypes && availableOutageTypes.length > 0 && (
            <button
              onClick={() => {
                setSearchType("outageType");
                onSearchTypeChange?.("outageType");
                setLocalValue("");
              }}
              className={`px-4 py-2 text-sm font-medium transition-colors ${
                searchType === "outageType"
                  ? "bg-blue-500 text-white"
                  : "text-gray-600 dark:text-gray-300 hover:bg-gray-300 dark:hover:bg-gray-600"
              }`}
            >
              {outageLabel || t("admin.outageType")}
            </button>
          )}
        </div>

        <button
          onClick={handleSearch}
          className="p-2 bg-blue-500 text-white rounded hover:bg-blue-600"
          title={t("admin.searchButton")}
        >
          <Search size={16} />
        </button>

        {localValue && (
          <button
            onClick={handleClear}
            className="p-2 bg-gray-400 text-white rounded hover:bg-gray-500"
            title="Cancella"
          >
            <X size={16} />
          </button>
        )}

        {searchType === "id" ? (
          <input
            type="number"
            value={localValue}
            onChange={(e) => setLocalValue(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={t("admin.vehicleReports.searchPlaceholder")}
            className="flex-1 px-4 py-2 text-base border border-gray-300 dark:border-gray-600 rounded bg-gray-200 dark:bg-gray-800 text-polarNight dark:text-softWhite placeholder-gray-500 focus:outline-none dark:placeholder-gray-400 focus:ring-2 focus:ring-polarNight transition"
          />
        ) : searchType === "status" ? (
          <select
            value={localValue}
            onChange={(e) => setLocalValue(e.target.value)}
            className="flex-1 px-4 py-2 text-base border border-gray-300 dark:border-gray-600 rounded bg-gray-200 dark:bg-gray-800 text-polarNight dark:text-softWhite placeholder-gray-500 focus:outline-none dark:placeholder-gray-400 focus:ring-2 focus:ring-polarNight transition"
          >
            <option value="">
              {selectPlaceholder || t("admin.searchButton.selectStatus")}
            </option>
            {defaultStatuses.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
        ) : searchType === "vin" ? (
          <input
            type="text"
            value={localValue}
            onChange={(e) => setLocalValue(e.target.value.toUpperCase())}
            onKeyDown={handleKeyDown}
            placeholder={vinPlaceholder || t("admin.vehicles.searchVinPlaceholder")}
            className="flex-1 px-4 py-2 text-base border border-gray-300 dark:border-gray-600 rounded bg-gray-200 dark:bg-gray-800 text-polarNight dark:text-softWhite placeholder-gray-500 focus:outline-none dark:placeholder-gray-400 focus:ring-2 focus:ring-polarNight transition"
          />
        ) : (
          <select
            value={localValue}
            onChange={(e) => setLocalValue(e.target.value)}
            className="flex-1 px-4 py-2 text-base border border-gray-300 dark:border-gray-600 rounded bg-gray-200 dark:bg-gray-800 text-polarNight dark:text-softWhite placeholder-gray-500 focus:outline-none dark:placeholder-gray-400 focus:ring-2 focus:ring-polarNight transition"
          >
            <option value="">{outageTypePlaceholder || t("admin.searchButton.outageTypePlaceholder")}</option>
            {(availableOutageTypes || []).map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        )}
      </div>
    );
  }

  if (searchMode === "vin-or-company") {
    return (
      <div className="flex-1 flex flex-col sm:flex-row gap-2">
        {/* Toggle VIN/Azienda - Full width su mobile */}
        <div className="flex bg-gray-200 dark:bg-gray-700 rounded overflow-hidden">
          <button
            onClick={() => {
              setSearchType("id");
              onSearchTypeChange?.("id");
              setLocalValue("");
            }}
            className={`flex-1 sm:flex-none px-4 py-3 sm:py-2 text-sm font-medium transition-colors ${
              searchType === "id"
                ? "bg-blue-500 text-white"
                : "text-gray-600 dark:text-gray-300 hover:bg-gray-300 dark:hover:bg-gray-600"
            }`}
          >
            {vatLabel}
          </button>
          <button
            onClick={() => {
              setSearchType("status");
              onSearchTypeChange?.("status");
              setLocalValue("");
            }}
            className={`flex-1 sm:flex-none px-4 py-3 sm:py-2 text-sm font-medium transition-colors ${
              searchType === "status"
                ? "bg-blue-500 text-white"
                : "text-gray-600 dark:text-gray-300 hover:bg-gray-300 dark:hover:bg-gray-600"
            }`}
          >
            {companyLabel}
          </button>
        </div>

        {/* Input e bottoni azione */}
        <div className="flex gap-2 flex-1">
          <input
            type="text"
            value={localValue}
            onChange={(e) => setLocalValue(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={
              searchType === "id"
                ? vinPlaceholder || t("admin.vehicles.searchVinPlaceholder")
                : companyPlaceholder ||
                  t("admin.vehicles.searchCompanyPlaceholder")
            }
            className="flex-1 min-w-0 px-4 py-3 sm:py-2 text-base border border-gray-300 dark:border-gray-600 rounded bg-gray-200 dark:bg-gray-800 text-polarNight dark:text-softWhite placeholder-gray-500 focus:outline-none dark:placeholder-gray-400 focus:ring-2 focus:ring-polarNight transition"
          />
          <button
            onClick={handleSearch}
            className="p-3 sm:p-2 bg-blue-500 text-white rounded hover:bg-blue-600 active:bg-blue-700 transition-colors"
            title={t("admin.searchButton")}
          >
            <Search size={20} className="sm:w-4 sm:h-4" />
          </button>
          {localValue && (
            <button
              onClick={handleClear}
              className="p-3 sm:p-2 bg-gray-400 text-white rounded hover:bg-gray-500 active:bg-gray-600 transition-colors"
              title="Cancella"
            >
              <X size={20} className="sm:w-4 sm:h-4" />
            </button>
          )}
        </div>
      </div>
    );
  }

  return (
    <div className="flex-1 flex gap-2">
      <button
        onClick={handleSearch}
        className="p-2 bg-blue-500 text-white rounded hover:bg-blue-600"
        title={t("admin.searchButton")}
      >
        <Search size={16} />
      </button>
      {localValue && (
        <button
          onClick={handleClear}
          className="p-2 bg-gray-400 text-white rounded hover:bg-gray-500"
          title="Cancella"
        >
          <X size={16} />
        </button>
      )}
      <input
        type="text"
        value={localValue}
        onChange={(e) => setLocalValue(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder={t(placeholderKey)}
        className="flex-1 px-4 py-2 text-base border border-gray-300 dark:border-gray-600 rounded bg-gray-200 dark:bg-gray-800 text-polarNight dark:text-softWhite placeholder-gray-500 dark:placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-polarNight transition"
      />
    </div>
  );
}
