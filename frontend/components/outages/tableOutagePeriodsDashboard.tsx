import { useState, useEffect, useCallback } from "react";
import { TFunction } from "i18next";
import {
  AlertTriangle,
  CheckCircle2,
  BarChart3,
  Radar,
  PenTool,
  Car,
  Server,
  Clock,
  Activity,
} from "lucide-react";
import { logFrontendEvent } from "@/utils/logger";
import Loader from "../generic/loader";

interface OutageStats {
  totalOutages: number;
  ongoingOutages: number;
  resolvedOutages: number;
  autoDetectedCount: number;
  manualCount: number;
  vehicleOutages: number;
  fleetApiOutages: number;
  avgOutageDurationMinutes: number;
  totalDowntimeMinutes: number;
}

const formatDuration = (minutes: number): string => {
  if (minutes === 0) return "0m";
  if (minutes < 60) return `${Math.round(minutes)}m`;
  const hours = Math.floor(minutes / 60);
  const mins = Math.round(minutes % 60);
  if (hours < 24) return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
  const days = Math.floor(hours / 24);
  const remainingHours = hours % 24;
  return remainingHours > 0 ? `${days}g ${remainingHours}h` : `${days}g`;
};

export default function DashboardOutagePeriods({ t }: { t: TFunction }) {
  const [stats, setStats] = useState<OutageStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const refreshInterval = 60;

  const fetchStats = useCallback(async () => {
    try {
      setError(null);
      const res = await fetch("/api/outagesystem/stats");
      if (!res.ok) {
        const errorText = await res.text();
        throw new Error(`HTTP ${res.status}: ${errorText}`);
      }
      const data = await res.json();

      setStats({
        totalOutages: data.totalOutages ?? 0,
        ongoingOutages: data.ongoingOutages ?? 0,
        resolvedOutages: data.resolvedOutages ?? 0,
        autoDetectedCount: data.autoDetectedCount ?? 0,
        manualCount: data.manualCount ?? 0,
        vehicleOutages: data.vehicleOutages ?? 0,
        fleetApiOutages: data.fleetApiOutages ?? 0,
        avgOutageDurationMinutes: data.avgOutageDurationMinutes ?? 0,
        totalDowntimeMinutes: data.totalDowntimeMinutes ?? 0,
      });

      logFrontendEvent(
        "DashboardOutagePeriods",
        "INFO",
        "Stats loaded",
        `Total: ${data.totalOutages}, Ongoing: ${data.ongoingOutages}`,
      );
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : String(err);
      setError(errorMsg);
      logFrontendEvent(
        "DashboardOutagePeriods",
        "ERROR",
        "Failed to load stats",
        errorMsg,
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchStats();
  }, [fetchStats]);

  useEffect(() => {
    const interval = setInterval(() => {
      fetchStats();
    }, refreshInterval * 1000);
    return () => clearInterval(interval);
  }, [fetchStats]);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchStats();
    setIsRefreshing(false);
  };

  return (
    <div className="relative bg-white dark:bg-gray-900 rounded-2xl shadow-lg border border-gray-200 dark:border-gray-700 overflow-hidden min-h-[200px]">
      {(loading || isRefreshing) && <Loader local />}

      <div className="bg-gradient-to-r from-coldIndigo/10 via-purple-500/5 to-glacierBlue/10 dark:from-coldIndigo/20 dark:via-purple-900/10 dark:to-glacierBlue/20 px-6 py-5 border-b border-gray-200 dark:border-gray-700">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="flex items-center space-x-4">
            <div className="flex items-center space-x-3">
              <button
                onClick={handleRefresh}
                disabled={isRefreshing || loading}
                className="p-3 bg-blue-500 hover:bg-blue-600 text-white rounded-lg text-sm font-medium transition-all duration-200 shadow-sm hover:shadow-md disabled:opacity-50"
              >
                {t("admin.tableRefreshButton")}
              </button>
            </div>
            <div className="p-3 bg-gradient-to-br from-orange-400 to-red-500 rounded-xl shadow-md">
              <Activity size={21} className="text-white" />
            </div>
            <div>
              <h1 className="text-xl sm:text-2xl font-bold text-polarNight dark:text-softWhite">
                {t("admin.outageDashboard.title")}
              </h1>
              <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">
                {t("admin.outageDashboard.autoRefresh", {
                  seconds: refreshInterval,
                })}
              </p>
            </div>
          </div>
        </div>
      </div>

      {stats ? (
        <div className="p-6">
          <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-8 gap-4">
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
                  {stats.totalOutages}
                </div>
                <div className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  {t("admin.outageDashboard.stats.total")}
                </div>
              </div>
            </div>

            <div className="group relative overflow-hidden bg-gradient-to-br from-red-50 to-rose-50 dark:from-red-900/30 dark:to-rose-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-red-200 dark:border-red-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-red-500 to-rose-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-red-200 dark:bg-red-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <AlertTriangle
                    size={20}
                    className="text-red-600 dark:text-red-400"
                  />
                </div>
                <div className="text-2xl font-bold text-red-700 dark:text-red-400">
                  {stats.ongoingOutages}
                </div>
                <div className="text-xs text-red-600 dark:text-red-500 mt-1">
                  {t("admin.outageDashboard.stats.ongoing")}
                </div>
              </div>
            </div>

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
                  {stats.resolvedOutages}
                </div>
                <div className="text-xs text-green-600 dark:text-green-500 mt-1">
                  {t("admin.outageDashboard.stats.resolved")}
                </div>
              </div>
            </div>

            <div className="group relative overflow-hidden bg-gradient-to-br from-purple-50 to-violet-50 dark:from-purple-900/30 dark:to-violet-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-purple-200 dark:border-purple-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-purple-400 to-violet-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-purple-200 dark:bg-purple-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <Radar
                    size={20}
                    className="text-purple-600 dark:text-purple-400"
                  />
                </div>
                <div className="text-2xl font-bold text-purple-700 dark:text-purple-400">
                  {stats.autoDetectedCount}
                </div>
                <div className="text-xs text-purple-600 dark:text-purple-500 mt-1">
                  {t("admin.outageDashboard.stats.autoDetected")}
                </div>
              </div>
            </div>

            <div className="group relative overflow-hidden bg-gradient-to-br from-blue-50 to-indigo-50 dark:from-blue-900/30 dark:to-indigo-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-blue-200 dark:border-blue-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-blue-400 to-indigo-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-blue-200 dark:bg-blue-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <PenTool
                    size={20}
                    className="text-blue-600 dark:text-blue-400"
                  />
                </div>
                <div className="text-2xl font-bold text-blue-700 dark:text-blue-400">
                  {stats.manualCount}
                </div>
                <div className="text-xs text-blue-600 dark:text-blue-500 mt-1">
                  {t("admin.outageDashboard.stats.manual")}
                </div>
              </div>
            </div>

            <div className="group relative overflow-hidden bg-gradient-to-br from-orange-50 to-amber-50 dark:from-orange-900/30 dark:to-amber-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-orange-200 dark:border-orange-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-orange-400 to-amber-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-orange-200 dark:bg-orange-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <Car
                    size={20}
                    className="text-orange-600 dark:text-orange-400"
                  />
                </div>
                <div className="text-2xl font-bold text-orange-700 dark:text-orange-400">
                  {stats.vehicleOutages}
                </div>
                <div className="text-xs text-orange-600 dark:text-orange-500 mt-1">
                  {t("admin.outageDashboard.stats.vehicle")}
                </div>
              </div>
            </div>

            <div className="group relative overflow-hidden bg-gradient-to-br from-cyan-50 to-sky-50 dark:from-cyan-900/30 dark:to-sky-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-cyan-200 dark:border-cyan-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-cyan-400 to-sky-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-cyan-200 dark:bg-cyan-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <Server
                    size={20}
                    className="text-cyan-600 dark:text-cyan-400"
                  />
                </div>
                <div className="text-2xl font-bold text-cyan-700 dark:text-cyan-400">
                  {stats.fleetApiOutages}
                </div>
                <div className="text-xs text-cyan-600 dark:text-cyan-500 mt-1">
                  {t("admin.outageDashboard.stats.fleetApi")}
                </div>
              </div>
            </div>

            <div className="group relative overflow-hidden bg-gradient-to-br from-yellow-50 to-amber-50 dark:from-yellow-900/30 dark:to-amber-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-yellow-200 dark:border-yellow-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-yellow-400 to-amber-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-yellow-200 dark:bg-yellow-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <Clock
                    size={20}
                    className="text-yellow-600 dark:text-yellow-400"
                  />
                </div>
                <div className="text-2xl font-bold text-yellow-700 dark:text-yellow-400">
                  {formatDuration(stats.avgOutageDurationMinutes)}
                </div>
                <div className="text-xs text-yellow-600 dark:text-yellow-500 mt-1">
                  {t("admin.outageDashboard.stats.avgDuration")}
                </div>
              </div>
            </div>
          </div>
        </div>
      ) : (
        !loading && (
          <div className="p-6 text-center">
            {error ? (
              <div className="text-red-500">
                <p className="font-semibold">{t("admin.error")}</p>
                <p className="text-sm mt-1">{error}</p>
              </div>
            ) : (
              <span className="text-gray-500">{t("admin.noData")}</span>
            )}
          </div>
        )
      )}
    </div>
  );
}
