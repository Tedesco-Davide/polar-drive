import { useTranslation } from "next-i18next";
import { logFrontendEvent } from "@/utils/logger";
import { useEffect } from "react";

export default function AdminLoader({ inline = false }: { inline?: boolean }) {
  const { t } = useTranslation();

  useEffect(() => {
    try {
      logFrontendEvent(
        "AdminLoader",
        "INFO",
        "AdminLoader mounted and displayed"
      );
    } catch (err) {
      const details = err instanceof Error ? err.message : String(err);
      logFrontendEvent(
        "AdminLoader",
        "ERROR",
        "Error while mounting AdminLoader",
        details
      );
    }
  }, []);

  if (inline) {
    return (
      <div className="w-5 h-5 border-2 border-t-polarNight border-gray-300 dark:border-gray-700 dark:border-t-softWhite rounded-full animate-spin" />
    );
  }

  return (
    <div className="fixed inset-0 z-[9999] flex flex-col items-center justify-center bg-background/90 backdrop-blur">
      <div className="flex flex-col items-center space-y-4">
        <div className="w-20 h-20 rounded-full border-4 border-t-polarNight border-gray-300 dark:border-gray-700 dark:border-t-softWhite animate-spin"></div>
        <p className="text-2xl text-gray-600 dark:text-softWhite animate-pulse">
          {t("admin.loading")}
        </p>
      </div>
    </div>
  );
}
