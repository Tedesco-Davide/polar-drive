import { useState, useEffect, useCallback } from "react";
import { TFunction } from "i18next";
import { motion } from "framer-motion";
import {
  AlertTriangle,
  CheckCircle,
  AlertCircle,
  Info,
  FileWarning,
  ExternalLink,
  BarChart3,
  ArrowUpCircle,
  CheckCircle2,
  XCircle,
} from "lucide-react";
import { format } from "date-fns";
import { logFrontendEvent } from "@/utils/logger";
import AdminGenericPaginationControls from "@/components/adminGenericPaginationControls";
import AdminGenericLoader from "./adminGenericLoader";
import AdminGapValidationModal from "./adminModalGapValidation";

interface GapAlert {
  id: number;
  vehicleId: number;
  pdfReportId: number | null;
  alertType: string;
  severity: string;
  detectedAt: string;
  description: string;
  metricsJson: string;
  status: string;
  resolvedAt: string | null;
  resolutionNotes: string | null;
  vin: string | null;
  brand: string | null;
  companyName: string | null;
}

interface GapAlertStats {
  totalAlerts: number;
  openAlerts: number;
  escalatedAlerts: number;
  completedAlerts: number;
  contractBreachAlerts: number;
  criticalAlerts: number;
  warningAlerts: number;
  infoAlerts: number;
}

const formatDateTime = (dateTime: string): string => {
  const date = new Date(dateTime.replace("Z", ""));
  return format(date, "dd/MM/yyyy HH:mm");
};

const getSeverityColor = (severity: string): string => {
  switch (severity) {
    case "CRITICAL":
      return "bg-red-100 text-red-700 border-red-500";
    case "WARNING":
      return "bg-orange-100 text-orange-700 border-orange-500";
    case "INFO":
      return "bg-blue-100 text-blue-700 border-blue-500";
    default:
      return "bg-gray-100 text-gray-700 border-gray-500";
  }
};

const getStatusColor = (status: string): string => {
  switch (status) {
    case "OPEN":
      return "bg-yellow-100 text-yellow-700 border-yellow-500";
    case "ESCALATED":
      return "bg-orange-100 text-orange-700 border-orange-500";
    case "COMPLETED":
      return "bg-green-100 text-green-700 border-green-500";
    case "CONTRACT_BREACH":
      return "bg-red-100 text-red-700 border-red-500";
    default:
      return "bg-gray-100 text-gray-700 border-gray-500";
  }
};

const getAlertTypeLabel = (alertType: string, t: TFunction): string => {
  switch (alertType) {
    case "LOW_CONFIDENCE":
      return t("admin.gapAlerts.alertType.lowConfidence");
    case "CONSECUTIVE_GAPS":
      return t("admin.gapAlerts.alertType.consecutiveGaps");
    case "PROFILED_ANOMALY":
      return t("admin.gapAlerts.alertType.profiledAnomaly");
    case "HIGH_GAP_PERCENTAGE":
      return t("admin.gapAlerts.alertType.highGapPercentage");
    case "MONTHLY_THRESHOLD":
      return t("admin.gapAlerts.alertType.monthlyThreshold");
    default:
      return alertType;
  }
};

export default function AdminTabPolarReports({ t }: { t: TFunction }) {
  const [alerts, setAlerts] = useState<GapAlert[]>([]);
  const [stats, setStats] = useState<GapAlertStats | null>(null);
  const [loading, setLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [refreshInterval, setRefreshInterval] = useState<number>(60);

  // State per modale validazione
  const [selectedAlert, setSelectedAlert] = useState<GapAlert | null>(null);

  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [severityFilter, setSeverityFilter] = useState<string>("");
  const pageSize = 5;

  const fetchAlerts = useCallback(
    async (page: number) => {
      try {
        setLoading(true);
        const params = new URLSearchParams({
          page: page.toString(),
          pageSize: pageSize.toString(),
        });
        if (statusFilter) params.append("status", statusFilter);
        if (severityFilter) params.append("severity", severityFilter);

        const res = await fetch(`/api/gapalerts?${params}`);
        if (!res.ok) throw new Error("HTTP " + res.status);

        const data = await res.json();
        setAlerts(data.data);
        setTotalCount(data.totalCount);
        setTotalPages(data.totalPages);
        setCurrentPage(data.page);

        logFrontendEvent(
          "AdminTabPolarReports",
          "INFO",
          "Alerts loaded",
          `Page: ${data.page}, Total: ${data.totalCount}`,
        );
      } catch (err) {
        logFrontendEvent(
          "AdminTabPolarReports",
          "ERROR",
          "Failed to load alerts",
          String(err),
        );
      } finally {
        setLoading(false);
      }
    },
    [statusFilter, severityFilter],
  );

  const fetchStats = useCallback(async () => {
    try {
      const res = await fetch("/api/gapalerts/stats");
      if (!res.ok) throw new Error("HTTP " + res.status);
      const data = await res.json();
      setStats(data);
    } catch (err) {
      logFrontendEvent(
        "AdminTabPolarReports",
        "ERROR",
        "Failed to load stats",
        String(err),
      );
    }
  }, []);

  const fetchMonitoringInterval = useCallback(async () => {
    try {
      const res = await fetch("/api/gapalerts/monitoring-interval");
      if (!res.ok) throw new Error("HTTP " + res.status);
      const data = await res.json();
      setRefreshInterval(data.checkIntervalMinutes);
    } catch {
      // Use default
    }
  }, []);

  useEffect(() => {
    fetchAlerts(currentPage);
    fetchStats();
    fetchMonitoringInterval();
  }, [currentPage, fetchAlerts, fetchStats, fetchMonitoringInterval]);

  useEffect(() => {
    // Auto-refresh based on backend monitoring interval
    const interval = setInterval(
      () => {
        fetchAlerts(currentPage);
        fetchStats();
      },
      refreshInterval * 60 * 1000,
    );
    return () => clearInterval(interval);
  }, [currentPage, refreshInterval, fetchAlerts, fetchStats]);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await Promise.all([fetchAlerts(currentPage), fetchStats()]);
    setIsRefreshing(false);
  };

  // Controlla se l'alert può aprire la modale (ha pdfReportId e status non finale)
  const canOpenModal = (alert: GapAlert): boolean => {
    return (
      alert.pdfReportId !== null &&
      (alert.status === "OPEN" || alert.status === "ESCALATED")
    );
  };

  // Gestisce il clic sulla riga per aprire la modale
  const handleAlertClick = (alert: GapAlert) => {
    if (canOpenModal(alert)) {
      setSelectedAlert(alert);
    }
  };

  // Callback quando la validazione è completata
  const handleValidationComplete = async () => {
    setSelectedAlert(null);
    // Refresh dati
    await Promise.all([fetchAlerts(currentPage), fetchStats()]);
  };

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: "easeOut" }}
      className="relative bg-white dark:bg-gray-900 rounded-2xl shadow-lg border border-gray-200 dark:border-gray-700 overflow-hidden"
    >
      {(loading || isRefreshing) && <AdminGenericLoader local />}

      <div className="bg-gradient-to-r from-coldIndigo/10 via-purple-500/5 to-glacierBlue/10 dark:from-coldIndigo/20 dark:via-purple-900/10 dark:to-glacierBlue/20 px-6 py-5 border-b border-gray-200 dark:border-gray-700">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="flex items-center space-x-4">
            <div className="flex items-center space-x-3">
              <button
                onClick={handleRefresh}
                disabled={isRefreshing}
                className="p-3 bg-coldIndigo hover:bg-coldIndigo/90 text-white rounded-lg text-sm font-medium transition-all duration-200 shadow-sm hover:shadow-md disabled:opacity-50 flex items-center gap-2"
              >
                {t("admin.tableRefreshButton")}
              </button>
            </div>{" "}
            <div className="p-3 bg-gradient-to-br from-orange-400 to-red-500 rounded-xl shadow-md">
              <FileWarning size={21} className="text-white" />
            </div>
            <div>
              <h1 className="text-xl sm:text-2xl font-bold text-polarNight dark:text-softWhite">
                {t("admin.gapAlerts.dashboardCardTitle")}
              </h1>
              {stats && (
                <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">
                  {t("admin.gapAlerts.autoRefresh", {
                    minutes: refreshInterval,
                  })}
                </p>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Stats Cards */}
      {stats && (
        <div className="p-6">
          <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-8 gap-4">
            {/* Total */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-gray-50 to-gray-100 dark:from-gray-800 dark:to-gray-750 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-gray-200 dark:border-gray-700 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-gray-400 to-gray-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-gray-200 dark:bg-gray-700 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <BarChart3
                    size={20}
                    className="text-gray-600 dark:text-gray-400"
                  />
                </div>
                <div className="text-2xl font-bold text-polarNight dark:text-softWhite">
                  {stats.totalAlerts}
                </div>
                <div className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  {t("admin.gapAlerts.stats.total")}
                </div>
              </div>
            </div>

            {/* Open */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-yellow-50 to-amber-50 dark:from-yellow-900/30 dark:to-amber-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-yellow-200 dark:border-yellow-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-yellow-400 to-amber-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-yellow-200 dark:bg-yellow-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <AlertCircle
                    size={20}
                    className="text-yellow-600 dark:text-yellow-400"
                  />
                </div>
                <div className="text-2xl font-bold text-yellow-700 dark:text-yellow-400">
                  {stats.openAlerts}
                </div>
                <div className="text-xs text-yellow-600 dark:text-yellow-500 mt-1">
                  {t("admin.gapAlerts.stats.open")}
                </div>
              </div>
            </div>

            {/* Escalated */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-orange-50 to-orange-100 dark:from-orange-900/30 dark:to-orange-800/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-orange-200 dark:border-orange-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-orange-400 to-orange-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-orange-200 dark:bg-orange-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <ArrowUpCircle
                    size={20}
                    className="text-orange-600 dark:text-orange-400"
                  />
                </div>
                <div className="text-2xl font-bold text-orange-700 dark:text-orange-400">
                  {stats.escalatedAlerts}
                </div>
                <div className="text-xs text-orange-600 dark:text-orange-500 mt-1">
                  {t("admin.gapAlerts.stats.escalated")}
                </div>
              </div>
            </div>

            {/* Completed */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-green-50 to-emerald-50 dark:from-green-900/30 dark:to-emerald-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-green-200 dark:border-green-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-green-400 to-emerald-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-green-200 dark:bg-green-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <CheckCircle2
                    size={20}
                    className="text-green-600 dark:text-green-400"
                  />
                </div>
                <div className="text-2xl font-bold text-green-700 dark:text-green-400">
                  {stats.completedAlerts}
                </div>
                <div className="text-xs text-green-600 dark:text-green-500 mt-1">
                  {t("admin.gapAlerts.stats.completed")}
                </div>
              </div>
            </div>

            {/* Contract Breach */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-red-50 to-rose-50 dark:from-red-900/30 dark:to-rose-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-red-200 dark:border-red-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-red-500 to-rose-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-red-200 dark:bg-red-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <XCircle
                    size={20}
                    className="text-red-600 dark:text-red-400"
                  />
                </div>
                <div className="text-2xl font-bold text-red-700 dark:text-red-400">
                  {stats.contractBreachAlerts}
                </div>
                <div className="text-xs text-red-600 dark:text-red-500 mt-1">
                  {t("admin.gapAlerts.stats.breach")}
                </div>
              </div>
            </div>

            {/* Critical */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-red-100 to-red-200 dark:from-red-900/50 dark:to-red-800/50 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-red-300 dark:border-red-700 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-red-600 to-red-700" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-red-300 dark:bg-red-700 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <AlertTriangle
                    size={20}
                    className="text-red-700 dark:text-red-300"
                  />
                </div>
                <div className="text-2xl font-bold text-red-700 dark:text-red-300">
                  {stats.criticalAlerts}
                </div>
                <div className="text-xs text-red-600 dark:text-red-400 mt-1">
                  {t("admin.gapAlerts.stats.critical")}
                </div>
              </div>
            </div>

            {/* Warning */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-orange-100 to-amber-100 dark:from-orange-900/40 dark:to-amber-900/40 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-orange-300 dark:border-orange-700 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-orange-500 to-amber-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-orange-300 dark:bg-orange-700 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <AlertCircle
                    size={20}
                    className="text-orange-700 dark:text-orange-300"
                  />
                </div>
                <div className="text-2xl font-bold text-orange-700 dark:text-orange-300">
                  {stats.warningAlerts}
                </div>
                <div className="text-xs text-orange-600 dark:text-orange-400 mt-1">
                  {t("admin.gapAlerts.stats.warning")}
                </div>
              </div>
            </div>

            {/* Info */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-blue-50 to-indigo-50 dark:from-blue-900/30 dark:to-indigo-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-blue-200 dark:border-blue-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-blue-400 to-indigo-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-blue-200 dark:bg-blue-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <Info
                    size={20}
                    className="text-blue-600 dark:text-blue-400"
                  />
                </div>
                <div className="text-2xl font-bold text-blue-700 dark:text-blue-400">
                  {stats.infoAlerts}
                </div>
                <div className="text-xs text-blue-600 dark:text-blue-500 mt-1">
                  {t("admin.gapAlerts.stats.info")}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Filters */}
      <div className="px-6 py-4 bg-gray-50 dark:bg-gray-800/50 border-y border-gray-200 dark:border-gray-700">
        <div className="flex flex-wrap items-center gap-4">
          <span className="text-sm font-medium text-gray-600 dark:text-gray-400">
            {t("admin.gapAlerts.filters.label") || "Filtri:"}
          </span>
          <select
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setCurrentPage(1);
            }}
            className="px-4 py-2 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg text-sm focus:ring-2 focus:ring-coldIndigo/50 focus:border-coldIndigo transition-all"
          >
            <option value="">{t("admin.gapAlerts.filters.allStatus")}</option>
            <option value="OPEN">OPEN</option>
            <option value="ESCALATED">ESCALATED</option>
            <option value="COMPLETED">COMPLETED</option>
            <option value="CONTRACT_BREACH">CONTRACT_BREACH</option>
          </select>
          <select
            value={severityFilter}
            onChange={(e) => {
              setSeverityFilter(e.target.value);
              setCurrentPage(1);
            }}
            className="px-4 py-2 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg text-sm focus:ring-2 focus:ring-coldIndigo/50 focus:border-coldIndigo transition-all"
          >
            <option value="">{t("admin.gapAlerts.filters.allSeverity")}</option>
            <option value="CRITICAL">CRITICAL</option>
            <option value="WARNING">WARNING</option>
            <option value="INFO">INFO</option>
          </select>
        </div>
      </div>

      {/* Alerts Table */}
      <div className="p-6 overflow-x-auto">
        <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
          <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
            <tr>
              <th className="p-3">{t("admin.gapAlerts.table.severity")}</th>
              <th className="p-3">{t("admin.gapAlerts.table.type")}</th>
              <th className="p-3">{t("admin.gapAlerts.table.vehicle")}</th>
              <th className="p-3">{t("admin.gapAlerts.table.detected")}</th>
              <th className="p-3">{t("admin.gapAlerts.table.status")}</th>
              <th className="p-3">{t("admin.gapAlerts.table.description")}</th>
              <th className="p-3"></th>
            </tr>
          </thead>
          <tbody>
            {alerts.length === 0 ? (
              <tr>
                <td colSpan={7} className="p-8 text-center text-gray-500">
                  <CheckCircle
                    size={32}
                    className="mx-auto mb-2 text-green-500"
                  />
                  {t("admin.gapAlerts.noAlerts")}
                </td>
              </tr>
            ) : (
              alerts.map((alert) => {
                const isClickable = canOpenModal(alert);
                return (
                  <tr
                    key={alert.id}
                    onClick={() => handleAlertClick(alert)}
                    className={`border-b border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800 ${
                      isClickable ? "cursor-pointer" : ""
                    }`}
                    title={
                      isClickable
                        ? t("admin.gapAlerts.clickToValidate")
                        : undefined
                    }
                  >
                    <td className="px-3 py-3">
                      <span
                        className={`px-2 py-1 text-xs rounded-full border ${getSeverityColor(alert.severity)}`}
                      >
                        {alert.severity}
                      </span>
                    </td>
                    <td className="px-3 py-3">
                      <div className="font-medium">
                        {getAlertTypeLabel(alert.alertType, t)}
                      </div>
                      <div className="text-xs text-gray-400">ID {alert.id}</div>
                    </td>
                    <td className="px-3 py-3">
                      <div className="font-mono text-xs">
                        {alert.vin || "N/A"}
                      </div>
                      <div className="text-xs text-gray-500">{alert.brand}</div>
                      <div className="text-xs text-gray-400">
                        {alert.companyName}
                      </div>
                    </td>
                    <td className="px-3 py-3">
                      <div className="text-sm">
                        {formatDateTime(alert.detectedAt)}
                      </div>
                      {alert.resolvedAt && (
                        <div className="text-xs text-green-600">
                          {t("admin.gapAlerts.resolved", {
                            date: formatDateTime(alert.resolvedAt),
                          })}
                        </div>
                      )}
                    </td>
                    <td className="px-3 py-3">
                      <span
                        className={`px-2 py-1 text-xs rounded-full border ${getStatusColor(alert.status)}`}
                      >
                        {alert.status}
                      </span>
                    </td>
                    <td className="px-3 py-3">
                      <div
                        className="max-w-md truncate text-sm"
                        title={alert.description}
                      >
                        {alert.description}
                      </div>
                    </td>
                    <td className="px-3 py-3">
                      {isClickable && (
                        <ExternalLink size={16} className="text-purple-500" />
                      )}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>

        {/* Pagination */}
        <div className="mt-4">
          <AdminGenericPaginationControls
            currentPage={currentPage}
            totalPages={totalPages}
            onPrev={() => setCurrentPage((p) => Math.max(1, p - 1))}
            onNext={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
          />
        </div>
      </div>

      {/* Modale Validazione Gap */}
      {selectedAlert && selectedAlert.pdfReportId && (
        <AdminGapValidationModal
          reportId={selectedAlert.pdfReportId}
          isOpen={true}
          onClose={() => setSelectedAlert(null)}
          onValidationComplete={handleValidationComplete}
          t={t}
          gapValidationStatus={
            selectedAlert.status === "ESCALATED" ? "ESCALATED" : null
          }
        />
      )}
    </motion.div>
  );
}
