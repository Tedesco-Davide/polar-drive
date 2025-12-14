import { useEffect, useState, useCallback } from "react";
import { useTranslation } from "next-i18next";
import { Shield } from "lucide-react";

import { logFrontendEvent } from "@/utils/logger";

interface GdprConsent {
  id: number;
  phoneNumber: string;
  brand: string;
  requestedAt: string;
  consentGivenAt: string | null;
  consentAccepted: boolean;
}

interface AdminSmsGdprModalProps {
  isOpen: boolean;
  onClose: () => void;
  brand: string;
}

export default function AdminSmsGdprModal({
  isOpen,
  onClose,
  brand,
}: AdminSmsGdprModalProps) {
  const { t } = useTranslation("");
  const [loading, setLoading] = useState(false);
  const [gdprConsents, setGdprConsents] = useState<GdprConsent[]>([]);

  const loadData = useCallback(async () => {
    try {
      setLoading(true);

      // Carica consensi GDPR
      const gdprResponse = await fetch(`/api/Sms/gdpr/consents?brand=${brand}`);
      if (gdprResponse.ok) {
        const consents = await gdprResponse.json();
        setGdprConsents(consents);
      }

      logFrontendEvent(
        "AdminSmsGdprModal",
        "INFO",
        "GDPR consents loaded successfully",
        "Brand: " + brand + ", Consents: " + gdprConsents.length
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
  }, [brand, gdprConsents.length]);

  useEffect(() => {
    if (isOpen) {
      loadData();
    }
  }, [isOpen, loadData]);

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString("it-IT");
  };

  if (!isOpen) return null;

  return (
    <div className="fixed top-[64px] md:top-[0px] inset-0 z-50 flex items-center justify-center bg-black/10 backdrop-blur-sm note-modal">
      <div className="w-full h-full p-6 relative overflow-y-auto bg-softWhite dark:bg-gray-800 border border-gray-300 dark:border-gray-600 shadow-none rounded-lg md:h-auto md:w-11/12">
        {/* Header */}
        <div className="flex items-start justify-between mb-4">
          <div>
            <h2 className="text-xl font-semibold text-polarNight dark:text-softWhite mb-0">
              🔐 {t("admin.smsManagement.titleGdpr")}
            </h2>
            <p className="text-sm text-gray-600 dark:text-gray-400 mt-1">
              {t("admin.smsManagement.brandLabel")}: {brand}
            </p>
          </div>
        </div>

        {/* Content */}
        <div className="min-h-[400px]">
          {gdprConsents.length === 0 ? (
            <p className="text-gray-500 py-8">
              {t("admin.smsManagement.noConsentsFound")} {brand}
            </p>
          ) : (
            gdprConsents.map((consent) => (
              <div
                key={consent.id}
                className={`p-4 rounded-lg mb-3 ${
                  consent.consentAccepted
                    ? "bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800"
                    : "bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800"
                }`}
              >
                <div className="flex items-center gap-2 mb-2">
                  <div className="flex items-center space-x-2">
                    <Shield size={16} />
                    <span className="font-semibold text-polarNight dark:text-softWhite">
                      {consent.phoneNumber}
                    </span>
                  </div>
                  <span
                    className={`px-2 py-1 rounded text-xs font-medium ${
                      consent.consentAccepted
                        ? "bg-green-100 text-green-700 dark:bg-green-900/40 dark:text-green-300"
                        : "bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300"
                    }`}
                  >
                    {consent.consentAccepted
                      ? t("admin.smsManagement.gdprStatusActive")
                      : t("admin.smsManagement.gdprStatusRevoked")}
                  </span>
                </div>
                <div className="text-sm text-gray-600 dark:text-gray-400">
                  {t("admin.smsManagement.brandLabel")}: {consent.brand}
                  <br />
                  {t("admin.smsManagement.requestedLabel")}:{" "}
                  {formatDate(consent.requestedAt)}
                  {consent.consentGivenAt && (
                    <>
                      <br />
                      {t("admin.smsManagement.acceptedLabel")}:{" "}
                      {formatDate(consent.consentGivenAt)}
                    </>
                  )}
                </div>
                <div className="mt-2 text-xs text-gray-500">
                  {t("admin.smsManagement.consentIdLabel")}: #{consent.id}
                </div>
              </div>
            ))
          )}
        </div>

        {/* Loading */}
        {loading && (
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
          </div>
        )}

        {/* Footer */}
        <div className="mt-6 flex">
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
