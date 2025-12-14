import { useEffect, useState, useCallback } from "react";
import { useTranslation } from "next-i18next";
import {
  MessageSquare,
  CheckCircle,
  AlertCircle,
  Clock,
  User,
  Shield,
} from "lucide-react";

import { logFrontendEvent } from "@/utils/logger";

interface SmsAuditLog {
  id: number;
  messageSid: string;
  fromPhoneNumber: string;
  toPhoneNumber: string;
  messageBody: string;
  receivedAt: string;
  processingStatus: string;
  errorMessage: string | null;
  vehicleIdResolved: number | null;
  responseSent: string | null;
}

interface AdaptiveProfileSession {
  id: number;
  vehicleId: number;
  adaptiveNumber: string;
  adaptiveSurnameName: string;
  receivedAt: string;
  expiresAt: string;
  parsedCommand: string;
  consentAccepted: boolean;
}

interface GdprConsent {
  id: number;
  phoneNumber: string;
  brand: string;
  requestedAt: string;
  consentGivenAt: string | null;
  consentAccepted: boolean;
}

interface AdminSmsManagementModalProps {
  isOpen: boolean;
  onClose: () => void;
  vehicleId: number;
  vehicleVin: string;
  vehicleBrand: string;
  companyName: string;
  isVehicleActive: boolean;
}

export default function AdminSmsManagementModal({
  isOpen,
  onClose,
  vehicleId,
  vehicleVin,
  vehicleBrand,
  companyName,
  isVehicleActive,
}: AdminSmsManagementModalProps) {
  const { t } = useTranslation("");
  const [loading, setLoading] = useState(false);
  const [auditLogs, setAuditLogs] = useState<SmsAuditLog[]>([]);
  const [profileSessions, setProfileSessions] = useState<AdaptiveProfileSession[]>([]);
  const [gdprConsents, setGdprConsents] = useState<GdprConsent[]>([]);
  const [activeTab, setActiveTab] = useState<"profile" | "gdpr" | "audit">("profile");

    const loadData = useCallback(async () => {
    try {
        setLoading(true);

        let sessions: AdaptiveProfileSession[] = [];
        let vehicleLogs: SmsAuditLog[] = [];
        let consents: GdprConsent[] = [];

        // Carica sessioni ADAPTIVE_PROFILE
        const profileResponse = await fetch(`/api/SmsAdaptiveProfile/${vehicleId}/history`);
        if (profileResponse.ok) {
        sessions = await profileResponse.json();
        setProfileSessions(sessions);
        }

        // Carica audit logs
        const auditResponse = await fetch(`/api/Sms/audit-logs?pageSize=50`);
        if (auditResponse.ok) {
        const auditData = await auditResponse.json();
        vehicleLogs = auditData.logs.filter(
            (log: SmsAuditLog) => log.vehicleIdResolved === vehicleId
        );
        setAuditLogs(vehicleLogs);
        }

        // Carica consensi GDPR
        const gdprResponse = await fetch(`/api/Sms/gdpr/consents?brand=${vehicleBrand}`);
        if (gdprResponse.ok) {
        consents = await gdprResponse.json();
        setGdprConsents(consents);
        }

        logFrontendEvent(
            "AdminSmsManagement",
            "INFO",
            "SMS data loaded successfully",
            "VehicleId: " + vehicleId + ", Sessions: " + sessions.length + ", Logs: " + vehicleLogs.length
        );
    } catch (error) {
        logFrontendEvent(
            "AdminSmsManagement",
            "ERROR",
            "Failed to load SMS data",
            error instanceof Error ? error.message : String(error)
        );
    } finally {
        setLoading(false);
    }
    }, [vehicleId, vehicleBrand]);

  useEffect(() => {
    if (isOpen) {
      loadData();
    }
  }, [isOpen, loadData]);

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString("it-IT");
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case "SUCCESS":
        return "text-green-600 bg-green-100 dark:bg-green-900/20";
      case "ERROR":
        return "text-red-600 bg-red-100 dark:bg-red-900/20";
      case "REJECTED":
        return "text-orange-600 bg-orange-100 dark:bg-orange-900/20";
      default:
        return "text-gray-600 bg-gray-100 dark:bg-gray-900/20";
    }
  };

  const isSessionActive = (session: AdaptiveProfileSession) => {
    return (
      session.consentAccepted &&
      session.parsedCommand === "ADAPTIVE_PROFILE_ON" &&
      new Date(session.expiresAt) > new Date()
    );
  };

  const getRemainingTime = (expiresAt: string) => {
    const now = new Date();
    const expires = new Date(expiresAt);
    const diff = expires.getTime() - now.getTime();
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
    return diff > 0 ? hours + "h " + minutes + "m" : t("admin.smsManagement.statusExpired");
  };

  if (!isOpen) return null;

  const activeSessions = profileSessions.filter(isSessionActive);

  return (
    <div className="fixed top-[64px] md:top-[0px] inset-0 z-50 flex items-center justify-center bg-black/10 backdrop-blur-sm note-modal">
      <div className="w-full h-full p-6 relative overflow-y-auto bg-softWhite dark:bg-gray-800 border border-gray-300 dark:border-gray-600 shadow-none rounded-lg md:h-auto md:w-11/12">
        {/* Header + Status */}
        <div className="flex items-start justify-between mb-4">
          <div>
            <div className="flex items-center space-x-3">
              <h2 className="text-xl font-semibold text-polarNight dark:text-softWhite mb-0">
                🔐 {t("admin.smsManagement.title")}
              </h2>

              <div className="flex items-center space-x-2">
                {isVehicleActive ? (
                  <CheckCircle className="text-green-500" size={16} />
                ) : (
                  <AlertCircle className="text-red-500" size={16} />
                )}
                <span
                  className={`font-semibold ${
                    isVehicleActive
                      ? "text-green-700 dark:text-green-300"
                      : "text-red-700 dark:text-red-300"
                  }`}
                >
                  {isVehicleActive ? t("admin.smsManagement.vehicleActiveStatus") : t("admin.smsManagement.vehicleInactiveStatus")}
                </span>
              </div>
            </div>

            <p className="text-sm text-gray-600 dark:text-gray-400 mt-1">
              {companyName} - {vehicleBrand} {vehicleVin}
            </p>

            {!isVehicleActive && (
              <p className="text-sm text-red-600 dark:text-red-400 mt-1">
                {t("admin.smsManagement.vehicleInactiveWarning")}
              </p>
            )}
          </div>
        </div>

        {/* Sessioni Attive */}
        {activeSessions.length > 0 && (
          <div className="mb-6 p-4 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
            <div className="flex items-center space-x-2 mb-3">
              <CheckCircle className="text-blue-500" size={20} />
              <span className="font-semibold text-blue-700 dark:text-blue-300">
                {activeSessions.length} {t("admin.smsManagement.activeSessions")}
              </span>
            </div>
            {activeSessions.map((session) => (
              <div key={session.id} className="mb-2 p-3 bg-white dark:bg-gray-800 rounded">
                <div className="flex items-center justify-between">
                  <div>
                    <div className="flex items-center space-x-2">
                      <User size={16} className="text-blue-600" />
                      <span className="font-semibold text-polarNight dark:text-softWhite">
                        {session.adaptiveSurnameName}
                      </span>
                      <span className="text-sm text-gray-600 dark:text-gray-400">
                        ({session.adaptiveNumber})
                      </span>
                    </div>
                    <div className="text-xs text-gray-500 mt-1">
                      {t("admin.smsManagement.expiresAt")}: {formatDate(session.expiresAt)}
                    </div>
                  </div>
                  <div className="flex items-center space-x-2">
                    <Clock size={16} className="text-blue-500" />
                    <span className="text-sm font-medium text-blue-600 dark:text-blue-400">
                      {getRemainingTime(session.expiresAt)}
                    </span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Tabs */}
        <div className="flex mb-4 border-b border-gray-300 dark:border-gray-600">
          <button
            className={`px-4 py-2 font-medium ${
              activeTab === "profile"
                ? "border-b-2 text-polarNight border-polarNight dark:text-articWhite dark:border-articWhite"
                : "text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
            }`}
            onClick={() => setActiveTab("profile")}
          >
            <User size={16} className="inline mr-2" />
            {t("admin.smsManagement.tabs.profile")} ({profileSessions.length})
          </button>
          <button
            className={`px-4 py-2 font-medium ml-4 ${
              activeTab === "gdpr"
                ? "border-b-2 text-polarNight border-polarNight dark:text-articWhite dark:border-articWhite"
                : "text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
            }`}
            onClick={() => setActiveTab("gdpr")}
          >
            <Shield size={16} className="inline mr-2" />
            {t("admin.smsManagement.tabs.gdpr")} ({gdprConsents.length})
          </button>
          <button
            className={`px-4 py-2 font-medium ml-4 ${
              activeTab === "audit"
                ? "border-b-2 text-polarNight border-polarNight dark:text-articWhite dark:border-articWhite"
                : "text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
            }`}
            onClick={() => setActiveTab("audit")}
          >
            <MessageSquare size={16} className="inline mr-2" />
            {t("admin.smsManagement.tabs.audit")} ({auditLogs.length})
          </button>
        </div>

        {/* Content */}
        <div className="min-h-[400px]">
          {/* Tab PROFILE */}
          {activeTab === "profile" && (
            <div>
              {profileSessions.length === 0 ? (
                <p className="text-gray-500 py-8">
                  {t("admin.smsManagement.noSessionsFound")}
                </p>
              ) : (
                profileSessions.map((session) => (
                  <div
                    key={session.id}
                    className={`p-4 rounded-lg mb-3 ${
                      isSessionActive(session)
                        ? "bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800"
                        : "bg-gray-50 dark:bg-gray-800"
                    }`}
                  >
                    <div className="flex items-center gap-2 mb-2">
                      <div className="flex items-center space-x-2">
                        <User size={16} />
                        <span className="font-semibold text-polarNight dark:text-softWhite">
                          {session.adaptiveSurnameName || t("admin.smsManagement.defaultName")}
                        </span>
                        <span className="text-sm text-gray-600 dark:text-gray-400">
                          {session.adaptiveNumber}
                        </span>
                      </div>
                      <span
                        className={`px-2 py-1 rounded text-xs font-medium ${
                          isSessionActive(session)
                            ? "bg-green-100 text-green-700 dark:bg-green-900/40 dark:text-green-300"
                            : "bg-gray-200 text-gray-700 dark:bg-gray-700 dark:text-gray-300"
                        }`}
                      >
                        {isSessionActive(session) ? t("admin.smsManagement.statusActive") : t("admin.smsManagement.statusExpired")}
                      </span>
                    </div>
                    <div className="text-sm text-gray-600 dark:text-gray-400">
                      {t("admin.smsManagement.sessionStartLabel")}: {formatDate(session.receivedAt)}
                      <br />
                      {t("admin.smsManagement.sessionExpiryLabel")}: {formatDate(session.expiresAt)}
                      {isSessionActive(session) && (
                        <span className="ml-2 text-green-600 dark:text-green-400 font-medium">
                          ({t("admin.smsManagement.remainingLabel")}: {getRemainingTime(session.expiresAt)})
                        </span>
                      )}
                    </div>
                    <div className="mt-2 flex items-center space-x-4 text-xs">
                      <span
                        className={`${
                          session.consentAccepted
                            ? "text-green-600 dark:text-green-400"
                            : "text-red-600 dark:text-red-400"
                        }`}
                      >
                        {t("admin.smsManagement.consentLabel")}:{" "}{session.consentAccepted ? t("admin.smsManagement.consentStatusActive") : t("admin.smsManagement.consentStatusRevoked")}
                      </span>
                      <span className="text-gray-500">{t("admin.smsManagement.commandLabel")}:{" "}{session.parsedCommand}</span>
                    </div>
                  </div>
                ))
              )}
            </div>
          )}

          {/* Tab GDPR */}
          {activeTab === "gdpr" && (
            <div>
              {gdprConsents.length === 0 ? (
                <p className="text-gray-500 py-8">
                  {t("admin.smsManagement.noConsentsFound")} {vehicleBrand}
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
                        {consent.consentAccepted ? t("admin.smsManagement.gdprStatusActive") : t("admin.smsManagement.gdprStatusRevoked")}
                      </span>
                    </div>
                    <div className="text-sm text-gray-600 dark:text-gray-400">
                      {t("admin.smsManagement.brandLabel")}:{" "}{consent.brand}
                      <br />
                      {t("admin.smsManagement.requestedLabel")}:{" "}{formatDate(consent.requestedAt)}
                      {consent.consentGivenAt && (
                        <>
                          <br />
                          {t("admin.smsManagement.acceptedLabel")}:{" "}{formatDate(consent.consentGivenAt)}
                        </>
                      )}
                    </div>
                    <div className="mt-2 text-xs text-gray-500">
                      {t("admin.smsManagement.consentIdLabel")}:{" #"}{consent.id}
                    </div>
                  </div>
                ))
              )}
            </div>
          )}

          {/* Tab AUDIT */}
          {activeTab === "audit" && (
            <div>
              {auditLogs.length === 0 ? (
                <p className="text-gray-500 py-8">
                  {t("admin.smsManagement.noAuditLogsFound")}
                </p>
              ) : (
                    auditLogs.map((log) => (
                    <div key={log.id} className="p-4 bg-gray-50 dark:bg-gray-800 rounded-lg mb-3">
                        <div className="flex items-center gap-2 mb-2">
                        <div className="flex items-center space-x-2">
                            <span
                            className={`px-2 py-1 rounded text-xs font-medium ${getStatusColor(
                                log.processingStatus
                            )}`}
                            >
                            {log.processingStatus}
                            </span>
                            <span className="font-semibold text-polarNight dark:text-softWhite">
                            {log.fromPhoneNumber}
                            </span>
                        </div>
                        <span className="text-xs text-gray-500">{formatDate(log.receivedAt)}</span>
                        </div>
                        
                        {/* ✅ AGGIUNGI QUESTA SEZIONE */}
                        <div className="text-xs text-gray-600 dark:text-gray-400 mb-2">
                        {t("admin.smsManagement.smsFrom")}:{" "}<span className="font-medium">{log.fromPhoneNumber}</span>
                        {" → "}
                        {t("admin.smsManagement.smsTo")}:{" "}<span className="font-medium">{log.toPhoneNumber}</span>
                        </div>

                        <div className="text-sm text-gray-600 dark:text-gray-400 mb-1">
                        {t("admin.smsManagement.smsMessage")} &quot;{log.messageBody}&quot;
                        </div>
                        {log.errorMessage && (
                        <div className="text-xs text-red-600 dark:text-red-400 mt-1">
                            {t("admin.smsManagement.smsError")} {log.errorMessage}
                        </div>
                        )}
                        <div className="text-xs text-gray-500 mt-2">{t("admin.smsManagement.smsSid")}:{" "}{log.messageSid}</div>
                    </div>
                    ))
              )}
            </div>
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
              logFrontendEvent("SmsModal", "INFO", "SMS modal closed");
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