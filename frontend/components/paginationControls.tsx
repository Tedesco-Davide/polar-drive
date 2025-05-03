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
    <div className="flex items-center justify-start space-x-4">
      <button
        onClick={onPrev}
        disabled={currentPage === 1}
        className="px-4 py-2 rounded transition-colors duration-200 text-polarNight bg-gray-200 dark:bg-white/10 dark:text-softWhite disabled:opacity-50"
      >
        ← {t("admin.prev")}
      </button>
      <span className="text-lg font-medium text-gray-700 dark:text-softWhite">
        {t("admin.page")} {currentPage} {t("admin.of")} {totalPages}
      </span>
      <button
        onClick={onNext}
        disabled={currentPage === totalPages}
        className="px-4 py-2 rounded transition-colors duration-200 text-polarNight bg-gray-200 dark:bg-white/10 dark:text-softWhite disabled:opacity-50"
      >
        {t("admin.next")} →
      </button>
    </div>
  );
}
