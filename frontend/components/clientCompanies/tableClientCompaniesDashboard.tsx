import { useState, useEffect, useCallback } from "react";
import { TFunction } from "i18next";
import {
  Building2,
  Car,
  CheckCircle2,
  FileSignature,
  FileCheck,
  FileX,
  FlagOff,
  RotateCcw,
  Activity,
} from "lucide-react";
import { logFrontendEvent } from "@/utils/logger";
import Loader from "../generic/loader";

interface ClientCompanyStats {
  totalCompanies: number;
  totalVehicles: number;
  activeCompanies: number;
  totalConsents: number;
  activationConsents: number;
  deactivationConsents: number;
  stopDataConsents: number;
  reactivationConsents: number;
}

export default function DashboardClientCompanies({ t }: { t: TFunction }) {
  const [stats, setStats] = useState<ClientCompanyStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const refreshInterval = 60;

  const fetchStats = useCallback(async () => {
    try {
      setError(null);

      // Singola chiamata API per tutte le statistiche (query SQL aggregate lato backend)
      const res = await fetch("/api/clientcompanies/stats");
      if (!res.ok) {
        const errorText = await res.text();
        throw new Error(`HTTP ${res.status}: ${errorText}`);
      }

      const data = await res.json();

      setStats({
        totalCompanies: data.totalCompanies ?? 0,
        totalVehicles: data.totalVehicles ?? 0,
        activeCompanies: data.activeVehicles ?? 0,
        totalConsents: data.totalConsents ?? 0,
        activationConsents: data.activationConsents ?? 0,
        deactivationConsents: data.deactivationConsents ?? 0,
        stopDataConsents: data.stopDataConsents ?? 0,
        reactivationConsents: data.reactivationConsents ?? 0,
      });

      logFrontendEvent(
        "DashboardClientCompanies",
        "INFO",
        "Stats loaded",
        `Companies: ${data.totalCompanies}, Consents: ${data.totalConsents}`
      );
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : String(err);
      setError(errorMsg);
      logFrontendEvent(
        "DashboardClientCompanies",
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
            <div className="p-3 bg-gradient-to-br from-blue-400 to-indigo-500 rounded-xl shadow-md">
              <Activity size={21} className="text-white" />
            </div>
            <div>
              <h1 className="text-xl sm:text-2xl font-bold text-polarNight dark:text-softWhite">
                {t("admin.clientCompaniesDashboard.title")}
              </h1>
              <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">
                {t("admin.clientCompaniesDashboard.autoRefresh", {
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
            {/* Totale Aziende */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-gray-50 to-gray-100 dark:from-gray-800 dark:to-gray-750 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-gray-200 dark:border-gray-700 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-gray-400 to-gray-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-gray-200 dark:bg-gray-700 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <Building2 size={20} className="text-gray-600 dark:text-gray-400" />
                </div>
                <div className="text-2xl font-bold text-polarNight dark:text-softWhite">
                  {stats.totalCompanies}
                </div>
                <div className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  {t("admin.clientCompaniesDashboard.stats.totalCompanies")}
                </div>
              </div>
            </div>

            {/* Veicoli Associati */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-blue-50 to-indigo-50 dark:from-blue-900/30 dark:to-indigo-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-blue-200 dark:border-blue-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-blue-400 to-indigo-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-blue-200 dark:bg-blue-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <Car size={20} className="text-blue-600 dark:text-blue-400" />
                </div>
                <div className="text-2xl font-bold text-blue-700 dark:text-blue-400">
                  {stats.totalVehicles}
                </div>
                <div className="text-xs text-blue-600 dark:text-blue-500 mt-1">
                  {t("admin.clientCompaniesDashboard.stats.totalVehicles")}
                </div>
              </div>
            </div>

            {/* Aziende Attive */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-green-50 to-emerald-50 dark:from-green-900/30 dark:to-emerald-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-green-200 dark:border-green-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-green-400 to-emerald-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-green-200 dark:bg-green-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <CheckCircle2 size={20} className="text-green-600 dark:text-green-400" />
                </div>
                <div className="text-2xl font-bold text-green-700 dark:text-green-400">
                  {stats.activeCompanies}
                </div>
                <div className="text-xs text-green-600 dark:text-green-500 mt-1">
                  {t("admin.clientCompaniesDashboard.stats.activeCompanies")}
                </div>
              </div>
            </div>

            {/* Totale Consensi */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-purple-50 to-violet-50 dark:from-purple-900/30 dark:to-violet-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-purple-200 dark:border-purple-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-purple-400 to-violet-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-purple-200 dark:bg-purple-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <FileSignature size={20} className="text-purple-600 dark:text-purple-400" />
                </div>
                <div className="text-2xl font-bold text-purple-700 dark:text-purple-400">
                  {stats.totalConsents}
                </div>
                <div className="text-xs text-purple-600 dark:text-purple-500 mt-1">
                  {t("admin.clientCompaniesDashboard.stats.totalConsents")}
                </div>
              </div>
            </div>

            {/* Consent Activation */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-emerald-50 to-teal-50 dark:from-emerald-900/30 dark:to-teal-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-emerald-200 dark:border-emerald-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-emerald-400 to-teal-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-emerald-200 dark:bg-emerald-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <FileCheck size={20} className="text-emerald-600 dark:text-emerald-400" />
                </div>
                <div className="text-2xl font-bold text-emerald-700 dark:text-emerald-400">
                  {stats.activationConsents}
                </div>
                <div className="text-xs text-emerald-600 dark:text-emerald-500 mt-1">
                  {t("admin.clientCompaniesDashboard.stats.activationConsents")}
                </div>
              </div>
            </div>

            {/* Consent Deactivation */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-yellow-50 to-amber-50 dark:from-yellow-900/30 dark:to-amber-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-yellow-200 dark:border-yellow-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-yellow-400 to-amber-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-yellow-200 dark:bg-yellow-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <FileX size={20} className="text-yellow-600 dark:text-yellow-400" />
                </div>
                <div className="text-2xl font-bold text-yellow-700 dark:text-yellow-400">
                  {stats.deactivationConsents}
                </div>
                <div className="text-xs text-yellow-600 dark:text-yellow-500 mt-1">
                  {t("admin.clientCompaniesDashboard.stats.deactivationConsents")}
                </div>
              </div>
            </div>

            {/* Consent Stop Data */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-red-50 to-rose-50 dark:from-red-900/30 dark:to-rose-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-red-200 dark:border-red-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-red-400 to-rose-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-red-200 dark:bg-red-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <FlagOff size={20} className="text-red-600 dark:text-red-400" />
                </div>
                <div className="text-2xl font-bold text-red-700 dark:text-red-400">
                  {stats.stopDataConsents}
                </div>
                <div className="text-xs text-red-600 dark:text-red-500 mt-1">
                  {t("admin.clientCompaniesDashboard.stats.stopDataConsents")}
                </div>
              </div>
            </div>

            {/* Consent Reactivation */}
            <div className="group relative overflow-hidden bg-gradient-to-br from-fuchsia-50 to-pink-50 dark:from-fuchsia-900/30 dark:to-pink-900/30 p-4 rounded-xl shadow-sm hover:shadow-md transition-all duration-300 border border-fuchsia-200 dark:border-fuchsia-800 hover:scale-[1.02] hover:-translate-y-0.5">
              <div className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-fuchsia-400 to-pink-500" />
              <div className="flex flex-col items-center text-center">
                <div className="p-2 bg-fuchsia-200 dark:bg-fuchsia-800 rounded-lg mb-2 group-hover:scale-110 transition-transform">
                  <RotateCcw size={20} className="text-fuchsia-600 dark:text-fuchsia-400" />
                </div>
                <div className="text-2xl font-bold text-fuchsia-700 dark:text-fuchsia-400">
                  {stats.reactivationConsents}
                </div>
                <div className="text-xs text-fuchsia-600 dark:text-fuchsia-500 mt-1">
                  {t("admin.clientCompaniesDashboard.stats.reactivationConsents")}
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
