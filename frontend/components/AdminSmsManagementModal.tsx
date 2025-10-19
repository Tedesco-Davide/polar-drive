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
import { API_BASE_URL } from "@/utils/api";
import { logFrontendEvent } from "@/utils/logger";

interface SmsAuditLog {
  id: number;
  messageSid: string;
  fromPhoneNumber: string;
  messageBody: string;
  receivedAt: string;
  processingStatus: string;
  errorMessage: string | null;
  vehicleIdResolved: number | null;
  responseSent: string | null;
}

interface AdaptiveProfilingSession {
  id: number;
  vehicleId: number;
  adaptiveProfilingNumber: string;
  adaptiveProfilingName: string;
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
  expiresAt: string | null;
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
  const [profilingSessions, setProfilingSessions] = useState<AdaptiveProfilingSession[]>([]);
  const [gdprConsents, setGdprConsents] = useState<GdprConsent[]>([]);
  const [activeTab, setActiveTab] = useState<"profiling" | "gdpr" | "audit">("profiling");

  const loadData = useCallback(async () => {
    try {
      setLoading(true);

      // Carica sessioni ADAPTIVE_PROFILING
      const profilingResponse = await fetch(
        `${API_BASE_URL}/api/SmsAdaptiveProfiling/${vehicleId}/history`
      );
      if (profilingResponse.ok) {
        const sessions = await profilingResponse.json();
        setProfilingSessions(sessions);
      }

      // Carica audit logs
      const auditResponse = await fetch(
        `${API_BASE_URL}/api/Sms/audit-logs?pageSize=50`
      );
      if (auditResponse.ok) {
        const auditData = await auditResponse.json();
        const vehicleLogs = auditData.logs.filter(
          (log: SmsAuditLog) => log.vehicleIdResolved === vehicleId
        );
        setAuditLogs(vehicleLogs);
      }

      // Carica consensi GDPR per il Brand
      const gdprResponse = await fetch(
        `${API_BASE_URL}/api/SmsAdaptiveGdpr/consents?brand=${vehicleBrand}`
      );
      if (gdprResponse.ok) {
        const consents = await gdprResponse.json();
        setGdprConsents(consents);
      }

      logFrontendEvent(
        "AdminSmsManagement",
        "INFO",
        "SMS data loaded successfully",
        `VehicleId: ${vehicleId}, Sessions: ${profilingSessions.length}, Logs: ${auditLogs.length}`
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

  const isSessionActive = (session: AdaptiveProfilingSession) => {
    return (
      session.consentAccepted &&
      session.parsedCommand === "ADAPTIVE_PROFILING_ON" &&
      new Date(session.expiresAt) > new Date()
    );
  };

  const getRemainingTime = (expiresAt: string) => {
    const now = new Date();
    const expires = new Date(expiresAt);
    const diff = expires.getTime() - now.getTime();
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
    return diff > 0 ? `${hours}h ${minutes}m` : "Scaduto";
  };

  if (!isOpen) return null;

  const activeSessions = profilingSessions.filter(isSessionActive);

  return (
    <div className="fixed top-[64px] md:top-[0px] inset-0 z-50 flex items-center justify-center bg-black/10 backdrop-blur-sm note-modal">
      <div className="w-full h-full p-6 relative overflow-y-auto bg-softWhite dark:bg-gray-800 border border-gray-300 dark:border-gray-600 shadow-none rounded-lg md:h-auto md:w-11/12">
        {/* Header */}
        <div className="flex items-center justify-between mb-4">
          <div>
            <h2 className="text-xl font-semibold text-polarNight dark:text-softWhite mb-2">
              🔐 Gestione SMS Adaptive
            </h2>
            <p className="text-sm text-gray-600 dark:text-gray-400">
              {companyName} - {vehicleBrand} {vehicleVin}
            </p>
          </div>
        </div>

        {/* Status Veicolo */}
        <div className="mb-6 p-4 bg-gray-50 dark:bg-gray-800 rounded-lg">
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
              Veicolo {isVehicleActive ? "ATTIVO" : "INATTIVO"}
            </span>
          </div>
          {!isVehicleActive && (
            <p className="text-sm text-red-600 dark:text-red-400 mt-1">
              ⚠️ Il veicolo deve essere attivo per le procedure Adaptive
            </p>
          )}
        </div>

        {/* Sessioni Attive */}
        {activeSessions.length > 0 && (
          <div className="mb-6 p-4 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
            <div className="flex items-center space-x-2 mb-3">
              <CheckCircle className="text-blue-500" size={20} />
              <span className="font-semibold text-blue-700 dark:text-blue-300">
                {activeSessions.length} Sessione/i ADAPTIVE_PROFILING Attiva/e
              </span>
            </div>
            {activeSessions.map((session) => (
              <div key={session.id} className="mb-2 p-3 bg-white dark:bg-gray-800 rounded">
                <div className="flex items-center justify-between">
                  <div>
                    <div className="flex items-center space-x-2">
                      <User size={16} className="text-blue-600" />
                      <span className="font-semibold text-polarNight dark:text-softWhite">
                        {session.adaptiveProfilingName}
                      </span>
                      <span className="text-sm text-gray-600 dark:text-gray-400">
                        ({session.adaptiveProfilingNumber})
                      </span>
                    </div>
                    <div className="text-xs text-gray-500 mt-1">
                      Scade: {formatDate(session.expiresAt)}
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
              activeTab === "profiling"
                ? "border-b-2 text-polarNight border-polarNight dark:text-articWhite dark:border-articWhite"
                : "text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
            }`}
            onClick={() => setActiveTab("profiling")}
          >
            <User size={16} className="inline mr-2" />
            Profiling ({profilingSessions.length})
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
            Consensi GDPR ({gdprConsents.length})
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
            Audit SMS ({auditLogs.length})
          </button>
        </div>

        {/* Content */}
        <div className="min-h-[400px]">
          {/* Tab PROFILING */}
          {activeTab === "profiling" && (
            <div>
              {profilingSessions.length === 0 ? (
                <p className="text-gray-500 text-center py-8">
                  Nessuna sessione ADAPTIVE_PROFILING registrata
                </p>
              ) : (
                profilingSessions.map((session) => (
                  <div
                    key={session.id}
                    className={`p-4 rounded-lg mb-3 ${
                      isSessionActive(session)
                        ? "bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800"
                        : "bg-gray-50 dark:bg-gray-800"
                    }`}
                  >
                    <div className="flex items-center justify-between mb-2">
                      <div className="flex items-center space-x-2">
                        <User size={16} />
                        <span className="font-semibold text-polarNight dark:text-softWhite">
                          {session.adaptiveProfilingName || "Nome non specificato"}
                        </span>
                        <span className="text-sm text-gray-600 dark:text-gray-400">
                          {session.adaptiveProfilingNumber}
                        </span>
                      </div>
                      <span
                        className={`px-2 py-1 rounded text-xs font-medium ${
                          isSessionActive(session)
                            ? "bg-green-100 text-green-700 dark:bg-green-900/40 dark:text-green-300"
                            : "bg-gray-200 text-gray-700 dark:bg-gray-700 dark:text-gray-300"
                        }`}
                      >
                        {isSessionActive(session) ? "ATTIVO" : "SCADUTO"}
                      </span>
                    </div>
                    <div className="text-sm text-gray-600 dark:text-gray-400">
                      Inizio: {formatDate(session.receivedAt)}
                      <br />
                      Scadenza: {formatDate(session.expiresAt)}
                      {isSessionActive(session) && (
                        <span className="ml-2 text-green-600 dark:text-green-400 font-medium">
                          (Rimanente: {getRemainingTime(session.expiresAt)})
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
                        Consenso: {session.consentAccepted ? "✅ Attivo" : "❌ Revocato"}
                      </span>
                      <span className="text-gray-500">Comando: {session.parsedCommand}</span>
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
                <p className="text-gray-500 text-center py-8">
                  Nessun consenso GDPR registrato per {vehicleBrand}
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
                    <div className="flex items-center justify-between mb-2">
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
                        {consent.consentAccepted ? "ATTIVO" : "REVOCATO"}
                      </span>
                    </div>
                    <div className="text-sm text-gray-600 dark:text-gray-400">
                      Brand: {consent.brand}
                      <br />
                      Richiesto: {formatDate(consent.requestedAt)}
                      {consent.consentGivenAt && (
                        <>
                          <br />
                          Accettato: {formatDate(consent.consentGivenAt)}
                        </>
                      )}
                      {consent.expiresAt && (
                        <>
                          <br />
                          Scadenza: {formatDate(consent.expiresAt)}
                        </>
                      )}
                    </div>
                    <div className="mt-2 text-xs text-gray-500">
                      ID Consenso: #{consent.id}
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
                <p className="text-gray-500 text-center py-8">
                  Nessun SMS ricevuto per questo veicolo
                </p>
              ) : (
                auditLogs.map((log) => (
                  <div key={log.id} className="p-4 bg-gray-50 dark:bg-gray-800 rounded-lg mb-3">
                    <div className="flex items-center justify-between mb-2">
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
                    <div className="text-sm text-gray-600 dark:text-gray-400 mb-1">
                      📩 &quot;{log.messageBody}&quot;
                    </div>
                    {log.errorMessage && (
                      <div className="text-xs text-red-600 dark:text-red-400 mt-1">
                        ❌ {log.errorMessage}
                      </div>
                    )}
                    <div className="text-xs text-gray-500 mt-2">SID: {log.messageSid}</div>
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
        <div className="mt-6 flex justify-end">
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