import React, { useState, useEffect, useCallback } from "react";
import { TFunction } from "i18next";
import { AlertTriangle, CheckCircle, AlertCircle, Info, FileWarning, ExternalLink } from "lucide-react";
import { format } from "date-fns";
import { logFrontendEvent } from "@/utils/logger";
import PaginationControls from "@/components/paginationControls";
import AdminLoader from "./adminLoader";
import GapValidationModal from "./gapValidationModal";

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

const getSeverityIcon = (severity: string) => {
  switch (severity) {
    case "CRITICAL":
      return <AlertTriangle size={18} className="text-red-600" />;
    case "WARNING":
      return <AlertCircle size={18} className="text-orange-600" />;
    case "INFO":
      return <Info size={18} className="text-blue-600" />;
    default:
      return <Info size={18} className="text-gray-600" />;
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

export default function AdminGapAlertsDashboard({ t }: { t: TFunction }) {
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
  const pageSize = 10;

  const fetchAlerts = useCallback(async (page: number) => {
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
        "AdminGapAlertsDashboard",
        "INFO",
        "Alerts loaded",
        `Page: ${data.page}, Total: ${data.totalCount}`
      );
    } catch (err) {
      logFrontendEvent(
        "AdminGapAlertsDashboard",
        "ERROR",
        "Failed to load alerts",
        String(err)
      );
    } finally {
      setLoading(false);
    }
  }, [statusFilter, severityFilter]);

  const fetchStats = useCallback(async () => {
    try {
      const res = await fetch("/api/gapalerts/stats");
      if (!res.ok) throw new Error("HTTP " + res.status);
      const data = await res.json();
      setStats(data);
    } catch (err) {
      logFrontendEvent(
        "AdminGapAlertsDashboard",
        "ERROR",
        "Failed to load stats",
        String(err)
      );
    }
  }, []);

  const fetchMonitoringInterval = useCallback(async () => {
    try {
      const res = await fetch("/api/gapalerts/monitoring-interval");
      if (!res.ok) throw new Error("HTTP " + res.status);
      const data = await res.json();
      setRefreshInterval(data.checkIntervalMinutes || 60);
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
    const interval = setInterval(() => {
      fetchAlerts(currentPage);
      fetchStats();
    }, refreshInterval * 60 * 1000);
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
    <div className="relative">
      {(loading || isRefreshing) && <AdminLoader local />}

      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center space-x-3">
          <FileWarning size={28} className="text-orange-500" />
          <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
            {t("admin.gapAlerts.dashboardTitle")}
          </h1>
          {stats && (
            <span className="px-3 py-1 text-sm font-medium bg-orange-100 text-orange-700 rounded-full">
              {t("admin.gapAlerts.openCount", { count: stats.openAlerts })}
            </span>
          )}
        </div>
        <button
          onClick={handleRefresh}
          disabled={isRefreshing}
          className="px-4 py-2 bg-blue-500 text-white rounded text-sm hover:bg-blue-600 disabled:opacity-50"
        >
          {t("admin.tableRefreshButton")}
        </button>
      </div>

      {/* Stats Cards */}
      {stats && (
        <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-8 gap-3 mb-6">
          <div className="bg-gray-100 dark:bg-gray-800 p-3 rounded-lg text-center">
            <div className="text-2xl font-bold">{stats.totalAlerts}</div>
            <div className="text-xs text-gray-500">{t("admin.gapAlerts.stats.total")}</div>
          </div>
          <div className="bg-yellow-100 dark:bg-yellow-900 p-3 rounded-lg text-center">
            <div className="text-2xl font-bold text-yellow-700">{stats.openAlerts}</div>
            <div className="text-xs text-yellow-600">{t("admin.gapAlerts.stats.open")}</div>
          </div>
          <div className="bg-orange-100 dark:bg-orange-900 p-3 rounded-lg text-center">
            <div className="text-2xl font-bold text-orange-700">{stats.escalatedAlerts}</div>
            <div className="text-xs text-orange-600">{t("admin.gapAlerts.stats.escalated")}</div>
          </div>
          <div className="bg-green-100 dark:bg-green-900 p-3 rounded-lg text-center">
            <div className="text-2xl font-bold text-green-700">{stats.completedAlerts}</div>
            <div className="text-xs text-green-600">{t("admin.gapAlerts.stats.completed")}</div>
          </div>
          <div className="bg-red-100 dark:bg-red-900 p-3 rounded-lg text-center">
            <div className="text-2xl font-bold text-red-700">{stats.contractBreachAlerts}</div>
            <div className="text-xs text-red-600">{t("admin.gapAlerts.stats.breach")}</div>
          </div>
          <div className="bg-red-50 dark:bg-red-950 p-3 rounded-lg text-center border-l-4 border-red-500">
            <div className="text-2xl font-bold text-red-600">{stats.criticalAlerts}</div>
            <div className="text-xs text-red-500">{t("admin.gapAlerts.stats.critical")}</div>
          </div>
          <div className="bg-orange-50 dark:bg-orange-950 p-3 rounded-lg text-center border-l-4 border-orange-500">
            <div className="text-2xl font-bold text-orange-600">{stats.warningAlerts}</div>
            <div className="text-xs text-orange-500">{t("admin.gapAlerts.stats.warning")}</div>
          </div>
          <div className="bg-blue-50 dark:bg-blue-950 p-3 rounded-lg text-center border-l-4 border-blue-500">
            <div className="text-2xl font-bold text-blue-600">{stats.infoAlerts}</div>
            <div className="text-xs text-blue-500">{t("admin.gapAlerts.stats.info")}</div>
          </div>
        </div>
      )}

      {/* Filters */}
      <div className="flex flex-wrap gap-3 mb-4">
        <select
          value={statusFilter}
          onChange={(e) => { setStatusFilter(e.target.value); setCurrentPage(1); }}
          className="px-3 py-2 border rounded dark:bg-gray-800 dark:border-gray-600"
        >
          <option value="">{t("admin.gapAlerts.filters.allStatus")}</option>
          <option value="OPEN">OPEN</option>
          <option value="ESCALATED">ESCALATED</option>
          <option value="COMPLETED">COMPLETED</option>
          <option value="CONTRACT_BREACH">CONTRACT_BREACH</option>
        </select>
        <select
          value={severityFilter}
          onChange={(e) => { setSeverityFilter(e.target.value); setCurrentPage(1); }}
          className="px-3 py-2 border rounded dark:bg-gray-800 dark:border-gray-600"
        >
          <option value="">{t("admin.gapAlerts.filters.allSeverity")}</option>
          <option value="CRITICAL">CRITICAL</option>
          <option value="WARNING">WARNING</option>
          <option value="INFO">INFO</option>
        </select>
      </div>

      {/* Alerts Table */}
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
                <CheckCircle size={32} className="mx-auto mb-2 text-green-500" />
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
                  title={isClickable ? t("admin.gapAlerts.clickToValidate") : undefined}
                >
                  <td className="px-3 py-3">
                    <div className="flex items-center space-x-2">
                      {getSeverityIcon(alert.severity)}
                      <span className={`px-2 py-1 text-xs rounded-full border ${getSeverityColor(alert.severity)}`}>
                        {alert.severity}
                      </span>
                    </div>
                  </td>
                  <td className="px-3 py-3">
                    <div className="font-medium">{getAlertTypeLabel(alert.alertType, t)}</div>
                    <div className="text-xs text-gray-400">ID {alert.id}</div>
                  </td>
                  <td className="px-3 py-3">
                    <div className="font-mono text-xs">{alert.vin || "N/A"}</div>
                    <div className="text-xs text-gray-500">{alert.brand}</div>
                    <div className="text-xs text-gray-400">{alert.companyName}</div>
                  </td>
                  <td className="px-3 py-3">
                    <div className="text-sm">{formatDateTime(alert.detectedAt)}</div>
                    {alert.resolvedAt && (
                      <div className="text-xs text-green-600">
                        {t("admin.gapAlerts.resolved", { date: formatDateTime(alert.resolvedAt) })}
                      </div>
                    )}
                  </td>
                  <td className="px-3 py-3">
                    <span className={`px-2 py-1 text-xs rounded-full border ${getStatusColor(alert.status)}`}>
                      {alert.status}
                    </span>
                  </td>
                  <td className="px-3 py-3">
                    <div className="max-w-md truncate text-sm" title={alert.description}>
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
        <PaginationControls
          currentPage={currentPage}
          totalPages={totalPages}
          onPrev={() => setCurrentPage((p) => Math.max(1, p - 1))}
          onNext={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
        />
      </div>

      {/* Auto-refresh info */}
      <div className="mt-4 text-xs text-gray-400 text-center">
        {t("admin.gapAlerts.autoRefresh", { minutes: refreshInterval })}
      </div>

      {/* Modale Validazione Gap */}
      {selectedAlert && selectedAlert.pdfReportId && (
        <GapValidationModal
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
    </div>
  );
}
