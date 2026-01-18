import { useState, useEffect, useCallback } from "react";
import { TFunction } from "i18next";
import {
  Car,
  CheckCircle2,
  Activity,
  Shield,
  Clock,
  Building2,
  Tags,
  CircleOff,
  Database,
} from "lucide-react";
import { logFrontendEvent } from "@/utils/logger";
import Loader from "../generic/loader";
import VehicleWorkflowModalSmsGdpr from "./vehicleWorkflowModalSmsGdpr";
import VehicleWorkflowModalSmsAudit from "./vehicleWorkflowModalSmsAudit";

interface VehicleWorkflowStats {
  totalVehicles: number;
  activeVehicles: number;
  fetchingVehicles: number;
  authorizedVehicles: number;
  pendingAuthVehicles: number;
  totalCompaniesWithVehicles: number;
  vehiclesByBrand: { brand: string; count: number }[];
}

export default function DashboardVehicleWorkflow({ t }: { t: TFunction }) {
  const [stats, setStats] = useState<VehicleWorkflowStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [smsGdprModalOpen, setSmsGdprModalOpen] = useState(false);
  const [smsAuditModalOpen, setSmsAuditModalOpen] = useState(false);
  const refreshInterval = 60;

  const fetchStats = useCallback(async () => {
    try {
      setError(null);

      const res = await fetch("/api/clientvehicles/stats");
      if (!res.ok) {
        const errorText = await res.text();
        throw new Error(`HTTP ${res.status}: ${errorText}`);
      }

      const data = await res.json();

      setStats({
        totalVehicles: data.totalVehicles ?? 0,
        activeVehicles: data.activeVehicles ?? 0,
        fetchingVehicles: data.fetchingVehicles ?? 0,
        authorizedVehicles: data.authorizedVehicles ?? 0,
        pendingAuthVehicles: data.pendingAuthVehicles ?? 0,
        totalCompaniesWithVehicles: data.totalCompaniesWithVehicles ?? 0,
        vehiclesByBrand: data.vehiclesByBrand ?? [],
      });

      logFrontendEvent(
        "DashboardVehicleWorkflow",
        "INFO",
        "Stats loaded",
        `Vehicles: ${data.totalVehicles}, Active: ${data.activeVehicles}`
      );
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : String(err);
      setError(errorMsg);
      logFrontendEvent(
        "DashboardVehicleWorkflow",
        "ERROR",
        "Failed to load stats",
        errorMsg
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

  // Calcola il numero di brand diversi e veicoli inattivi
  const differentBrandsCount = stats?.vehiclesByBrand?.length ?? 0;
  const inactiveVehicles = (stats?.totalVehicles ?? 0) - (stats?.activeVehicles ?? 0);

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
              <button
                onClick={() => setSmsGdprModalOpen(true)}
                className="p-3 bg-green-500 hover:bg-green-600 text-white rounded-lg text-sm font-medium transition-all duration-200 shadow-sm hover:shadow-md"
                title={t("admin.smsManagement.buttonGdpr")}
              >
                {t("admin.smsManagement.buttonGdprShort")}
              </button>
              <button
                onClick={() => setSmsAuditModalOpen(true)}
                className="p-3 bg-green-500 hover:bg-green-600 text-white rounded-lg text-sm font-medium transition-all duration-200 shadow-sm hover:shadow-md"
                title={t("admin.smsManagement.titleAudit")}
              >
                {t("admin.smsManagement.buttonAuditShort")}
              </button>
            </div>
            <div className="p-3 bg-gradient-to-br from-blue-400 to-indigo-500 rounded-xl shadow-md">
              <Database size={21} className="text-white" />
            </div>
            <div>
              <h1 className="text-xl sm:text-2xl font-bold text-polarNight dark:text-softWhite">
                {t("admin.vehicleWorkflowDashboard.title")}
              </h1>
              <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">
                {t("admin.vehicleWorkflowDashboard.autoRefresh", {
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
            {/* Totale Veicoli */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-gray-50 to-gray-100 dark:from-gray-800 dark:to-gray-750 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-gray-200 dark:border-gray-700 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-gray-400 to-gray-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-gray-200 dark:bg-gray-700 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <Car size={20} className="text-gray-600 dark:text-gray-400" />
                </div>
                <div className="text-2xl font-bold text-polarNight dark:text-softWhite">
                  {stats.totalVehicles}
                </div>
                <div className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  {t("admin.vehicleWorkflowDashboard.stats.totalVehicles")}
                </div>
              </div>
            </div>

            {/* Veicoli Attivi */}
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
                  {stats.activeVehicles}
                </div>
                <div className="text-xs text-green-600 dark:text-green-500 mt-1">
                  {t("admin.vehicleWorkflowDashboard.stats.activeVehicles")}
                </div>
              </div>
            </div>

            {/* Veicoli Inattivi */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-red-50 to-rose-50 dark:from-red-900/30 dark:to-rose-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-red-200 dark:border-red-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-red-400 to-rose-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-red-200 dark:bg-red-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <CircleOff
                    size={20}
                    className="text-red-600 dark:text-red-400"
                  />
                </div>
                <div className="text-2xl font-bold text-red-700 dark:text-red-400">
                  {inactiveVehicles}
                </div>
                <div className="text-xs text-red-600 dark:text-red-500 mt-1">
                  {t("admin.vehicleWorkflowDashboard.stats.inactiveVehicles")}
                </div>
              </div>
            </div>

            {/* In Acquisizione Dati */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-blue-50 to-indigo-50 dark:from-blue-900/30 dark:to-indigo-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-blue-200 dark:border-blue-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-blue-400 to-indigo-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-blue-200 dark:bg-blue-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <Activity
                    size={20}
                    className="text-blue-600 dark:text-blue-400"
                  />
                </div>
                <div className="text-2xl font-bold text-blue-700 dark:text-blue-400">
                  {stats.fetchingVehicles}
                </div>
                <div className="text-xs text-blue-600 dark:text-blue-500 mt-1">
                  {t("admin.vehicleWorkflowDashboard.stats.fetchingVehicles")}
                </div>
              </div>
            </div>

            {/* OAuth Autorizzati */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-purple-50 to-violet-50 dark:from-purple-900/30 dark:to-violet-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-purple-200 dark:border-purple-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-purple-400 to-violet-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-purple-200 dark:bg-purple-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <Shield
                    size={20}
                    className="text-purple-600 dark:text-purple-400"
                  />
                </div>
                <div className="text-2xl font-bold text-purple-700 dark:text-purple-400">
                  {stats.authorizedVehicles}
                </div>
                <div className="text-xs text-purple-600 dark:text-purple-500 mt-1">
                  {t("admin.vehicleWorkflowDashboard.stats.authorizedVehicles")}
                </div>
              </div>
            </div>

            {/* OAuth Pendenti */}
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
                  {stats.pendingAuthVehicles}
                </div>
                <div className="text-xs text-yellow-600 dark:text-yellow-500 mt-1">
                  {t("admin.vehicleWorkflowDashboard.stats.pendingAuthVehicles")}
                </div>
              </div>
            </div>

            {/* Aziende Associate */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-indigo-50 to-blue-50 dark:from-indigo-900/30 dark:to-blue-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-indigo-200 dark:border-indigo-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-indigo-400 to-blue-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-indigo-200 dark:bg-indigo-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <Building2
                    size={20}
                    className="text-indigo-600 dark:text-indigo-400"
                  />
                </div>
                <div className="text-2xl font-bold text-indigo-700 dark:text-indigo-400">
                  {stats.totalCompaniesWithVehicles}
                </div>
                <div className="text-xs text-indigo-600 dark:text-indigo-500 mt-1">
                  {t(
                    "admin.vehicleWorkflowDashboard.stats.companiesWithVehicles"
                  )}
                </div>
              </div>
            </div>

            {/* Brand Diversi */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-orange-50 to-amber-50 dark:from-orange-900/30 dark:to-amber-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-orange-200 dark:border-orange-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-orange-400 to-amber-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-orange-200 dark:bg-orange-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <Tags
                    size={20}
                    className="text-orange-600 dark:text-orange-400"
                  />
                </div>
                <div className="text-2xl font-bold text-orange-700 dark:text-orange-400">
                  {differentBrandsCount}
                </div>
                <div className="text-xs text-orange-600 dark:text-orange-500 mt-1">
                  {t("admin.vehicleWorkflowDashboard.stats.differentBrands")}
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

      <VehicleWorkflowModalSmsGdpr
        isOpen={smsGdprModalOpen}
        onClose={() => setSmsGdprModalOpen(false)}
      />

      <VehicleWorkflowModalSmsAudit
        isOpen={smsAuditModalOpen}
        onClose={() => setSmsAuditModalOpen(false)}
      />
    </div>
  );
}
