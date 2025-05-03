import React from "react";
import { useTranslation } from "next-i18next";

type SearchBarProps = {
  query: string;
  setQuery: (value: string) => void;
  resetPage: () => void;
};

export default function SearchBar({
  query,
  setQuery,
  resetPage,
}: SearchBarProps) {
  const { t } = useTranslation();

  return (
    <div className="flex-1">
      <input
        type="text"
        value={query}
        onChange={(e) => {
          setQuery(e.target.value);
          resetPage();
        }}
        placeholder={t("admin.searchPlaceholder")}
        className="w-full px-4 py-2 text-base border border-gray-300 dark:border-gray-600 rounded bg-gray-200 dark:bg-gray-800 text-polarNight dark:text-softWhite placeholder-gray-500 dark:placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-polarNight transition"
      />
    </div>
  );
}
