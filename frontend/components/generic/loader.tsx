import { useTranslation } from "next-i18next";

export default function Loader({ 
  inline = false, 
  local = false 
}: { 
  inline?: boolean; 
  local?: boolean; 
}) {
  const { t } = useTranslation();

  if (inline) {
    return (
      <div className="w-4 h-4 border-2 border-t-polarNight border-gray-300 dark:border-gray-700 dark:border-t-softWhite rounded-full animate-spin" />
    );
  }

  if (local) {
    return (
      <div className="absolute inset-0 z-50 flex flex-col items-center justify-center bg-softWhite/80 dark:bg-polarNight/80 backdrop-blur-sm rounded-lg">
        <div className="flex flex-col items-center space-y-4">
          <div className="w-24 h-24 rounded-full border-4 border-t-polarNight border-gray-300 dark:border-gray-700 dark:border-t-softWhite animate-spin"></div>
          <p className="text-2xl text-gray-600 dark:text-softWhite animate-pulse">
            {t("admin.loading")}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="fixed inset-0 z-[9999] flex flex-col items-center justify-center bg-background/90 backdrop-blur">
      <div className="flex flex-col items-center space-y-4">
        <div className="w-24 h-24 rounded-full border-4 border-t-polarNight border-gray-300 dark:border-gray-700 dark:border-t-softWhite animate-spin"></div>
        <p className="text-2xl text-gray-600 dark:text-softWhite animate-pulse">
          {t("admin.loading")}
        </p>
      </div>
    </div>
  );
}