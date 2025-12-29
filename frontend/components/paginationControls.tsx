import React from "react";
import { useTranslation } from "next-i18next";

type PaginationProps = {
  currentPage: number;
  totalPages: number;
  onPrev: () => void;
  onNext: () => void;
};

export default function PaginationControls({
  currentPage,
  totalPages,
  onPrev,
  onNext,
}: PaginationProps) {
  const { t } = useTranslation();

  return (
    <div className="flex items-center justify-center sm:justify-start gap-2 sm:gap-4">
      <button
        onClick={onPrev}
        disabled={currentPage === 1}
        className="px-3 sm:px-4 py-3 sm:py-2 rounded transition-colors duration-200 text-polarNight bg-gray-200 dark:bg-white/10 dark:text-softWhite disabled:opacity-50 active:bg-gray-300 dark:active:bg-white/20 text-sm sm:text-base font-medium"
      >
        ← <span className="hidden sm:inline">{t("admin.prev")}</span>
      </button>
      <span className="text-base sm:text-lg font-medium text-gray-700 dark:text-softWhite whitespace-nowrap">
        {currentPage} / {totalPages}
      </span>
      <button
        onClick={onNext}
        disabled={currentPage === totalPages}
        className="px-3 sm:px-4 py-3 sm:py-2 rounded transition-colors duration-200 text-polarNight bg-gray-200 dark:bg-white/10 dark:text-softWhite disabled:opacity-50 active:bg-gray-300 dark:active:bg-white/20 text-sm sm:text-base font-medium"
      >
        <span className="hidden sm:inline">{t("admin.next")}</span> →
      </button>
    </div>
  );
}
