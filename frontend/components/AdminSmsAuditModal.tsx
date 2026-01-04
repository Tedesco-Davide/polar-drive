import { useEffect, useState, useCallback, useMemo } from "react";
import { useTranslation } from "next-i18next";
import { MessageSquare, Search, CheckCircle, AlertCircle, Clock } from "lucide-react";

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

interface AdminSmsAuditModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function AdminSmsAuditModal({
  isOpen,
  onClose,
}: AdminSmsAuditModalProps) {
  const { t } = useTranslation("");
  const [loading, setLoading] = useState(false);
  const [auditLogs, setAuditLogs] = useState<SmsAuditLog[]>([]);
  const [searchFilter, setSearchFilter] = useState("");
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const loadData = useCallback(async () => {
    try {
      setLoading(true);

      // Carica tutti gli audit logs SMS
      const auditResponse = await fetch(`/api/Sms/audit-logs?pageSize=100&page=${page}`);
      if (auditResponse.ok) {
        const data = await auditResponse.json();
        setAuditLogs(data.logs);
        setTotalPages(data.totalPages);
      }

      logFrontendEvent(
        "AdminSmsAuditModal",
        "INFO",
        "SMS Audit logs loaded successfully",
        `Page: ${page}, Logs: ${auditLogs.length}`
      );
    } catch (error) {
      logFrontendEvent(
        "AdminSmsAuditModal",
        "ERROR",
        "Failed to load SMS audit logs",
        error instanceof Error ? error.message : String(error)
      );
    } finally {
      setLoading(false);
    }
  }, [page, auditLogs.length]);

  useEffect(() => {
    if (isOpen) {
      loadData();
    }
  }, [isOpen, page, loadData]);

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString("it-IT");
  };

  const getStatusIcon = (status: string) => {
    switch (status.toUpperCase()) {
      case "SUCCESS":
        return <CheckCircle size={16} className="text-green-600" />;
      case "ERROR":
      case "REJECTED":
        return <AlertCircle size={16} className="text-red-600" />;
      case "DLR":
        return <Clock size={16} className="text-blue-600" />;
      default:
        return <MessageSquare size={16} className="text-gray-600" />;
    }
  };

  const getStatusClass = (status: string) => {
    switch (status.toUpperCase()) {
      case "SUCCESS":
        return "bg-green-50 dark:bg-green-900/10 hover:bg-green-100 dark:hover:bg-green-900/20";
      case "ERROR":
      case "REJECTED":
        return "bg-red-50 dark:bg-red-900/10 hover:bg-red-100 dark:hover:bg-red-900/20";
      case "DLR":
        return "bg-blue-50 dark:bg-blue-900/10 hover:bg-blue-100 dark:hover:bg-blue-900/20";
      default:
        return "hover:bg-gray-50 dark:hover:bg-gray-700/50";
    }
  };

  // Filtra gli audit logs in base alla ricerca
  const filteredLogs = useMemo(() => {
    if (!searchFilter.trim()) return auditLogs;
    
    const lowerSearch = searchFilter.toLowerCase();
    return auditLogs.filter(
      (log) =>
        log.fromPhoneNumber.toLowerCase().includes(lowerSearch) ||
        log.toPhoneNumber.toLowerCase().includes(lowerSearch) ||
        log.messageBody.toLowerCase().includes(lowerSearch) ||
        log.processingStatus.toLowerCase().includes(lowerSearch) ||
        log.messageSid.toLowerCase().includes(lowerSearch) ||
        log.id.toString().includes(lowerSearch) ||
        (log.errorMessage && log.errorMessage.toLowerCase().includes(lowerSearch)) ||
        (log.responseSent && log.responseSent.toLowerCase().includes(lowerSearch))
    );
  }, [auditLogs, searchFilter]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/10 backdrop-blur-sm note-modal p-4">
      <div className="w-full max-h-[80vh] p-6 relative flex flex-col bg-softWhite dark:bg-gray-800 border border-gray-300 dark:border-gray-600 shadow-lg rounded-lg">
        {/* Header */}
        <div className="flex items-start justify-between mb-4">
          <div>
            <h2 className="text-xl font-semibold text-polarNight dark:text-softWhite mb-0">
              📊 {t("admin.smsManagement.titleAudit")}
            </h2>
            <p className="text-sm text-gray-600 dark:text-gray-400 mt-1">
              {t("admin.smsManagement.auditDescription")}
            </p>
          </div>
        </div>

        {/* Search Bar */}
        <div className="mb-4">
          <div className="relative">
            <Search
              className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400"
              size={20}
            />
            <input
              type="text"
              placeholder={t("admin.smsManagement.searchAuditGlobalPlaceholder")}
              value={searchFilter}
              onChange={(e) => setSearchFilter(e.target.value)}
              className="w-full pl-10 pr-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-polarNight dark:text-softWhite focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
            {t("admin.smsManagement.resultsCountWithPage", { filtered: filteredLogs.length, total: auditLogs.length, page, totalPages })}
          </p>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto min-h-0 border border-gray-300 dark:border-gray-600 rounded-lg">
          {filteredLogs.length === 0 ? (
            <p className="text-gray-500 py-8 text-center">
              {searchFilter
                ? t("admin.smsManagement.noResultsFound")
                : t("admin.smsManagement.noSmsLogsFound")}
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
                      <MessageSquare size={14} />
                      SID
                    </div>
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    {t("admin.smsManagement.fromHeader")}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    {t("admin.smsManagement.toHeader")}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    {t("admin.smsManagement.messageHeader")}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    {t("admin.smsManagement.statusHeader")}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    {t("admin.smsManagement.vehicleIdHeader")}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    {t("admin.smsManagement.receivedHeader")}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    {t("admin.smsManagement.errorHeader")}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase">
                    {t("admin.smsManagement.responseHeader")}
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
                {filteredLogs.map((log) => (
                  <tr
                    key={log.id}
                    className={`${getStatusClass(log.processingStatus)} transition-colors`}
                  >
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      #{log.id}
                    </td>
                    <td className="px-4 py-3 text-xs font-mono text-gray-600 dark:text-gray-300">
                      {log.messageSid.substring(0, 12)}...
                    </td>
                    <td className="px-4 py-3 text-sm text-polarNight dark:text-softWhite">
                      {log.fromPhoneNumber}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      {log.toPhoneNumber}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300 max-w-xs">
                      {log.messageBody}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        {getStatusIcon(log.processingStatus)}
                        <span className="text-xs font-medium">
                          {log.processingStatus}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      {log.vehicleIdResolved || "-"}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                      {formatDate(log.receivedAt)}
                    </td>
                    <td className="px-4 py-3 text-sm text-red-600 dark:text-red-400 max-w-xs">
                      {log.errorMessage || "-"}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300 max-w-xs">
                      {log.responseSent || "-"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Loading */}
        {loading && (
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="mt-4 flex items-center justify-center gap-2">
            <button
              onClick={() => setPage(Math.max(1, page - 1))}
              disabled={page === 1}
              className="px-3 py-1 bg-gray-300 dark:bg-gray-600 text-gray-700 dark:text-gray-200 rounded hover:bg-gray-400 dark:hover:bg-gray-500 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              ← {t("admin.smsManagement.prevButton")}
            </button>
            <span className="text-sm text-gray-600 dark:text-gray-300">
              {t("admin.smsManagement.pageInfo", { page, totalPages })}
            </span>
            <button
              onClick={() => setPage(Math.min(totalPages, page + 1))}
              disabled={page === totalPages}
              className="px-3 py-1 bg-gray-300 dark:bg-gray-600 text-gray-700 dark:text-gray-200 rounded hover:bg-gray-400 dark:hover:bg-gray-500 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {t("admin.smsManagement.nextButton")} →
            </button>
          </div>
        )}

        {/* Footer */}
        <div className="mt-6 flex flex-shrink-0 pt-4 border-t border-gray-200 dark:border-gray-700">
          <button
            className="bg-gray-400 text-white px-6 py-2 rounded hover:bg-gray-500"
            onClick={() => {
              logFrontendEvent("SmsAuditModal", "INFO", "SMS Audit modal closed");
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
