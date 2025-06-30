import { useEffect, useState } from "react";
import { useTranslation } from "next-i18next";
import {
  MessageSquare,
  Phone,
  Trash2,
  CheckCircle,
  AlertCircle,
} from "lucide-react";
import { API_BASE_URL } from "@/utils/api";
import { logFrontendEvent } from "@/utils/logger";

interface PhoneMapping {
  id: number;
  phoneNumber: string;
  vehicleId: number;
  vehicleVin: string;
  companyName: string;
  createdAt: string;
  updatedAt: string;
  notes: string | null;
}

interface SmsAuditLog {
  id: number;
  messageSid: string;
  fromPhoneNumber: string;
  messageBody: string;
  receivedAt: string;
  processingStatus: string;
  errorMessage: string | null;
  vehicleIdResolved: number | null;
  responseStatus: string | null;
}

interface AdminSmsManagementModalProps {
  isOpen: boolean;
  onClose: () => void;
  vehicleId: number;
  vehicleVin: string;
  companyName: string;
  isVehicleActive: boolean;
}

export default function AdminSmsManagementModal({
  isOpen,
  onClose,
  vehicleId,
  vehicleVin,
  companyName,
  isVehicleActive,
}: AdminSmsManagementModalProps) {
  const { t } = useTranslation("");
  const [loading, setLoading] = useState(false);
  const [phoneMappings, setPhoneMappings] = useState<PhoneMapping[]>([]);
  const [auditLogs, setAuditLogs] = useState<SmsAuditLog[]>([]);
  const [newPhoneNumber, setNewPhoneNumber] = useState("");
  const [newPhoneNotes, setNewPhoneNotes] = useState("");
  const [activeTab, setActiveTab] = useState<"mappings" | "audit">("mappings");

  const [adaptiveStatus, setAdaptiveStatus] = useState({
    isActive: false,
    sessionStartedAt: null as string | null,
    sessionEndTime: null as string | null,
    remainingMinutes: 0,
    description: null as string | null,
  });

  useEffect(() => {
    if (isOpen) {
      loadData();
      loadAdaptiveStatus();
    }
  }, [isOpen, vehicleId]);

  const loadData = async () => {
    try {
      setLoading(true);

      // Carica mappature telefoni
      const mappingsResponse = await fetch(
        `${API_BASE_URL}/api/TwilioSms/phone-mappings`
      );
      if (mappingsResponse.ok) {
        const allMappings = await mappingsResponse.json();
        // Filtra solo per questo veicolo
        const vehicleMappings = allMappings.filter(
          (m: PhoneMapping) => m.vehicleId === vehicleId
        );
        setPhoneMappings(vehicleMappings);
      }

      // Carica audit logs
      const auditResponse = await fetch(
        `${API_BASE_URL}/api/TwilioSms/audit-logs?pageSize=20`
      );
      if (auditResponse.ok) {
        const auditData = await auditResponse.json();
        // Filtra logs per questo veicolo
        const vehicleLogs = auditData.logs.filter(
          (log: SmsAuditLog) => log.vehicleIdResolved === vehicleId
        );
        setAuditLogs(vehicleLogs);
      }

      logFrontendEvent(
        "AdminSmsManagement",
        "INFO",
        "SMS data loaded successfully",
        `VehicleId: ${vehicleId}, Mappings: ${phoneMappings.length}, Logs: ${auditLogs.length}`
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
  };

  const loadAdaptiveStatus = async () => {
    try {
      const response = await fetch(
        `${API_BASE_URL}/api/AdaptiveProfilingSms/${vehicleId}/status`
      );
      if (response.ok) {
        const status = await response.json();
        setAdaptiveStatus(status);
      }
    } catch (error) {
      console.error("Error loading adaptive status:", error);
    }
  };

  const handleAddPhoneMapping = async () => {
    if (!newPhoneNumber.trim()) {
      alert("Inserisci un numero di telefono valido");
      return;
    }

    // Validazione numero telefono italiano
    const phoneRegex = /^(\+39)?[0-9]{10}$/;
    if (!phoneRegex.test(newPhoneNumber.replace(/\s/g, ""))) {
      alert(
        "Formato numero non valido. Usa formato: +393401234567 o 3401234567"
      );
      return;
    }

    try {
      setLoading(true);

      const response = await fetch(
        `${API_BASE_URL}/api/TwilioSms/register-phone`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            phoneNumber: newPhoneNumber,
            vehicleId: vehicleId,
            notes: newPhoneNotes || `Associato via admin per ${companyName}`,
          }),
        }
      );

      if (response.ok) {
        const result = await response.json();
        alert(
          `✅ Numero ${result.phoneNumber} associato con successo al veicolo ${vehicleVin}`
        );
        setNewPhoneNumber("");
        setNewPhoneNotes("");
        await loadData(); // Ricarica i dati

        logFrontendEvent(
          "AdminSmsManagement",
          "INFO",
          "Phone mapping created successfully",
          `Phone: ${result.phoneNumber}, VehicleId: ${vehicleId}`
        );
      } else {
        const error = await response.json();
        alert(`❌ Errore: ${error.error || "Impossibile associare il numero"}`);
      }
    } catch (error) {
      alert(`❌ Errore di connessione: ${error}`);
      logFrontendEvent(
        "AdminSmsManagement",
        "ERROR",
        "Failed to create phone mapping",
        error instanceof Error ? error.message : String(error)
      );
    } finally {
      setLoading(false);
    }
  };

  const handleDeletePhoneMapping = async (
    mappingId: number,
    phoneNumber: string
  ) => {
    if (
      !confirm(
        `Sei sicuro di voler eliminare l'associazione con ${phoneNumber}?`
      )
    ) {
      return;
    }

    try {
      setLoading(true);

      // Note: Assumo che esista un endpoint DELETE per le mappature
      const response = await fetch(
        `${API_BASE_URL}/api/TwilioSms/phone-mappings/${mappingId}`,
        {
          method: "DELETE",
        }
      );

      if (response.ok) {
        alert(`✅ Associazione con ${phoneNumber} eliminata`);
        await loadData();

        logFrontendEvent(
          "AdminSmsManagement",
          "INFO",
          "Phone mapping deleted successfully",
          `Phone: ${phoneNumber}, MappingId: ${mappingId}`
        );
      } else {
        alert("❌ Errore durante l'eliminazione");
      }
    } catch (error) {
      alert(`❌ Errore: ${error}`);
    } finally {
      setLoading(false);
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString("it-IT");
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case "SUCCESS":
        return "text-green-600 bg-green-100";
      case "ERROR":
        return "text-red-600 bg-red-100";
      case "REJECTED":
        return "text-orange-600 bg-orange-100";
      default:
        return "text-gray-600 bg-gray-100";
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed top-[64px] md:top-[0px] inset-0 z-50 flex items-center justify-center bg-black/10 backdrop-blur-sm note-modal">
      <div className="w-full h-full p-6 relative overflow-y-auto bg-softWhite dark:bg-gray-800 border border-gray-300 dark:border-gray-600 shadow-none rounded-lg md:h-auto md:w-11/12">
        {/* Header */}
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center space-x-3">
            <div>
              <h2 className="whitespace-normal text-xl font-semibold text-polarNight dark:text-softWhite mb-4">
                Gestione SMS
              </h2>
              <p className="text-sm text-gray-600 dark:text-gray-400">
                {companyName} - {vehicleVin}
              </p>
            </div>
          </div>
        </div>

        {/* Status Adaptive Profiling */}
        {adaptiveStatus.isActive && (
          <div className="mb-6 p-4 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
            <div className="flex items-center space-x-2 mb-2">
              <CheckCircle className="text-blue-500" size={16} />
              <span className="font-semibold text-blue-700 dark:text-blue-300">
                🔄 Adaptive Profiling ATTIVO
              </span>
            </div>
            <p className="text-sm text-blue-600 dark:text-blue-400">
              Sessione attiva fino a{" "}
              {adaptiveStatus.sessionEndTime
                ? new Date(adaptiveStatus.sessionEndTime).toLocaleString(
                    "it-IT"
                  )
                : "N/A"}
              <br />
              Tempo rimanente: {adaptiveStatus.remainingMinutes} minuti
            </p>
          </div>
        )}

        {/* Status Veicolo */}
        <div className="mb-6 bg-gray-50 dark:bg-gray-800 rounded-lg">
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
              ⚠️ Il veicolo deve essere attivo per ricevere SMS
            </p>
          )}
        </div>

        {/* Tabs */}
        <div className="flex mb-4 border-y border-gray-300 dark:border-gray-600">
          <button
            className={`px-4 py-2 font-medium ${
              activeTab === "mappings"
                ? "border-b-2 text-polarNight border-polarNight dark:text-articWhite  dark:border-articWhite"
                : "text-gray-500 hover:text-gray-700"
            }`}
            onClick={() => setActiveTab("mappings")}
          >
            <Phone size={16} className="inline mr-2" />
            Numeri Associati ( {phoneMappings.length} )
          </button>
          <button
            className={`px-4 py-2 font-medium ml-4 ${
              activeTab === "audit"
                ? "border-b-2 text-polarNight border-polarNight dark:text-articWhite  dark:border-articWhite"
                : "text-gray-500 hover:text-gray-700"
            }`}
            onClick={() => setActiveTab("audit")}
          >
            <MessageSquare size={16} className="inline mr-2" />
            SMS Ricevuti ( {auditLogs.length} )
          </button>
        </div>

        {/* Content */}
        {activeTab === "mappings" && (
          <div>
            {/* Form Aggiunta Numero */}
            <div className="p-4 bg-blue-50 dark:bg-blue-900/20 rounded-lg">
              <h3 className="font-semibold mb-3 text-polarNight dark:text-softWhite">
                Associa Nuovo Numero
              </h3>
              <div className="flex items-center gap-3">
                <button
                  onClick={handleAddPhoneMapping}
                  disabled={loading || !isVehicleActive}
                  className={`px-4 py-2 rounded text-white font-bold ${
                    !isVehicleActive
                      ? "bg-gray-400 cursor-not-allowed opacity-50"
                      : "bg-blue-500 hover:bg-blue-600"
                  }`}
                >
                  Associa
                </button>
                <input
                  type="tel"
                  placeholder="+393401234567"
                  value={newPhoneNumber}
                  onChange={(e) => setNewPhoneNumber(e.target.value)}
                  className="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-polarNight dark:text-softWhite"
                  disabled={!isVehicleActive}
                />
                <input
                  type="text"
                  placeholder="Note opzionali"
                  value={newPhoneNotes}
                  onChange={(e) => setNewPhoneNotes(e.target.value)}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-polarNight dark:text-softWhite"
                  disabled={!isVehicleActive}
                />
              </div>
              {!isVehicleActive && (
                <p className="text-sm text-orange-600 dark:text-orange-400 mt-2">
                  ⚠️ Attiva il veicolo per poter associare numeri SMS
                </p>
              )}
            </div>

            {/* Lista Numeri */}
            <div className="space-y-3">
              {phoneMappings.length === 0 ? (
                <p className="text-gray-500 text-start py-4">
                  Nessun numero associato a questo veicolo
                </p>
              ) : (
                phoneMappings.map((mapping) => (
                  <div
                    key={mapping.id}
                    className="flex items-center py-4 bg-gray-50 dark:bg-gray-800 rounded-lg space-x-4"
                  >
                    <button
                      className="p-2 bg-red-500 text-softWhite rounded hover:bg-red-600"
                      title={t("admin.filemanager.deleteJob")}
                      disabled={loading}
                      onClick={() =>
                        handleDeletePhoneMapping(
                          mapping.id,
                          mapping.phoneNumber
                        )
                      }
                    >
                      <Trash2 size={16} />
                    </button>
                    <div>
                      <div className="font-semibold text-polarNight dark:text-softWhite">
                        {mapping.phoneNumber}
                      </div>
                      <div className="text-sm text-gray-600 dark:text-gray-400">
                        Creato: {formatDate(mapping.createdAt)}
                        {mapping.notes && (
                          <span className="ml-2">• {mapping.notes}</span>
                        )}
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
            <button
              className="bg-gray-400 text-white px-6 py-2 rounded hover:bg-gray-500"
              onClick={() => {
                logFrontendEvent(
                  "SmsModal",
                  "INFO",
                  "Sms modal closed without saving"
                );
                onClose();
              }}
            >
              {t("admin.cancelEditRow")}
            </button>
          </div>
        )}

        {activeTab === "audit" && (
          <div className="space-y-3">
            {auditLogs.length === 0 ? (
              <p className="text-gray-500 text-start py-4">
                Nessun SMS ricevuto per questo veicolo
              </p>
            ) : (
              auditLogs.map((log) => (
                <div
                  key={log.id}
                  className="p-4 bg-gray-50 dark:bg-gray-800 rounded-lg"
                >
                  <div className="flex items-center justify-between mb-2">
                    <div className="font-semibold text-polarNight dark:text-softWhite">
                      {log.fromPhoneNumber}
                    </div>
                    <span
                      className={`px-2 py-1 rounded text-xs font-medium ${getStatusColor(
                        log.processingStatus
                      )}`}
                    >
                      {log.processingStatus}
                    </span>
                  </div>
                  <div className="text-sm text-gray-600 dark:text-gray-400 mb-1">
                    Messaggio: &quot;{log.messageBody}&quot;
                  </div>
                  <div className="text-xs text-gray-500">
                    {formatDate(log.receivedAt)} • ID: {log.messageSid}
                  </div>
                  {log.errorMessage && (
                    <div className="text-xs text-red-500 mt-1">
                      Errore: {log.errorMessage}
                    </div>
                  )}
                </div>
              ))
            )}
          </div>
        )}

        {/* Loading */}
        {loading && (
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
          </div>
        )}
      </div>
    </div>
  );
}
