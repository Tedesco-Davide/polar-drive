import { useEffect, useState, useCallback, useMemo } from "react";
import { useTranslation } from "next-i18next";
import {
  MessageSquare,
  CheckCircle,
  AlertCircle,
  Clock,
  User,
  Search,
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

interface AdminSmsProfileModalProps {
  isOpen: boolean;
  onClose: () => void;
  vehicleId: number;
  vehicleVin: string;
  vehicleBrand: string;
  companyName: string;
  isVehicleActive: boolean;
}

export default function AdminSmsProfileModal({
  isOpen,
  onClose,
  vehicleId,
  vehicleVin,
  vehicleBrand,
  companyName,
  isVehicleActive,
}: AdminSmsProfileModalProps) {
  const { t } = useTranslation("");
  const [loading, setLoading] = useState(false);
  const [auditLogs, setAuditLogs] = useState<SmsAuditLog[]>([]);
  const [profileSessions, setProfileSessions] = useState<AdaptiveProfileSession[]>([]);
  const [activeTab, setActiveTab] = useState<"profile" | "audit">("profile");
  const [profileSearchFilter, setProfileSearchFilter] = useState("");
  const [auditSearchFilter, setAuditSearchFilter] = useState("");

    const loadData = useCallback(async () => {
    try {
        setLoading(true);

        let sessions: AdaptiveProfileSession[] = [];
        let vehicleLogs: SmsAuditLog[] = [];

        // Carica sessioni ADAPTIVE_PROFILE
        const profileResponse = await fetch(`/api/Sms/adaptive-profile/${vehicleId}/history`);
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

        logFrontendEvent(
            "AdminSmsProfile",
            "INFO",
            "SMS data loaded successfully",
            "VehicleId: " + vehicleId + ", Sessions: " + sessions.length + ", Logs: " + vehicleLogs.length
        );
    } catch (error) {
        logFrontendEvent(
            "AdminSmsProfile",
            "ERROR",
            "Failed to load SMS data",
            error instanceof Error ? error.message : String(error)
        );
    } finally {
        setLoading(false);
    }
    }, [vehicleId]);

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

  // Filtra le sessioni profilo in base alla ricerca
  const filteredProfileSessions = useMemo(() => {
    if (!profileSearchFilter.trim()) return profileSessions;
    
    const lowerSearch = profileSearchFilter.toLowerCase();
    return profileSessions.filter(
      (session) =>
        session.adaptiveNumber.toLowerCase().includes(lowerSearch) ||
        session.adaptiveSurnameName.toLowerCase().includes(lowerSearch) ||
        session.id.toString().includes(lowerSearch) ||
        session.parsedCommand.toLowerCase().includes(lowerSearch)
    );
  }, [profileSessions, profileSearchFilter]);

  // Filtra gli audit logs in base alla ricerca
  const filteredAuditLogs = useMemo(() => {
    if (!auditSearchFilter.trim()) return auditLogs;
    
    const lowerSearch = auditSearchFilter.toLowerCase();
    return auditLogs.filter(
      (log) =>
        log.fromPhoneNumber.toLowerCase().includes(lowerSearch) ||
        log.toPhoneNumber.toLowerCase().includes(lowerSearch) ||
        log.messageBody.toLowerCase().includes(lowerSearch) ||
        log.messageSid.toLowerCase().includes(lowerSearch) ||
        log.processingStatus.toLowerCase().includes(lowerSearch) ||
        log.id.toString().includes(lowerSearch)
    );
  }, [auditLogs, auditSearchFilter]);

  if (!isOpen) return null;

  const activeSessions = profileSessions.filter(isSessionActive);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/10 backdrop-blur-sm note-modal p-4">
      <div className="w-full max-w-7xl max-h-[80vh] p-6 relative flex flex-col bg-softWhite dark:bg-gray-800 border border-gray-300 dark:border-gray-600 shadow-lg rounded-lg">
        {/* Header + Status */}
        <div className="flex items-start justify-between mb-4">
          <div>
            <div className="flex items-center space-x-2">
              <h2 className="text-xl font-semibold text-polarNight dark:text-softWhite mb-0">
                🔐 {t("admin.smsManagement.titleProfile")}
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
                ? "border-b-2 text-polarNight border-polarNight dark:text-arcticWhite dark:border-arcticWhite"
                : "text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
            }`}
            onClick={() => setActiveTab("profile")}
          >
            <User size={16} className="inline mr-2" />
            {t("admin.smsManagement.tabs.profile")} ({profileSessions.length})
          </button>
          <button
            className={`px-4 py-2 font-medium ml-4 ${
              activeTab === "audit"
                ? "border-b-2 text-polarNight border-polarNight dark:text-arcticWhite dark:border-arcticWhite"
                : "text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
            }`}
            onClick={() => setActiveTab("audit")}
          >
            <MessageSquare size={16} className="inline mr-2" />
            {t("admin.smsManagement.tabs.auditVehicleSpecific")} ({auditLogs.length})
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto min-h-0 p-2">
          {/* Tab PROFILE */}
          {activeTab === "profile" && (
            <div className="h-full flex flex-col">
              {/* Search Bar Profile */}
              <div className="mb-4">
                <div className="relative">
                  <Search
                    className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400"
                    size={20}
                  />
                  <input
                    type="text"
                    placeholder="Cerca per numero, nome, ID o comando..."
                    value={profileSearchFilter}
                    onChange={(e) => setProfileSearchFilter(e.target.value)}
                    className="w-full pl-10 pr-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-polarNight dark:text-softWhite focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
                <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  {filteredProfileSessions.length} di {profileSessions.length} risultati
                </p>
              </div>

              {/* Table Profile */}
              <div className="flex-1 overflow-y-auto border border-gray-300 dark:border-gray-600 rounded-lg">
                {filteredProfileSessions.length === 0 ? (
                  <p className="text-gray-500 py-8 text-center">
                    {profileSearchFilter
                      ? "Nessun risultato trovato"
                      : t("admin.smsManagement.noSessionsFound")}
                  </p>
                ) : (
                  <table className="w-full">
                    <thead className="bg-gray-100 dark:bg-gray-700 sticky top-0">
                      <tr>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          ID
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          <div className="flex items-center gap-2">
                            <User size={14} />
                            Nome/Numero
                          </div>
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          {t("admin.smsManagement.profilingProcedureLabel")}
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          Comando
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          {t("admin.smsManagement.gdprConsentLabel")}
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          Ricevuto
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          Scadenza
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
                      {filteredProfileSessions.map((session) => (
                        <tr
                          key={session.id}
                          className={`${
                            isSessionActive(session)
                              ? "bg-green-50 dark:bg-green-900/10 hover:bg-green-100 dark:hover:bg-green-900/20"
                              : "bg-gray-50 dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700"
                          } transition-colors`}
                        >
                          <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                            #{session.id}
                          </td>
                          <td className="px-4 py-3">
                            <div className="text-sm font-semibold text-polarNight dark:text-softWhite">
                              {session.adaptiveSurnameName || t("admin.smsManagement.defaultName")}
                            </div>
                            <div className="text-xs text-gray-600 dark:text-gray-400">
                              {session.adaptiveNumber}
                            </div>
                          </td>
                          <td className="px-4 py-3">
                            <span
                              className={`px-3 py-1 rounded-full text-xs font-medium ${
                                isSessionActive(session)
                                  ? "bg-green-100 text-green-700 dark:bg-green-900/40 dark:text-green-300"
                                  : "bg-gray-200 text-gray-700 dark:bg-gray-700 dark:text-gray-300"
                              }`}
                            >
                              {isSessionActive(session) ? t("admin.smsManagement.statusActive") : t("admin.smsManagement.statusExpired")}
                            </span>
                          </td>
                          <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                            {session.parsedCommand}
                          </td>
                          <td className="px-4 py-3">
                            <span
                              className={`text-xs font-medium ${
                                session.consentAccepted
                                  ? "text-green-600 dark:text-green-400"
                                  : "text-red-600 dark:text-red-400"
                              }`}
                            >
                              {session.consentAccepted ? t("admin.smsManagement.gdprStatusActive") : t("admin.smsManagement.gdprStatusRevoked")}
                            </span>
                          </td>
                          <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                            {formatDate(session.receivedAt)}
                          </td>
                          <td className="px-4 py-3">
                            <div className="text-sm text-gray-600 dark:text-gray-300">
                              {formatDate(session.expiresAt)}
                            </div>
                            {isSessionActive(session) && (
                              <div className="text-xs text-green-600 dark:text-green-400 font-medium">
                                {getRemainingTime(session.expiresAt)}
                              </div>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </div>
          )}

          {/* Tab AUDIT */}
          {activeTab === "audit" && (
            <div className="h-full flex flex-col">
              {/* Search Bar Audit */}
              <div className="mb-4">
                <div className="relative">
                  <Search
                    className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400"
                    size={20}
                  />
                  <input
                    type="text"
                    placeholder="Cerca per numero, messaggio, stato o SID..."
                    value={auditSearchFilter}
                    onChange={(e) => setAuditSearchFilter(e.target.value)}
                    className="w-full pl-10 pr-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-polarNight dark:text-softWhite focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
                <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  {filteredAuditLogs.length} di {auditLogs.length} risultati
                </p>
              </div>

              {/* Table Audit */}
              <div className="flex-1 overflow-y-auto border border-gray-300 dark:border-gray-600 rounded-lg">
                {filteredAuditLogs.length === 0 ? (
                  <p className="text-gray-500 py-8 text-center">
                    {auditSearchFilter
                      ? "Nessun risultato trovato"
                      : t("admin.smsManagement.noAuditLogsFound")}
                  </p>
                ) : (
                  <table className="w-full">
                    <thead className="bg-gray-100 dark:bg-gray-700 sticky top-0">
                      <tr>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          ID
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          Stato
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          Da
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          A
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          Messaggio
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          Ricevuto
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                          SID
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
                      {filteredAuditLogs.map((log) => (
                        <tr
                          key={log.id}
                          className="bg-gray-50 dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
                        >
                          <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                            #{log.id}
                          </td>
                          <td className="px-4 py-3">
                            <span
                              className={`px-3 py-1 rounded-full text-xs font-medium ${getStatusColor(
                                log.processingStatus
                              )}`}
                            >
                              {log.processingStatus}
                            </span>
                          </td>
                          <td className="px-4 py-3 text-sm text-polarNight dark:text-softWhite">
                            {log.fromPhoneNumber}
                          </td>
                          <td className="px-4 py-3 text-sm font-semibold text-gray-600 dark:text-gray-300">
                            {log.toPhoneNumber}
                          </td>
                          <td className="px-4 py-3 max-w-xs">
                            <div className="text-sm text-gray-600 dark:text-gray-300 whitespace-normal break-words">
                              {log.messageBody}
                            </div>
                            {log.errorMessage && (
                              <div className="text-xs text-red-600 dark:text-red-400 mt-1">
                                ⚠️ {log.errorMessage}
                              </div>
                            )}
                          </td>
                          <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                            {formatDate(log.receivedAt)}
                          </td>
                          <td className="px-4 py-3 text-xs text-gray-500 dark:text-gray-400 font-mono">
                            {log.messageSid}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
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
        <div className="mt-6 flex flex-shrink-0 pt-4 border-t border-gray-200 dark:border-gray-700">
          <button
            className="bg-gray-400 text-white px-6 py-2 rounded hover:bg-gray-500"
            onClick={() => {
              logFrontendEvent("SmsProfileModal", "INFO", "SMS Profile modal closed");
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