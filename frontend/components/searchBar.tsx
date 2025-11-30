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
  externalSearchType?: "id" | "status";
  onSearchTypeChange?: (type: "id" | "status") => void;
  availableStatuses?: string[];
  statusLabel?: string;
  selectPlaceholder?: string;
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
  selectPlaceholder,
}: SearchBarProps) {
  const { t } = useTranslation();
  const [localValue, setLocalValue] = useState(query);
  const [searchType, setSearchType] = useState<"id" | "status">(
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
    setQuery(trimmedValue);
    resetPage();
    onSearch?.(trimmedValue);
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
        ) : (
          <select
            value={localValue}
            onChange={(e) => setLocalValue(e.target.value)}
            className="flex-1 px-4 py-2 text-base border border-gray-300 dark:border-gray-600 rounded bg-gray-200 dark:bg-gray-800 text-polarNight dark:text-softWhite placeholder-gray-500 focus:outline-none dark:placeholder-gray-400 focus:ring-2 focus:ring-polarNight transition"
          >
            <option value="">{selectPlaceholder || t("admin.searchButton.selectStatus")}</option>
            {defaultStatuses.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
        )}
      </div>
    );
  }

  if (searchMode === "vin-or-company") {
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
            VIN
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
            {t("admin.company")}
          </button>
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

        <input
          type="text"
          value={localValue}
          onChange={(e) => setLocalValue(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={
            searchType === "id"
              ? t("admin.vehicles.searchVinPlaceholder")
              : t("admin.vehicles.searchCompanyPlaceholder")
          }
          className="flex-1 px-4 py-2 text-base border border-gray-300 dark:border-gray-600 rounded bg-gray-200 dark:bg-gray-800 text-polarNight dark:text-softWhite placeholder-gray-500 focus:outline-none dark:placeholder-gray-400 focus:ring-2 focus:ring-polarNight transition"
        />
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
