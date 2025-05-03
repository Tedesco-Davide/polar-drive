import { useTranslation } from "next-i18next";

export default function AdminLoader() {
  const { t } = useTranslation();

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
